using UnityEngine;
using Mirror;

public class ChoppingStation : BaseStation
{
    public enum Axis { X, Y, Z }

    [Header("Refs")]
    public ItemPlacePoint placePoint;
    public GameObject usableHighlight;

    [Header("Processing")]
    public ProcessType stationProcessType = ProcessType.Chop;
    [Tooltip("切菜板的基础速度，暂设为1")]
    [Min(0.01f)] public float baseProcessingSpeed = 1f;

    [Header("Visual")]
    public Transform rotatingPart;
    public Axis rotateAxis = Axis.Z;
    public float angleRange = 45f;
    public float swingSpeed = 5f;

    [SyncVar] float syncProcessProgress;
    /// <summary>与 Cmd 内 BeginInteract 同步：客机本地 isInteracting 恒为 false，需用此值驱动进度/UI。</summary>
    [SyncVar] bool syncChoppingActive;

    [Header("Diagnostics")]
    [Tooltip("联机 Guest 砧台闪烁排查：syncChopping 开着但高亮/可加工条件丢失时打印（含节流到避免刷屏）。")]
    [SerializeField] bool debugFlickerDiagnostics = false;

    private Quaternion initialRotation;
    private float currentPhase = 0f;
    private bool isInteracting = false;

    /// <summary>上一帧 Guest 是否在「应显示切菜表现」的好状态（用于边沿检测）。</summary>
    bool _lastGuestChopVisualOk = true;
    float _nextFlickerDiagLogTime;

    void Start()
    {
        if (rotatingPart != null)
            initialRotation = rotatingPart.localRotation;
            
        if (usableHighlight != null)
            usableHighlight.SetActive(false);
    }

    /// <summary>获取实际切菜速度（含羁绊加成和属性修正）。不修改 baseProcessingSpeed 字段本身。</summary>
    float GetEffectiveProcessingSpeed()
    {
        // 1. 获取基础速度
        float speed = baseProcessingSpeed;

        // 2. 累乘玩家属性
        float playerMulti = 1.0f;
        if (CurrentPlayerAttributes != null)
            playerMulti = CurrentPlayerAttributes.chopSpeedMultiplier;

        // 3. 累加全局加成
        float globalAddon = 0f;
        if (GlobalOrderManager.Instance != null)
            globalAddon = GlobalOrderManager.Instance.globalChopSpeedAddon;

        // 4. 计算公式：最终速度 = (基础速度 * 玩家乘数) + 全局加成
        float finalSpeed = (speed * playerMulti) + globalAddon;

        // 保留原有的羁绊效果（作为额外的系数叠加，影响最终速度）
        if (BondRuntimeBridge.Instance != null
            && BondRuntimeBridge.Instance.State != null
            && BondRuntimeBridge.Instance.State.IsActive(RecipeBondTag.Vegetable))
        {
            finalSpeed *= 1.2f;
        }

        // 安全界限限制（保证最小有0.01速度运转防止卡死）
        return Mathf.Max(0.01f, finalSpeed);
    }

    void Update()
    {
        bool chopping = isInteracting;
        if (NetworkClient.active && !NetworkServer.active)
            chopping = syncChoppingActive;

        if (!chopping)
        {
            ResetVisualIfNeeded();
            return;
        }

        // 离开范围后停止推进
        if (!isSensorTargeted)
        {
            ResetVisualIfNeeded();
            return;
        }

        bool isServerOrOffline = !NetworkClient.active || NetworkServer.active;

        IProcessable processable = GetCurrentProcessable();
        if (processable == null)
        {
            if (isServerOrOffline) EndInteract(cachedInteractor);
            else ResetVisualIfNeeded();
            return;
        }

        if (isServerOrOffline)
        {
            processable.ApplyProgress(stationProcessType, GetEffectiveProcessingSpeed() * Time.deltaTime, this);
            syncProcessProgress = processable.CurrentProgress;
        }
        else
        {
            var bp = processable as BaseProcessable;
            if (bp != null) bp.CurrentProgress = syncProcessProgress;
        }

        UpdateSwingVisual();

        if (isServerOrOffline && processable.IsComplete)
        {
            EndInteract(cachedInteractor);
        }

        LogGuestFlickerDiagIfNeeded(chopping);
    }

    /// <summary>供 PlayerItemInteractor 诊断：为何当前无法 GetCurrentProcessable。</summary>
    public string GetGuestProcessableDenyReasonLine()
    {
        return BuildProcessableDenyDetail();
    }

    void LogGuestFlickerDiagIfNeeded(bool chopping)
    {
        if (!debugFlickerDiagnostics) return;
        if (!NetworkClient.active || NetworkServer.active) return;
        if (!chopping)
        {
            _lastGuestChopVisualOk = true;
            return;
        }

        IProcessable proc = GetCurrentProcessable();
        bool ok = isSensorTargeted && proc != null;
        bool edgeToBad = _lastGuestChopVisualOk && !ok;
        if (!ok && (edgeToBad || Time.unscaledTime >= _nextFlickerDiagLogTime))
        {
            string reason = proc == null ? BuildProcessableDenyDetail() : "processable ok";
            Debug.Log(
                $"[ChopFlickerDiag][Guest] frame={Time.frameCount} name={name} " +
                $"syncChop={syncChoppingActive} isSensorTargeted={isSensorTargeted} procNull={proc == null} " +
                $"syncProg={syncProcessProgress:F2} | {reason}",
                this);
            _nextFlickerDiagLogTime = Time.unscaledTime + 0.15f;
        }

        _lastGuestChopVisualOk = ok;
    }

    string BuildProcessableDenyDetail()
    {
        if (placePoint == null) return "placePoint=null";
        CarryableItem item = placePoint.CurrentItem;
        if (item == null) return "placePoint.CurrentItem=null";

        MonoBehaviour[] all = item.GetComponents<MonoBehaviour>();
        foreach (var mb in all)
        {
            if (mb is IProcessable p)
            {
                if (p is BaseProcessable bp)
                {
                    return $"item={item.name} prog={bp.CurrentProgress:F2}/{bp.RequiredProgress:F2} " +
                           $"IsComplete={bp.IsComplete} CanProcessChop={p.CanProcess(stationProcessType)}";
                }

                return $"item={item.name} IProcessable 无 BaseProcessable 细节 CanProcess={p.CanProcess(stationProcessType)}";
            }
        }

        return $"item={item.name} 无 IProcessable 组件";
    }

    public override bool CanInteract(PlayerItemInteractor interactor)
    {
        // 只要台子上有能被该台子加工的物体，就允许互动
        return GetCurrentProcessable() != null;
    }

    public override void BeginInteract(PlayerItemInteractor interactor)
    {
        if (isInteracting) return;
        if (!CanInteract(interactor)) return;

        cachedInteractor = interactor;
        isInteracting = true;
        if (NetworkServer.active)
            syncChoppingActive = true;
        isSensorTargeted = true;
        currentPhase = 0f;

        if (debugLog) Debug.Log($"[ChoppingStation] Begin interact: {name} | isServer={NetworkServer.active}");
    }

    public override void EndInteract(PlayerItemInteractor interactor)
    {
        isInteracting = false;
        if (NetworkServer.active)
            syncChoppingActive = false;
        ResetVisualIfNeeded();

        if (debugLog) Debug.Log($"[ChoppingStation] End interact: {name} | isServer={NetworkServer.active}");
    }

    // 重写基础高亮，接收 Interactor 的高亮指令
    public override void SetSensorHighlight(bool on)
    {
        base.SetSensorHighlight(on);
        
        // 核心解耦：台子不再自己算要不要亮，Interactor 说合法就亮这个提示
        if (usableHighlight != null)
            usableHighlight.SetActive(on);
    }

    public CarryableItem GetCurrentPlacedItem()
    {
        if (placePoint == null) return null;
        return placePoint.CurrentItem;
    }

    public IProcessable GetCurrentProcessable()
    {
        CarryableItem item = GetCurrentPlacedItem();
        if (item == null) return null;

        MonoBehaviour[] all = item.GetComponents<MonoBehaviour>();
        foreach (var mb in all)
        {
            if (mb is IProcessable processable && processable.CanProcess(stationProcessType))
                return processable;
        }

        return null;
    }

    void UpdateSwingVisual()
    {
        if (rotatingPart == null) return;

        currentPhase += Time.deltaTime * swingSpeed;
        float angle = Mathf.Sin(currentPhase) * angleRange;

        Vector3 axisVec = Vector3.forward;
        switch (rotateAxis)
        {
            case Axis.X: axisVec = Vector3.right; break;
            case Axis.Y: axisVec = Vector3.up; break;
            case Axis.Z: axisVec = Vector3.forward; break;
        }

        rotatingPart.localRotation = initialRotation * Quaternion.AngleAxis(angle, axisVec);
    }

    void ResetVisualIfNeeded()
    {
        if (rotatingPart != null)
            rotatingPart.localRotation = initialRotation;
    }
}