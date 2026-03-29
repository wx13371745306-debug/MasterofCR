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
    public bool isPickable = true;
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

    protected virtual void Awake()
    {
        if (itemColliders == null || itemColliders.Length == 0)
            itemColliders = GetComponentsInChildren<Collider>(includeInactive: true);

        if (useHighlightObject != null)
            useHighlightObject.SetActive(false);

        SetSensorHighlight(false);
    }

    protected virtual void Start()
    {
        if (state == ItemState.Free && initialPlacePoint != null)
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
        return isPickable && state != ItemState.Held;
    }

    public void BeginHold(Transform holdPoint)
    {
        if (holdPoint == null) return;

        lastPlacePointBeforeHold = currentPlacePoint;

        if (currentPlacePoint != null)
        {
            currentPlacePoint.ClearOccupant();
            currentPlacePoint = null;
        }

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
        if (debugLog) Debug.Log($"[CarryableItem] Begin hold: {name}");
    }

    // 放置动作交给目标点位主导
    public bool TryReleaseToPoint(ItemPlacePoint targetPoint)
    {
        if (targetPoint == null) return false;
        return targetPoint.TryAcceptItem(this);
    }

    // 仅供 ItemPlacePoint 内部调用的状态切换
    public void InternalSetPlacedState(ItemPlacePoint point)
    {
        currentPlacePoint = point;
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
        if (debugLog) Debug.Log($"[CarryableItem] Placed internally: {name} at {point.name}");
    }

    // 仅供 ItemPlacePoint 调用的清除占用
    public void ClearPlaceState()
    {
        currentPlacePoint = null;
        state = ItemState.Free;
    }

    public void DropToGround()
    {
        if (currentPlacePoint != null)
        {
            currentPlacePoint.ClearOccupant();
        }

        transform.SetParent(null);
        SetAttachedPhysics(false);

        if (rb != null)
        {
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
        if (debugLog) Debug.Log($"[CarryableItem] Dropped to ground: {name}");
    }

    public void ForcePlaceAtStart(ItemPlacePoint point)
    {
        if (point != null)
        {
            lastPlacePointBeforeHold = point;
            point.TryAcceptItem(this);
        }
    }

    public bool TryUse(PlayerItemInteractor interactor, PlayerInteractionSensor sensor)
    {
        if (!isUsable)
        {
            if (debugLog) Debug.Log($"[CarryableItem] Use ignored: {name} is not usable.");
            return false;
        }

        MonoBehaviour[] all = GetComponents<MonoBehaviour>();
        foreach (var mb in all)
        {
            if (mb is IHoldUseTool tool)
            {
                bool used = tool.TryUse(interactor, sensor, this);
                if (used) return true;
            }
        }

        if (useHighlightObject != null)
        {
            bool next = !useHighlightObject.activeSelf;
            useHighlightObject.SetActive(next);
            if (debugLog) Debug.Log($"[CarryableItem] Use toggled highlight on {name}: {next}");
            return true;
        }

        if (debugLog) Debug.Log($"[CarryableItem] Use called on {name}, but no tool handled it.");
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
            if (attached)
            {
                // 变成被附着状态：先清空物理动量，再开启运动学
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
                rb.useGravity = false;
            }
            else
            {
                // 掉落到物理世界：先关闭运动学，再清空速度
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
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
