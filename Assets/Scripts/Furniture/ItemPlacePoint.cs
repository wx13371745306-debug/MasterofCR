using UnityEngine;

public class ItemPlacePoint : MonoBehaviour
{
    [Header("Refs")]
    public Transform attachPoint;
    public InteractableHighlight sensorHighlight;

    [Header("Placement Rules")]
    public bool allowAnyCategory = true;
    public ItemCategory allowedCategories = ItemCategory.None;

    [Header("Debug")]
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
        if (item == null)
            return false;

        if (currentItem != null && currentItem != item)
            return false;

        if (allowAnyCategory)
            return true;

        bool allowed = item.HasAnyCategory(allowedCategories);

        if (debugLog && !allowed)
            Debug.Log($"[ItemPlacePoint] CanPlace rejected: {item.name} categories={item.categories} not in allowed={allowedCategories} on {name}");

        return allowed;
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