using UnityEngine;

public class ChoppingStation : BaseStation
{
    public enum Axis { X, Y, Z }

    [Header("Refs")]
    public ItemPlacePoint placePoint;
    public GameObject usableHighlight;

    [Header("Processing")]
    public ProcessType stationProcessType = ProcessType.Chop;
    [Min(0.01f)] public float processingSpeed = 20f;

    [Header("Visual")]
    public Transform rotatingPart;
    public Axis rotateAxis = Axis.Z;
    public float angleRange = 45f;
    public float swingSpeed = 5f;

    private Quaternion initialRotation;
    private float currentPhase = 0f;
    private bool isInteracting = false;

    void Start()
    {
        if (rotatingPart != null)
            initialRotation = rotatingPart.localRotation;
            
        if (usableHighlight != null)
            usableHighlight.SetActive(false);
    }

    /// <summary>获取实际切菜速度（含羁绊加成）。不修改 processingSpeed 字段本身。</summary>
    float GetEffectiveProcessingSpeed()
    {
        if (BondRuntimeBridge.Instance != null
            && BondRuntimeBridge.Instance.State != null
            && BondRuntimeBridge.Instance.State.IsActive(RecipeBondTag.Vegetable))
        {
            return processingSpeed * 1.2f;
        }
        return processingSpeed;
    }

    void Update()
    {
        if (!isInteracting)
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

        IProcessable processable = GetCurrentProcessable();
        if (processable == null)
        {
            EndInteract(cachedInteractor);
            return;
        }

        processable.ApplyProgress(stationProcessType, GetEffectiveProcessingSpeed() * Time.deltaTime, this);
        UpdateSwingVisual();

        if (processable.IsComplete)
        {
            EndInteract(cachedInteractor);
        }
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
        currentPhase = 0f;

        if (debugLog) Debug.Log($"[ChoppingStation] Begin interact: {name}");
    }

    public override void EndInteract(PlayerItemInteractor interactor)
    {
        isInteracting = false;
        ResetVisualIfNeeded();

        if (debugLog) Debug.Log($"[ChoppingStation] End interact: {name}");
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