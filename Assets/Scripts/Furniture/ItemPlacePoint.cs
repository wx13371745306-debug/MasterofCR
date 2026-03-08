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

    public void SetSensorHighlight(bool on)
    {
        if (sensorHighlight != null)
            sensorHighlight.SetHighlighted(on);
    }
}
