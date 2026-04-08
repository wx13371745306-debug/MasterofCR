using UnityEngine;

/// <summary>
/// 重构后的箱子系统。
/// 打开箱子后对着箱子：
///   - 点按 J：拿一个物品
///   - 长按 J：直接生成一个 Stack 到玩家手中
/// 箱子内不再生成物理实体，彻底避免碰撞弹飞。
/// 剩余 >1 时显示 multipleItemsModel，剩余 ==1 时显示 lastItemModel，
/// 所有拿取均在视线外实例化后送到玩家手上。
/// </summary>
public class SupplyBox : BaseStation
{
    [Header("Supply Settings")]
    [Tooltip("箱子里装的物品预制体（如：番茄）")]
    public GameObject itemPrefab;
    [Tooltip("箱子的总容量")]
    public int maxCount = 20;
    [Tooltip("当前剩余数量（运行时可动态查看）")]
    public int currentCount;

    [Header("Stack Settings")]
    [Tooltip("是否允许长按拿取整组 Stack")]
    public bool enableStackDispense = false;
    [Tooltip("通用的 DynamicItemStack 预制体")]
    public GameObject stackPrefab;
    [Tooltip("一个 Stack 里装多少个（从物品的 StackableProp 读取，这里为手动兜底值）")]
    public int stackSize = 4;
    [Tooltip("Stack 的排布方式")]
    public StackLayout stackLayout = StackLayout.Grid;
    [Tooltip("Stack 的物品间距（Vertical 时为 Y 间距，Grid 时为 XZ 间距）")]
    public float stackOffset = 0.05f;
    [Header("Grid Stack Settings")]
    public int gridColumns = 2;
    public int gridRows = 2;
    public float gridSpacing = 0.15f;

    [Header("Box References")]
    public GameObject closedModel;
    public GameObject openedModel;
    
    [Tooltip("当剩余数量 > 1 时显示的额外装饰模型（请务必将其设为 openedModel 的子物体）")]
    public GameObject multipleItemsModel;

    [Tooltip("当剩余数量 == 1 时显示的最后一个物品装饰模型（请务必将其设为 openedModel 的子物体）")]
    public GameObject lastItemModel;

    [Header("UI References")]
    [Tooltip("箱子上方显示的菜品图标（如：番茄图片）")]
    public GameObject itemIconUI;
    [Tooltip("箱子空了时显示的Empty图标")]
    public GameObject emptyIconUI;

    private CarryableItem boxCarryable;
    private bool isOpened = false;

    // 缓存 interactor 引用
    private PlayerItemInteractor playerInteractor;

    protected override void Awake()
    {
        base.Awake();
        
        boxCarryable = GetComponent<CarryableItem>();
        currentCount = maxCount;

        // 尝试从 itemPrefab 的 StackableProp 上读取堆叠参数
        ReadStackParamsFromPrefab();
        
        // 初始化模型显示状态
        if (closedModel != null) closedModel.SetActive(true);
        if (openedModel != null) openedModel.SetActive(false);

        UpdateItemVisuals(); 
        UpdateUIVisual();
    }

    /// <summary>
    /// 从 itemPrefab 上的 StackableProp 自动读取堆叠参数，
    /// 这样就不用在箱子和物品上重复配置了。
    /// </summary>
    void ReadStackParamsFromPrefab()
    {
        if (itemPrefab == null) return;
        StackableProp sp = itemPrefab.GetComponent<StackableProp>();
        if (sp == null) return;

        stackPrefab = sp.dynamicStackPrefab;
        stackLayout = sp.layoutType;
        stackOffset = sp.stackYOffset;
        gridColumns = sp.gridColumns;
        gridRows = sp.gridRows;
        gridSpacing = sp.gridSpacing;

        if (stackLayout == StackLayout.Grid)
            stackSize = sp.gridColumns * sp.gridRows;
        else
            stackSize = sp.maxStackCount;

        if (debugLog) Debug.Log($"<color=#FFA500>[SupplyBox]</color> 从 {itemPrefab.name} 的 StackableProp 读取了堆叠参数: stackSize={stackSize}, layout={stackLayout}");
    }

    public override bool CanInteract(PlayerItemInteractor interactor)
    {
        return true; 
    }

    public override void BeginInteract(PlayerItemInteractor interactor)
    {
        playerInteractor = interactor;
        isOpened = !isOpened;

        if (isOpened)
        {
            if (closedModel != null) closedModel.SetActive(false);
            if (openedModel != null) openedModel.SetActive(true);

            // 打开箱子时禁止搬走
            if (boxCarryable != null) boxCarryable.isPickable = false;
        }
        else
        {
            if (closedModel != null) closedModel.SetActive(true);
            if (openedModel != null) openedModel.SetActive(false);

            // 关闭箱子时恢复可搬运
            if (boxCarryable != null) boxCarryable.isPickable = true;
        }
        
        UpdateItemVisuals(); 
        UpdateUIVisual(); 
    }

    public override void EndInteract(PlayerItemInteractor interactor)
    {
    }

    /// <summary>
    /// 空手 + 箱子已打开时由 PlayerItemInteractor 调用。
    /// isLongPress = false: 拿一个
    /// isLongPress = true:  拿一个 Stack
    /// </summary>
    public bool TryDispenseToPlayer(PlayerItemInteractor interactor, bool isLongPress)
    {
        if (!isOpened || currentCount <= 0 || interactor.IsHoldingItem()) return false;
        if (itemPrefab == null) return false;

        if (isLongPress && enableStackDispense && stackPrefab != null && currentCount >= 2)
        {
            return DispenseStack(interactor);
        }
        else
        {
            return DispenseSingle(interactor);
        }
    }

    /// <summary>
    /// 生成单个物品直接到玩家手里。
    /// </summary>
    bool DispenseSingle(PlayerItemInteractor interactor)
    {
        GameObject obj = Instantiate(itemPrefab, Vector3.one * -9999f, Quaternion.identity);
        CarryableItem item = obj.GetComponent<CarryableItem>();
        if (item == null)
        {
            Destroy(obj);
            return false;
        }

        item.BeginHold(interactor.GetHoldPoint());
        interactor.ReplaceHeldItem(item);
        currentCount--;

        // 尝试继承箱子自身的腐烂进度
        DecayableProp boxDecay = GetComponent<DecayableProp>();
        if (boxDecay != null)
        {
            DecayableProp itemDecay = item.GetComponent<DecayableProp>();
            if (itemDecay != null) boxDecay.CopyStateTo(itemDecay);
        }

        UpdateItemVisuals();
        UpdateUIVisual();

        if (debugLog) Debug.Log($"<color=#FFA500>[SupplyBox]</color> 拿取了一个 {item.name}，剩余: {currentCount}");
        return true;
    }

    /// <summary>
    /// 生成一个 Stack 直接到玩家手里。
    /// </summary>
    bool DispenseStack(PlayerItemInteractor interactor)
    {
        int dispenseCount = Mathf.Min(stackSize, currentCount);
        
        // 如果只够出一个就退化为单个
        if (dispenseCount <= 1)
            return DispenseSingle(interactor);

        // 在远离场景的位置实例化 Stack 预制体
        GameObject stackObj = Instantiate(stackPrefab, Vector3.one * -9999f, Quaternion.identity);
        DynamicItemStack stack = stackObj.GetComponent<DynamicItemStack>();
        if (stack == null)
        {
            Debug.LogError("[SupplyBox] Stack 预制体上没有 DynamicItemStack 组件!");
            Destroy(stackObj);
            return false;
        }

        // 用批量方法填充 Stack
        stack.InitializeFromPrefab(itemPrefab, dispenseCount,
            stackSize, stackLayout, stackOffset,
            gridColumns, gridRows, gridSpacing);

        stack.BeginHold(interactor.GetHoldPoint());
        interactor.ReplaceHeldItem(stack);
        currentCount -= dispenseCount;

        // 为 Stack 内部所有生成的物品复制箱子的腐烂状态
        DecayableProp boxDecay = GetComponent<DecayableProp>();
        if (boxDecay != null)
        {
            foreach (var stackedItem in stack.GetItems())
            {
                if (stackedItem != null)
                {
                    DecayableProp itemDecay = stackedItem.GetComponent<DecayableProp>();
                    if (itemDecay != null) boxDecay.CopyStateTo(itemDecay);
                }
            }
        }

        UpdateItemVisuals();
        UpdateUIVisual();

        if (debugLog) Debug.Log($"<color=#FFA500>[SupplyBox]</color> 拿取了一个 Stack({dispenseCount}个)，剩余: {currentCount}");
        return true;
    }

    void UpdateItemVisuals()
    {
        if (multipleItemsModel != null)
            multipleItemsModel.SetActive(currentCount > 1);
        if (lastItemModel != null)
            lastItemModel.SetActive(currentCount == 1);
    }

    void UpdateUIVisual()
    {
        if (isOpened)
        {
            if (itemIconUI != null) itemIconUI.SetActive(false);
            if (emptyIconUI != null) emptyIconUI.SetActive(false);
        }
        else
        {
            if (currentCount > 0)
            {
                if (itemIconUI != null) itemIconUI.SetActive(true);
                if (emptyIconUI != null) emptyIconUI.SetActive(false);
            }
            else
            {
                if (itemIconUI != null) itemIconUI.SetActive(false);
                if (emptyIconUI != null) emptyIconUI.SetActive(true);
            }
        }
    }
}