using System;
using UnityEngine;
using Mirror;

/// <summary>
/// 挂在 GlobalManagers 上。准备(15s)+营业(300s) 共 315s 倒计时；之后延迟营业→打烊→次日。
/// 联网时，Host 驱动全部逻辑，Guest 通过 NetworkDayCycleBridge 的 SyncVar 同步阶段。
/// </summary>
[DefaultExecutionOrder(-100)]
public class DayCycleManager : MonoBehaviour
{
    public static DayCycleManager Instance { get; private set; }

    [Header("Timing")]
    [SerializeField] private float prepDuration = 15f;
    [SerializeField] private float businessDuration = 300f;
    [SerializeField] private float shopDeliveryDelayFromPrepStart = 5f;

    [Header("Week")]
    [SerializeField] private string[] weekDayNames =
    {
        "周一/Monday", "周二/Tuesday", "周三/Wednesday", "周四/Thursday", "周五/Friday", "周六/Saturday", "周日/Sunday"
    };

    [Header("Refs")]
    [SerializeField] private CustomerSpawner customerSpawner;
    [SerializeField] private DayStatsTracker statsTracker;
    [SerializeField] private ShopDeliveryQueue shopDeliveryQueue;
    [SerializeField] private ShopItemCatalog shopCatalog;
    [SerializeField] private ScreenFadeController screenFade;
    [SerializeField] private Transform playerSpawnPoint;
    [SerializeField] private GameObject playerObject;
    [SerializeField] private GlobalOrderUI globalOrderUI;

    [Header("Debug")]
    [SerializeField] private bool debugLog;

    public event Action<DayCyclePhase> OnPhaseChanged;
    public event Action OnPrepStart;
    public event Action OnBusinessStart;
    public event Action OnBusinessTimerEnded;
    public event Action OnEnterClosing;
    public event Action OnDayAdvanced;
    public event Action OnEnterDayZero;

    DayCyclePhase phase = DayCyclePhase.DayZero;
    int currentDayIndex;
    float prepElapsed;
    float remainingClockSeconds;
    OrderResponse[] cachedTables;

    /// <summary>联网模式下，当前机器是纯客户端（Guest）而非 Host/Server</summary>
    bool IsNetworkGuest => NetworkClient.active && !NetworkServer.active;

    public DayCyclePhase Phase => phase;
    public int CurrentDayIndex => currentDayIndex;
    public float RemainingClockSeconds => remainingClockSeconds;
    public float PrepElapsed => prepElapsed;
    public string CurrentWeekdayName => weekDayNames != null && weekDayNames.Length > 0
        ? weekDayNames[Mathf.Clamp(currentDayIndex, 0, weekDayNames.Length - 1)]
        : "?";

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        cachedTables = FindObjectsByType<OrderResponse>(FindObjectsSortMode.None);
    }

    void Start()
    {
        BeginDayZero();
    }

    /// <summary>开局：无计时、不生成客人，可订菜；订货在第一天准备阶段与批次一起生成。</summary>
    void BeginDayZero()
    {
        phase = DayCyclePhase.DayZero;
        currentDayIndex = 0;
        prepElapsed = 0f;
        remainingClockSeconds = 0f;

        customerSpawner?.SetSpawningPaused(true);
        SetGlobalOrdersVisible(false);

        OnPhaseChanged?.Invoke(phase);
        OnEnterDayZero?.Invoke();

        if (debugLog) Debug.Log("[DayCycle] DayZero (pre-run, no timer)");
    }

    /// <summary>Day0 面板上「开始第一天」：黑屏后进入周一准备阶段。</summary>
    public void RequestStartFirstDay()
    {
        if (phase != DayCyclePhase.DayZero) return;
        if (screenFade != null && screenFade.IsBusy)
        {
            if (debugLog) Debug.LogWarning("[DayCycle] RequestStartFirstDay 被忽略：ScreenFadeController 仍在播放。");
            return;
        }

        phase = DayCyclePhase.NextDayTransition;
        OnPhaseChanged?.Invoke(phase);

        if (screenFade != null)
            screenFade.RunFadeInHoldFadeOut(OnMidBlackStartFirstDay);
        else
        {
            if (debugLog) Debug.LogWarning("[DayCycle] DayCycleManager.screenFade 未赋值，将直接开始第一天且无黑屏。请在 Inspector 绑定 ScreenFadeController。");
            OnMidBlackStartFirstDay();
        }
    }

    void OnMidBlackStartFirstDay()
    {
        TeleportPlayerToSpawn();
        currentDayIndex = 0;
        StartPrepInternal(resetStats: true);
        if (debugLog) Debug.Log("[DayCycle] First day (Mon) prep started");
    }

    void StartPrepInternal(bool resetStats)
    {
        phase = DayCyclePhase.Prep;
        prepElapsed = 0f;
        remainingClockSeconds = prepDuration + businessDuration;

        if (resetStats && statsTracker != null)
            statsTracker.ClearDay();

        shopDeliveryQueue?.OnNewPrepStarted();
        customerSpawner?.ConfigureForDay(currentDayIndex);
        LogDayCycleCustomerSpawnerAlign("StartPrepInternal（含首日/次日换日后的准备阶段）");
        customerSpawner?.SetSpawningPaused(true);

        SetGlobalOrdersVisible(false);
        OnPhaseChanged?.Invoke(phase);
        OnPrepStart?.Invoke();

        if (debugLog) Debug.Log($"[DayCycle] Prep start day={currentDayIndex}");
    }

    /// <summary>在 <see cref="CustomerSpawner.ConfigureForDay"/> 之后打 Log，便于与 <c>[Spawner][天数变更]</c> 对照。</summary>
    void LogDayCycleCustomerSpawnerAlign(string sourceTag)
    {
        if (!debugLog) return;
        Debug.Log($"[DayCycle][客人生成-天数] {sourceTag} → currentDayIndex={currentDayIndex}，已下发 CustomerSpawner 当日关卡");
    }

    void Update()
    {
        if (IsNetworkGuest)
        {
            var bridge = NetworkDayCycleBridge.Instance;
            if (bridge != null)
                remainingClockSeconds = bridge.GetSyncedRemainingClock();
            return;
        }

        switch (phase)
        {
            case DayCyclePhase.Prep:
                TickPrep();
                break;
            case DayCyclePhase.Business:
                TickBusiness();
                break;
            case DayCyclePhase.ExtendedBusiness:
                TickExtendedBusiness();
                break;
        }
    }

    void TickPrep()
    {
        float dt = Time.deltaTime;
        prepElapsed += dt;
        remainingClockSeconds -= dt;
        if (remainingClockSeconds < 0f) remainingClockSeconds = 0f;

        shopDeliveryQueue?.TryDeliverBatchIfDue(prepElapsed, shopDeliveryDelayFromPrepStart, shopCatalog);

        if (prepElapsed >= prepDuration)
        {
            phase = DayCyclePhase.Business;
            customerSpawner?.SetSpawningPaused(false);
            SetGlobalOrdersVisible(true);
            OnPhaseChanged?.Invoke(phase);
            OnBusinessStart?.Invoke();
            if (debugLog)
            {
                Debug.Log("[DayCycle] Business start");
                Debug.Log($"[DayCycle][客人生成-天数] 准备阶段结束 → 营业刷怪开启，currentDayIndex={currentDayIndex}（与 Spawner._trackedDayIndex 一致）");
            }
        }
    }

    void TickBusiness()
    {
        remainingClockSeconds -= Time.deltaTime;
        if (remainingClockSeconds < 0f) remainingClockSeconds = 0f;

        if (remainingClockSeconds <= 0f)
        {
            phase = DayCyclePhase.ExtendedBusiness;
            OnBusinessTimerEnded?.Invoke();
            OnPhaseChanged?.Invoke(phase);
            customerSpawner?.SetSpawningPaused(true);
            if (debugLog) Debug.Log("[DayCycle] Extended business (timer ended)");
        }
    }

    void TickExtendedBusiness()
    {
        if (cachedTables == null || cachedTables.Length == 0)
            cachedTables = FindObjectsByType<OrderResponse>(FindObjectsSortMode.None);
        foreach (var t in cachedTables)
        {
            if (t != null)
                t.TryForceLeaveForBusinessEnd();
        }

        if (CustomerGroup.ActiveGroupCount <= 0)
        {
            phase = DayCyclePhase.Closing;
            SetGlobalOrdersVisible(false);
            OnEnterClosing?.Invoke();
            OnPhaseChanged?.Invoke(phase);
            customerSpawner?.SetSpawningPaused(true);
            if (debugLog) Debug.Log("[DayCycle] Closing");
        }
    }

    void SetGlobalOrdersVisible(bool visible)
    {
        if (globalOrderUI == null) return;
        globalOrderUI.SetOrdersVisible(visible);
    }

    /// <summary>打烊面板「下一天」按钮调用。</summary>
    public void RequestNextDay()
    {
        if (phase != DayCyclePhase.Closing) return;
        if (screenFade != null && screenFade.IsBusy)
        {
            if (debugLog) Debug.LogWarning("[DayCycle] RequestNextDay 被忽略：ScreenFadeController 仍在播放。");
            return;
        }

        phase = DayCyclePhase.NextDayTransition;
        OnPhaseChanged?.Invoke(phase);

        if (screenFade != null)
            screenFade.RunFadeInHoldFadeOut(OnMidBlackNextDay);
        else
        {
            if (debugLog) Debug.LogWarning("[DayCycle] DayCycleManager.screenFade 未赋值，将直接换日且无黑屏。请在 Inspector 绑定 ScreenFadeController。");
            OnMidBlackNextDay();
        }
    }

    void OnMidBlackNextDay()
    {
        // 先处理所有桌子的天数切换：保留脏盘子状态，清理残留的顾客引用
        if (cachedTables == null || cachedTables.Length == 0)
            cachedTables = FindObjectsByType<OrderResponse>(FindObjectsSortMode.None);
        foreach (var t in cachedTables)
        {
            if (t != null) t.HandleDayTransition();
        }

        currentDayIndex = (currentDayIndex + 1) % 7;
        TeleportPlayerToSpawn();
        StartPrepInternal(resetStats: true);
        OnDayAdvanced?.Invoke();

        if (debugLog) Debug.Log($"[DayCycle] Advanced to day index {currentDayIndex}");
    }

    void TeleportPlayerToSpawn()
    {
        if (playerSpawnPoint == null || playerObject == null) return;

        Vector3 pos = playerSpawnPoint.position;
        Quaternion rot = playerSpawnPoint.rotation;

        var cc = playerObject.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        var rb = playerObject.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = pos;
            rb.rotation = rot;
        }

        playerObject.transform.SetPositionAndRotation(pos, rot);

        if (cc != null) cc.enabled = true;
    }

    /// <summary>供 SinglePlayerSpawner 等外部脚本在运行时注入玩家引用。</summary>
    public void SetPlayerObject(GameObject player)
    {
        playerObject = player;
        if (debugLog) Debug.Log($"[DayCycle] 外部注入 playerObject: {(player != null ? player.name : "NULL")}");
    }

    /// <summary>供 SinglePlayerSpawner 等外部脚本在运行时注入出生点。</summary>
    public void SetPlayerSpawnPoint(Transform point)
    {
        playerSpawnPoint = point;
        if (debugLog) Debug.Log($"[DayCycle] 外部注入 playerSpawnPoint: {(point != null ? point.name : "NULL")}");
    }

    /// <summary>订单收入等是否记入「今日营业额」（营业与延迟营业期间）。</summary>
    public bool ShouldRecordOrderRevenue()
    {
        return phase == DayCyclePhase.Business || phase == DayCyclePhase.ExtendedBusiness;
    }

    public float ShopDeliveryDelayFromPrepStart => shopDeliveryDelayFromPrepStart;

    /// <summary>某日关卡配置中计划的顾客总数（各波 groupSize 之和）。</summary>
    public int GetPlannedGuestCountForDay(int dayIndex)
    {
        return customerSpawner != null ? customerSpawner.GetPlannedGuestCountForDay(dayIndex) : 0;
    }

    /// <summary>打烊界面用：即将进入的下一日计划顾客数（currentDayIndex 的下一环）。</summary>
    public int GetPlannedGuestCountForNextDay()
    {
        int next = (currentDayIndex + 1) % 7;
        return GetPlannedGuestCountForDay(next);
    }

    // ── 网络同步入口（仅供 NetworkDayCycleBridge 在 Guest 端调用）──

    /// <summary>
    /// Guest 端收到 Host 的阶段同步后，直接跳转到对应阶段并触发本地事件/表现。
    /// </summary>
    public void ApplyNetworkPhase(DayCyclePhase newPhase, int dayIndex)
    {
        if (debugLog)
            Debug.Log($"[DayCycle] ApplyNetworkPhase: {phase} → {newPhase}, dayIndex={dayIndex}");

        DayCyclePhase oldPhase = phase;
        phase = newPhase;
        currentDayIndex = dayIndex;

        switch (newPhase)
        {
            case DayCyclePhase.DayZero:
                prepElapsed = 0f;
                remainingClockSeconds = 0f;
                customerSpawner?.SetSpawningPaused(true);
                SetGlobalOrdersVisible(false);
                OnEnterDayZero?.Invoke();
                break;

            case DayCyclePhase.NextDayTransition:
                if (cachedTables == null || cachedTables.Length == 0)
                    cachedTables = FindObjectsByType<OrderResponse>(FindObjectsSortMode.None);
                if (oldPhase == DayCyclePhase.Closing)
                {
                    foreach (var t in cachedTables)
                        if (t != null) t.HandleDayTransition();
                }
                TeleportPlayerToSpawn();
                break;

            case DayCyclePhase.Prep:
                prepElapsed = 0f;
                remainingClockSeconds = prepDuration + businessDuration;
                if (statsTracker != null) statsTracker.ClearDay();
                shopDeliveryQueue?.OnNewPrepStarted();
                customerSpawner?.ConfigureForDay(currentDayIndex);
                LogDayCycleCustomerSpawnerAlign("ApplyNetworkPhase(Prep) 联机阶段同步");
                customerSpawner?.SetSpawningPaused(true);
                SetGlobalOrdersVisible(false);
                OnPrepStart?.Invoke();
                break;

            case DayCyclePhase.Business:
                customerSpawner?.SetSpawningPaused(false);
                SetGlobalOrdersVisible(true);
                OnBusinessStart?.Invoke();
                if (debugLog)
                    Debug.Log($"[DayCycle][客人生成-天数] ApplyNetworkPhase(Business) 联机同步 → 刷怪暂停解除，currentDayIndex={currentDayIndex}");
                break;

            case DayCyclePhase.ExtendedBusiness:
                customerSpawner?.SetSpawningPaused(true);
                OnBusinessTimerEnded?.Invoke();
                break;

            case DayCyclePhase.Closing:
                SetGlobalOrdersVisible(false);
                customerSpawner?.SetSpawningPaused(true);
                OnEnterClosing?.Invoke();
                break;
        }

        OnPhaseChanged?.Invoke(newPhase);

        if (newPhase == DayCyclePhase.Prep && oldPhase != DayCyclePhase.DayZero)
            OnDayAdvanced?.Invoke();
    }

    /// <summary>
    /// Guest：收到 Host 进入换日过渡时播放本地黑屏，并在「全黑」时执行与 Host OnMidBlack 对称的桌子/传送逻辑。
    /// </summary>
    public void ApplyGuestNextDayTransitionWithFade(int dayIndex)
    {
        DayCyclePhase oldPhase = phase;
        phase = DayCyclePhase.NextDayTransition;
        currentDayIndex = dayIndex;
        OnPhaseChanged?.Invoke(DayCyclePhase.NextDayTransition);

        if (screenFade != null)
        {
            screenFade.RunFadeInHoldFadeOut(() => ApplyNetworkMidBlackGuestTransition(oldPhase));
        }
        else
        {
            if (debugLog)
                Debug.LogWarning("[DayCycle] Guest 换日：screenFade 未赋值，直接执行中段逻辑。");
            ApplyNetworkMidBlackGuestTransition(oldPhase);
        }
    }

    void ApplyNetworkMidBlackGuestTransition(DayCyclePhase oldPhase)
    {
        if (cachedTables == null || cachedTables.Length == 0)
            cachedTables = FindObjectsByType<OrderResponse>(FindObjectsSortMode.None);
        if (oldPhase == DayCyclePhase.Closing)
        {
            foreach (var t in cachedTables)
                if (t != null) t.HandleDayTransition();
        }
        TeleportPlayerToSpawn();
    }
}
