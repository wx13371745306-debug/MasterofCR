using System.Collections.Generic;
using UnityEngine;

public class PlayerInteractionSensor : MonoBehaviour
{
    [Header("Masks")]
    public LayerMask itemMask;
    public LayerMask placePointMask;
    public LayerMask stationMask;

    [Header("Refs")]
    public Transform playerRoot;
    public PlayerItemInteractor interactor;

    [Header("Debug")]
    public bool debugLog = true;

    private readonly HashSet<CarryableItem> itemCandidates = new HashSet<CarryableItem>();
    private readonly HashSet<ItemPlacePoint> placePointCandidates = new HashSet<ItemPlacePoint>();
    private readonly HashSet<IInteractiveStation> stationCandidates = new HashSet<IInteractiveStation>();

    private CarryableItem currentItem;
    private ItemPlacePoint currentPlacePoint;
    private IInteractiveStation currentStation;

    void Update()
    {
        if (playerRoot == null || interactor == null) return;

        if (interactor.IsHoldingItem())
        {
            ClearCurrentItem();
            ClearCurrentStation();
            PickNearestPlacePoint();
        }
        else
        {
            ClearCurrentPlacePoint();

            // 优先 item，其次 station
            PickNearestItem();

            if (currentItem != null)
            {
                ClearCurrentStation();
            }
            else
            {
                PickNearestStation();
            }
        }
    }

    void PickNearestItem()
    {
        CarryableItem best = null;
        float bestDist = float.MaxValue;
        Vector3 origin = playerRoot.position;

        foreach (var item in itemCandidates)
        {
            if (item == null) continue;
            if (!item.CanBePickedUp()) continue;

            float dist = Vector3.Distance(origin, item.transform.position);
            if (dist < bestDist - 0.001f)
            {
                bestDist = dist;
                best = item;
            }
            else if (Mathf.Abs(dist - bestDist) <= 0.001f && best != null)
            {
                if (item.GetInstanceID() < best.GetInstanceID())
                    best = item;
            }
        }

        if (best != currentItem)
        {
            if (currentItem != null)
                currentItem.SetSensorHighlight(false);

            currentItem = best;

            if (currentItem != null)
                currentItem.SetSensorHighlight(true);

            if (debugLog)
            {
                string to = best == null ? "None" : best.name;
                Debug.Log($"[PlayerInteractionSensor] Item switch -> {to}");
            }
        }
    }

    void PickNearestPlacePoint()
    {
        ItemPlacePoint best = null;
        float bestDist = float.MaxValue;
        Vector3 origin = playerRoot.position;
        CarryableItem heldItem = interactor.GetHeldItem();

        foreach (var point in placePointCandidates)
        {
            if (point == null) continue;
            if (heldItem != null && !point.CanPlace(heldItem)) continue;

            float dist = Vector3.Distance(origin, point.transform.position);
            if (dist < bestDist - 0.001f)
            {
                bestDist = dist;
                best = point;
            }
            else if (Mathf.Abs(dist - bestDist) <= 0.001f && best != null)
            {
                if (point.GetInstanceID() < best.GetInstanceID())
                    best = point;
            }
        }

        if (best != currentPlacePoint)
        {
            if (currentPlacePoint != null)
                currentPlacePoint.SetSensorHighlight(false);

            currentPlacePoint = best;

            if (currentPlacePoint != null)
                currentPlacePoint.SetSensorHighlight(true);

            if (debugLog)
            {
                string to = best == null ? "None" : best.name;
                Debug.Log($"[PlayerInteractionSensor] PlacePoint switch -> {to}");
            }
        }
    }

    void PickNearestStation()
    {
        IInteractiveStation best = null;
        float bestDist = float.MaxValue;
        Vector3 origin = playerRoot.position;

        foreach (var station in stationCandidates)
        {
            if (station == null) continue;
            if (!station.CanInteract(interactor)) continue;

            MonoBehaviour mb = station as MonoBehaviour;
            if (mb == null) continue;

            float dist = Vector3.Distance(origin, mb.transform.position);
            if (dist < bestDist - 0.001f)
            {
                bestDist = dist;
                best = station;
            }
            else if (Mathf.Abs(dist - bestDist) <= 0.001f && best != null)
            {
                if (mb.GetInstanceID() < (best as MonoBehaviour).GetInstanceID())
                    best = station;
            }
        }

        if (best != currentStation)
        {
            if (currentStation != null)
                currentStation.SetSensorHighlight(false);

            currentStation = best;

            if (currentStation != null)
                currentStation.SetSensorHighlight(true);

            if (debugLog)
            {
                string to = "None";
                if (best != null)
                {
                    MonoBehaviour mb = best as MonoBehaviour;
                    if (mb != null) to = mb.name;
                }
                Debug.Log($"[PlayerInteractionSensor] Station switch -> {to}");
            }
        }
    }

    void ClearCurrentItem()
    {
        if (currentItem != null)
        {
            currentItem.SetSensorHighlight(false);
            currentItem = null;
        }
    }

    void ClearCurrentPlacePoint()
    {
        if (currentPlacePoint != null)
        {
            currentPlacePoint.SetSensorHighlight(false);
            currentPlacePoint = null;
        }
    }

    void ClearCurrentStation()
    {
        if (currentStation != null)
        {
            currentStation.SetSensorHighlight(false);
            currentStation = null;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (((1 << other.gameObject.layer) & itemMask) != 0)
        {
            CarryableItem item = other.GetComponentInParent<CarryableItem>();
            if (item != null)
                itemCandidates.Add(item);
        }

        if (((1 << other.gameObject.layer) & placePointMask) != 0)
        {
            ItemPlacePoint point = other.GetComponentInParent<ItemPlacePoint>();
            if (point != null)
                placePointCandidates.Add(point);
        }

        if (((1 << other.gameObject.layer) & stationMask) != 0)
        {
            IInteractiveStation station = other.GetComponentInParent<IInteractiveStation>();
            if (station != null)
                stationCandidates.Add(station);
        }
    }

    void OnTriggerExit(Collider other)
    {
        CarryableItem item = other.GetComponentInParent<CarryableItem>();
        if (item != null)
        {
            itemCandidates.Remove(item);
            if (item == currentItem)
            {
                item.SetSensorHighlight(false);
                currentItem = null;
            }
        }

        ItemPlacePoint point = other.GetComponentInParent<ItemPlacePoint>();
        if (point != null)
        {
            placePointCandidates.Remove(point);
            if (point == currentPlacePoint)
            {
                point.SetSensorHighlight(false);
                currentPlacePoint = null;
            }
        }

        IInteractiveStation station = other.GetComponentInParent<IInteractiveStation>();
        if (station != null)
        {
            stationCandidates.Remove(station);
            if (station == currentStation)
            {
                station.SetSensorHighlight(false);
                currentStation = null;
            }
        }
    }

    public CarryableItem GetCurrentItem()
    {
        return currentItem;
    }

    public ItemPlacePoint GetCurrentPlacePoint()
    {
        return currentPlacePoint;
    }

    public IInteractiveStation GetCurrentStation()
    {
        return currentStation;
    }
}