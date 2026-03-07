using UnityEngine;

public class PlaceableSurface : MonoBehaviour
{
    public Transform placePoint;
    public GameObject highlightObject;
    public bool isOccupied = false;
    public bool debugLog = true;

    void Awake()
    {
        SetHighlighted(false);
    }

    public void SetHighlighted(bool on)
    {
        if (highlightObject != null)
            highlightObject.SetActive(on);

        if (debugLog)
            Debug.Log($"[PlaceableSurface] Highlight {(on ? "ON" : "OFF")} : {name}");
    }

    public bool CanPlace()
    {
        return !isOccupied;
    }

    public void MarkOccupied(bool occupied)
    {
        isOccupied = occupied;

        if (debugLog)
            Debug.Log($"[PlaceableSurface] {name} occupied = {isOccupied}");
    }

    public void RemoveDish()
    {
        isOccupied = false;
    }
}