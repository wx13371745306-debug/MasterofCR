using UnityEngine;

public class CarryableItem : MonoBehaviour
{
    public enum ItemState
    {
        Free,
        Held,
        Placed
    }

    [Header("Refs")]
    public Rigidbody rb;
    public Collider[] itemColliders;
    public AttachPoseConfig poseConfig;
    public InteractableHighlight sensorHighlight;

    [Header("Category")]
    public ItemCategory categories = ItemCategory.None;

    [Header("Placement")]
    public ItemPlacePoint initialPlacePoint;

    [Header("Use")]
    public bool isUsable = true;
    public GameObject useHighlightObject;

    [Header("Debug")]
    public bool debugLog = true;

    public ItemPlacePoint CurrentPlacePoint => currentPlacePoint;
    public ItemState State => state;

    private ItemPlacePoint currentPlacePoint;
    private ItemPlacePoint lastPlacePointBeforeHold;
    private ItemState state = ItemState.Free;

    void Reset()
    {
        rb = GetComponent<Rigidbody>();
        poseConfig = GetComponent<AttachPoseConfig>();
        sensorHighlight = GetComponent<InteractableHighlight>();
        itemColliders = GetComponentsInChildren<Collider>(includeInactive: true);
    }

    void Awake()
    {
        if (itemColliders == null || itemColliders.Length == 0)
            itemColliders = GetComponentsInChildren<Collider>(includeInactive: true);

        if (useHighlightObject != null)
            useHighlightObject.SetActive(false);

        SetSensorHighlight(false);
    }

    void Start()
    {
        if (initialPlacePoint != null)
            ForcePlaceAtStart(initialPlacePoint);
    }

    public bool HasAnyCategory(ItemCategory mask)
    {
        if (mask == ItemCategory.None)
            return false;

        return (categories & mask) != 0;
    }

    public bool CanBePickedUp()
    {
        return state != ItemState.Held;
    }

    public void BeginHold(Transform holdPoint)
    {
        if (holdPoint == null) return;

        if (currentPlacePoint != null)
        {
            lastPlacePointBeforeHold = currentPlacePoint;
            currentPlacePoint.ClearOccupant(this);
            currentPlacePoint = null;
        }
        else if (initialPlacePoint != null)
        {
            lastPlacePointBeforeHold = initialPlacePoint;
        }

        SetSensorHighlight(false);
        SetAttachedPhysics(true);

        transform.SetParent(holdPoint, false);

        if (poseConfig != null)
        {
            transform.localPosition = poseConfig.holdLocalPosition;
            transform.localRotation = Quaternion.Euler(poseConfig.holdLocalEulerAngles);
        }
        else
        {
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        state = ItemState.Held;

        if (debugLog)
            Debug.Log($"[CarryableItem] BeginHold: {name}");
    }

    public bool TryReleaseToPoint(ItemPlacePoint targetPoint)
    {
        if (targetPoint == null)
        {
            if (debugLog)
                Debug.Log($"[CarryableItem] TryReleaseToPoint failed: targetPoint is null for {name}");
            return false;
        }

        if (!targetPoint.CanPlace(this))
        {
            if (debugLog)
                Debug.Log($"[CarryableItem] TryReleaseToPoint failed: targetPoint cannot place {name}");
            return false;
        }

        PlaceAtPoint(targetPoint);
        return true;
    }

    public void PlaceAtPoint(ItemPlacePoint point)
    {
        if (point == null || point.attachPoint == null)
        {
            if (debugLog)
                Debug.LogWarning($"[CarryableItem] PlaceAtPoint failed on {name}: point or attachPoint is null.");
            return;
        }

        if (currentPlacePoint != null && currentPlacePoint != point)
            currentPlacePoint.ClearOccupant(this);

        currentPlacePoint = point;
        currentPlacePoint.SetOccupant(this);

        SetAttachedPhysics(true);
        transform.SetParent(point.attachPoint, false);

        if (poseConfig != null)
        {
            transform.localPosition = poseConfig.placeLocalPosition;
            transform.localRotation = Quaternion.Euler(poseConfig.placeLocalEulerAngles);
        }
        else
        {
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        state = ItemState.Placed;

        if (debugLog)
            Debug.Log($"[CarryableItem] PlaceAtPoint: {name} -> {point.name}");
    }

    public void DropToGround()
    {
        if (currentPlacePoint != null)
        {
            currentPlacePoint.ClearOccupant(this);
            currentPlacePoint = null;
        }

        transform.SetParent(null, true);

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = false;
            rb.useGravity = true;
        }

        if (itemColliders != null)
        {
            foreach (var c in itemColliders)
            {
                if (c == null) continue;
                c.isTrigger = false;
            }
        }

        state = ItemState.Free;

        if (debugLog)
            Debug.Log($"[CarryableItem] Dropped to ground: {name}");
    }

    public void ForcePlaceAtStart(ItemPlacePoint point)
    {
        lastPlacePointBeforeHold = point;
        PlaceAtPoint(point);
    }

    public bool TryUse(PlayerItemInteractor interactor, PlayerInteractionSensor sensor)
    {
        if (!isUsable)
        {
            if (debugLog)
                Debug.Log($"[CarryableItem] Use ignored: {name} is not usable.");
            return false;
        }

        MonoBehaviour[] all = GetComponents<MonoBehaviour>();
        foreach (var mb in all)
        {
            if (mb is IHoldUseTool tool)
            {
                bool used = tool.TryUse(interactor, sensor, this);
                if (used)
                    return true;
            }
        }

        // 没有工具组件接管时，保留你原来的默认行为
        if (useHighlightObject != null)
        {
            bool next = !useHighlightObject.activeSelf;
            useHighlightObject.SetActive(next);

            if (debugLog)
                Debug.Log($"[CarryableItem] Use toggled highlight on {name}: {next}");

            return true;
        }

        if (debugLog)
            Debug.Log($"[CarryableItem] Use called on {name}, but no tool handled it.");

        return false;
    }

    public void SetSensorHighlight(bool on)
    {
        if (sensorHighlight != null)
            sensorHighlight.SetHighlighted(on);
    }

    private void SetAttachedPhysics(bool attached)
    {
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = attached;
            rb.useGravity = !attached;
        }

        if (itemColliders != null)
        {
            foreach (var c in itemColliders)
            {
                if (c == null) continue;
                c.isTrigger = attached;
            }
        }
    }
}