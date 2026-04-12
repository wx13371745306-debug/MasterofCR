using UnityEngine;
using Mirror;

/// <summary>
/// 洗碗池：脏盘入池按进度洗净；干净盘出口为库存计数 + Visual Root 纯视觉叠放。
/// 玩家从出口取盘请使用 <see cref="CleanPlateDispenserStation"/>（按 J 向服务端请求生成网络盘子），勿依赖出口常驻实体。
/// 放回干净盘仍使用 outputPlacePoint。预制体：outputPlacePoint、plateVisualRoot、cleanPlatePrefab（仅用于发放时生成）。
/// </summary>
public class DishWashingStation : BaseStation
{
    [Header("Sink Settings")]
    [Tooltip("脏盘子的放置点，注意把它的 AllowedCategories 改为 DirtyPlate")]
    public ItemPlacePoint inputDropPoint;

    [Tooltip("干净盘子放置/拾取点（通常为 CleanPlateOutputArea/PlateSpawnPoint）")]
    public ItemPlacePoint outputPlacePoint;

    [Tooltip("可拾取的干净盘子网络预制体（需 CarryableItem + NetworkIdentity）")]
    public GameObject cleanPlatePrefab;

    [Tooltip("游戏开始时出口干净盘库存数量")]
    public int initialCleanPlates = 5;

    [Tooltip("出口干净盘最大数量（达到后洗碗暂停往出口增加）")]
    public int maxCleanPlatesAtOutput = 20;

    [Header("Clean Plate Visual Stack")]
    [Tooltip("叠放视觉父节点（通常为 CleanPlateOutputArea/Visual Root）")]
    public Transform plateVisualRoot;

    [Tooltip("除顶层可拾取盘外，其余层使用的纯视觉预制体（无网络组件）；可留空则仅显示顶层实体")]
    public GameObject cleanPlateVisualPrefab;

    [Tooltip("叠放每层垂直间距（与脏盘 dirtyStackYOffset 含义类似）")]
    public float cleanPlateVerticalOffset = 0.05f;

    [Tooltip("若 outputPlacePoint.attachPoint 为 stackAnchor 子物体，则随库存高度调整其 localPosition.y")]
    public bool moveAttachWithStack = true;

    [Tooltip("叠放锚点（堆底）。默认可不填，将使用 plateVisualRoot")]
    public Transform stackAnchor;

    [Header("Washing Process")]
    [Tooltip("洗碗的基础速度，暂设为1")]
    public float baseProcessingSpeed = 1f;
    [Tooltip("每洗净一个盘子需要的进度量，原为需要的总时间")]
    public float requiredWashTimePerPlate = 2f;

    [SyncVar(hook = nameof(OnSyncDirtyPlatesInSinkChanged))]
    private int syncDirtyPlatesInSink;

    /// <summary>池内脏盘数量（联机由 SyncVar 同步，供 UI/调试读取）。</summary>
    public int dirtyPlatesInSink => syncDirtyPlatesInSink;

    // 【公开字段】供 UI 显示当前洗碗进度
    public float currentWashProgress = 0f;

    /// <summary>当前出口干净盘库存（与 SyncVar 同步）。</summary>
    public int CleanPlateStock => syncCleanPlateCount;

    [SyncVar(hook = nameof(OnSyncCleanPlateCountChanged))]
    private int syncCleanPlateCount;

    /// <summary>服务端自动逻辑时跳过 OnCleanPlateReturned 吸收。</summary>
    private bool suppressOutputPlacedAbsorb;

    private bool isWashing = false;

    /// <summary>客机 UI 用：当前这一盘洗碗进度 0~1（与 ChoppingStation 的 syncProcessProgress 同理）。</summary>
    [SyncVar]
    private float syncWashProgress01;

    /// <summary>客机 UI 用：是否处于长按 K 洗碗中（服务端权威）。</summary>
    [SyncVar]
    private bool syncWashingActive;

    [Header("Visual Inside Sink")]
    public GameObject singleDirtyPlateVisual;
    public Transform dirtyVisualRoot;
    public float dirtyStackYOffset = 0.05f;
    private System.Collections.Generic.List<GameObject> dirtyVisualPlates = new System.Collections.Generic.List<GameObject>();

    public override void OnStartServer()
    {
        base.OnStartServer();
        ApplyCleanPlateCountOnServer(Mathf.Clamp(initialCleanPlates, 0, maxCleanPlatesAtOutput));
    }

    protected override void Awake()
    {
        base.Awake();
        if (inputDropPoint != null)
            inputDropPoint.OnItemPlacedEvent += OnDirtyPlateDropped;

        if (outputPlacePoint != null)
            outputPlacePoint.OnItemPlacedEvent += OnCleanPlateReturned;
    }

    private void OnDestroy()
    {
        if (inputDropPoint != null)
            inputDropPoint.OnItemPlacedEvent -= OnDirtyPlateDropped;

        if (outputPlacePoint != null)
            outputPlacePoint.OnItemPlacedEvent -= OnCleanPlateReturned;
    }

    private void OnSyncCleanPlateCountChanged(int oldVal, int newVal)
    {
        RebuildCleanPlateVisualsLocal();
        AlignOutputAttachPoint(newVal);
    }

    void OnSyncDirtyPlatesInSinkChanged(int oldVal, int newVal)
    {
        UpdateDirtyVisuals();
    }

    /// <summary>仅在服务端修改库存并同步（不再在出口生成常驻可拾取实体）。</summary>
    private void ApplyCleanPlateCountOnServer(int newCount)
    {
        if (!NetworkServer.active) return;

        newCount = Mathf.Clamp(newCount, 0, maxCleanPlatesAtOutput);
        syncCleanPlateCount = newCount;
    }

    /// <summary>服务端：从库存取 1 个干净盘网络物体（虚空生成，与 SupplyBox 发蛋类似）。</summary>
    public GameObject ServerTryDispenseCleanPlate()
    {
        if (!NetworkServer.active) return null;
        if (syncCleanPlateCount <= 0 || cleanPlatePrefab == null) return null;

        ApplyCleanPlateCountOnServer(syncCleanPlateCount - 1);

        GameObject obj = Instantiate(cleanPlatePrefab, new Vector3(-9999f, -9999f, -9999f), Quaternion.identity);
        NetworkServer.Spawn(obj);
        return obj;
    }

    private void RebuildCleanPlateVisualsLocal()
    {
        if (plateVisualRoot == null) return;

        for (int i = plateVisualRoot.childCount - 1; i >= 0; i--)
            Destroy(plateVisualRoot.GetChild(i).gameObject);

        int count = syncCleanPlateCount;
        if (count <= 0 || cleanPlateVisualPrefab == null) return;

        for (int i = 0; i < count; i++)
        {
            GameObject go = Instantiate(cleanPlateVisualPrefab, plateVisualRoot);
            go.transform.localPosition = new Vector3(0, i * cleanPlateVerticalOffset, 0);
            go.transform.localRotation = Quaternion.identity;
        }
    }

    private Transform ResolveStackAnchor()
    {
        if (stackAnchor != null) return stackAnchor;
        return plateVisualRoot;
    }

    private void AlignOutputAttachPoint(int count)
    {
        if (!moveAttachWithStack || outputPlacePoint == null || outputPlacePoint.attachPoint == null)
            return;

        Transform anchor = ResolveStackAnchor();
        if (anchor == null) return;

        Transform ap = outputPlacePoint.attachPoint;
        if (!ap.IsChildOf(anchor))
            return;

        float y = Mathf.Max(0, count - 1) * cleanPlateVerticalOffset;
        Vector3 lp = ap.localPosition;
        ap.localPosition = new Vector3(lp.x, y, lp.z);
    }

    /// <summary>玩家放回干净盘：销毁实体并增加库存（仅服务端）。</summary>
    private void OnCleanPlateReturned(CarryableItem item)
    {
        if (item == null || !NetworkServer.active) return;
        if (suppressOutputPlacedAbsorb) return;
        // 以类别为准（预制体 categories 损坏时 HasAnyCategory 也会失败，需在预制体勾选 CleanPlate）
        if (!item.HasAnyCategory(ItemCategory.CleanPlate))
            return;

        if (IsOutputFull())
            return;

        outputPlacePoint.ClearOccupant(silent: true);
        NetworkServer.Destroy(item.gameObject);
        ApplyCleanPlateCountOnServer(syncCleanPlateCount + 1);
    }

    /// <summary>获取实际洗碗速度（含羁绊加成和属性修正）。</summary>
    public float GetEffectiveWashingSpeed()
    {
        float speed = baseProcessingSpeed;

        float playerMulti = 1.0f;
        if (CurrentPlayerAttributes != null)
            playerMulti = CurrentPlayerAttributes.washSpeedMultiplier;

        float globalAddon = 0f;
        if (GlobalOrderManager.Instance != null)
            globalAddon = GlobalOrderManager.Instance.globalWashSpeedAddon;

        float finalSpeed = (speed * playerMulti) + globalAddon;
        return Mathf.Max(0.01f, finalSpeed);
    }

    private void Update()
    {
        if (!NetworkServer.active) return;

        bool isOutputFull = IsOutputFull();

        if (isWashing && isOutputFull)
        {
            isWashing = false;
            currentWashProgress = 0f;
            if (debugLog) Debug.LogWarning("<color=#00FFFF>[DishWashingStation]</color> 出口干净盘已满！已强制停止清洗！");
        }

        if (isWashing && syncDirtyPlatesInSink > 0 && !isOutputFull)
        {
            currentWashProgress += GetEffectiveWashingSpeed() * Time.deltaTime;

            if (currentWashProgress >= requiredWashTimePerPlate)
            {
                currentWashProgress = 0f;
                syncDirtyPlatesInSink--;

                ApplyCleanPlateCountOnServer(syncCleanPlateCount + 1);

                if (debugLog) Debug.Log($"<color=#00FFFF>[DishWashingStation]</color> 成功洗完 1 个盘子！目前水池里还剩: {syncDirtyPlatesInSink} 个。");

                if (syncDirtyPlatesInSink <= 0 || IsOutputFull())
                {
                    isWashing = false;
                    currentWashProgress = 0f;
                }
            }
        }

        PushWashProgressToSyncVars();
    }

    /// <summary>仅服务端：把当前洗碗进度同步给客机 UI。</summary>
    void PushWashProgressToSyncVars()
    {
        if (!NetworkServer.active) return;

        bool washing = isWashing && syncDirtyPlatesInSink > 0 && !IsOutputFull();
        syncWashingActive = washing;

        if (washing && requiredWashTimePerPlate > 0f)
            syncWashProgress01 = Mathf.Clamp01(currentWashProgress / requiredWashTimePerPlate);
        else
            syncWashProgress01 = 0f;
    }

    private void OnDirtyPlateDropped(CarryableItem item)
    {
        if (item == null || !NetworkServer.active) return;

        DirtyPlateStack dirtyStack = item as DirtyPlateStack;
        DynamicItemStack dynamicStack = item as DynamicItemStack;

        if (dirtyStack != null)
        {
            syncDirtyPlatesInSink += dirtyStack.plateCount;
            inputDropPoint.ClearOccupant();
            NetworkServer.Destroy(dirtyStack.gameObject);
        }
        else if (dynamicStack != null && item.HasAnyCategory(ItemCategory.DirtyPlate))
        {
            int stackCount = dynamicStack.Count;
            syncDirtyPlatesInSink += Mathf.Max(1, stackCount);
            inputDropPoint.ClearOccupant();

            foreach (var stackedItem in dynamicStack.GetItems())
            {
                if (stackedItem != null)
                    NetworkServer.Destroy(stackedItem.gameObject);
            }
            NetworkServer.Destroy(dynamicStack.gameObject);
        }
        else if (item.HasAnyCategory(ItemCategory.DirtyPlate))
        {
            syncDirtyPlatesInSink += 1;
            inputDropPoint.ClearOccupant();
            NetworkServer.Destroy(item.gameObject);
        }

        if (debugLog) Debug.Log($"<color=#00FFFF>[DishWashingStation]</color> 接收了脏盘子，当前总数: {syncDirtyPlatesInSink}");
    }

    /// <summary>检查出口干净盘是否已达上限。</summary>
    public bool IsOutputFull()
    {
        return syncCleanPlateCount >= maxCleanPlatesAtOutput;
    }

    private void UpdateDirtyVisuals()
    {
        if (singleDirtyPlateVisual == null || dirtyVisualRoot == null) return;

        foreach (var p in dirtyVisualPlates)
        {
            if (p != null) Destroy(p);
        }
        dirtyVisualPlates.Clear();

        for (int i = 0; i < syncDirtyPlatesInSink; i++)
        {
            GameObject plate = Instantiate(singleDirtyPlateVisual, dirtyVisualRoot);
            plate.transform.localPosition = new Vector3(0, i * dirtyStackYOffset, 0);
            plate.transform.localRotation = Quaternion.identity;
            dirtyVisualPlates.Add(plate);
        }
    }

    public override bool CanInteract(PlayerItemInteractor interactor)
    {
        return syncDirtyPlatesInSink > 0 && !IsOutputFull();
    }

    public override void BeginInteract(PlayerItemInteractor interactor)
    {
        if (!CanInteract(interactor))
        {
            if (debugLog)
                Debug.Log($"<color=#FF6666>[DishWashingStation]</color> BeginInteract 被拒绝（服务端）: dirtyInSink={syncDirtyPlatesInSink} outputFull={IsOutputFull()} | 若客户端曾高亮，多为显示与权威不同步或池内实际为 0。");
            return;
        }

        cachedInteractor = interactor;
        isWashing = true;
        PushWashProgressToSyncVars();
        if (debugLog)
            Debug.Log("<color=#00FFFF>[DishWashingStation]</color> 玩家开始洗碗！长按 K 保持…");
    }

    public override void EndInteract(PlayerItemInteractor interactor)
    {
        isWashing = false;
        PushWashProgressToSyncVars();
        if (debugLog) Debug.Log("<color=#00FFFF>[DishWashingStation]</color> 玩家松开了洗碗按键。");
    }

    public float GetWashProgressNormalized()
    {
        if (requiredWashTimePerPlate <= 0f) return 0f;
        // 服务端（含 Host）用本地累计值；纯客机用 SyncVar，否则进度条不涨
        if (NetworkServer.active)
            return Mathf.Clamp01(currentWashProgress / requiredWashTimePerPlate);
        return Mathf.Clamp01(syncWashProgress01);
    }

    /// <summary>纯客机：是否正在洗碗（用于 UI 可选逻辑）。</summary>
    public bool IsWashingSynced => syncWashingActive;
}
