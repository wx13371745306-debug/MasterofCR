using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerItemInteractor : MonoBehaviour
{
    [Header("Refs")]
    public PlayerInteractionSensor sensor;
    public Transform holdPoint;
    public bool debugLog = true;

    private CarryableItem heldItem;
    private IInteractiveStation activeStation;

    // 【新增】用于追踪当前真正亮起的高亮目标
    private CarryableItem highlightedItem;
    private ItemPlacePoint highlightedPlacePoint;
    private IInteractiveStation highlightedStation;

    public bool IsHoldingItem() => heldItem != null;
    public CarryableItem GetHeldItem() => heldItem;

    void Update()
    {
        if (Keyboard.current == null) return;

        UpdateHighlights(); // 【新增】每帧计算并控制高亮

        if (Keyboard.current.jKey.wasPressedThisFrame && heldItem == null && activeStation == null)
            TryBeginHold();

        if (Keyboard.current.jKey.wasReleasedThisFrame && heldItem != null)
            TryEndHold();

        if (Keyboard.current.kKey.wasPressedThisFrame)
        {
            if (heldItem != null)
            {
                if (activeStation != null) TryEndStationInteract();
                heldItem.TryUse(this, sensor);
            }
            else if (activeStation == null)
            {
                TryBeginStationInteract();
            }
        }

        if (Keyboard.current.kKey.wasReleasedThisFrame && activeStation != null)
            TryEndStationInteract();
    }

    // 【核心新增模块】统一管理所有高亮
    void UpdateHighlights()
    {
        // 1. 清理上一帧的高亮
        if (highlightedItem != null) { highlightedItem.SetSensorHighlight(false); highlightedItem = null; }
        if (highlightedPlacePoint != null) { highlightedPlacePoint.SetSensorHighlight(false); highlightedPlacePoint = null; }
        if (highlightedStation != null) { highlightedStation.SetSensorHighlight(false); highlightedStation = null; }

        if (sensor == null) return;

        // 2. 根据玩家状态重新计算合法的高亮目标
        if (!IsHoldingItem())
        {
            // 空手状态：可以捡起物体 (J)，可以互动台子 (K)
            CarryableItem item = sensor.GetCurrentItem();
            if (item != null && item.CanBePickedUp())
            {
                highlightedItem = item;
                highlightedItem.SetSensorHighlight(true);
            }

            IInteractiveStation station = sensor.GetCurrentStation();
            if (station != null && station.CanInteract(this))
            {
                highlightedStation = station;
                highlightedStation.SetSensorHighlight(true);
            }
        }
        else
        {
            // 持物状态：可以放下物体 (J)，也可以对台子使用工具 (K)
            ItemPlacePoint point = sensor.GetCurrentPlacePoint();
            if (point != null && point.CanPlace(heldItem))
            {
                highlightedPlacePoint = point;
                highlightedPlacePoint.SetSensorHighlight(true);
            }

            // (如果有特殊的工具对 Station 的互动需求，可以写在这里)
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

        // 【核心修复】：不仅要目标不为空，还要目标真的能被捡起来！
        if (target == null || !target.CanBePickedUp())
            return false;

        target.BeginHold(holdPoint);
        heldItem = target;



        if (debugLog)
            Debug.Log($"[PlayerItemInteractor] Begin hold: {target.name}");

        if (debugLog)
            Debug.Log($"<color=#FF00FF>[大脑 拿取]</color> 按下J键拿起了: {target.name}。它的父级瞬间从原来的变成了 -> <b>{target.transform.parent.name}</b>");

        return true;
    }

    void TryEndHold()
    {
        if (heldItem == null) return;

        ItemPlacePoint targetPoint = sensor != null ? sensor.GetCurrentPlacePoint() : null;
        CarryableItem item = heldItem;

        // 只有在点位合法时才执行放置
        if (targetPoint != null && targetPoint.CanPlace(item))
        {
            if (targetPoint.TryAcceptItem(item)) // 【修改点】调用新的放置接口
            {
                heldItem = null;
                return;
            }
        }

        // 不合法或没对准，掉地上
        item.DropToGround();
        heldItem = null;
    }

    bool TryBeginStationInteract()
    {
        if (sensor == null) return false;
        IInteractiveStation station = sensor.GetCurrentStation();
        if (station == null || !station.CanInteract(this)) return false;

        activeStation = station;
        activeStation.BeginInteract(this);
        return true;
    }

    void TryEndStationInteract()
    {
        if (activeStation == null) return;
        activeStation.EndInteract(this);
        activeStation = null;
    }

    public void ReplaceHeldItem(CarryableItem newItem) { heldItem = newItem; }
    public Transform GetHoldPoint() { return holdPoint; }
}