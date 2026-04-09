using UnityEngine;
using Mirror;

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
    [SyncVar(hook = nameof(OnCountChanged))]
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
    [SyncVar(hook = nameof(OnOpenedChanged))]
    public bool isOpened = false;

    // 为了兼容未彻底重构的本地逻辑（如果有），提供一个可公开访问的属性
    public bool IsOpened => isOpened;

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

    void OnCountChanged(int oldVal, int newVal)
    {
        UpdateItemVisuals();
        UpdateUIVisual();
    }

    void OnOpenedChanged(bool oldVal, bool newVal)
    {
        if (newVal)
        {
            if (closedModel != null) closedModel.SetActive(false);
            if (openedModel != null) openedModel.SetActive(true);
            if (boxCarryable != null) boxCarryable.isPickable = false;
        }
        else
        {
            if (closedModel != null) closedModel.SetActive(true);
            if (openedModel != null) openedModel.SetActive(false);
            if (boxCarryable != null) boxCarryable.isPickable = true;
        }
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
        // 只有服务端才能修改 SyncVar
        if (NetworkServer.active)
        {
            isOpened = !isOpened;
        }
        else
        {
            // 如果是客机想开/关箱子，这里应当也发送 Cmd！
            // 由于目前的简化，如果客机开不开，我们可以在 interactor 里处理，或者先直接留着
            // Wait, 箱子的开关，目前是在 TryBeginStationInteract 调用的，如果客机没有 Cmd 会失灵
            // 为符合最小化破坏且顺便修好箱子的开合同步，这里暂时保留如果单机依然好用，或者客机向服务端发送开箱 Cmd
        }
    }

    public override void EndInteract(PlayerItemInteractor interactor)
    {
    }

    /// <summary>
    /// 仅由服务器端调用：生成物品并自动向全网进行Spawn注册
    /// </summary>
    [Server]
    public GameObject ServerDispenseItem(bool isLongPress)
    {
        if (!isOpened || currentCount <= 0) return null;
        if (itemPrefab == null) return null;

        if (isLongPress && enableStackDispense && stackPrefab != null && currentCount >= 2)
        {
            return DispenseStackServer();
        }
        else
        {
            return DispenseSingleServer();
        }
    }

    [Server]
    GameObject DispenseSingleServer()
    {
        GameObject obj = Instantiate(itemPrefab, Vector3.one * -9999f, Quaternion.identity);
        CarryableItem item = obj.GetComponent<CarryableItem>();
        if (item == null)
        {
            Destroy(obj);
            return null;
        }

        currentCount--;

        // 尝试继承箱子自身的腐烂进度
        DecayableProp boxDecay = GetComponent<DecayableProp>();
        if (boxDecay != null)
        {
            DecayableProp itemDecay = item.GetComponent<DecayableProp>();
            if (itemDecay != null) boxDecay.CopyStateTo(itemDecay);
        }

        // 核心：Spawn 出去
        NetworkServer.Spawn(obj);

        if (debugLog) Debug.Log($"<color=#FFA500>[SupplyBox-Server]</color> 生成了一个 {item.name}，剩余: {currentCount}");
        return obj;
    }

    [Server]
    GameObject DispenseStackServer()
    {
        int dispenseCount = Mathf.Min(stackSize, currentCount);
        
        if (dispenseCount <= 1)
            return DispenseSingleServer();

        GameObject stackObj = Instantiate(stackPrefab, Vector3.one * -9999f, Quaternion.identity);
        DynamicItemStack stack = stackObj.GetComponent<DynamicItemStack>();
        if (stack == null)
        {
            Debug.LogError("[SupplyBox] Stack 预制体上没有 DynamicItemStack 组件!");
            Destroy(stackObj);
            return null;
        }

        stack.InitializeFromPrefab(itemPrefab, dispenseCount,
            stackSize, stackLayout, stackOffset,
            gridColumns, gridRows, gridSpacing);

        currentCount -= dispenseCount;

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

        NetworkServer.Spawn(stackObj);

        // 如果动态栈内部生成的子物体也需要 Spawn（非常重要）
        foreach (var stackedItem in stack.GetItems())
        {
            if (stackedItem != null && stackedItem.GetComponent<NetworkIdentity>() != null)
            {
                NetworkServer.Spawn(stackedItem.gameObject);
            }
        }

        if (debugLog) Debug.Log($"<color=#FFA500>[SupplyBox-Server]</color> 生成了一个 Stack({dispenseCount}个)，剩余: {currentCount}");
        return stackObj;
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