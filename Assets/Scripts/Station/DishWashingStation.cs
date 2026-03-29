using UnityEngine;

public class DishWashingStation : BaseStation
{
    [Header("Sink Setting")]
    [Tooltip("脏盘子的放置点，注意把它的 AllowedCategories 改为 DirtyPlate")]
    public ItemPlacePoint inputDropPoint;
    public CleanPlateDispenser outputDispenser;

    [Header("Washing Process")]
    public float requiredWashTimePerPlate = 2f;
    public int dirtyPlatesInSink = 0;
    
    // 【公开字段】供 UI 显示当前洗碗进度
    public float currentWashProgress = 0f;

    private bool isWashing = false;

    // ----- 新增：用来在水池里展示收到的这一整坨脏盘子的外观 -----
    [Header("Visual Inside Sink (Optional)")]
    public GameObject singleDirtyPlateVisual;
    public Transform dirtyVisualRoot;
    public float dirtyStackYOffset = 0.05f;
    private System.Collections.Generic.List<GameObject> dirtyVisualPlates = new System.Collections.Generic.List<GameObject>();

    protected override void Awake()
    {
        base.Awake();
        if (inputDropPoint != null)
        {
            // 监听丢盘子进来的事件
            inputDropPoint.OnItemPlacedEvent += OnDirtyPlateDropped;
        }
    }

    private void OnDestroy()
    {
        if (inputDropPoint != null)
            inputDropPoint.OnItemPlacedEvent -= OnDirtyPlateDropped;
    }

    private void Update()
    {
        if (isWashing && dirtyPlatesInSink > 0)
        {
            currentWashProgress += Time.deltaTime;
            
            if (currentWashProgress >= requiredWashTimePerPlate)
            {
                // 完成清洗一个盘子
                currentWashProgress = 0f;
                dirtyPlatesInSink--;
                UpdateDirtyVisuals();

                if (outputDispenser != null)
                {
                    outputDispenser.AddPlate(1);
                }
                
                if (debugLog) Debug.Log($"[DishWashingStation] 成功洗完 1 个盘子！目前水池里还剩: {dirtyPlatesInSink} 个。");

                // 如果没盘子了，自动停掉
                if (dirtyPlatesInSink <= 0)
                {
                    isWashing = false;
                }
            }
        }
    }

    private void OnDirtyPlateDropped(CarryableItem item)
    {
        DirtyPlateStack dirtyStack = item as DirtyPlateStack;

        // 如果是一个盘子堆，读取数量然后吞并
        if (dirtyStack != null)
        {
            dirtyPlatesInSink += dirtyStack.plateCount;
            // 清理原本的物理物品对象
            inputDropPoint.ClearOccupant();
            Destroy(dirtyStack.gameObject);
        }
        else if (item.HasAnyCategory(ItemCategory.DirtyPlate)) // 兼容如果不小心放进来的是单个普通的脏盘子
        {
            dirtyPlatesInSink += 1;
            inputDropPoint.ClearOccupant();
            Destroy(item.gameObject);
        }

        UpdateDirtyVisuals();
        
        if (debugLog) Debug.Log($"[DishWashingStation] 接收了新的脏盘子，当前总数: {dirtyPlatesInSink}");
    }

    // 更新水池中的脏盘子视觉堆叠
    private void UpdateDirtyVisuals()
    {
        if (singleDirtyPlateVisual == null || dirtyVisualRoot == null) return;

        foreach (var p in dirtyVisualPlates)
        {
            if (p != null) Destroy(p);
        }
        dirtyVisualPlates.Clear();

        for (int i = 0; i < dirtyPlatesInSink; i++)
        {
            GameObject plate = Instantiate(singleDirtyPlateVisual, dirtyVisualRoot);
            plate.transform.localPosition = new Vector3(0, i * dirtyStackYOffset, 0);
            plate.transform.localRotation = Quaternion.identity;
            dirtyVisualPlates.Add(plate);
        }
    }

    public override bool CanInteract(PlayerItemInteractor interactor)
    {
        // 只有水池里有脏盘子时，才能按 K 长按洗碗
        return dirtyPlatesInSink > 0;
    }

    public override void BeginInteract(PlayerItemInteractor interactor)
    {
        if (dirtyPlatesInSink > 0)
        {
            isWashing = true;
            if (debugLog) Debug.Log("[DishWashingStation] 玩家开始洗碗！长按 K...");
        }
    }

    public override void EndInteract(PlayerItemInteractor interactor)
    {
        isWashing = false;
        // 注意：玩家中途松手时进度不被清除（类似断点续洗），如果想强制清零，可以把 currentWashProgress 置 0
        if (debugLog) Debug.Log("[DishWashingStation] 玩家松开了洗碗按键。");
    }

    // 提供给 UI 拿到清洗进度的 normalized value
    public float GetWashProgressNormalized()
    {
        if (requiredWashTimePerPlate <= 0) return 0f;
        return Mathf.Clamp01(currentWashProgress / requiredWashTimePerPlate);
    }
}
