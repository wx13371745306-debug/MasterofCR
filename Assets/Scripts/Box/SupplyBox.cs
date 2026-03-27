using UnityEngine;

public class SupplyBox : BaseStation
{
    [Header("Supply Settings")]
    [Tooltip("箱子里装的物品预制体（如：番茄）")]
    public GameObject itemPrefab;
    [Tooltip("箱子的总容量")]
    public int maxCount = 5;
    [Tooltip("当前剩余数量（运行时可动态查看）")]
    public int currentCount;

    [Header("Box References")]
    public GameObject closedModel;
    public GameObject openedModel;
    
    [Tooltip("当剩余数量 > 1 时显示的额外模型（请务必将其设为 openedModel 的子物体）")]
    public GameObject multipleItemsModel;

    [Tooltip("箱子内部生成物品的放置点")]
    public ItemPlacePoint internalPlacePoint;
    
    // ================== 【新增的 UI 引用】 ==================
    [Header("UI References")]
    [Tooltip("箱子上方显示的菜品图标（如：番茄图片）")]
    public GameObject itemIconUI;
    [Tooltip("箱子空了时显示的Empty图标")]
    public GameObject emptyIconUI;
    // ========================================================

    private CarryableItem boxCarryable;
    private bool isOpened = false;

    protected override void Awake()
    {
        base.Awake();
        
        boxCarryable = GetComponent<CarryableItem>();
        currentCount = maxCount;
        
        // 初始化模型显示状态
        if (closedModel != null) closedModel.SetActive(true);
        if (openedModel != null) openedModel.SetActive(false);

        // 初始化视觉与状态
        UpdateMultipleItemsVisual(); 
        UpdatePlacePointState(); 
        UpdateUIVisual(); // 【新增】：初始化 UI 图标状态
    }

    public override bool CanInteract(PlayerItemInteractor interactor)
    {
        return true; 
    }

    public override void BeginInteract(PlayerItemInteractor interactor)
    {
        isOpened = !isOpened; // 切换开关状态

        if (isOpened)
        {
            // --- 打开箱子 ---
            if (closedModel != null) closedModel.SetActive(false);
            if (openedModel != null) openedModel.SetActive(true);

            // 关键：禁止箱子被搬走
            if (boxCarryable != null) boxCarryable.isPickable = false;
        }
        else
        {
            // --- 关闭箱子 ---
            if (closedModel != null) closedModel.SetActive(true);
            if (openedModel != null) openedModel.SetActive(false);

            // 关键：恢复箱子的可搬运状态
            if (boxCarryable != null) boxCarryable.isPickable = true;

            // 如果箱子关闭时，台面上还有没被拿走的物品，直接回收
            if (internalPlacePoint.CurrentItem != null)
            {
                Destroy(internalPlacePoint.CurrentItem.gameObject);
                internalPlacePoint.ClearOccupant();
                currentCount++; // 退回库存
                
                // 数量恢复了，可能重新 > 1，更新显示和放置点状态
                UpdateMultipleItemsVisual(); 
            }
        }
        
        // 无论打开还是关闭，都更新一次放置点状态
        UpdatePlacePointState(); 

        // 【新增】：无论打开还是关闭，都刷新一次 UI 状态
        UpdateUIVisual(); 
    }

    public override void EndInteract(PlayerItemInteractor interactor)
    {
    }

    void Update()
    {
        // 自动补货逻辑：如果箱子开着，且内部点位是空的，且还有库存，则自动生成一个
        if (isOpened && currentCount > 0 && internalPlacePoint.CurrentItem == null)
        {
            SpawnItem();
        }
    }

    void SpawnItem()
    {
        if (itemPrefab == null) return;

        // 生成物品
        GameObject obj = Instantiate(itemPrefab, internalPlacePoint.attachPoint.position, internalPlacePoint.attachPoint.rotation);
        CarryableItem item = obj.GetComponent<CarryableItem>();
        
        if (item != null)
        {
            // 强制绑定出生点，防止幽灵乱飞
            item.initialPlacePoint = internalPlacePoint;

            // 让内部放置点尝试接管这个生成的物品
            bool accepted = internalPlacePoint.TryAcceptItem(item);
            
            if (accepted)
            {
                currentCount--;
                
                // 更新各种视觉状态
                UpdateMultipleItemsVisual(); 
                UpdatePlacePointState(); 
                UpdateUIVisual(); // 【新增】：库存扣减时同步更新UI状态，保证数据一致性
            }
            else
            {
                Destroy(obj);
            }
        }
    }

    void UpdateMultipleItemsVisual()
    {
        if (multipleItemsModel != null)
        {
            multipleItemsModel.SetActive(currentCount > 1);
        }
    }

    void UpdatePlacePointState()
    {
        if (internalPlacePoint != null)
        {
            // 只有当箱子打开时，才允许放入
            internalPlacePoint.allowAnyCategory = isOpened;
        }
    }

    // ================== 【新增的核心 UI 逻辑】 ==================
    void UpdateUIVisual()
    {
        if (isOpened)
        {
            // 开启箱子时：把所有图标隐藏掉
            if (itemIconUI != null) itemIconUI.SetActive(false);
            if (emptyIconUI != null) emptyIconUI.SetActive(false);
        }
        else
        {
            // 关闭箱子时：根据数量显示图标
            if (currentCount > 0)
            {
                // 还有货：显示菜的图标，隐藏 Empty
                if (itemIconUI != null) itemIconUI.SetActive(true);
                if (emptyIconUI != null) emptyIconUI.SetActive(false);
            }
            else
            {
                // 空了：显示 Empty，隐藏菜的图标
                if (itemIconUI != null) itemIconUI.SetActive(false);
                if (emptyIconUI != null) emptyIconUI.SetActive(true);
            }
        }
    }
}