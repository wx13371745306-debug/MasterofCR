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

    [Tooltip("这桌拥有的椅子位置（把椅子子物体拖进来）")]
    public List<Transform> chairs = new List<Transform>();

    [Header("Order Settings")]
    public int minDishes = 1;
    public int maxDishes = 2;
    public float requiredOrderTime = 2f;

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

    private bool isInteracting = false;

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

        currentState = TableState.Empty;
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

        // 玩家端走脏盘子堆后，桌子彻底空出
        if (currentState == TableState.WaitingForCleanup)
        {
            if (itemPlacePoint != null && itemPlacePoint.CurrentItem == null)
            {
                // 玩家按下J把大坨脏盘子拿走后，才真正销毁留在桌子上的各个独立空盘子模型
                if (dishPlaceSystem != null) dishPlaceSystem.ClearAllDishes();
                dishesOnTable.Clear();

                currentState = TableState.Empty;
                isReserved = false;
                Debug.Log($"[OrderResponse] 桌号 {tableId} 脏盘堆被收走，桌子重置为 Empty 空闲状态！");
            }
        }
    }

    // ================== 【供 AI 调用的接口】 ==================
    [ContextMenu("测试：模拟顾客入座")]
    public void GroupSeated()
    {
        if (currentState != TableState.Empty) return;

        // 【修改点】：不再直接等待点单，而是进入看菜单状态
        currentState = TableState.ReadingMenu;
        StartCoroutine(ReadingMenuRoutine());
        Debug.Log($"[OrderResponse] 桌号 {tableId} 顾客已落座，正在看菜单...");
    }

    // 【新增】：看菜单的随机缓冲时间
    private IEnumerator ReadingMenuRoutine()
    {
        float waitTime = Random.Range(2f, 5f);
        yield return new WaitForSeconds(waitTime);

        currentState = TableState.WaitingToOrder;
        currentOrderProgress = 0f;
        Debug.Log($"[OrderResponse] 桌号 {tableId} 顾客看完了，头顶亮起图标请求点单！");
    }

    // ================== 【BaseStation 交互接口实现】 ==================
    public override bool CanInteract(PlayerItemInteractor interactor)
    {
        return currentState == TableState.WaitingToOrder || currentState == TableState.Ordering;
    }

    public override void BeginInteract(PlayerItemInteractor interactor)
    {
        isInteracting = true;
        if (currentState == TableState.WaitingToOrder)
        {
            currentState = TableState.Ordering; // 开始读条
        }
    }

    public override void EndInteract(PlayerItemInteractor interactor)
    {
        isInteracting = false;
        if (currentState == TableState.Ordering)
        {
            currentState = TableState.WaitingToOrder; // 松手退回等待图标状态
        }
    }

    // ================== 【核心业务逻辑】 ==================
    void CompleteOrdering()
    {
        currentState = TableState.WaitingForFood;
        isInteracting = false;
        currentOrderProgress = requiredOrderTime;

        currentOrder.Clear();
        dishesOnTable.Clear();

        if (orderGenerator != null)
        {
            var next = orderGenerator.GenerateOrder(minDishes, maxDishes, dishPlaceSystem);
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
            if (MoneyManager.Instance != null) MoneyManager.Instance.AddMoney(recipe.price);
            currentOrder.RemoveAt(idx);
            Debug.Log($"[OrderResponse] 桌号 {tableId} 成功上对菜: {recipe.recipeName}。剩 {currentOrder.Count} 道菜！消单加钱！");
        }
        else
        {
            isCorrectOrder = false;
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
        if (dish == null || recipeDatabase == null) return null;
        DishRecipeTag tag = dish.GetComponent<DishRecipeTag>() ?? dish.GetComponentInParent<DishRecipeTag>();
        return tag != null ? recipeDatabase.FindByName(tag.recipeName) : null;
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
        
        Debug.Log($"[OrderResponse] 桌号 {tableId} 用餐完毕，生成各个空盘模型以及整体的隐形脏盘子堆...");
        
        int dirtyCount = 0;
        foreach (var d in dishesOnTable)
        {
            if (d.physicalItem != null) dirtyCount++;
            
            // --- 恢复你想要的单个菜的“盘子剥离并生成变空盘子”独立逻辑 ---
            if (d.physicalItem == null) continue;
            GameObject dishObj = d.physicalItem.gameObject;

            if (!dishObj.activeSelf) continue;

            foreach(Transform child in dishObj.transform)
            {
                child.gameObject.SetActive(false);
            }
            Renderer[] rootRenderers = dishObj.GetComponents<Renderer>();
            foreach(var r in rootRenderers) r.enabled = false;

            if (d.recipe != null && d.recipe.eatenPrefab != null)
            {
                GameObject emptyPlate = Instantiate(d.recipe.eatenPrefab, dishObj.transform);
                emptyPlate.transform.localPosition = Vector3.zero;
                emptyPlate.transform.localRotation = Quaternion.identity;
            }
        }

        // 注意：不在这里调用 ClearAllDishes，把散落的空盘继续留在桌上给玩家看

        // 2. 生成一个代表整体的脏盘子堆，直接放在桌子的中心放置点上
        if (dirtyCount > 0 && dirtyPlateStackPrefab != null)
        {
            DirtyPlateStack stack = Instantiate(dirtyPlateStackPrefab, transform.position, Quaternion.identity);
            stack.SetPlateCount(dirtyCount);
            stack.HideVisualsForTable(); // 关键修正：强制它在被拿起前绝对隐身！
            
            if (itemPlacePoint != null)
            {
                itemPlacePoint.TryAcceptItem(stack); 
            }
        }

        // 3. 将桌子状态转为 WaitingForCleanup，ui 显示待清理
        currentState = TableState.WaitingForCleanup;
        eatRoutine = null;
    }

    public void CleanUpTable()
    {
        if (dishPlaceSystem != null) dishPlaceSystem.ClearAllDishes();
        dishesOnTable.Clear(); // 清理记录
        currentState = TableState.Empty; 
        
        // 【新增】：客人吃完走人，桌子解开预定锁
        isReserved = false; 
    }

    public float GetOrderProgressNormalized() => requiredOrderTime > 0f ? Mathf.Clamp01(currentOrderProgress / requiredOrderTime) : 0f;
    public IReadOnlyList<FryRecipeDatabase.FryRecipe> GetCurrentOrder() => currentOrder;
}