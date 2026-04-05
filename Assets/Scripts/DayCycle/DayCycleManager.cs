using System;
using UnityEngine;

/// <summary>
/// 挂在 GlobalManagers 上。准备(15s)+营业(300s) 共 315s 倒计时；之后延迟营业→打烊→次日。
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
        "周一", "周二", "周三", "周四", "周五", "周六", "周日"
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
        customerSpawner?.SetSpawningPaused(true);

        SetGlobalOrdersVisible(false);
        OnPhaseChanged?.Invoke(phase);
        OnPrepStart?.Invoke();

        if (debugLog) Debug.Log($"[DayCycle] Prep start day={currentDayIndex}");
    }

    void Update()
    {
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
            if (debugLog) Debug.Log("[DayCycle] Business start");
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
}
