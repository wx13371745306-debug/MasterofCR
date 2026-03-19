using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OrderResponse : MonoBehaviour
{
    [Header("Refs")]
    public ItemPlacePoint itemPlacePoint;
    public DishPlaceSystem dishPlaceSystem;
    public OrderGenerator orderGenerator;
    public FryRecipeDatabase recipeDatabase;

    [Header("Order Settings")]
    [Tooltip("这桌最少点几个菜")]
    public int minDishes = 1;
    [Tooltip("这桌最多点几个菜")]
    public int maxDishes = 2;

    [Header("State")]
    public bool waitingForCleanup = false;

    private readonly List<FryRecipeDatabase.FryRecipe> currentOrder = new List<FryRecipeDatabase.FryRecipe>();
    private readonly List<FryRecipeDatabase.FryRecipe> servedThisRound = new List<FryRecipeDatabase.FryRecipe>();
    private Coroutine eatRoutine;

    void OnEnable()
    {
        if (itemPlacePoint != null)
        {
            itemPlacePoint.OnItemPlacedEvent += OnItemPlaced;
        }
    }

    void OnDisable()
    {
        if (itemPlacePoint != null)
        {
            itemPlacePoint.OnItemPlacedEvent -= OnItemPlaced;
        }

        if (eatRoutine != null)
        {
            StopCoroutine(eatRoutine);
            eatRoutine = null;
        }
    }

    void Start()
    {
        if (recipeDatabase == null && orderGenerator != null)
            recipeDatabase = orderGenerator.recipeDatabase;

        StartNewOrder();
    }

    public IReadOnlyList<FryRecipeDatabase.FryRecipe> GetCurrentOrder() => currentOrder;

    public void StartNewOrder()
    {
        Debug.Log($"[OrderResponse] StartNewOrder: 开始为桌子 {gameObject.name} 生成新订单...");
        waitingForCleanup = false;
        currentOrder.Clear();
        servedThisRound.Clear();

        if (eatRoutine != null)
        {
            StopCoroutine(eatRoutine);
            eatRoutine = null;
        }

        if (orderGenerator != null)
        {
            // 【修改点】：把这桌的需求和排版系统传给生成器
            var next = orderGenerator.GenerateOrder(minDishes, maxDishes, dishPlaceSystem);
            currentOrder.AddRange(next);
            Debug.Log($"[OrderResponse] 新订单生成完毕，共 {currentOrder.Count} 道菜。");
        }
        else
        {
            Debug.LogWarning($"[OrderResponse] 无法生成订单：未配置 OrderGenerator。");
        }
    }

    void OnItemPlaced(CarryableItem item)
    {
        if (waitingForCleanup) 
        {
            Debug.Log($"[OrderResponse] 收到物品 {item.name}，但桌子正在等待清理，忽略。");
            return;
        }
        if (item == null) return;

        Debug.Log($"[OrderResponse] 玩家放置了物品: {item.name}。开始解析配方...");

        FryRecipeDatabase.FryRecipe recipe = ResolveRecipeFromDish(item);
        if (recipe == null)
        {
            Debug.LogWarning($"[OrderResponse] 匹配失败：物品 {item.name} 身上没有找到 DishRecipeTag，或配方库中不存在该菜品。直接销毁。");
            itemPlacePoint.ClearOccupant();
            Destroy(item.gameObject);
            return;
        }

        int idx = FindInOrder(recipe);
        if (idx < 0)
        {
            Debug.LogWarning($"[OrderResponse] 匹配失败：当前订单不需要菜品 '{recipe.recipeName}'。直接销毁。");
            itemPlacePoint.ClearOccupant();
            Destroy(item.gameObject);
            return;
        }

        Debug.Log($"[OrderResponse] 匹配成功！当前订单接收了菜品 '{recipe.recipeName}'。准备结算加钱。");
        currentOrder.RemoveAt(idx);
        servedThisRound.Add(recipe);

        if (MoneyManager.Instance != null)
        {
            MoneyManager.Instance.AddMoney(recipe.price);
            Debug.Log($"[OrderResponse] 结算：增加金钱 {recipe.price}。");
        }

        itemPlacePoint.ClearOccupant();
        if (dishPlaceSystem != null)
        {
            dishPlaceSystem.AcceptDish(item, recipe.size);
        }

        if (currentOrder.Count == 0)
        {
            float totalEatTime = 0f;
            for (int i = 0; i < servedThisRound.Count; i++)
            {
                if (servedThisRound[i] == null) continue;
                // 注意：这里是累加吃菜时间，如果你希望顾客是同时吃，可以改成 Mathf.Max
                totalEatTime += Mathf.Max(0f, servedThisRound[i].eatTime); 
            }

            Debug.Log($"[OrderResponse] 本桌订单已全部上齐！进入用餐倒计时：{totalEatTime} 秒。");
            eatRoutine = StartCoroutine(EatCountdown(totalEatTime));
        }
        else
        {
            Debug.Log($"[OrderResponse] 订单进度更新：还差 {currentOrder.Count} 道菜。");
        }
    }

    private FryRecipeDatabase.FryRecipe ResolveRecipeFromDish(CarryableItem dish)
    {
        if (dish == null) return null;
        if (recipeDatabase == null) return null;

        DishRecipeTag tag = dish.GetComponent<DishRecipeTag>();
        if (tag == null) tag = dish.GetComponentInParent<DishRecipeTag>();
        if (tag == null) return null;

        // 这里需要你在 FryRecipeDatabase 里加一个 FindByName 的方法，或者让 Trae 帮你补上
        return recipeDatabase.FindByName(tag.recipeName);
    }

    private int FindInOrder(FryRecipeDatabase.FryRecipe recipe)
    {
        if (recipe == null) return -1;

        for (int i = 0; i < currentOrder.Count; i++)
        {
            var r = currentOrder[i];
            if (r == null) continue;
            if (r == recipe) return i;
            if (r.recipeName == recipe.recipeName) return i;
        }
        return -1;
    }

    private IEnumerator EatCountdown(float seconds)
    {
        Debug.Log($"[OrderResponse] 开始用餐，耗时 {seconds} 秒...");
        float t = Mathf.Max(0f, seconds);
        while (t > 0f)
        {
            t -= Time.deltaTime;
            yield return null;
        }

        Debug.Log($"[OrderResponse] 用餐结束，进入待清理状态。");
        waitingForCleanup = true;
        eatRoutine = null;
    }

    public void CleanUpTable()
    {
        Debug.Log($"[OrderResponse] 开始清理桌子...");
        if (dishPlaceSystem != null)
            dishPlaceSystem.ClearAllDishes();

        waitingForCleanup = false;
        Debug.Log($"[OrderResponse] 桌子清理完毕，准备生成下一单。");
        StartNewOrder();
    }
}