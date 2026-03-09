using UnityEngine;

public class ChoppingStation : BaseStation
{
    public enum Axis { X, Y, Z }

    [Header("Refs")]
    public ItemPlacePoint placePoint;
    public GameObject usableHighlight;
    public GameObject placeableHighlight;

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

        RefreshHighlights();
    }

    void Update()
    {
        RefreshHighlights();

        if (!isInteracting)
        {
            ResetVisualIfNeeded();
            return;
        }

        // 离开范围后停止推进，但不重置进度
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

        processable.ApplyProgress(stationProcessType, processingSpeed * Time.deltaTime, this);
        UpdateSwingVisual();
        
        if (processable.IsComplete)
        {
            EndInteract(cachedInteractor);
        }
    }

    public override bool CanInteract(PlayerItemInteractor interactor)
    {
        return isSensorTargeted && GetCurrentProcessable() != null;
    }

    public override void BeginInteract(PlayerItemInteractor interactor)
    {
        if (isInteracting) return;
        if (!CanInteract(interactor)) return;

        cachedInteractor = interactor;
        isInteracting = true;
        currentPhase = 0f;

        if (debugLog)
            Debug.Log($"[ChoppingStation] Begin interact: {name}");
    }

    public override void EndInteract(PlayerItemInteractor interactor)
    {
        if (!isInteracting) return;

        isInteracting = false;
        ResetVisualIfNeeded();

        if (debugLog)
            Debug.Log($"[ChoppingStation] End interact: {name}");
    }

    protected override void OnSensorHighlightChanged()
    {
        RefreshHighlights();
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

    void RefreshHighlights()
    {
        bool playerHoldingItem = cachedInteractor != null && cachedInteractor.IsHoldingItem();
        bool hasPlacedItem = GetCurrentPlacedItem() != null;
        bool canProcess = GetCurrentProcessable() != null;

        bool showUsable = isSensorTargeted && !playerHoldingItem && hasPlacedItem && canProcess;
        bool showPlaceable = isSensorTargeted && playerHoldingItem && !hasPlacedItem;

        if (usableHighlight != null)
            usableHighlight.SetActive(showUsable);

        if (placeableHighlight != null)
            placeableHighlight.SetActive(showPlaceable);
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