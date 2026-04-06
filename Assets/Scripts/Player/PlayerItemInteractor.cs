using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerItemInteractor : MonoBehaviour
{
    [Header("Refs")]
    public PlayerInteractionSensor sensor;
    public Transform holdPoint;
    public bool debugLog = true;

    [Tooltip("持锅对台装盘：在 Sensor 原点周围搜索空盘")]
    [SerializeField] float plateServeOverlapRadius = 2.2f;

    private CarryableItem heldItem;
    private IInteractiveStation activeStation;

    // 【新增】用于追踪当前真正亮起的高亮目标
    private CarryableItem highlightedItem;
    private ItemPlacePoint highlightedPlacePoint;
    private IInteractiveStation highlightedStation;

    // 【新增】长短按追踪
    private float jKeyPressTime = 0f;
    private bool isJKeyHeld = false;
    private bool hasHandledJKeyThisPress = false;

    public bool IsHoldingItem() => heldItem != null;
    public CarryableItem GetHeldItem() => heldItem;

    void Update()
    {
        if (Keyboard.current == null) return;

        UpdateHighlights(); // 【新增】每帧计算并控制高亮

        // 记录按下时刻
        if (Keyboard.current.jKey.wasPressedThisFrame)
        {
            jKeyPressTime = Time.time;
            isJKeyHeld = true;
            hasHandledJKeyThisPress = false;
        }

        // 按住期间检测是否超过 0.25s，如果是且未处理过，触发长按
        if (isJKeyHeld && Keyboard.current.jKey.isPressed)
        {
            float holdDuration = Time.time - jKeyPressTime;
            if (holdDuration >= 0.25f && !hasHandledJKeyThisPress)
            {
                hasHandledJKeyThisPress = true; // 标记已处理
                if (heldItem == null && activeStation == null)
                    TryBeginHold(isLongPress: true); // 强制拿起整堆
            }
        }

        // 短按松开
        if (Keyboard.current.jKey.wasReleasedThisFrame)
        {
            isJKeyHeld = false;
            if (!hasHandledJKeyThisPress)
            {
                hasHandledJKeyThisPress = true;
                if (heldItem == null && activeStation == null)
                    TryBeginHold(isLongPress: false);
                else if (heldItem != null)
                {
                    if (!TrySpecialInteraction())
                        TryEndHold();
                }
            }
        }

        if (Keyboard.current.kKey.wasPressedThisFrame)
        {
            if (heldItem != null)
            {
                if (activeStation != null) TryEndStationInteract();

                if (!TrySpecialInteraction())
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
            if (item != null && (item.CanBePickedUp() || item is DirtyPlateStack))
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
            // 持物状态：可以放下物体 (J)
            ItemPlacePoint point = sensor.GetCurrentPlacePoint();
            if (point != null && point.CanPlace(heldItem))
            {
                highlightedPlacePoint = point;
                highlightedPlacePoint.SetSensorHighlight(true);
            }

            // 持物状态下也检查堆叠目标（另一个同类物体 或 已有的 Stack）
            CarryableItem stackTarget = sensor.GetCurrentStackTarget();
            if (stackTarget != null)
            {
                highlightedItem = stackTarget;
                highlightedItem.SetSensorHighlight(true);
            }

            // 持物状态下高亮 BinStation（J/K 均可清空/丢弃）
            IInteractiveStation station = sensor.GetCurrentStation();
            if (station is BinStation)
            {
                highlightedStation = station;
                highlightedStation.SetSensorHighlight(true);
            }

            if (highlightedItem == null)
            {
                FryPot heldPot = heldItem.GetComponent<FryPot>();
                if (heldPot != null && heldPot.CanServe())
                {
                    CarryableItem plateTarget = FindServablePlateTarget();
                    if (plateTarget != null)
                    {
                        highlightedItem = plateTarget;
                        highlightedItem.SetSensorHighlight(true);
                    }
                }
            }
        }
    }

    bool TryBeginHold(bool isLongPress = false)
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

        // 【优先级0】如果面对的是一个打开的 SupplyBox，直接由箱子出货
        IInteractiveStation currentStation = sensor.GetCurrentStation();
        if (currentStation is SupplyBox supplyBox)
        {
            bool dispensed = supplyBox.TryDispenseToPlayer(this, isLongPress);
            if (dispensed)
            {
                if (debugLog) Debug.Log($"<color=#FFA500>[大脑 拿取]</color> 从箱子 {supplyBox.name} 中{(isLongPress ? "长按取出Stack" : "点按取出单个")}");
                return true;
            }
        }

        CarryableItem target = sensor.GetCurrentItem();

        // 不仅要目标不为空，还要目标真的能被捡起来！
        // 但 DirtyPlateStack 虽然 isPickable=false，我们仍然需要特殊处理它
        if (target == null)
            return false;

        // 【优先级1】如果目标是 DirtyPlateStack，走分发逻辑而不是直接拿取
        if (target is DirtyPlateStack dirtyStack)
        {
            bool dispensed = dirtyStack.TryDispenseToPlayer(this, isLongPress);
            if (dispensed)
            {
                if (debugLog) Debug.Log($"<color=#8B4513>[大脑 拿取]</color> 从脏盘堆中{(isLongPress ? "长按取出Stack" : "点按取出单个")}");
                return true;
            }
            return false;
        }

        if (!target.CanBePickedUp())
            return false;

        // 【动态堆逻辑改造】：判断目标是不是 DynamicItemStack 堆
        DynamicItemStack targetStack = target as DynamicItemStack;
        
        if (targetStack != null && !isLongPress)
        {
            // 点按(Tap)：弹出堆内的一个物体
            CarryableItem singleItem = targetStack.PopItem();
            if (singleItem != null)
            {
                singleItem.BeginHold(holdPoint);
                heldItem = singleItem;
                if (debugLog) Debug.Log($"<color=#FF00FF>[大脑 拿取]</color> (单点抽出) 拿起了 {singleItem.name}");
                return true;
            }
            return false; // 如果堆空了就失败
        }

        // 正常长按整锅端、或者不是堆的普通物体
        target.BeginHold(holdPoint);
        heldItem = target;

        if (debugLog)
            Debug.Log($"[PlayerItemInteractor] Begin hold: {target.name} | isLongPress: {isLongPress}");

        if (debugLog)
            Debug.Log($"<color=#FF00FF>[大脑 拿取]</color> 按下了J键{(isLongPress?"(长按版)":"(点按)")} 拿起了: {target.name}。它的父级变为了 -> <b>{target.transform.parent?.name}</b>");

        return true;
    }

    void TryEndHold()
    {
        if (heldItem == null) return;

        CarryableItem item = heldItem;

        // 【优先级1】尝试堆叠 —— 如果 Sensor 发现了可堆叠目标
        CarryableItem stackTarget = sensor != null ? sensor.GetCurrentStackTarget() : null;
        if (stackTarget != null && item.GetComponent<StackableProp>() != null)
        {
            // 情况A：目标是已有的 DynamicItemStack → 直接推入
            DynamicItemStack existingStack = stackTarget as DynamicItemStack;
            if (existingStack != null)
            {
                if (existingStack.PushItem(item, sensor))
                {
                    heldItem = null;
                    if (debugLog) Debug.Log($"<color=#00FF00>[大脑 放下]</color> 将 {item.name} 推入已有的 Stack: {existingStack.name}");
                    return;
                }
            }
            else
            {
                // 情况B：目标是另一个落单的同类物品 → 合并创建新 Stack
                StackableProp targetProp = stackTarget.GetComponent<StackableProp>();
                if (targetProp != null)
                {
                    DynamicItemStack newStack = targetProp.MergeIntoStack(item, sensor);
                    if (newStack != null)
                    {
                        heldItem = null;
                        if (debugLog) Debug.Log($"<color=#00FF00>[大脑 放下]</color> {item.name} 和 {stackTarget.name} 合并成了新的 Stack: {newStack.name}");
                        return;
                    }
                }
            }
        }

        // 【优先级2】正常放置到 PlacePoint
        ItemPlacePoint targetPoint = sensor != null ? sensor.GetCurrentPlacePoint() : null;
        if (targetPoint != null)
        {
            if (targetPoint.CanPlace(item) && targetPoint.TryAcceptItem(item))
            {
                heldItem = null;
                if (debugLog) Debug.Log($"<color=#00FF00>[大脑 放下]</color> 成功放置 {item.name} → {targetPoint.name}");
                return;
            }

            // 场景B：PlacePoint 存在但已被占用 → 拒绝放置，物品留在手中
            if (debugLog) Debug.Log($"<color=#FF0000>[大脑 放下]</color> 拒绝放置: {targetPoint.name} 已占用 (占用者: {targetPoint.CurrentItem?.name ?? "N/A"})，物品留在手中");
            return;
        }

        // 场景C：前方无任何 PlacePoint → 丢弃到地面
        if (debugLog) Debug.Log($"<color=#FFFF00>[大脑 放下]</color> 前方无放置点，{item.name} 丢弃到地面");
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

    bool TrySpecialInteraction()
    {
        if (heldItem == null || sensor == null) return false;

        IInteractiveStation station = sensor.GetCurrentStation();
        if (station is BinStation bin)
        {
            if (bin.TryDumpHeldItem(this, heldItem))
                return true;
        }

        PlateTool plateTool = heldItem.GetComponent<PlateTool>();
        if (plateTool != null)
        {
            if (plateTool.TryUse(this, sensor, heldItem))
                return true;
        }

        FryPot heldPot = heldItem.GetComponent<FryPot>();
        if (heldPot != null && heldPot.CanServe())
        {
            CarryableItem targetPlate = FindServablePlateTarget();
            if (targetPlate != null)
            {
                ExecutePotToPlateServe(heldPot, targetPlate);
                return true;
            }
        }

        return false;
    }

    CarryableItem FindServablePlateTarget()
    {
        if (sensor == null) return null;

        Vector3 origin = sensor.sensorOrigin != null ? sensor.sensorOrigin.position : sensor.playerRoot.position;
        Collider[] cols = Physics.OverlapSphere(
            origin,
            plateServeOverlapRadius,
            sensor.itemMask,
            QueryTriggerInteraction.Collide);

        CarryableItem best = null;
        float bestSqr = float.MaxValue;

        foreach (var col in cols)
        {
            if (col == null) continue;
            CarryableItem ci = col.GetComponentInParent<CarryableItem>();
            if (ci == null || ci == heldItem) continue;
            if (ci.State == CarryableItem.ItemState.Held) continue;
            if (!IsValidEmptyPlate(ci)) continue;

            float sqr = (origin - ci.transform.position).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = ci;
            }
        }

        return best;
    }

    bool IsValidEmptyPlate(CarryableItem item)
    {
        if (item.GetComponent<PlateItem>() == null) return false;
        if (item.GetComponent<DishRecipeTag>() != null) return false;
        if (item is DynamicItemStack) return false;
        if (item.GetComponentInParent<DirtyPlateStack>() != null) return false;
        if (item.GetComponentInParent<SupplyBox>() != null) return false;
        return true;
    }

    void ExecutePotToPlateServe(FryPot pot, CarryableItem plate)
    {
        GameObject dishPrefab = pot.Serve();
        if (dishPrefab == null) return;

        ItemPlacePoint platePoint = plate.CurrentPlacePoint;
        Vector3 fallbackPos = plate.transform.position;
        Quaternion fallbackRot = plate.transform.rotation;

        if (platePoint != null)
            platePoint.ClearOccupant();
        Destroy(plate.gameObject);

        GameObject dishObj = Instantiate(dishPrefab);
        CarryableItem dishItem = dishObj.GetComponent<CarryableItem>();

        if (platePoint != null && dishItem != null)
        {
            platePoint.TryAcceptItem(dishItem);
        }
        else
        {
            dishObj.transform.position = fallbackPos;
            dishObj.transform.rotation = fallbackRot;
        }
    }

    public void ReplaceHeldItem(CarryableItem newItem) { heldItem = newItem; }
    public Transform GetHoldPoint() { return holdPoint; }
}