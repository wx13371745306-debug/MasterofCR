using UnityEngine;

/// <summary>
/// 翻新后的洗碗池逻辑。
/// 洗碗不再借助 CleanPlateDispenser 而是直接管理输出点位。
/// 洗完后，出口如果为空，会生成单个盘子；如果有1个普通盘子，会堆成盘子堆；如果是盘子堆，会塞入其中。
/// 盘子堆满时，洗碗池停止工作，关闭交互高亮（Sensor 检测时会把 IsOutputFull() 作为返回 false 的条件）。
/// </summary>
public class DishWashingStation : BaseStation
{
    [Header("Sink Settings")]
    [Tooltip("脏盘子的放置点，注意把它的 AllowedCategories 改为 DirtyPlate")]
    public ItemPlacePoint inputDropPoint;
    
    [Tooltip("【新增】洗完后干净盘子生成的放置点")]
    public ItemPlacePoint outputPlacePoint;
    
    [Tooltip("【新增】干净盘子的预制体（必须挂载了 CarryableItem 和 StackableProp）")]
    public GameObject cleanPlatePrefab;
    
    [Tooltip("【新增】游戏开始时，默认在出口生成的干净盘子数量")]
    public int initialCleanPlates = 5;

    [Header("Washing Process")]
    [Tooltip("洗碗的基础速度，暂设为1")]
    public float baseProcessingSpeed = 1f;
    [Tooltip("每洗净一个盘子需要的进度量，原为需要的总时间")]
    public float requiredWashTimePerPlate = 2f;
    public int dirtyPlatesInSink = 0;
    
    // 【公开字段】供 UI 显示当前洗碗进度
    public float currentWashProgress = 0f;

    private bool isWashing = false;

    [Header("Visual Inside Sink")]
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

    private void Start()
    {
        // 初始生成一叠干净盘子
        if (initialCleanPlates > 0)
        {
            SpawnCleanPlates(initialCleanPlates);
        }
    }

    private void OnDestroy()
    {
        if (inputDropPoint != null)
            inputDropPoint.OnItemPlacedEvent -= OnDirtyPlateDropped;
    }

    /// <summary>获取实际洗碗速度（含羁绊加成和属性修正）。</summary>
    public float GetEffectiveWashingSpeed()
    {
        // 1. 获取基础速度
        float speed = baseProcessingSpeed;

        // 2. 累乘玩家属性
        float playerMulti = 1.0f;
        if (CurrentPlayerAttributes != null)
            playerMulti = CurrentPlayerAttributes.washSpeedMultiplier;

        // 3. 累加全局加成
        float globalAddon = 0f;
        if (GlobalOrderManager.Instance != null)
            globalAddon = GlobalOrderManager.Instance.globalWashSpeedAddon;

        // 4. 计算公式：最终速度 = (基础速度 * 玩家乘数) + 全局加成
        float finalSpeed = (speed * playerMulti) + globalAddon;

        // 安全界限限制（保证最小有0.01速度运转防止卡死）
        return Mathf.Max(0.01f, finalSpeed);
    }

    private void Update()
    {
        bool isOutputFull = IsOutputFull();

        // 当洗碗池输出已满时，如果正在洗也是强制中断
        if (isWashing && isOutputFull)
        {
            isWashing = false;
            currentWashProgress = 0f;
            if (debugLog) Debug.LogWarning("<color=#00FFFF>[DishWashingStation]</color> 出口盘子堆已满！已强制停止清洗！");
        }

        if (isWashing && dirtyPlatesInSink > 0 && !isOutputFull)
        {
            currentWashProgress += GetEffectiveWashingSpeed() * Time.deltaTime;
            
            if (currentWashProgress >= requiredWashTimePerPlate)
            {
                // 完成清洗一个盘子
                currentWashProgress = 0f;
                dirtyPlatesInSink--;
                UpdateDirtyVisuals();

                SpawnCleanPlates(1); // 洗出一个新盘子发配到出口
                
                if (debugLog) Debug.Log($"<color=#00FFFF>[DishWashingStation]</color> 成功洗完 1 个盘子！目前水池里还剩: {dirtyPlatesInSink} 个。");

                // 如果此时刚好满或者没脏盘子了，自动停掉
                if (dirtyPlatesInSink <= 0 || IsOutputFull())
                {
                    isWashing = false;
                    currentWashProgress = 0f;
                }
            }
        }
    }

    private void OnDirtyPlateDropped(CarryableItem item)
    {
        if (item == null) return;

        DirtyPlateStack dirtyStack = item as DirtyPlateStack;

        if (dirtyStack != null)
        {
            dirtyPlatesInSink += dirtyStack.plateCount;
            inputDropPoint.ClearOccupant();
            Destroy(dirtyStack.gameObject);
        }
        else if (item.HasAnyCategory(ItemCategory.DirtyPlate)) 
        {
            // 兼容单个脏盘子
            dirtyPlatesInSink += 1;
            inputDropPoint.ClearOccupant();
            Destroy(item.gameObject);
        }

        UpdateDirtyVisuals();
        
        if (debugLog) Debug.Log($"<color=#00FFFF>[DishWashingStation]</color> 接收了脏盘子，当前总数: {dirtyPlatesInSink}");
    }

    /// <summary>
    /// 生成 N 个盘子放入出口的位置。
    /// 涵盖：无盘子(变单个)、单个(合并变堆)、已有堆(推入)、初始批量建堆 等所有情况
    /// </summary>
    private void SpawnCleanPlates(int count)
    {
        if (outputPlacePoint == null || cleanPlatePrefab == null || count <= 0) return;

        CarryableItem currentItem = outputPlacePoint.CurrentItem;
        StackableProp sp = cleanPlatePrefab.GetComponent<StackableProp>();

        if (sp == null)
        {
            Debug.LogError("[DishWashingStation] 你的 cleanPlatePrefab 上缺少 StackableProp！洗碗机功能部分失效。");
            return;
        }

        if (currentItem == null)
        {
            // 当前完全是空的
            if (count > 1)
            {
                // [初始化调用] 需要直接捏一个完整的堆出来
                int maxCap = (sp.layoutType == StackLayout.Grid) ? sp.gridColumns * sp.gridRows : sp.maxStackCount;
                GameObject stackObj = Instantiate(sp.dynamicStackPrefab, outputPlacePoint.attachPoint.position, Quaternion.identity);
                DynamicItemStack stack = stackObj.GetComponent<DynamicItemStack>();

                stack.InitializeFromPrefab(cleanPlatePrefab, count, maxCap, sp.layoutType, sp.stackYOffset, sp.gridColumns, sp.gridRows, sp.gridSpacing);
                stack.ForcePlaceAtStart(outputPlacePoint);
            }
            else
            {
                // 洗完第一个盘子，出来是一个普通单盘被放在台上
                GameObject obj = Instantiate(cleanPlatePrefab, outputPlacePoint.attachPoint.position, Quaternion.identity);
                CarryableItem singlePlate = obj.GetComponent<CarryableItem>();
                singlePlate.ForcePlaceAtStart(outputPlacePoint);
            }
        }
        else
        {
            // 台上已经有东西了
            if (currentItem is DynamicItemStack stack)
            {
                // 已经成堆了，那就往里塞
                for (int i = 0; i < count; i++)
                {
                    if (stack.IsFull) break;

                    // 在远处产卵避免物理异常
                    GameObject obj = Instantiate(cleanPlatePrefab, Vector3.one * -9999f, Quaternion.identity);
                    CarryableItem newPlate = obj.GetComponent<CarryableItem>();
                    stack.PushItem(newPlate);
                }
            }
            else
            {
                // 台上是个单盘：它与第一块刚洗好的盘子合并成一坨新堆
                // 因为目前洗碗基本 count = 1，仅考虑这个场景引发合并
                if (count == 1)
                {
                    // 这里借用了已有的 MergeIntoStack 方法，它会自动吸纳这个单盘与传入的心盘并把自己替换为 DynamicItemStack
                    StackableProp existProp = currentItem.GetComponent<StackableProp>();
                    // 此处通过实例化一个假盘子传给它进行合并，合并后俩东西都被吸扯进一个新堆
                    if (existProp != null && existProp.CanStackWith(cleanPlatePrefab.GetComponent<CarryableItem>()))
                    {
                        GameObject obj = Instantiate(cleanPlatePrefab, Vector3.one * -9999f, Quaternion.identity);
                        CarryableItem newPlate = obj.GetComponent<CarryableItem>();
                        existProp.MergeIntoStack(newPlate, null);
                    }
                }
            }
        }
    }

    /// <summary>
    /// 检查出口点位是不是已经饱和了
    /// </summary>
    public bool IsOutputFull()
    {
        if (outputPlacePoint == null) return false;
        CarryableItem currentItem = outputPlacePoint.CurrentItem;

        if (currentItem == null) return false;

        // 如果是个堆装，它提供了公开的满载查询方法
        if (currentItem is DynamicItemStack stack)
        {
            return stack.IsFull;
        }
        else
        {
            // 如果只有 1 个盘子，肯定没满（默认可堆叠）
            return false;
        }
    }

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
        // 关键条件：
        // 1. 水池里得有待洗的脏盘子
        // 2. 并且由于洗碗必定会往出口放盘子，所以必须保证出口没满
        return dirtyPlatesInSink > 0 && !IsOutputFull();
    }

    public override void BeginInteract(PlayerItemInteractor interactor)
    {
        if (CanInteract(interactor))
        {
            cachedInteractor = interactor; // 记录当前互动玩家
            isWashing = true;
            if (debugLog) Debug.Log("<color=#00FFFF>[DishWashingStation]</color> 玩家开始洗碗！长按 K...");
        }
    }

    public override void EndInteract(PlayerItemInteractor interactor)
    {
        isWashing = false;
        if (debugLog) Debug.Log("<color=#00FFFF>[DishWashingStation]</color> 玩家松开了洗碗按键。");
    }

    public float GetWashProgressNormalized()
    {
        if (requiredWashTimePerPlate <= 0) return 0f;
        return Mathf.Clamp01(currentWashProgress / requiredWashTimePerPlate);
    }
}
