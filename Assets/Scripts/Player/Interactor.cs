using UnityEngine;
using UnityEngine.InputSystem;

public class Interactor : MonoBehaviour
{
    [Header("Refs")]
    public InteractSensor sensor;
    public Transform holdPoint;     // 手持搬运挂点（现在先只用一个）
    public Transform equipPoint;    // 装备挂点

    [Header("Debug")]
    public bool debugLog = true;

    // 当前手里拿着的物体（二选一）
    private CarryableDish heldDish;
    private CarryableTool heldTool;

    // 当前已装备的工具
    private CarryableTool equippedTool;

    void Update()
    {
        // 空格按下
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            if (equippedTool != null)
            {
                TryUseEquippedTool();
            }
            else
            {
                TryPickUp();
            }
        }

        // 空格松开
        if (Keyboard.current.spaceKey.wasReleasedThisFrame)
        {
            // 只有“未装备状态”下，空格松开才表示放手/放下
            if (equippedTool == null)
            {
                TryDropHeldItem();
            }
        }

        // J：装备 / 卸下
        if (Keyboard.current.jKey.wasPressedThisFrame)
        {
            if (equippedTool != null)
            {
                TryUnequipTool();
            }
            else
            {
                TryEquipHeldTool();
            }
        }
    }

    // 给 InteractSensor 用：
    // 只要手里拿着东西，或者已经装备了工具，都算“正在持有东西”
    public bool IsHoldingSomething()
    {
        return heldDish != null || heldTool != null || equippedTool != null;
    }

    public bool HasEquippedTool()
    {
        return equippedTool != null;
    }

    public CarryableTool GetEquippedTool()
    {
        return equippedTool;
    }

    void TryPickUp()
    {
        // 已装备工具时，不允许再拿别的
        if (equippedTool != null)
        {
            if (debugLog)
                Debug.Log("[Interactor] PickUp blocked: already equipped with a tool.");
            return;
        }

        // 手里已经拿着一个东西时，不允许再拿
        if (heldDish != null || heldTool != null)
        {
            if (debugLog)
                Debug.Log("[Interactor] PickUp blocked: already holding an item.");
            return;
        }

        var currentHighlight = sensor ? sensor.GetCurrentDish() : null;
        if (currentHighlight == null)
        {
            if (debugLog)
                Debug.Log("[Interactor] No current item to pick up.");
            return;
        }

        // 先判断是不是 Tool
        CarryableTool tool = currentHighlight.GetComponentInParent<CarryableTool>();
        if (tool != null)
        {
            if (holdPoint == null)
            {
                if (debugLog)
                    Debug.Log("[Interactor] FAIL: holdPoint is not assigned.");
                return;
            }

            tool.PickUp(holdPoint);
            heldTool = tool;

            if (debugLog)
                Debug.Log($"[Interactor] Picked up tool: {tool.name}");
            return;
        }

        // 再判断是不是 Dish
        CarryableDish dish = currentHighlight.GetComponentInParent<CarryableDish>();
        if (dish != null)
        {
            if (holdPoint == null)
            {
                if (debugLog)
                    Debug.Log("[Interactor] FAIL: holdPoint is not assigned.");
                return;
            }

            dish.PickUp(holdPoint);
            heldDish = dish;

            if (debugLog)
                Debug.Log($"[Interactor] Picked up dish: {dish.name}");
            return;
        }

        if (debugLog)
            Debug.Log($"[Interactor] FAIL: {currentHighlight.name} has neither CarryableTool nor CarryableDish.");
    }

    void TryDropHeldItem()
    {
        // 优先处理手里拿着的 Dish
        if (heldDish != null)
        {
            CarryableDish dish = heldDish;
            heldDish = null;

            PlaceableSurface surface = sensor ? sensor.GetCurrentSurface() : null;

            if (surface != null && surface.CanPlace())
            {
                if (debugLog)
                    Debug.Log($"[Interactor] Place dish on surface: {dish.name} -> {surface.name}");

                dish.PlaceOnSurface(surface);
            }
            else
            {
                if (debugLog)
                    Debug.Log($"[Interactor] Drop dish to ground: {dish.name}");

                dish.Drop();
            }

            return;
        }

        // 再处理手里拿着的 Tool
        if (heldTool != null)
        {
            CarryableTool tool = heldTool;
            heldTool = null;

            PlaceableSurface surface = sensor ? sensor.GetCurrentSurface() : null;

            if (surface != null && surface.CanPlace())
            {
                if (debugLog)
                    Debug.Log($"[Interactor] Place tool on surface: {tool.name} -> {surface.name}");

                tool.PlaceOnSurface(surface);
            }
            else
            {
                if (debugLog)
                    Debug.Log($"[Interactor] Drop tool to ground: {tool.name}");

                tool.Drop();
            }
            return;
        }

        if (debugLog)
            Debug.Log("[Interactor] Drop ignored: nothing held.");
    }

    void TryEquipHeldTool()
    {
        if (equippedTool != null)
        {
            if (debugLog)
                Debug.Log("[Interactor] Equip blocked: already have an equipped tool.");
            return;
        }

        if (heldTool == null)
        {
            if (debugLog)
                Debug.Log("[Interactor] Equip ignored: no held tool.");
            return;
        }

        if (equipPoint == null)
        {
            if (debugLog)
                Debug.Log("[Interactor] Equip failed: equipPoint is not assigned.");
            return;
        }

        equippedTool = heldTool;
        heldTool = null;

        equippedTool.Equip(equipPoint);

        if (debugLog)
            Debug.Log($"[Interactor] Equipped tool: {equippedTool.name}");
    }

    void TryUnequipTool()
    {
        if (equippedTool == null)
        {
            if (debugLog)
                Debug.Log("[Interactor] Unequip ignored: no equipped tool.");
            return;
        }

        CarryableTool tool = equippedTool;
        equippedTool = null;

        // 当前最小版本：卸下直接掉地上
        // 以后如果你想支持“优先放 PlacePoint”，
        // 给 CarryableTool 增加 PlaceOnSurface() 就可以了。
        tool.UnequipToGround();

        if (debugLog)
            Debug.Log($"[Interactor] Unequipped tool: {tool.name}");
    }

    void TryUseEquippedTool()
    {
        if (equippedTool == null)
        {
            if (debugLog)
                Debug.Log("[Interactor] Use ignored: no equipped tool.");
            return;
        }

        // 当前先只打印日志
        // 以后这里可以根据工具类型分发功能：
        // Knife -> 切菜
        // Broom -> 清理污渍
        // Plate -> 盛菜
        if (debugLog)
            Debug.Log($"[Interactor] Use equipped tool: {equippedTool.name}");
    }
}