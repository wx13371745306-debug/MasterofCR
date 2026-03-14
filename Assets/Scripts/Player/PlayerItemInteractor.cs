using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerItemInteractor : MonoBehaviour
{
    [Header("Refs")]
    public PlayerInteractionSensor sensor;
    public Transform holdPoint;

    [Header("Debug")]
    public bool debugLog = true;

    private CarryableItem heldItem;
    private IInteractiveStation activeStation;

    public bool IsHoldingItem()
    {
        return heldItem != null;
    }

    public CarryableItem GetHeldItem()
    {
        return heldItem;
    }

    public bool IsInteractingStation()
    {
        return activeStation != null;
    }

    void Update()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.jKey.wasPressedThisFrame)
        {
            if (heldItem == null && activeStation == null)
            {
                TryBeginHold();
            }
        }

        if (Keyboard.current.kKey.wasPressedThisFrame)
        {
            if (heldItem != null)
            {
                if (activeStation != null)
                    TryEndStationInteract();

                // 规范形式：Interactor 只负责触发 heldItem 的 use
                heldItem.TryUse(this, sensor);
            }
            else
            {
                if (activeStation == null)
                {
                    bool began = TryBeginStationInteract();
                    if (!began && debugLog)
                        Debug.Log("[PlayerItemInteractor] Station interact ignored: no available station.");
                }
            }
        }

        if (Keyboard.current.jKey.wasReleasedThisFrame)
        {
            if (heldItem != null)
                TryEndHold();
        }

        if (Keyboard.current.kKey.wasReleasedThisFrame)
        {
            if (activeStation != null)
                TryEndStationInteract();
        }
    }

    bool TryBeginHold()
    {
        if (sensor == null)
        {
            if (debugLog) Debug.Log("[PlayerItemInteractor] Pick failed: sensor is null.");
            return false;
        }

        if (holdPoint == null)
        {
            if (debugLog) Debug.Log("[PlayerItemInteractor] Pick failed: holdPoint is null.");
            return false;
        }

        CarryableItem target = sensor.GetCurrentItem();
        if (target == null)
            return false;

        target.BeginHold(holdPoint);
        heldItem = target;

        if (debugLog)
            Debug.Log($"[PlayerItemInteractor] Begin hold: {target.name}");

        return true;
    }

    void TryEndHold()
    {
        if (heldItem == null) return;

        ItemPlacePoint targetPoint = sensor != null ? sensor.GetCurrentPlacePoint() : null;
        CarryableItem item = heldItem;

        if (targetPoint != null && targetPoint.CanPlace(item))
        {
            bool released = item.TryReleaseToPoint(targetPoint);
            if (!released)
            {
                if (debugLog)
                    Debug.LogWarning($"[PlayerItemInteractor] Release failed for {item.name}.");
                return;
            }

            heldItem = null;

            if (debugLog)
            {
                string pointName = item.CurrentPlacePoint != null ? item.CurrentPlacePoint.name : "None";
                Debug.Log($"[PlayerItemInteractor] End hold: {item.name} -> {pointName}");
            }

            return;
        }

        item.DropToGround();
        heldItem = null;

        if (debugLog)
            Debug.Log($"[PlayerItemInteractor] End hold: {item.name} -> Dropped to ground");
    }

    bool TryBeginStationInteract()
    {
        if (sensor == null) return false;

        IInteractiveStation station = sensor.GetCurrentStation();
        if (station == null) return false;
        if (!station.CanInteract(this)) return false;

        activeStation = station;
        activeStation.BeginInteract(this);

        if (debugLog)
        {
            MonoBehaviour mb = activeStation as MonoBehaviour;
            string stationName = mb != null ? mb.name : "UnknownStation";
            Debug.Log($"[PlayerItemInteractor] Begin station interact: {stationName}");
        }

        return true;
    }

    void TryEndStationInteract()
    {
        if (activeStation == null) return;

        MonoBehaviour mb = activeStation as MonoBehaviour;
        string stationName = mb != null ? mb.name : "UnknownStation";

        activeStation.EndInteract(this);
        activeStation = null;

        if (debugLog)
            Debug.Log($"[PlayerItemInteractor] End station interact: {stationName}");
    }

    // 给工具脚本用的小工具函数：允许工具替换当前手上的物体
    public void ReplaceHeldItem(CarryableItem newItem)
    {
        heldItem = newItem;
    }

    public Transform GetHoldPoint()
    {
        return holdPoint;
    }
}