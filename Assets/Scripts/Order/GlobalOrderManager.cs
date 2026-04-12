using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor; // 用于自动分配桌号保存数据的编辑器功能
#endif

public class GlobalOrderManager : MonoBehaviour
{
    // 单例模式，方便全场景任意脚本呼叫它
    public static GlobalOrderManager Instance { get; private set; }

    [Header("饮料菜谱（供各桌自动获取）")]
    public DrinkRecipeDatabase drinkRecipeDatabase;

    [Tooltip("与 OrderGenerator 一致，用于联机客户端根据 recipeName 还原全局订单卡片")]
    public FryRecipeDatabase fryRecipeDatabase;

    [Header("全局耐心修正 (乘数，1.0 = 不修改)")]
    [Tooltip("所有顾客的初始耐心值乘数")]
    public float globalPatienceMultiplier = 1.0f;
    [Tooltip("所有顾客的耐心衰减速度乘数（>1 更快衰减，<1 更慢衰减）")]
    public float globalPatienceLossMultiplier = 1.0f;
    [Tooltip("上菜后回复耐心的乘数")]
    public float globalServeBonusMultiplier = 1.0f;

    [Header("全局耐心修正 (加算，在乘数结果基础上再加)")]
    [Tooltip("所有顾客的初始耐心值附加值")]
    public float globalPatienceAddon = 0f;
    [Tooltip("所有顾客的耐心衰减速度附加值（正数 = 更快衰减）")]
    public float globalPatienceLossAddon = 0f;

    [Header("全局设施速度修饰 (加减数)")]
    [Tooltip("所有切菜台处理速度的全局附加值 (正=加速，负=减速)")]
    public float globalChopSpeedAddon = 0f;
    [Tooltip("所有洗碗池处理速度的全局附加值 (正=加速，负=减速)")]
    public float globalWashSpeedAddon = 0f;

    [Header("Debug")]
    public bool debugLog = false;

    [Header("实时订单数据 (仅供观察)")]
    public List<OrderInstance> activeOrders = new List<OrderInstance>();

    // ================== 【核心事件：解耦 UI 的关键】 ==================
    // 当有新菜加入订单时触发，UI 监听此事件来生成 Card
    public static event Action<OrderInstance> OnOrderAdded;
    // 当某道菜被端上桌（完成）时触发，UI 监听此事件来销毁对应的 Card
    public static event Action<OrderInstance> OnOrderRemoved;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // ================== 【全局耐心修正系统】 ==================

    /// <summary>
    /// 耐心修正器数据结构。用于在 CustomerGroup 基础值上施加全局影响。
    /// 最终计算公式：最终值 = 基础值 × multiplier + addon
    /// </summary>
    public struct PatienceModifiers
    {
        public float patienceMultiplier;      // 乘在耐心初始值上
        public float patienceLossMultiplier;  // 乘在耐心衰减速度上
        public float serveBonusMultiplier;    // 乘在上菜回复量上
        public float patienceAddon;           // 在乘积结果上再加（耐心初始值）
        public float patienceLossAddon;       // 在乘积结果上再加（衰减速度）

        /// <summary>默认无修正（1.0倍 + 0加算）</summary>
        public static PatienceModifiers Default => new PatienceModifiers
        {
            patienceMultiplier = 1f,
            patienceLossMultiplier = 1f,
            serveBonusMultiplier = 1f,
            patienceAddon = 0f,
            patienceLossAddon = 0f
        };
    }

    /// <summary>
    /// 获取当前生效的全局耐心修正值（整合羁绊、天赋等效果）。
    /// OrderResponse 在接收顾客数据时调用此方法。
    /// </summary>
    public PatienceModifiers GetCurrentModifiers()
    {
        var mods = new PatienceModifiers
        {
            patienceMultiplier = globalPatienceMultiplier,
            patienceLossMultiplier = globalPatienceLossMultiplier,
            serveBonusMultiplier = globalServeBonusMultiplier,
            patienceAddon = globalPatienceAddon,
            patienceLossAddon = globalPatienceLossAddon
        };

        // 整合羁绊效果
        bool bridgeOk = BondRuntimeBridge.Instance != null && BondRuntimeBridge.Instance.State != null;

        // 家常羁绊：等菜阶段耐心衰减速度降低 20%（乘以 0.8）
        if (bridgeOk && BondRuntimeBridge.Instance.State.IsActive(RecipeBondTag.HomeCooking))
        {
            mods.patienceLossMultiplier *= 0.8f;
            if (debugLog) Debug.Log($"[GlobalOrderManager] 家常羁绊生效：patienceLossMultiplier 修正为 {mods.patienceLossMultiplier:F2}");
        }

        // 未来可在此处添加更多羁绊/天赋/道具效果：
        // if (bridgeOk && BondRuntimeBridge.Instance.State.IsActive(RecipeBondTag.XXX))
        // {
        //     mods.patienceMultiplier *= 1.2f;  // 示例
        // }

        return mods;
    }

    /// <summary>
    /// 桌子点完菜后，调用此方法把整桌的菜注册到全局中心
    /// </summary>
    public List<OrderInstance> RegisterOrdersForTable(int tableId, List<FryRecipeDatabase.FryRecipe> recipes)
    {
        var created = new List<OrderInstance>();
        foreach (var recipe in recipes)
        {
            if (recipe == null) continue;

            OrderInstance newOrder = new OrderInstance(tableId, recipe);
            activeOrders.Add(newOrder);
            OnOrderAdded?.Invoke(newOrder);
            created.Add(newOrder);

            Debug.Log($"[GlobalOrderManager] 接收新订单: 桌号 {tableId}, 菜品 {recipe.recipeName}, ID: {newOrder.orderId}");
        }
        return created;
    }

    /// <summary>联机纯客户端：镜像服务端已注册的全局订单（不经过服务端 Register 路径）。</summary>
    public void ClientMirrorRegisterOrders(int tableId, string[] orderIds, string[] recipeNames)
    {
        if (orderIds == null || recipeNames == null || orderIds.Length != recipeNames.Length) return;

        for (int i = 0; i < orderIds.Length; i++)
        {
            string oid = orderIds[i];
            string rname = recipeNames[i];
            if (string.IsNullOrEmpty(oid) || string.IsNullOrEmpty(rname)) continue;

            var recipe = ResolveRecipeByName(rname);
            if (recipe == null)
            {
                Debug.LogWarning($"[GlobalOrderManager] ClientMirror 无法解析菜谱: {rname}");
                continue;
            }

            var newOrder = new OrderInstance(tableId, recipe, oid);
            activeOrders.Add(newOrder);
            OnOrderAdded?.Invoke(newOrder);
        }
    }

    /// <summary>联机纯客户端：核销与 Host 一致的订单实例。</summary>
    public void ClientMirrorFulfillOrder(string orderId)
    {
        if (string.IsNullOrEmpty(orderId)) return;
        for (int i = 0; i < activeOrders.Count; i++)
        {
            var order = activeOrders[i];
            if (order != null && order.orderId == orderId)
            {
                activeOrders.RemoveAt(i);
                OnOrderRemoved?.Invoke(order);
                return;
            }
        }
    }

    /// <summary>联机纯客户端：撤掉某桌全部全局订单。</summary>
    public void ClientMirrorRemoveAllOrdersForTable(int tableId)
    {
        for (int i = activeOrders.Count - 1; i >= 0; i--)
        {
            var order = activeOrders[i];
            if (order == null || order.tableId != tableId) continue;
            activeOrders.RemoveAt(i);
            OnOrderRemoved?.Invoke(order);
        }
    }

    FryRecipeDatabase.FryRecipe ResolveRecipeByName(string recipeName)
    {
        if (string.IsNullOrEmpty(recipeName)) return null;
        if (fryRecipeDatabase != null)
        {
            var r = fryRecipeDatabase.FindByName(recipeName);
            if (r != null) return r;
        }
        if (drinkRecipeDatabase != null)
        {
            var r = drinkRecipeDatabase.FindByName(recipeName);
            if (r != null) return r;
        }
        var gen = UnityEngine.Object.FindAnyObjectByType<OrderGenerator>();
        if (gen != null && gen.recipeDatabase != null)
            return gen.recipeDatabase.FindByName(recipeName);
        return null;
    }

    /// <summary>
    /// 玩家把菜放到桌子上时，桌子调用此方法尝试核销订单
    /// </summary>
    /// <returns>如果是这桌需要的菜，返回 true 并将其从全局列表中划掉；否则返回 false</returns>
    public bool TryFulfillOrder(int tableId, string recipeName, out string removedOrderId)
    {
        removedOrderId = null;
        for (int i = 0; i < activeOrders.Count; i++)
        {
            var order = activeOrders[i];

            if (order.tableId == tableId && order.recipe != null && order.recipe.recipeName == recipeName)
            {
                removedOrderId = order.orderId;
                activeOrders.RemoveAt(i);
                OnOrderRemoved?.Invoke(order);

                Debug.Log($"[GlobalOrderManager] 订单核销成功: 桌号 {tableId}, 菜品 {recipeName}");
                return true;
            }
        }

        Debug.LogWarning($"[GlobalOrderManager] 订单核销失败: 桌号 {tableId} 并没有点 '{recipeName}'，或者已经上过了。");
        return false;
    }

    /// <summary>
    /// 顾客愤怒离场等情况下，移除该桌在全局队列中的全部订单并通知 UI。
    /// </summary>
    public void RemoveAllOrdersForTable(int tableId)
    {
        for (int i = activeOrders.Count - 1; i >= 0; i--)
        {
            var order = activeOrders[i];
            if (order == null || order.tableId != tableId) continue;

            activeOrders.RemoveAt(i);
            OnOrderRemoved?.Invoke(order);
            Debug.Log($"[GlobalOrderManager] 整桌撤单: 桌号 {tableId}, 菜品 {(order.recipe != null ? order.recipe.recipeName : "?")}, ID: {order.orderId}");
        }
    }

    // ================== 【工程化小工具：一键分配桌号】 ==================
#if UNITY_EDITOR
    [ContextMenu("自动分配场景中的桌号 (Auto Assign Table IDs)")]
    public void AutoAssignTableIDs()
    {
        // 找到场景中所有的桌子组件
        OrderResponse[] allTables = FindObjectsByType<OrderResponse>(FindObjectsSortMode.None);
        
        // 按照物体在 Hierarchy 里的名字进行排序（比如 Table1, Table2...）
        Array.Sort(allTables, (a, b) => a.name.CompareTo(b.name));

        // 挨个赋予桌号（从 1 开始）
        for (int i = 0; i < allTables.Length; i++)
        {
            allTables[i].tableId = i + 1; // 这里的 tableId 我们下一步会加到 OrderResponse 里
            
            // 标记物体已修改，确保按下 Ctrl+S 能保存进去
            EditorUtility.SetDirty(allTables[i]); 
        }

        Debug.Log($"<color=#00FF00>[系统维护]</color> 成功为场景中的 {allTables.Length} 张桌子分配了连续的桌号！");
    }
#endif
}