using UnityEngine;

public class ItemPlacePoint : MonoBehaviour
{
    public Transform attachPoint;
    public InteractableHighlight sensorHighlight;
    public bool debugLog = true;

    public CarryableItem CurrentItem => currentItem;

    private CarryableItem currentItem;

    void Reset()
    {
        if (attachPoint == null)
            attachPoint = transform;

        if (sensorHighlight == null)
            sensorHighlight = GetComponent<InteractableHighlight>();
    }

    void Awake()
    {
        if (attachPoint == null)
            attachPoint = transform;

        SetSensorHighlight(false);
    }

    public bool CanPlace(CarryableItem item)
    {
        return currentItem == null || currentItem == item;
    }

    public void SetOccupant(CarryableItem item)
    {
        currentItem = item;

        if (debugLog)
            Debug.Log($"[ItemPlacePoint] Occupant set: {name} -> {(item != null ? item.name : "None")}");
    }

    public void ClearOccupant(CarryableItem item)
    {
        if (currentItem == item)
        {
            currentItem = null;

            if (debugLog)
                Debug.Log($"[ItemPlacePoint] Occupant cleared: {name}");
        }
    }

    public bool ForcePlace(CarryableItem item)
    {
        if (item == null)
            return false;

        if (!CanPlace(item))
            return false;

        // 不自己硬改 transform，不自己硬改物理
        // 直接复用项目里已经稳定工作的“正式放置流程”
        bool placed = item.TryReleaseToPoint(this);

        if (debugLog)
            Debug.Log($"[ItemPlacePoint] ForcePlace: {name} -> {(placed ? item.name : "FAILED")}");

        return placed;
    }

    public void SetSensorHighlight(bool on)
    {
        if (sensorHighlight != null)
            sensorHighlight.SetHighlighted(on);
    }
}