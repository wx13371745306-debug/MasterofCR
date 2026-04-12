using System.Collections.Generic;
using UnityEngine;
using Mirror;

/// <summary>
/// 金钱、商店购物、菜谱的网络同步桥接层。
/// 挂在有 NetworkIdentity 的管理物体上（与 NetworkDayCycleBridge 同物体即可）。
/// Host 端驱动所有逻辑，Guest 端通过 SyncVar / SyncList 接收状态。
/// </summary>
public class NetworkShopBridge : NetworkBehaviour
{
    [Header("Refs")]
    [SerializeField] private ShopItemCatalog shopCatalog;
    [SerializeField] private ShopDeliveryQueue shopDeliveryQueue;
    [SerializeField] private DayCycleManager dayCycleManager;
    [SerializeField] private MenuSO menuSO;
    [Tooltip("所有菜谱数据源（FryRecipeDatabase / DrinkRecipeDatabase），用于按 recipeName 还原引用")]
    [SerializeField] private List<ScriptableObject> recipeSources = new List<ScriptableObject>();

    [Header("Debug")]
    public bool debugLog = true;

    // ── 金钱同步 ──
    [SyncVar(hook = nameof(OnSyncedMoneyChanged))]
    private int syncedMoney;

    /// <summary>Host 侧 ShopDeliveryQueue 是否有待交付记账；供 Guest UI 校验（与本地队列解耦）。</summary>
    [SyncVar]
    private bool syncedHasPendingShopOrders;

    // ── 菜谱同步 ──
    readonly SyncList<string> syncedRecipeNames = new SyncList<string>();

    public static NetworkShopBridge Instance { get; private set; }
    bool IsHostAuthority => NetworkServer.active;

    void Awake()
    {
        Instance = this;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        syncedRecipeNames.Callback += OnSyncedRecipeListChanged;

        if (!IsHostAuthority)
        {
            ApplyMoneyFromHost(syncedMoney);
            RebuildMenuFromSyncList();
        }
    }

    void Update()
    {
        if (!NetworkClient.active) return;

        if (IsHostAuthority)
        {
            if (MoneyManager.Instance != null)
                syncedMoney = MoneyManager.Instance.CurrentMoney;
            if (shopDeliveryQueue != null)
                syncedHasPendingShopOrders = shopDeliveryQueue.HasPendingOrders;
        }
    }

    /// <summary>联机时以 Host 为准；单机读本地 <see cref="ShopDeliveryQueue"/>。</summary>
    public static bool HasPendingOrdersForUiValidation(ShopDeliveryQueue localQueue)
    {
        if (NetworkClient.active && Instance != null)
            return Instance.GetAuthoritativeHasPendingOrders();
        return localQueue != null && localQueue.HasPendingOrders;
    }

    bool GetAuthoritativeHasPendingOrders()
    {
        if (IsHostAuthority && shopDeliveryQueue != null)
            return shopDeliveryQueue.HasPendingOrders;
        return syncedHasPendingShopOrders;
    }

    // ── 金钱 SyncVar Hook ──

    void OnSyncedMoneyChanged(int oldVal, int newVal)
    {
        if (IsHostAuthority) return;
        ApplyMoneyFromHost(newVal);
    }

    void ApplyMoneyFromHost(int value)
    {
        if (MoneyManager.Instance != null)
            MoneyManager.Instance.ForceSetMoney(value);
    }

    // ── Guest 购物请求 ──

    /// <summary>
    /// Guest 端调用：将购物车以 itemId=qty 对的形式发给 Host 执行扣款 + 生成货物。
    /// </summary>
    public void NetworkPlaceOrder(Dictionary<string, int> cart, int totalPrice)
    {
        if (IsHostAuthority)
        {
            ExecuteOrder(cart, totalPrice);
        }
        else
        {
            string[] ids = new string[cart.Count];
            int[] qtys = new int[cart.Count];
            int i = 0;
            foreach (var kv in cart)
            {
                ids[i] = kv.Key;
                qtys[i] = kv.Value;
                i++;
            }
            CmdPlaceOrder(ids, qtys, totalPrice);
        }
    }

    [Command(requiresAuthority = false)]
    void CmdPlaceOrder(string[] ids, int[] qtys, int totalPrice)
    {
        if (debugLog) Debug.Log($"[NetworkShopBridge] Host 收到 Guest 购物请求，总价={totalPrice}");
        var cart = new Dictionary<string, int>(System.StringComparer.Ordinal);
        for (int i = 0; i < ids.Length && i < qtys.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(ids[i]) && qtys[i] > 0)
                cart[ids[i]] = qtys[i];
        }
        ExecuteOrder(cart, totalPrice);
    }

    void ExecuteOrder(Dictionary<string, int> cart, int totalPrice)
    {
        if (MoneyManager.Instance == null || !MoneyManager.Instance.TrySpendMoney(totalPrice))
        {
            if (debugLog) Debug.LogWarning("[NetworkShopBridge] 扣款失败（余额不足或 MoneyManager 缺失）");
            return;
        }

        var dcm = dayCycleManager != null ? dayCycleManager : DayCycleManager.Instance;
        DayCyclePhase currentPhase = dcm != null ? dcm.Phase : DayCyclePhase.Prep;

        if (shopDeliveryQueue != null && shopCatalog != null)
        {
            if (currentPhase == DayCyclePhase.Prep && dcm != null)
                shopDeliveryQueue.EnqueueOrSpawn(cart, shopCatalog, dcm.PrepElapsed, dcm.ShopDeliveryDelayFromPrepStart);
            else
                shopDeliveryQueue.EnqueueForNextPrep(cart);
        }
    }

    // ── 菜谱同步 ──

    /// <summary>任一端确认菜谱后调用，将 recipeName 列表同步给所有端。</summary>
    public void NetworkSyncMenu(List<string> recipeNames)
    {
        if (IsHostAuthority)
        {
            ApplySyncedRecipeNames(recipeNames);
        }
        else
        {
            CmdSyncMenu(recipeNames.ToArray());
        }
    }

    [Command(requiresAuthority = false)]
    void CmdSyncMenu(string[] names)
    {
        if (debugLog) Debug.Log($"[NetworkShopBridge] Host 收到菜谱同步请求，数量={names.Length}");
        ApplySyncedRecipeNames(new List<string>(names));
    }

    void ApplySyncedRecipeNames(List<string> names)
    {
        syncedRecipeNames.Clear();
        foreach (var n in names)
            syncedRecipeNames.Add(n);

        ApplyMenuToLocal(names);
    }

    void OnSyncedRecipeListChanged(SyncList<string>.Operation op, int index, string oldItem, string newItem)
    {
        if (IsHostAuthority) return;
        RebuildMenuFromSyncList();
    }

    void RebuildMenuFromSyncList()
    {
        var names = new List<string>();
        foreach (var n in syncedRecipeNames)
            names.Add(n);
        ApplyMenuToLocal(names);
    }

    void ApplyMenuToLocal(List<string> names)
    {
        if (menuSO == null) return;

        menuSO.Clear();
        foreach (var recipeName in names)
        {
            var recipe = FindRecipeByName(recipeName);
            if (recipe != null)
                menuSO.selectedRecipes.Add(recipe);
            else if (debugLog)
                Debug.LogWarning($"[NetworkShopBridge] 无法还原菜谱 '{recipeName}'，所有数据源中均未找到");
        }

        if (debugLog) Debug.Log($"[NetworkShopBridge] 本地 MenuSO 已同步，共 {menuSO.selectedRecipes.Count} 道菜");

        if (BondRuntimeBridge.Instance != null)
            BondRuntimeBridge.Instance.Refresh();

        var bondHud = Object.FindAnyObjectByType<HUDActiveBondUI>();
        if (bondHud != null)
            bondHud.RefreshBondsDisplay();
    }

    FryRecipeDatabase.FryRecipe FindRecipeByName(string recipeName)
    {
        if (string.IsNullOrEmpty(recipeName)) return null;

        foreach (var source in recipeSources)
        {
            if (source == null) continue;
            if (source is FryRecipeDatabase fryDb)
            {
                var r = fryDb.FindByName(recipeName);
                if (r != null) return r;
            }
            else if (source is DrinkRecipeDatabase drinkDb)
            {
                var r = drinkDb.FindByName(recipeName);
                if (r != null) return r;
            }
        }
        return null;
    }
}
