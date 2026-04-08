using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OrderResponse : BaseStation
{
    public enum TableState
    {
        Empty,              // 空闲（无顾客）
        ReadingMenu,        // 【新增】：顾客刚坐下，正在看菜单（随机2-5秒）
        WaitingToOrder,     // 看完菜单了，头顶亮起呼叫图标，等玩家过来点菜
        Ordering,           // 玩家正在长按K点菜读条中
        WaitingForFood,     // 点菜完成，等上菜
        Eating,             // 菜上齐了，正在吃
        WaitingForCleanup   // 吃完了，等清理
    }

    [Header("Refs")]
    public ItemPlacePoint itemPlacePoint;
    public DishPlaceSystem dishPlaceSystem;
    public OrderGenerator orderGenerator;
    public FryRecipeDatabase recipeDatabase;
    public DrinkRecipeDatabase drinkRecipeDatabase;
    [Tooltip("生成的整体脏盘子堆预制体")]
    public DirtyPlateStack dirtyPlateStackPrefab;

    [Header("Table Identity")]
    public int tableId = 0;

    [Header("State (ReadOnly)")]
    public TableState currentState = TableState.Empty;
    public float currentOrderProgress = 0f;
    public float currentEatTime = 0f; // 当前剩余用餐时间
    
    // 【新增】：预定锁。只要被分配了哪怕人还没到，这桌也不能再接客了
    public bool isReserved = false;

    // 【新增】：这桌专属的开阔地寻路点
    [Tooltip("桌子旁边的空地，AI先走到这里再入座")]
    public Transform approachPoint;

    [Tooltip("这桌的座位点（在椅子上创建空子物体作为坐姿位置，拖入此处）")]
    public List<Transform> chairs = new List<Transform>();

    [Header("Patience (运行时，由 CustomerGroup + GlobalOrderManager 注入)")]
    [HideInInspector] public float effectiveMaxPatienceOrder = 100f;
    [HideInInspector] public float effectiveLossPerSecondOrder = 10f;
    [HideInInspector] public float effectiveMaxPatienceFood = 100f;
    [HideInInspector] public float effectiveLossPerSecondFood = 5f;
    [HideInInspector] public float effectiveServePatienceBonus = 60f;
    [HideInInspector] public float effectivePatienceCap = 100f;
    [HideInInspector] public float effectiveImpatientThreshold = 40f;

    public float currentPatienceOrder;
    public float currentPatienceFood;

    [Header("Debug")]
    public bool debugLog = true;

    [Header("Order Settings")]
    [Tooltip("桌子物理上限（兜底），实际点菜数以顾客组配置为准")]
    public int maxDishes = 6;
    public float requiredOrderTime = 2f;
    [HideInInspector] public int currentCustomerCount = 1;
    [HideInInspector] public int currentMinDishes = 1;

    private readonly List<FryRecipeDatabase.FryRecipe> currentOrder = new List<FryRecipeDatabase.FryRecipe>();
    private Coroutine eatRoutine;

    [System.Serializable]
    public class PlacedDishRecord
    {
        public FryRecipeDatabase.FryRecipe recipe; // 配方数据
        public CarryableItem physicalItem;         // 场景里真实的物理盘子物体
        public bool isCorrectOrder;                // 标记：上对的还是白送的？
    }

    private readonly List<PlacedDishRecord> dishesOnTable = new List<PlacedDishRecord>();

    // 【新增】：用来存储纯净无害的“吃完后”独立视觉模型，防止跟物理逻辑产生任何藕断丝连
    private readonly List<GameObject> activeEatenModels = new List<GameObject>();
    private int pendingDirtyPlatesCount = 0;

    private bool isInteracting = false;
    private CustomerGroup boundGroup;
    private bool isAbandoningPatience;
    private Coroutine readingMenuCoroutine;

    public void RegisterCustomerGroup(CustomerGroup group) => boundGroup = group;

    /// <summary>顾客组离场结束后由 <see cref="CustomerGroup"/> 调用。</summary>
    public void NotifyPatienceLeaveComplete()
    {
        PatienceLeaveDbg("NotifyPatienceLeaveComplete：顾客组已离场，桌子预定解锁");
        boundGroup = null;
        isReserved = false;
        isAbandoningPatience = false;
    }

    void OnEnable() { if (itemPlacePoint != null) itemPlacePoint.OnItemPlacedEvent += OnItemPlaced; }
    void OnDisable()
    {
        if (itemPlacePoint != null) itemPlacePoint.OnItemPlacedEvent -= OnItemPlaced;
        if (eatRoutine != null) StopCoroutine(eatRoutine);
    }

    protected override void Awake() { base.Awake(); }

    void Start()
    {
        // 【终极工程化解法】：拒绝场景遍历！
        if (orderGenerator == null)
        {
            // 直接通过全局唯一的管家单例，拿到它身上的大脑组件。
            // 这是一个 O(1) 级别的查找，速度极快，且绝对不会找错人！
            if (GlobalOrderManager.Instance != null)
            {
                orderGenerator = GlobalOrderManager.Instance.GetComponent<OrderGenerator>();
            }
        }

        if (recipeDatabase == null && orderGenerator != null)
            recipeDatabase = orderGenerator.recipeDatabase;

        if (drinkRecipeDatabase == null && orderGenerator != null)
            drinkRecipeDatabase = orderGenerator.drinkRecipeDatabase;

        if (drinkRecipeDatabase == null && GlobalOrderManager.Instance != null)
            drinkRecipeDatabase = GlobalOrderManager.Instance.drinkRecipeDatabase;

        currentState = TableState.Empty;
    }

    // ================== 【耐心数据注入接口】 ==================

    /// <summary>
    /// 由 CustomerGroup 初始化时调用，注入该组顾客的耐心与点菜配置。
    /// 结合 GlobalOrderManager 的全局修正，计算出最终生效值。
    /// </summary>
    public void ApplyGroupConfig(CustomerGroup group)
    {
        if (group == null) return;

        var mods = GlobalOrderManager.Instance != null
            ? GlobalOrderManager.Instance.GetCurrentModifiers()
            : GlobalOrderManager.PatienceModifiers.Default;

        // 最终值 = 基础值 × 乘数 + 加算
        effectiveMaxPatienceOrder  = group.basePatienceOrder * mods.patienceMultiplier + mods.patienceAddon;
        effectiveLossPerSecondOrder = group.baseLossPerSecondOrder * mods.patienceLossMultiplier + mods.patienceLossAddon;
        effectiveMaxPatienceFood   = group.basePatienceFood * mods.patienceMultiplier + mods.patienceAddon;
        effectiveLossPerSecondFood  = group.baseLossPerSecondFood * mods.patienceLossMultiplier + mods.patienceLossAddon;
        effectiveServePatienceBonus = group.baseServePatienceBonus * mods.serveBonusMultiplier;
        effectivePatienceCap       = group.basePatienceCap * mods.patienceMultiplier + mods.patienceAddon;
        effectiveImpatientThreshold = group.baseImpatientThreshold;

        currentMinDishes = group.minDishes;
        maxDishes = Mathf.Max(group.maxDishes, maxDishes); // 取较大值作为上限兜底
        currentCustomerCount = group.groupSize;

        // 预设初始耐心
        currentPatienceOrder = effectiveMaxPatienceOrder;
        currentPatienceFood  = effectiveMaxPatienceFood;

        if (debugLog) Debug.Log($"[OrderResponse] 桌号 {tableId} 接收顾客组配置 | " +
            $"耐心Order={effectiveMaxPatienceOrder:F0} Loss={effectiveLossPerSecondOrder:F1}/s | " +
            $"耐心Food={effectiveMaxPatienceFood:F0} Loss={effectiveLossPerSecondFood:F1}/s | " +
            $"上菜回复={effectiveServePatienceBonus:F0} Cap={effectivePatienceCap:F0} | " +
            $"人数={currentCustomerCount} 点菜={currentMinDishes}-{maxDishes}");
    }

    void Update()
    {
        if (isInteracting && currentState == TableState.Ordering)
        {
            currentOrderProgress += Time.deltaTime;
            if (currentOrderProgress >= requiredOrderTime)
            {
                CompleteOrdering();
            }
        }

        if (!isAbandoningPatience)
        {
            if (currentState == TableState.WaitingToOrder)
            {
                currentPatienceOrder -= effectiveLossPerSecondOrder * Time.deltaTime;
                if (currentPatienceOrder <= 0f)
                {
                    currentPatienceOrder = 0f;
                    PatienceLeaveDbg("耐心1 归零，触发 AbandonTableDueToPatience");
                    AbandonTableDueToPatience();
                }
            }
            else if (currentState == TableState.WaitingForFood)
            {
                // 衰减速度已在 ApplyGroupConfig 中整合了全局修正（含羁绊），直接使用
                currentPatienceFood -= effectiveLossPerSecondFood * Time.deltaTime;
                if (currentPatienceFood <= 0f)
                {
                    currentPatienceFood = 0f;
                    PatienceLeaveDbg("耐心2 归零，触发 AbandonTableDueToPatience");
                    AbandonTableDueToPatience();
                }
            }
        }

        // 玩家将在 WaitingForCleanup 阶段中通过交互 (BeginInteract) 拿取脏盘并清空桌面，
        // 不再在 Update 中轮询清理阶段状态。
    }

    // ================== 【供 AI 调用的接口】 ==================
    [ContextMenu("测试：模拟顾客入座")]
    public void GroupSeated()
    {
        if (currentState != TableState.Empty) return;

        // 【修改点】：不再直接等待点单，而是进入看菜单状态
        currentState = TableState.ReadingMenu;
        if (readingMenuCoroutine != null)
            StopCoroutine(readingMenuCoroutine);
        readingMenuCoroutine = StartCoroutine(ReadingMenuRoutine());
        Debug.Log($"[OrderResponse] 桌号 {tableId} 顾客已落座，正在看菜单...");
    }

    // 【新增】：看菜单的随机缓冲时间
    private IEnumerator ReadingMenuRoutine()
    {
        float waitTime = Random.Range(2f, 5f);
        yield return new WaitForSeconds(waitTime);
        readingMenuCoroutine = null;

        currentState = TableState.WaitingToOrder;
        currentOrderProgress = 0f;
        currentPatienceOrder = effectiveMaxPatienceOrder;
        Debug.Log($"[OrderResponse] 桌号 {tableId} 顾客看完了，头顶亮起图标请求点单！");
    }

    // ================== 【BaseStation 交互接口实现】 ==================
    public override bool CanInteract(PlayerItemInteractor interactor)
    {
        if (currentState == TableState.WaitingForCleanup && pendingDirtyPlatesCount > 0 && !interactor.IsHoldingItem()) return true;
        return currentState == TableState.WaitingToOrder || currentState == TableState.Ordering;
    }

    public override void BeginInteract(PlayerItemInteractor interactor)
    {
        isInteracting = true;
        if (currentState == TableState.WaitingToOrder)
        {
            if (debugLog) Debug.Log($"[OrderResponse] 玩家开始对桌号 {tableId} 点单交互");
            currentState = TableState.Ordering; // 开始读条
        }
        else if (currentState == TableState.WaitingForCleanup)
        {
            if (pendingDirtyPlatesCount > 0 && !interactor.IsHoldingItem())
            {
                if (debugLog) Debug.Log($"[OrderResponse] 玩家端起桌号 {tableId} 的所有脏盘子 (数量: {pendingDirtyPlatesCount})");
                
                // 复用 dirtyPlateStackPrefab 中的信息发放脏盘并送到玩家手上
                if (dirtyPlateStackPrefab != null && pendingDirtyPlatesCount == 1)
                {
                    GameObject obj = Instantiate(dirtyPlateStackPrefab.singlePlatePrefab, Vector3.one * -9999f, Quaternion.identity);
                    CarryableItem item = obj.GetComponent<CarryableItem>();
                    if (item != null)
                    {
                        item.BeginHold(interactor.GetHoldPoint());
                        interactor.ReplaceHeldItem(item);
                    }
                }
                else if (dirtyPlateStackPrefab != null && pendingDirtyPlatesCount > 1)
                {
                    GameObject stackPrefabToUse = dirtyPlateStackPrefab.stackPrefab;
                    StackableProp sp = dirtyPlateStackPrefab.singlePlatePrefab != null ? dirtyPlateStackPrefab.singlePlatePrefab.GetComponent<StackableProp>() : null;
                    if (stackPrefabToUse == null && sp != null)
                    {
                        stackPrefabToUse = sp.dynamicStackPrefab;
                    }

                    if (stackPrefabToUse == null)
                    {
                        Debug.LogError($"[OrderResponse] 无法生成多盘堆叠，因为没有找到有效的 stackPrefab 参数！请检查 {dirtyPlateStackPrefab.name}");
                        return;
                    }

                    GameObject stackObj = Instantiate(stackPrefabToUse, Vector3.one * -9999f, Quaternion.identity);
                    DynamicItemStack stack = stackObj.GetComponent<DynamicItemStack>();
                    if (stack != null)
                    {
                        int maxStack = sp != null && sp.layoutType == StackLayout.Grid && sp.gridColumns * sp.gridRows > 0 ? sp.gridColumns * sp.gridRows : (sp != null ? sp.maxStackCount : pendingDirtyPlatesCount);
                        StackLayout layout = sp != null ? sp.layoutType : StackLayout.Vertical;
                        float offset = dirtyPlateStackPrefab.stackYOffset;
                        int cols = sp != null ? sp.gridColumns : 1;
                        int rows = sp != null ? sp.gridRows : 1;
                        float spacing = sp != null ? sp.gridSpacing : 0.15f;
                        
                        stack.InitializeFromPrefab(dirtyPlateStackPrefab.singlePlatePrefab, pendingDirtyPlatesCount, maxStack, layout, offset, cols, rows, spacing);
                        stack.BeginHold(interactor.GetHoldPoint());
                        interactor.ReplaceHeldItem(stack);
                    }
                }
                
                pendingDirtyPlatesCount = 0;
                CleanUpTable();  // 自动销毁残羹、重置桌面状态
                isInteracting = false;
            }
        }
    }

    public override void EndInteract(PlayerItemInteractor interactor)
    {
        isInteracting = false;
        if (currentState == TableState.Ordering)
        {
            currentState = TableState.WaitingToOrder; // 松手退回等待图标状态
        }

        if (DayCycleManager.Instance != null && DayCycleManager.Instance.Phase == DayCyclePhase.ExtendedBusiness)
            TryForceLeaveForBusinessEnd();
    }

    // ================== 【核心业务逻辑】 ==================
    void CompleteOrdering()
    {
        currentState = TableState.WaitingForFood;
        isInteracting = false;
        currentOrderProgress = requiredOrderTime;
        currentPatienceFood = effectiveMaxPatienceFood;

        if (debugLog) Debug.Log($"[OrderResponse] 桌{tableId} 点菜完成进入等菜 | lossPerSecondFood={effectiveLossPerSecondFood:F1}");

        currentOrder.Clear();
        dishesOnTable.Clear();

        if (orderGenerator != null)
        {
            var next = orderGenerator.GenerateOrder(currentMinDishes, maxDishes, dishPlaceSystem, currentCustomerCount);
            currentOrder.AddRange(next);

            if (GlobalOrderManager.Instance != null)
            {
                // 这里发送到全局，由于还没写 UI，暂时在后台起作用
                GlobalOrderManager.Instance.RegisterOrdersForTable(tableId, currentOrder);
            }
            Debug.Log($"[OrderResponse] 桌号 {tableId} 订单生成完毕！");
        }
    }

    void OnItemPlaced(CarryableItem item)
    {
        if (item == null) return;
        
        // 【关键防护】：由于生成大脏盘堆并放入中心点时也会触发此事件，
        // 且它不属于“上餐”行为，所以遇到脏盘堆直接放行，切勿执行拦截拒收和订单判定逻辑！
        if (item is DirtyPlateStack) return;

        // 1. 拦截非 WaitingForFood 状态的放置
        if (currentState != TableState.WaitingForFood)
        {
            Debug.LogWarning($"[OrderResponse] 桌号 {tableId} 当前不在等菜状态，忽略放置！");
            itemPlacePoint.ClearOccupant();
            return;
        }

        // 2. 解析 item 对应的 recipe
        FryRecipeDatabase.FryRecipe recipe = ResolveRecipeFromDish(item);
        if (recipe == null)
        {
            Debug.LogWarning($"[OrderResponse] 无法解析放置物 {item.name} 的配方！销毁物品。");
            itemPlacePoint.ClearOccupant();
            Destroy(item.gameObject);
            return;
        }

        // 3. 判断是否在 currentOrder 中
        int idx = FindInOrder(recipe);
        bool isCorrectOrder = false;

        if (idx >= 0)
        {
            isCorrectOrder = true;
            if (GlobalOrderManager.Instance != null)
            {
                GlobalOrderManager.Instance.TryFulfillOrder(tableId, recipe.recipeName);
            }
            if (MoneyManager.Instance != null)
            {
                MoneyManager.Instance.AddMoney(recipe.price);
                // 触发桌子的金钱弹窗
                TableOrderProgressUI uiComponent = GetComponentInChildren<TableOrderProgressUI>(true);
                if (uiComponent == null && transform.parent != null)
                {
                    uiComponent = transform.parent.GetComponentInChildren<TableOrderProgressUI>(true);
                }
                if (uiComponent != null) uiComponent.ShowMoneyEarned(recipe.price);
            }
            if (DayStatsTracker.Instance != null)
            {
                DayStatsTracker.Instance.RegisterPlacedItem(true, recipe.size == DishSize.D);
                if (DayCycleManager.Instance != null && DayCycleManager.Instance.ShouldRecordOrderRevenue())
                    DayStatsTracker.Instance.RegisterRevenue(recipe.price);
            }
            currentOrder.RemoveAt(idx);
            Debug.Log($"[OrderResponse] 桌号 {tableId} 成功上对菜: {recipe.recipeName}。剩 {currentOrder.Count} 道菜！消单加钱！");
        }
        else
        {
            isCorrectOrder = false;
            if (DayStatsTracker.Instance != null)
                DayStatsTracker.Instance.RegisterPlacedItem(false, recipe.size == DishSize.D);
            Debug.LogWarning($"[OrderResponse] 桌号 {tableId} 上错菜惩罚: {recipe.recipeName} 不在订单中！当做白送！");
        }

        // 4. 尝试放入并防溢出
        if (dishPlaceSystem != null)
        {
            bool placed = dishPlaceSystem.AcceptDish(item, recipe.size);
            if (!placed)
            {
                Debug.LogWarning($"[OrderResponse] 桌号 {tableId} 物理槽满载，触发防溢出，寻找最老的同类菜品隐藏腾位...");
                
                // 遍历 dishesOnTable 找到有数据且最老(activeSelf==true)的一盘对应形态的被隐者
                foreach (var d in dishesOnTable)
                {
                    if (d.physicalItem != null && d.physicalItem.gameObject.activeSelf)
                    {
                        bool isBothDrink = (recipe.size == DishSize.D && d.recipe.size == DishSize.D);
                        bool isBothFood = (recipe.size != DishSize.D && d.recipe.size != DishSize.D);
                        
                        if (isBothDrink || isBothFood)
                        {
                            Debug.Log($"[OrderResponse] 隐藏最先放上来的视觉模型: {d.physicalItem.gameObject.name}，释放自身占用！");
                            d.physicalItem.gameObject.SetActive(false);
                            break;
                        }
                    }
                }
                
                // 此时直接强制塞入新菜由于原本在位置的已被 SetActive(false) ，会在下一次 DishPlaceSystem 中分配给这个新菜
                dishPlaceSystem.ForceAcceptDish(item, recipe.size);
            }
        }
        else
        {
            item.transform.SetParent(transform);
        }

        itemPlacePoint.ClearOccupant();

        // 5. 封装记录
        PlacedDishRecord record = new PlacedDishRecord
        {
            recipe = recipe,
            physicalItem = item,
            isCorrectOrder = isCorrectOrder
        };
        dishesOnTable.Add(record);

        // 【新增】：将该 dish 设置为不可拿取
        item.isPickable = false;

        currentPatienceFood = Mathf.Min(effectivePatienceCap, currentPatienceFood + effectiveServePatienceBonus);

        // 6. 判断是否点菜全齐
        if (currentOrder.Count == 0)
        {
            float totalEatTime = 0f;
            foreach (var d in dishesOnTable)
            {
                if (d.isCorrectOrder && d.recipe != null)
                {
                    totalEatTime += Mathf.Max(0f, d.recipe.eatTime);
                }
            }
            
            Debug.Log($"[OrderResponse] 桌号 {tableId} 菜品全齐！进入 Eating 状态，总耗时估算: {totalEatTime}s");
            currentState = TableState.Eating;
            eatRoutine = StartCoroutine(EatCountdown(totalEatTime));
        }
    }

    private FryRecipeDatabase.FryRecipe ResolveRecipeFromDish(CarryableItem dish)
    {
        if (dish == null) return null;
        DishRecipeTag tag = dish.GetComponent<DishRecipeTag>() ?? dish.GetComponentInParent<DishRecipeTag>();
        if (tag == null) return null;

        var recipe = recipeDatabase != null ? recipeDatabase.FindByName(tag.recipeName) : null;
        if (recipe != null) return recipe;

        return drinkRecipeDatabase != null ? drinkRecipeDatabase.FindByName(tag.recipeName) : null;
    }

    private int FindInOrder(FryRecipeDatabase.FryRecipe recipe)
    {
        if (recipe == null) return -1;
        for (int i = 0; i < currentOrder.Count; i++)
        {
            if (currentOrder[i] == recipe || currentOrder[i].recipeName == recipe.recipeName) return i;
        }
        return -1;
    }

    private IEnumerator EatCountdown(float seconds)
    {
        currentEatTime = Mathf.Max(0f, seconds);
        while (currentEatTime > 0f) 
        { 
            currentEatTime -= Time.deltaTime; 
            yield return null; 
        }
        currentEatTime = 0f;
        
        Debug.Log($"[Debug-Eat] 桌号 {tableId} 开始执行用餐完毕逻辑，当前桌上记录菜品数量: {dishesOnTable.Count}");
        
        int dirtyCount = 0;

        foreach (var d in dishesOnTable)
        {
            string recipeName = d.recipe != null ? d.recipe.recipeName : "未知菜品";
            Debug.Log($"[Debug-Eat] 处理桌上菜品: {recipeName}, physicalItem 是否为空: {d.physicalItem == null}");

            if (d.physicalItem == null) continue;

            // 饮料被餐桌"吸收"，不计入脏盘、不生成残羹模型
            if (d.recipe != null && d.recipe.size == DishSize.D)
            {
                Debug.Log($"[Debug-Eat] - {recipeName} 是饮料，直接吸收，不产生脏杯子。");
                continue;
            }

            dirtyCount++;

            Vector3 pos = d.physicalItem.transform.position;
            Quaternion rot = d.physicalItem.transform.rotation;

            bool hasEatenPrefab = (d.recipe != null && d.recipe.eatenPrefab != null);
            Debug.Log($"[Debug-Eat] - {recipeName} 物理坐标: {pos}, 是否有吃完模型(eatenPrefab): {hasEatenPrefab}");

            // 只需要管 Instantiate 独立并且不带标签也没有逻辑的纯视觉预制体
            if (hasEatenPrefab)
            {
                GameObject cleanEatenVisual = Instantiate(d.recipe.eatenPrefab, pos, rot, this.transform);
                activeEatenModels.Add(cleanEatenVisual);
                Debug.Log($"[Debug-Eat] - 成功生成【{cleanEatenVisual.name}】并加入 activeEatenModels 列表。当前列表总量: {activeEatenModels.Count}");
            }
        }

        // --- 破而后立：毫不留情地切断一切旧物引用，秒删 ---
        Debug.Log($"[Debug-Eat] 开始清理 DishPlaceSystem 原物理菜品...");
        if (dishPlaceSystem != null) dishPlaceSystem.ClearAllDishes();
        dishesOnTable.Clear();

        // 2. 将脏盘子计数并直接转为待清理状态
        pendingDirtyPlatesCount = dirtyCount;
        if (debugLog) Debug.Log($"[OrderResponse] 客人用餐完毕，桌号 {tableId} 剩余 {pendingDirtyPlatesCount} 个脏盘子待端走。");
        
        currentState = TableState.WaitingForCleanup;
        if (DayStatsTracker.Instance != null)
            DayStatsTracker.Instance.RegisterGuestsServed(currentCustomerCount);
        Debug.Log($"[Debug-Eat] 切换状态为 WaitingForCleanup。流程结束。");

        // 用餐完毕须主动通知顾客组离场；此前仅耐心/营业结束会走 PerformTableResetAndCustomerLeave → BeginLeaveGroup，
        // 正常吃完进入 WaitingForCleanup 时若不调此处，顾客会永远坐在原地。
        if (boundGroup != null)
        {
            Debug.Log($"[OrderResponse] 桌号 {tableId} 用餐完毕，触发顾客离场 (BeginLeaveGroup)");
            boundGroup.BeginLeaveGroup();
        }
        else
            Debug.LogWarning($"[OrderResponse] 桌号 {tableId} 用餐完毕但 boundGroup 为空，顾客无法离场");

        eatRoutine = null;

        // 如果刚好没留下任何脏盘子，代表桌子可以直接用了（比如全员饮品吸收了）
        if (pendingDirtyPlatesCount == 0)
        {
            CleanUpTable();
        }
    }

    public void CleanUpTable()
    {
        if (dishPlaceSystem != null) dishPlaceSystem.ClearAllDishes();
        dishesOnTable.Clear(); // 清理记录
        
        foreach (var model in activeEatenModels)
        {
            if (model != null) Destroy(model);
        }
        activeEatenModels.Clear();

        currentState = TableState.Empty; 
        
        // 【新增】：客人吃完走人，桌子解开预定锁
        isReserved = false;

        currentPatienceOrder = effectiveMaxPatienceOrder;
        currentPatienceFood = effectiveMaxPatienceFood;
    }

    void PatienceLeaveDbg(string msg)
    {
        if (debugLog) Debug.Log($"[PatienceLeave][桌{tableId}] {msg}");
    }

    void AbandonTableDueToPatience()
    {
        if (isAbandoningPatience) return;
        if (currentState != TableState.WaitingToOrder && currentState != TableState.WaitingForFood) return;

        isAbandoningPatience = true;
        if (DayStatsTracker.Instance != null)
            DayStatsTracker.Instance.RegisterGuestsFailed(currentCustomerCount);
        PerformTableResetAndCustomerLeave("耐心离场");
    }

    /// <summary>
    /// 延迟营业：未进入 WaitingForFood 的桌离场；点菜中且按住 K 则本帧跳过。
    /// </summary>
    public void TryForceLeaveForBusinessEnd()
    {
        if (isAbandoningPatience) return;
        if (currentState == TableState.Empty) return;
        if (currentState == TableState.WaitingForFood || currentState == TableState.Eating ||
            currentState == TableState.WaitingForCleanup)
            return;
        if (currentState == TableState.Ordering && isInteracting) return;

        isAbandoningPatience = true;
        if (DayStatsTracker.Instance != null)
            DayStatsTracker.Instance.RegisterGuestsFailed(currentCustomerCount);

        if (readingMenuCoroutine != null)
        {
            StopCoroutine(readingMenuCoroutine);
            readingMenuCoroutine = null;
        }

        PerformTableResetAndCustomerLeave("营业结束强制离场");
    }

    void PerformTableResetAndCustomerLeave(string dbgReason)
    {
        PatienceLeaveDbg($"{dbgReason} 开始 | boundGroup={(boundGroup != null ? boundGroup.name : "NULL")}");

        if (eatRoutine != null)
        {
            StopCoroutine(eatRoutine);
            eatRoutine = null;
        }

        if (GlobalOrderManager.Instance != null)
            GlobalOrderManager.Instance.RemoveAllOrdersForTable(tableId);

        if (dishPlaceSystem != null)
            dishPlaceSystem.ClearAllDishes();

        foreach (var model in activeEatenModels)
        {
            if (model != null) Destroy(model);
        }
        activeEatenModels.Clear();

        dishesOnTable.Clear();
        currentOrder.Clear();

        if (itemPlacePoint != null && itemPlacePoint.CurrentItem != null)
        {
            var occ = itemPlacePoint.CurrentItem;
            itemPlacePoint.ClearOccupant();
            if (occ != null) Destroy(occ.gameObject);
        }

        isInteracting = false;
        currentOrderProgress = 0f;
        currentEatTime = 0f;
        currentPatienceOrder = effectiveMaxPatienceOrder;
        currentPatienceFood = effectiveMaxPatienceFood;

        currentState = TableState.Empty;

        if (boundGroup != null)
        {
            PatienceLeaveDbg("调用 CustomerGroup.BeginLeaveGroup()");
            boundGroup.BeginLeaveGroup();
        }
        else
        {
            PatienceLeaveDbg("无 boundGroup，已直接 isReserved=false");
            isReserved = false;
            isAbandoningPatience = false;
        }
    }

    /// <summary>
    /// 天数切换时由 DayCycleManager 调用：保留脏盘子等物理物品，清理残留的顾客相关状态。
    /// </summary>
    public void HandleDayTransition()
    {
        // WaitingForCleanup：保留脏盘子、残羹模型，只清理顾客相关引用
        if (currentState == TableState.WaitingForCleanup)
        {
            boundGroup = null;
            isReserved = false;
            isAbandoningPatience = false;
            currentOrder.Clear();
            dishesOnTable.Clear();
            currentOrderProgress = 0f;
            currentPatienceOrder = effectiveMaxPatienceOrder;
            currentPatienceFood = effectiveMaxPatienceFood;
            Debug.Log($"[OrderResponse] 桌号 {tableId} 天数切换：保留 WaitingForCleanup 状态（脏盘子留存）");
            return;
        }

        if (currentState == TableState.Empty)
        {
            boundGroup = null;
            isReserved = false;
            isAbandoningPatience = false;
            return;
        }

        // 其他状态（安全兜底）：正常流程中不应出现在打烊后，但以防万一做防御性重置
        if (eatRoutine != null)
        {
            StopCoroutine(eatRoutine);
            eatRoutine = null;
        }
        if (readingMenuCoroutine != null)
        {
            StopCoroutine(readingMenuCoroutine);
            readingMenuCoroutine = null;
        }

        if (GlobalOrderManager.Instance != null)
            GlobalOrderManager.Instance.RemoveAllOrdersForTable(tableId);

        if (dishPlaceSystem != null)
            dishPlaceSystem.ClearAllDishes();

        foreach (var model in activeEatenModels)
        {
            if (model != null) Destroy(model);
        }
        activeEatenModels.Clear();
        dishesOnTable.Clear();
        currentOrder.Clear();

        if (itemPlacePoint != null && itemPlacePoint.CurrentItem != null)
        {
            var occ = itemPlacePoint.CurrentItem;
            itemPlacePoint.ClearOccupant();
            if (occ != null) Destroy(occ.gameObject);
        }

        isInteracting = false;
        currentOrderProgress = 0f;
        currentEatTime = 0f;
        currentPatienceOrder = effectiveMaxPatienceOrder;
        currentPatienceFood = effectiveMaxPatienceFood;
        currentState = TableState.Empty;
        boundGroup = null;
        isReserved = false;
        isAbandoningPatience = false;

        Debug.Log($"[OrderResponse] 桌号 {tableId} 天数切换：非预期状态 → 强制重置为 Empty");
    }

    public float GetOrderProgressNormalized() => requiredOrderTime > 0f ? Mathf.Clamp01(currentOrderProgress / requiredOrderTime) : 0f;

    /// <summary>等待点菜阶段：始终显示「呼叫点餐」Icon（颜色由 UI 根据耐心值渐变）。</summary>
    public bool ShouldShowWaitingToOrderCallIcon => currentState == TableState.WaitingToOrder;

    /// <summary>等上菜阶段：耐心低于阈值时显示不耐烦 Icon（颜色由 UI 根据耐心值渐变）。</summary>
    public bool ShouldShowWaitingFoodImpatientIcon =>
        currentState == TableState.WaitingForFood && currentPatienceFood < effectiveImpatientThreshold;
    public IReadOnlyList<FryRecipeDatabase.FryRecipe> GetCurrentOrder() => currentOrder;

    /// <summary>
    /// 桌上「特殊残羹」视觉数量（与 DirtyPlateStack.plateCount 应对齐）。
    /// </summary>
    public int GetActiveEatenModelCount() => activeEatenModels.Count;

    /// <summary>
    /// 玩家从本桌的 <see cref="DirtyPlateStack"/> 取走 count 个盘子时调用：按栈顶顺序销毁对应数量的特殊残羹模型。
    /// </summary>
    public void OnDirtyPlatesDispensed(int count)
    {
        if (count <= 0) return;
        int remove = Mathf.Min(count, activeEatenModels.Count);
        for (int i = 0; i < remove; i++)
        {
            int idx = activeEatenModels.Count - 1;
            if (activeEatenModels[idx] != null)
                Destroy(activeEatenModels[idx]);
            activeEatenModels.RemoveAt(idx);
        }
    }
}