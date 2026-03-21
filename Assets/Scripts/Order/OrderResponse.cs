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

    [Header("Table Identity")]
    public int tableId = 0;

    [Header("State (ReadOnly)")]
    public TableState currentState = TableState.Empty;
    public float currentOrderProgress = 0f;
    
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
    private readonly List<FryRecipeDatabase.FryRecipe> servedThisRound = new List<FryRecipeDatabase.FryRecipe>();
    private Coroutine eatRoutine;

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
        servedThisRound.Clear();

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
        if (item == null || currentState != TableState.WaitingForFood)
        {
            itemPlacePoint.ClearOccupant();
            return;
        }

        FryRecipeDatabase.FryRecipe recipe = ResolveRecipeFromDish(item);
        if (recipe == null)
        {
            itemPlacePoint.ClearOccupant();
            Destroy(item.gameObject);
            return;
        }

        bool isFulfilled = GlobalOrderManager.Instance != null &&
                           GlobalOrderManager.Instance.TryFulfillOrder(tableId, recipe.recipeName);

        if (!isFulfilled)
        {
            itemPlacePoint.ClearOccupant();
            Destroy(item.gameObject);
            return;
        }

        int idx = FindInOrder(recipe);
        if (idx >= 0) currentOrder.RemoveAt(idx);

        servedThisRound.Add(recipe);
        if (MoneyManager.Instance != null) MoneyManager.Instance.AddMoney(recipe.price);

        itemPlacePoint.ClearOccupant();
        if (dishPlaceSystem != null) dishPlaceSystem.AcceptDish(item, recipe.size);

        if (currentOrder.Count == 0)
        {
            float totalEatTime = 0f;
            foreach (var r in servedThisRound) totalEatTime += Mathf.Max(0f, r.eatTime);
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
        float t = Mathf.Max(0f, seconds);
        while (t > 0f) { t -= Time.deltaTime; yield return null; }
        currentState = TableState.WaitingForCleanup;
        eatRoutine = null;
    }

    public void CleanUpTable()
    {
        if (dishPlaceSystem != null) dishPlaceSystem.ClearAllDishes();
        currentState = TableState.Empty; 
        
        // 【新增】：客人吃完走人，桌子解开预定锁
        isReserved = false; 
    }

    public float GetOrderProgressNormalized() => requiredOrderTime > 0f ? Mathf.Clamp01(currentOrderProgress / requiredOrderTime) : 0f;
    public IReadOnlyList<FryRecipeDatabase.FryRecipe> GetCurrentOrder() => currentOrder;
}