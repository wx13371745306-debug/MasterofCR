using UnityEngine;

/// <summary>
/// 【纯数据+触发器组件】
/// 挂载在可以被堆起来的单个物品上（如西红柿、盘子）。
/// 自身不创建任何子物体或 PlacePoint，只提供堆叠配置参数和兼容性检查方法。
/// 真正的堆叠检测和执行由 PlayerInteractionSensor + PlayerItemInteractor 负责。
/// </summary>
[RequireComponent(typeof(CarryableItem))]
public class StackableProp : MonoBehaviour
{
    [Header("Stack Rules")]
    [Tooltip("存放包含DynamicItemStack脚本的空栈预制体")]
    public GameObject dynamicStackPrefab;
    public StackLayout layoutType = StackLayout.Vertical;
    [Tooltip("仅 Vertical 模式有效：最大堆叠数量")]
    public int maxStackCount = 5;
    [Tooltip("物品间距（Vertical = Y轴间距，Grid = XZ轴间距）")]
    public float stackYOffset = 0.05f;

    [Header("Grid Layout (仅 Grid 模式)")]
    [Tooltip("每排几个")]
    public int gridColumns = 2;
    [Tooltip("一共几排")]
    public int gridRows = 2;
    [Tooltip("Grid 模式下物品之间的 XZ 间距")]
    public float gridSpacing = 0.15f;

    [Header("Debug")]
    public bool debugLog = true;

    private CarryableItem myItem;

    public CarryableItem MyItem => myItem;

    void Awake()
    {
        myItem = GetComponent<CarryableItem>();
    }

    /// <summary>
    /// 检查另一个物品是否能和我堆在一起。
    /// 条件：对方也有 StackableProp，并且和我有相同的 ItemCategory。
    /// </summary>
    public bool CanStackWith(CarryableItem other)
    {
        if (other == null || other == myItem) return false;
        // 对方必须也能被堆叠
        StackableProp otherProp = other.GetComponent<StackableProp>();
        if (otherProp == null) return false;
        // 类别必须有交集
        return other.HasAnyCategory(myItem.categories);
    }

    /// <summary>
    /// 将自己(桌上的落单物体)和一个新来的同类物品合并成一个 Stack。
    /// 返回新创建的 DynamicItemStack，如果失败返回 null。
    /// sensor 参数用于将新建的 Stack 手动注册到 Sensor 中（因为原地创建不触发 OnTriggerEnter）。
    /// </summary>
    public DynamicItemStack MergeIntoStack(CarryableItem incomingItem, PlayerInteractionSensor sensor = null)
    {
        if (incomingItem == null || dynamicStackPrefab == null)
        {
            if (debugLog) Debug.LogError($"<color=#FF0000>[StackableProp]</color> MergeIntoStack 失败: incomingItem={incomingItem}, prefab={dynamicStackPrefab}");
            return null;
        }

        if (incomingItem == myItem)
        {
            if (debugLog) Debug.LogWarning($"<color=#FF9900>[StackableProp]</color> 不能把自己和自己合并！");
            return null;
        }

        if (!CanStackWith(incomingItem))
        {
            if (debugLog) Debug.LogWarning($"<color=#FF0000>[StackableProp]</color> 类别不匹配！我是 {myItem.categories}，对方是 {incomingItem.categories}");
            return null;
        }

        if (debugLog) Debug.Log($"<color=#00FF00>[StackableProp]</color> 开始合并: {myItem.name} + {incomingItem.name} -> DynamicItemStack");

        // 1. 获取我们自己当前坐在哪里 (哪张桌子)
        ItemPlacePoint currentTable = myItem.CurrentPlacePoint;

        // 2. 实例化通用的堆预制体
        GameObject stackObj = Instantiate(dynamicStackPrefab, transform.position, transform.rotation);
        DynamicItemStack newStack = stackObj.GetComponent<DynamicItemStack>();
        if (newStack == null)
        {
            Debug.LogError("[StackableProp] 实例化出来的物体上没有 DynamicItemStack 脚本！请检查预制体配置。");
            Destroy(stackObj);
            return null;
        }

        // 3. 把新建立的 Stack 放置到我们原来呆着的桌子上
        if (currentTable != null)
        {
            myItem.ClearPlaceState();
            currentTable.ClearOccupant();
            newStack.ForcePlaceAtStart(currentTable);
        }

        // 4. 让 Stack 吸收我们俩
        int effectiveMaxCount = (layoutType == StackLayout.Grid) ? gridColumns * gridRows : maxStackCount;
        newStack.Initialize(myItem, incomingItem, effectiveMaxCount, layoutType, stackYOffset, gridColumns, gridRows, gridSpacing);

        if (debugLog) Debug.Log($"<color=#00FF00>[StackableProp]</color> 合并完成！{myItem.name} 和 {incomingItem.name} 已归入新的 Stack: {stackObj.name}");

        return newStack;
    }
}
