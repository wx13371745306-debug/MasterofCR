using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

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
    [Tooltip("收盘时 Instantiate 的脏盘堆网络预制体（需 NetworkIdentity，且加入 NetworkManager Spawn 列表）；含 singlePlatePrefab 等视觉参数")]
    public DirtyPlateStack dirtyPlateStackPrefab;
    [Tooltip("可选：与 dirtyPlateStackPrefab 相同即可；未填则使用 dirtyPlateStackPrefab")]
    public DirtyPlateStack dirtyPlateStackHandPrefab;

    [Header("Table Identity")]
    public int tableId = 0;

    [Header("State (ReadOnly)")]
    [SyncVar(hook = nameof(OnCurrentStateSynced))]
    public TableState currentState = TableState.Empty;
    [SyncVar(hook = nameof(OnOrderProgressSynced))]
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

    /// <summary>联机同步到客户端：用于 UI 计算耐心比例（与 effectiveMaxPatienceOrder 一致，由服务端写入）。</summary>
    [SyncVar] public float netMaxPatienceOrder = 100f;
    /// <summary>联机同步到客户端：等上菜阶段耐心上限。</summary>
    [SyncVar] public float netMaxPatienceFood = 100f;
    /// <summary>联机同步到客户端：等上菜「不耐烦」图标阈值。</summary>
    [SyncVar] public float netImpatientThreshold = 40f;

    [SyncVar(hook = nameof(OnPatienceOrderSynced))]
    public float currentPatienceOrder;
    [SyncVar(hook = nameof(OnPatienceFoodSynced))]
    public float currentPatienceFood;

    [Header("Order Settings")]
    [Tooltip("桌子物理上限（兜底），实际点菜数以顾客组配置为准")]
    public int maxDishes = 6;
    public float requiredOrderTime = 2f;
    [HideInInspector] public int currentCustomerCount = 1;
    [HideInInspector] public int currentMinDishes = 1;

    private readonly List<FryRecipeDatabase.FryRecipe> currentOrder = new List<FryRecipeDatabase.FryRecipe>();
    private readonly List<FryRecipeDatabase.FryRecipe> clientOrderView = new List<FryRecipeDatabase.FryRecipe>();

    /// <summary>服务端维护；客户端通过 SyncList 还原 GetCurrentOrder。</summary>
    public readonly SyncList<string> syncedOrderRecipeNames = new SyncList<string>();

    /// <summary>桌面进度 UI：状态/耐心/读条/订单列表任一同步变化时触发（含服务端写入 SyncVar 时）。</summary>
    public event Action OnTableProgressUiSync;

    private Coroutine eatRoutine;

    [System.Serializable]
    public class PlacedDishRecord
    {
        public FryRecipeDatabase.FryRecipe recipe; // 配方数据
        public CarryableItem physicalItem;         // 场景里真实的物理盘子物体
        public bool isCorrectOrder;                // 标记：上对的还是白送的？
    }

    private readonly List<PlacedDishRecord> dishesOnTable = new List<PlacedDishRecord>();

    // 【新增】：用来存储纯净无害的“吃完后”独立视觉模型，防止跟物理逻辑产生任何藕断丝连（仅服务端）
    private readonly List<GameObject> activeEatenModels = new List<GameObject>();
    /// <summary>纯客户端：RpcSpawnClientEatenVisuals 生成的残羹副本，与 activeEatenModels 对应。</summary>
    private readonly List<GameObject> clientReplicaEatenModels = new List<GameObject>();

    [SyncVar(hook = nameof(OnPendingDirtyPlatesHook))]
    private int syncPendingDirtyPlates;

    /// <summary>待收脏盘数量（联机由 SyncVar 同步；单机无 Mirror 时直接写 syncPendingDirtyPlates）。</summary>
    public int PendingDirtyPlatesCount => syncPendingDirtyPlates;

    private bool isInteracting = false;
    private CustomerGroup boundGroup;
    private bool isAbandoningPatience;
    private Coroutine readingMenuCoroutine;

    void SetPendingDirtyPlates(int value)
    {
        value = Mathf.Max(0, value);
        if (NetworkServer.active)
            syncPendingDirtyPlates = value;
        else if (!NetworkClient.active && !NetworkServer.active)
            syncPendingDirtyPlates = value;
    }

    void OnPendingDirtyPlatesHook(int oldVal, int newVal)
    {
        if (NetworkServer.active) return;
        if (newVal == 0 && clientReplicaEatenModels.Count > 0)
            ClearAllClientReplicaEaten();
    }

    void ClearAllClientReplicaEaten()
    {
        foreach (var go in clientReplicaEatenModels)
        {
            if (go != null) Destroy(go);
        }
        clientReplicaEatenModels.Clear();
    }

    [ClientRpc]
    void RpcSpawnClientEatenVisuals(string[] recipeNames, Vector3[] positions, Quaternion[] rotations)
    {
        if (NetworkServer.active) return;
        if (recipeNames == null || positions == null || rotations == null) return;
        int n = Mathf.Min(recipeNames.Length, positions.Length, rotations.Length);
        for (int i = 0; i < n; i++)
        {
            if (string.IsNullOrEmpty(recipeNames[i])) continue;
            FryRecipeDatabase.FryRecipe recipe = recipeDatabase != null ? recipeDatabase.FindByName(recipeNames[i]) : null;
            if (recipe == null && drinkRecipeDatabase != null)
                recipe = drinkRecipeDatabase.FindByName(recipeNames[i]);
            if (recipe == null || recipe.eatenPrefab == null) continue;
            GameObject go = Instantiate(recipe.eatenPrefab, positions[i], rotations[i], transform);
            clientReplicaEatenModels.Add(go);
        }
    }

    void ServerNotifyClientEatenLeftovers(List<string> names, List<Vector3> poss, List<Quaternion> rots)
    {
        if (!NetworkServer.active || names == null || names.Count == 0) return;
        RpcSpawnClientEatenVisuals(names.ToArray(), poss.ToArray(), rots.ToArray());
    }

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

    private static int autoTableIdCounter = 0;

    protected override void Awake()
    {
        base.Awake();
        if (tableId <= 0)
        {
            tableId = ++autoTableIdCounter;
            Debug.LogWarning($"[OrderResponse] 桌号未手动分配，自动赋值为 {tableId}（请在编辑器中使用 GlobalOrderManager 的「自动分配桌号」功能）");
        }
    }

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

        // SyncVar 仅服务端初始化；客机依赖同步与 hook 更新 itemPlacePoint
        if (!NetworkClient.active || NetworkServer.active)
            SetState(TableState.Empty);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        syncedOrderRecipeNames.Callback += OnSyncedOrderRecipeNamesChanged;
        ApplyItemPlaceLockForState(currentState);
    }

    public override void OnStopClient()
    {
        syncedOrderRecipeNames.Callback -= OnSyncedOrderRecipeNamesChanged;
        base.OnStopClient();
    }

    void OnSyncedOrderRecipeNamesChanged(SyncList<string>.Operation op, int index, string oldItem, string newItem)
    {
        RaiseTableProgressUiSync();
    }

    void RaiseTableProgressUiSync()
    {
        OnTableProgressUiSync?.Invoke();
    }

    void OnPatienceOrderSynced(float oldValue, float newValue)
    {
        RaiseTableProgressUiSync();
    }

    void OnPatienceFoodSynced(float oldValue, float newValue)
    {
        RaiseTableProgressUiSync();
    }

    void OnOrderProgressSynced(float oldValue, float newValue)
    {
        RaiseTableProgressUiSync();
    }

    void OnCurrentStateSynced(TableState oldState, TableState newState)
    {
        ApplyItemPlaceLockForState(newState);
        RaiseTableProgressUiSync();
    }

    /// <summary>桌面 UI：等菜耐心比例（联机用 net 同步上限）。</summary>
    public float GetDisplayMaxPatienceOrder()
    {
        return netMaxPatienceOrder > 0.01f ? netMaxPatienceOrder : effectiveMaxPatienceOrder;
    }

    /// <summary>桌面 UI：上菜后耐心比例（联机用 net 同步上限）。</summary>
    public float GetDisplayMaxPatienceFood()
    {
        return netMaxPatienceFood > 0.01f ? netMaxPatienceFood : effectiveMaxPatienceFood;
    }

    /// <summary>桌面 UI：不耐烦图标阈值（联机用 net 同步）。</summary>
    public float GetDisplayImpatientThreshold()
    {
        return netImpatientThreshold > 0.001f ? netImpatientThreshold : effectiveImpatientThreshold;
    }

    void ApplyItemPlaceLockForState(TableState state)
    {
        if (itemPlacePoint == null) return;
        bool allowServe = (state == TableState.WaitingForFood || state == TableState.Eating);
        itemPlacePoint.externalLock = !allowServe;
    }

    void SetState(TableState newState)
    {
        currentState = newState;
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

        netMaxPatienceOrder = effectiveMaxPatienceOrder;
        netMaxPatienceFood = effectiveMaxPatienceFood;
        netImpatientThreshold = effectiveImpatientThreshold;

        currentMinDishes = group.minDishes;
        maxDishes = Mathf.Max(group.maxDishes, maxDishes); // 取较大值作为上限兜底
        currentCustomerCount = group.groupSize;

        // 预设初始耐心（SyncVar 仅服务端或单机写入）
        if (!NetworkClient.active || NetworkServer.active)
        {
            currentPatienceOrder = effectiveMaxPatienceOrder;
            currentPatienceFood = effectiveMaxPatienceFood;
        }

        if (debugLog) Debug.Log($"[OrderResponse] 桌号 {tableId} 接收顾客组配置 | " +
            $"耐心Order={effectiveMaxPatienceOrder:F0} Loss={effectiveLossPerSecondOrder:F1}/s | " +
            $"耐心Food={effectiveMaxPatienceFood:F0} Loss={effectiveLossPerSecondFood:F1}/s | " +
            $"上菜回复={effectiveServePatienceBonus:F0} Cap={effectivePatienceCap:F0} | " +
            $"人数={currentCustomerCount} 点菜={currentMinDishes}-{maxDishes}");
    }

    void Update()
    {
        bool isServerOrOffline = !Mirror.NetworkClient.active || Mirror.NetworkServer.active;
        if (!isServerOrOffline) return;

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
        if (NetworkClient.active && !NetworkServer.active) return;
        if (currentState != TableState.Empty) return;

        // 【修改点】：不再直接等待点单，而是进入看菜单状态
        SetState(TableState.ReadingMenu);
        if (readingMenuCoroutine != null)
            StopCoroutine(readingMenuCoroutine);
        readingMenuCoroutine = StartCoroutine(ReadingMenuRoutine());
        Debug.Log($"[OrderResponse] 桌号 {tableId} 顾客已落座，正在看菜单...");
    }

    // 【新增】：看菜单的随机缓冲时间
    private IEnumerator ReadingMenuRoutine()
    {
        float waitTime = UnityEngine.Random.Range(2f, 5f);
        yield return new WaitForSeconds(waitTime);
        readingMenuCoroutine = null;

        SetState(TableState.WaitingToOrder);
        currentOrderProgress = 0f;
        if (!NetworkClient.active || NetworkServer.active)
            currentPatienceOrder = effectiveMaxPatienceOrder;
        Debug.Log($"[OrderResponse] 桌号 {tableId} 顾客看完了，头顶亮起图标请求点单！");
    }

    /// <summary>
    /// 服务端：生成整堆网络脏盘、清桌并交给指定玩家（J/K 收盘共用）。
    /// </summary>
    public bool ServerTakeAllDirtyPlatesFor(PlayerNetworkController player)
    {
        if (!NetworkServer.active) return false;
        if (player == null) return false;
        if (currentState != TableState.WaitingForCleanup || syncPendingDirtyPlates <= 0) return false;

        PlayerItemInteractor pi = player.GetComponent<PlayerItemInteractor>();
        if (pi != null && pi.IsHoldingItem()) return false;

        DirtyPlateStack prefabRoot = dirtyPlateStackHandPrefab != null ? dirtyPlateStackHandPrefab : dirtyPlateStackPrefab;
        if (prefabRoot == null)
        {
            if (debugLog) Debug.LogError($"[OrderResponse] 桌 {tableId} 未配置 dirtyPlateStackPrefab，无法收盘。");
            return false;
        }

        int count = syncPendingDirtyPlates;
        GameObject go = Instantiate(prefabRoot.gameObject, new Vector3(-9999f, -9999f, -9999f), Quaternion.identity);
        DirtyPlateStack stack = go.GetComponent<DirtyPlateStack>();
        if (stack == null)
        {
            Destroy(go);
            return false;
        }

        stack.plateCount = Mathf.Max(1, count);
        NetworkServer.Spawn(go);

        CleanUpTable();
        player.ServerAssignPickupToCaller(go, false);
        return true;
    }

    /// <summary>
    /// 无 Mirror 网络时：本地 Instantiate 整堆并清桌。
    /// </summary>
    public void TryTakeAllDirtyPlatesOffline(PlayerItemInteractor interactor)
    {
        if (NetworkClient.active || NetworkServer.active) return;
        if (interactor == null || interactor.IsHoldingItem()) return;
        if (currentState != TableState.WaitingForCleanup || syncPendingDirtyPlates <= 0) return;

        DirtyPlateStack prefabRoot = dirtyPlateStackHandPrefab != null ? dirtyPlateStackHandPrefab : dirtyPlateStackPrefab;
        if (prefabRoot == null) return;

        int count = syncPendingDirtyPlates;
        GameObject go = Instantiate(prefabRoot.gameObject, new Vector3(-9999f, -9999f, -9999f), Quaternion.identity);
        DirtyPlateStack stack = go.GetComponent<DirtyPlateStack>();
        if (stack == null)
        {
            Destroy(go);
            return;
        }

        stack.plateCount = Mathf.Max(1, count);
        stack.BeginHold(interactor.GetHoldPoint());
        interactor.ReplaceHeldItem(stack);
        CleanUpTable();
    }

    // ================== 【BaseStation 交互接口实现】 ==================
    public override bool CanInteract(PlayerItemInteractor interactor)
    {
        if (currentState == TableState.WaitingForCleanup && syncPendingDirtyPlates > 0 && !interactor.IsHoldingItem()) return true;
        return currentState == TableState.WaitingToOrder || currentState == TableState.Ordering;
    }

    public override void BeginInteract(PlayerItemInteractor interactor)
    {
        isInteracting = true;
        if (currentState == TableState.WaitingToOrder)
        {
            if (debugLog) Debug.Log($"[OrderResponse] 玩家开始对桌号 {tableId} 点单交互");
            SetState(TableState.Ordering);
        }
        else if (currentState == TableState.WaitingForCleanup)
        {
            // 仅服务端执行（K 经 CmdSetStationInteractState）；J 走 CmdRequestTakeAllDirtyPlatesFromTable
            if (syncPendingDirtyPlates > 0 && !interactor.IsHoldingItem() && NetworkServer.active)
            {
                if (debugLog) Debug.Log($"[OrderResponse] 玩家端起桌号 {tableId} 的所有脏盘子 (数量: {syncPendingDirtyPlates})");
                PlayerNetworkController pnc = interactor.GetComponent<PlayerNetworkController>();
                if (pnc != null)
                    ServerTakeAllDirtyPlatesFor(pnc);
            }
            isInteracting = false;
        }
    }

    public override void EndInteract(PlayerItemInteractor interactor)
    {
        isInteracting = false;
        if (currentState == TableState.Ordering)
        {
            SetState(TableState.WaitingToOrder);
        }

        if (DayCycleManager.Instance != null && DayCycleManager.Instance.Phase == DayCyclePhase.ExtendedBusiness)
            TryForceLeaveForBusinessEnd();
    }

    // ================== 【核心业务逻辑】 ==================
    void CompleteOrdering()
    {
        SetState(TableState.WaitingForFood);
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
            ServerRefreshSyncedOrderList();

            if (GlobalOrderManager.Instance != null)
            {
                var created = GlobalOrderManager.Instance.RegisterOrdersForTable(tableId, currentOrder);
                if (NetworkServer.active && created != null && created.Count > 0)
                {
                    int n = created.Count;
                    var ids = new string[n];
                    var names = new string[n];
                    for (int i = 0; i < n; i++)
                    {
                        ids[i] = created[i].orderId;
                        names[i] = created[i].recipe != null ? created[i].recipe.recipeName : "";
                    }
                    RpcMirrorGlobalOrdersAdded(tableId, ids, names);
                }
            }
            Debug.Log($"[OrderResponse] 桌号 {tableId} 订单生成完毕！");
        }
    }

    /// <summary>纯客户端：仅同步桌面摆盘表现（DishPlaceSystem），不执行金钱/订单/统计。</summary>
    void ClientOnlyMirrorDishPlacement(CarryableItem item)
    {
        if (dishPlaceSystem == null) return;

        if (currentState != TableState.WaitingForFood && currentState != TableState.Eating)
            return;

        FryRecipeDatabase.FryRecipe recipe = ResolveRecipeFromDish(item);
        if (recipe == null)
        {
            Debug.LogWarning($"[OrderResponse] 客户端镜像：无法解析 {item.name} 的配方，跳过 DishPlaceSystem。");
            return;
        }

        bool placed = dishPlaceSystem.AcceptDish(item, recipe.size);
        if (!placed)
        {
            // 客机无 dishesOnTable，无法按服务端逻辑隐藏最老盘；满载时强制塞入以对齐多数情况
            dishPlaceSystem.ForceAcceptDish(item, recipe.size);
        }

        if (itemPlacePoint != null)
            itemPlacePoint.ClearOccupant();

        item.isPickable = false;
    }

    void OnItemPlaced(CarryableItem item)
    {
        if (item == null) return;

        // 【关键防护】：由于生成大脏盘堆并放入中心点时也会触发此事件，
        // 且它不属于“上餐”行为，所以遇到脏盘堆直接放行，切勿执行拦截拒收和订单判定逻辑！
        if (item is DirtyPlateStack) return;

        if (NetworkClient.active && !NetworkServer.active)
        {
            ClientOnlyMirrorDishPlacement(item);
            return;
        }

        // 1. 只有 WaitingForFood 和 Eating 阶段允许上菜
        if (currentState != TableState.WaitingForFood && currentState != TableState.Eating)
        {
            Debug.LogWarning($"[OrderResponse] 桌号 {tableId} 当前状态 {currentState} 不允许上菜，忽略放置！");
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
        bool isCorrectOrder = (idx >= 0);

        if (isCorrectOrder)
        {
            if (GlobalOrderManager.Instance != null)
            {
                if (GlobalOrderManager.Instance.TryFulfillOrder(tableId, recipe.recipeName, out string removedOrderId))
                {
                    if (!string.IsNullOrEmpty(removedOrderId))
                        RpcMirrorGlobalOrderRemoved(removedOrderId);
                }
            }

            DishQualityTag qt = item.GetComponent<DishQualityTag>();
            float qualityMul = qt != null ? qt.PriceMultiplier : 1f;
            int finalPrice = Mathf.RoundToInt(recipe.price * qualityMul);

            if (MoneyManager.Instance != null)
            {
                MoneyManager.Instance.AddMoney(finalPrice);
                BroadcastTableMoneyPopup(finalPrice);

                // 蝴蝶结小费：检查端菜玩家是否有小费加成
                PlayerAttributes serverPlayer = item.lastHolderPlayer;
                if (serverPlayer != null && serverPlayer.accessoryTipRate > 0f)
                {
                    int tip = Mathf.RoundToInt(finalPrice * serverPlayer.accessoryTipRate);
                    if (tip > 0)
                    {
                        MoneyManager.Instance.AddMoney(tip);
                        BroadcastTableMoneyPopup(tip);
                        Debug.Log($"[OrderResponse] 蝴蝶结小费: +{tip} (端菜玩家小费率={serverPlayer.accessoryTipRate})");
                    }
                }
            }
            if (DayStatsTracker.Instance != null)
            {
                DayStatsTracker.Instance.RegisterPlacedItem(true, recipe.size == DishSize.D);
                if (DayCycleManager.Instance != null && DayCycleManager.Instance.ShouldRecordOrderRevenue())
                    DayStatsTracker.Instance.RegisterRevenue(finalPrice);
            }
            currentOrder.RemoveAt(idx);
            ServerRefreshSyncedOrderList();
            string qualityStr = qt != null ? $" [{qt.quality} x{qualityMul}]" : "";
            Debug.Log($"[OrderResponse] 桌号 {tableId} 成功上对菜: {recipe.recipeName}{qualityStr}。售价 {finalPrice}。剩 {currentOrder.Count} 道菜！");
        }
        else
        {
            if (DayStatsTracker.Instance != null)
                DayStatsTracker.Instance.RegisterPlacedItem(false, recipe.size == DishSize.D);
            Debug.LogWarning($"[OrderResponse] 桌号 {tableId} 上错菜: {recipe.recipeName} 不在订单中！白送给客人，不给钱，订单不变。");
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
            SetState(TableState.Eating);
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
        var clientRpcNames = new List<string>();
        var clientRpcPoss = new List<Vector3>();
        var clientRpcRots = new List<Quaternion>();

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
                if (d.recipe != null && NetworkServer.active)
                {
                    clientRpcNames.Add(d.recipe.recipeName);
                    clientRpcPoss.Add(pos);
                    clientRpcRots.Add(rot);
                }
            }
        }

        // --- 破而后立：毫不留情地切断一切旧物引用，秒删 ---
        Debug.Log($"[Debug-Eat] 开始清理 DishPlaceSystem 原物理菜品...");
        if (dishPlaceSystem != null) dishPlaceSystem.ClearAllDishes();
        dishesOnTable.Clear();

        // 2. 将脏盘子计数并直接转为待清理状态
        SetPendingDirtyPlates(dirtyCount);
        if (debugLog) Debug.Log($"[OrderResponse] 客人用餐完毕，桌号 {tableId} 剩余 {dirtyCount} 个脏盘子待端走。");

        if (NetworkServer.active && clientRpcNames.Count > 0)
            ServerNotifyClientEatenLeftovers(clientRpcNames, clientRpcPoss, clientRpcRots);
        
        SetState(TableState.WaitingForCleanup);
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
        if (dirtyCount == 0)
        {
            CleanUpTable();
        }
    }

    public void CleanUpTable()
    {
        SetPendingDirtyPlates(0);

        if (dishPlaceSystem != null) dishPlaceSystem.ClearAllDishes();
        dishesOnTable.Clear(); // 清理记录
        
        foreach (var model in activeEatenModels)
        {
            if (model != null) Destroy(model);
        }
        activeEatenModels.Clear();

        SetState(TableState.Empty);
        
        // 【新增】：客人吃完走人，桌子解开预定锁
        isReserved = false;
        isAbandoningPatience = false;

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
        if (NetworkClient.active && !NetworkServer.active) return;
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
        if (readingMenuCoroutine != null)
        {
            StopCoroutine(readingMenuCoroutine);
            readingMenuCoroutine = null;
        }

        if (GlobalOrderManager.Instance != null)
        {
            GlobalOrderManager.Instance.RemoveAllOrdersForTable(tableId);
            if (NetworkServer.active)
                RpcMirrorGlobalRemoveTableOrders(tableId);
        }

        if (dishesOnTable.Count > 0)
        {
            // 有已经上的菜，转为脏盘子并进入 WaitingForCleanup
            int dirtyCount = 0;
            var clientRpcNames = new List<string>();
            var clientRpcPoss = new List<Vector3>();
            var clientRpcRots = new List<Quaternion>();

            foreach (var d in dishesOnTable)
            {
                if (d.physicalItem == null) continue;
                if (d.recipe != null && d.recipe.size == DishSize.D) continue; // 饮料吸收不留脏杯

                dirtyCount++;
                Vector3 pos = d.physicalItem.transform.position;
                Quaternion rot = d.physicalItem.transform.rotation;

                if (d.recipe != null && d.recipe.eatenPrefab != null)
                {
                    GameObject cleanEatenVisual = Instantiate(d.recipe.eatenPrefab, pos, rot, this.transform);
                    activeEatenModels.Add(cleanEatenVisual);
                    if (NetworkServer.active)
                    {
                        clientRpcNames.Add(d.recipe.recipeName);
                        clientRpcPoss.Add(pos);
                        clientRpcRots.Add(rot);
                    }
                }
            }

            if (dishPlaceSystem != null) dishPlaceSystem.ClearAllDishes();
            dishesOnTable.Clear();
            currentOrder.Clear();
            ServerRefreshSyncedOrderList();

            SetPendingDirtyPlates(dirtyCount);
            if (NetworkServer.active && clientRpcNames.Count > 0)
                ServerNotifyClientEatenLeftovers(clientRpcNames, clientRpcPoss, clientRpcRots);

            SetState(TableState.WaitingForCleanup);

            if (boundGroup != null)
            {
                PatienceLeaveDbg("带着脏盘子离场，调用 CustomerGroup.BeginLeaveGroup()");
                boundGroup.BeginLeaveGroup();
            }

            if (dirtyCount == 0)
            {
                CleanUpTable(); // 如果全是饮料，没有留下真正需要收的盘子，就直接重置
            }
        }
        else
        {
            if (dishPlaceSystem != null)
                dishPlaceSystem.ClearAllDishes();

            foreach (var model in activeEatenModels)
            {
                if (model != null) Destroy(model);
            }
            activeEatenModels.Clear();

            dishesOnTable.Clear();
            currentOrder.Clear();
            ServerRefreshSyncedOrderList();

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

            SetState(TableState.Empty);

            if (boundGroup != null)
            {
                PatienceLeaveDbg("没有留下菜，干干净净离场，调用 CustomerGroup.BeginLeaveGroup()");
                boundGroup.BeginLeaveGroup();
            }
            else
            {
                PatienceLeaveDbg("无 boundGroup，已直接 isReserved=false");
                isReserved = false;
                isAbandoningPatience = false;
            }
        }
    }

    /// <summary>
    /// 天数切换时由 DayCycleManager 调用：保留脏盘子等物理物品，清理残留的顾客相关状态。
    /// </summary>
    public void HandleDayTransition()
    {
        if (NetworkClient.active && !NetworkServer.active) return;

        // WaitingForCleanup：保留脏盘子、残羹模型，只清理顾客相关引用
        if (currentState == TableState.WaitingForCleanup)
        {
            boundGroup = null;
            isReserved = false;
            isAbandoningPatience = false;
            currentOrder.Clear();
            ServerRefreshSyncedOrderList();
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
        {
            GlobalOrderManager.Instance.RemoveAllOrdersForTable(tableId);
            if (NetworkServer.active)
                RpcMirrorGlobalRemoveTableOrders(tableId);
        }

        if (dishPlaceSystem != null)
            dishPlaceSystem.ClearAllDishes();

        foreach (var model in activeEatenModels)
        {
            if (model != null) Destroy(model);
        }
        activeEatenModels.Clear();
        dishesOnTable.Clear();
        currentOrder.Clear();
        ServerRefreshSyncedOrderList();

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
        SetState(TableState.Empty);
        boundGroup = null;
        isReserved = false;
        isAbandoningPatience = false;

        Debug.Log($"[OrderResponse] 桌号 {tableId} 天数切换：非预期状态 → 强制重置为 Empty");
    }

    void ServerRefreshSyncedOrderList()
    {
        if (NetworkServer.active)
        {
            syncedOrderRecipeNames.Clear();
            foreach (var r in currentOrder)
            {
                if (r != null && !string.IsNullOrEmpty(r.recipeName))
                    syncedOrderRecipeNames.Add(r.recipeName);
            }
        }

        // 单机无 NetworkServer 时 SyncList 不更新，仍需驱动桌面订单 UI
        if (!NetworkClient.active && !NetworkServer.active)
            RaiseTableProgressUiSync();
    }

    void RebuildClientOrderViewFromSyncList()
    {
        clientOrderView.Clear();
        foreach (var name in syncedOrderRecipeNames)
        {
            if (string.IsNullOrEmpty(name)) continue;
            var r = recipeDatabase != null ? recipeDatabase.FindByName(name) : null;
            if (r == null && drinkRecipeDatabase != null)
                r = drinkRecipeDatabase.FindByName(name);
            if (r != null)
                clientOrderView.Add(r);
        }
    }

    [ClientRpc]
    void RpcMirrorGlobalOrdersAdded(int tableId, string[] orderIds, string[] recipeNames)
    {
        if (NetworkServer.active) return;
        GlobalOrderManager.Instance?.ClientMirrorRegisterOrders(tableId, orderIds, recipeNames);
    }

    [ClientRpc]
    void RpcMirrorGlobalOrderRemoved(string orderId)
    {
        if (NetworkServer.active) return;
        GlobalOrderManager.Instance?.ClientMirrorFulfillOrder(orderId);
    }

    [ClientRpc]
    void RpcMirrorGlobalRemoveTableOrders(int tableId)
    {
        if (NetworkServer.active) return;
        GlobalOrderManager.Instance?.ClientMirrorRemoveAllOrdersForTable(tableId);
    }

    /// <summary>联机 Host 发 Rpc；纯单机无 NetworkServer 时直接本地飘字。</summary>
    void BroadcastTableMoneyPopup(int amount)
    {
        if (amount <= 0) return;
        if (NetworkServer.active)
            RpcTableMoneyPopup(amount);
        else
            ApplyTableMoneyPopupLocal(amount);
    }

    void ApplyTableMoneyPopupLocal(int amount)
    {
        if (amount <= 0) return;
        TableOrderProgressUI uiComponent = GetComponentInChildren<TableOrderProgressUI>(true);
        if (uiComponent == null && transform.parent != null)
            uiComponent = transform.parent.GetComponentInChildren<TableOrderProgressUI>(true);
        if (uiComponent != null)
            uiComponent.ShowMoneyEarned(amount);
    }

    [ClientRpc]
    void RpcTableMoneyPopup(int amount)
    {
        ApplyTableMoneyPopupLocal(amount);
    }

    public float GetOrderProgressNormalized() => requiredOrderTime > 0f ? Mathf.Clamp01(currentOrderProgress / requiredOrderTime) : 0f;

    /// <summary>等待点菜阶段：始终显示「呼叫点餐」Icon（颜色由 UI 根据耐心值渐变）。</summary>
    public bool ShouldShowWaitingToOrderCallIcon => currentState == TableState.WaitingToOrder;

    /// <summary>等上菜阶段：耐心低于阈值时显示不耐烦 Icon（颜色由 UI 根据耐心值渐变）。</summary>
    public bool ShouldShowWaitingFoodImpatientIcon =>
        currentState == TableState.WaitingForFood && currentPatienceFood < GetDisplayImpatientThreshold();
    public IReadOnlyList<FryRecipeDatabase.FryRecipe> GetCurrentOrder()
    {
        if (!NetworkClient.active && !NetworkServer.active)
            return currentOrder;
        if (isServer)
            return currentOrder;
        RebuildClientOrderViewFromSyncList();
        return clientOrderView;
    }

    /// <summary>桌上「特殊残羹」视觉数量（仅服务端/客机副本列表）。</summary>
    public int GetActiveEatenModelCount()
    {
        if (NetworkClient.active && !NetworkServer.active)
            return clientReplicaEatenModels.Count;
        return activeEatenModels.Count;
    }
}