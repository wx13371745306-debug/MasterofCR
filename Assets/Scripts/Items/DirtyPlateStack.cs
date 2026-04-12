using UnityEngine;
using Mirror;

/// <summary>
/// 脏盘子堆：桌上或地上为数量追踪 + 视觉；联机分发时仅生成带 NetworkIdentity 的整堆 <see cref="DirtyPlateStack"/>，不再生成单盘或 DynamicItemStack。
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class DirtyPlateStack : CarryableItem
{
    [Header("Dirty Plate Stack")]
    [SyncVar(hook = nameof(OnPlateCountChanged))]
    public int plateCount = 1;
    [Tooltip("代表单个脏盘子的预制体（必须有 CarryableItem + StackableProp）")]
    public GameObject singlePlatePrefab;
    [Tooltip("（旧）DynamicItemStack 预制体；分发逻辑已改为 portableHandStackPrefab")]
    public GameObject stackPrefab;
    [Tooltip("从本堆分发到玩家手上时 Instantiate 的网络 DirtyPlateStack 预制体（需 NI）；未填则 ServerDispense 失败")]
    public DirtyPlateStack portableHandStackPrefab;
    [Tooltip("垂直堆叠的Y轴间隔，决定了垒起来的高度间距")]
    public float stackYOffset = 0.05f;
    [Tooltip("存放所有视觉盘子的父节点")]
    public Transform visualRoot;

    [Header("Stack Dispense Settings")]
    [Tooltip("长按一次最多拿走多少个盘子")]
    public int maxDispensePerStack = 5;

    private BoxCollider stackCollider;
    private float baseColliderSizeY = 0.05f;
    private float baseColliderCenterY = 0.025f;

    [SyncVar(hook = nameof(OnForcedHiddenChanged))]
    private bool forcedHidden = false;

    /// <summary>
    /// 生成本堆的餐桌；用于与桌上的「特殊残羹」视觉同步，避免短按后出现通用盘堆叠模型叠在残羹上。
    /// </summary>
    private OrderResponse boundTable;

    // 缓存从单盘预制体上读取的堆叠参数
    private StackLayout dispenseLayout = StackLayout.Vertical;
    private int dispenseGridCols = 1;
    private int dispenseGridRows = 1;
    private float dispenseGridSpacing = 0.15f;

    protected override void Awake()
    {
        base.Awake();
        stackCollider = GetComponent<BoxCollider>();
        if (stackCollider != null)
        {
            baseColliderSizeY = stackCollider.size.y;
            baseColliderCenterY = stackCollider.center.y;
        }

        if (visualRoot == null)
        {
            visualRoot = new GameObject("VisualRoot").transform;
            visualRoot.SetParent(this.transform);
            visualRoot.localPosition = Vector3.zero;
            visualRoot.localRotation = Quaternion.identity;
        }

        categories |= ItemCategory.DirtyPlate;

        // 禁止直接拾取整个 DirtyPlateStack（必须通过 Dispense 接口拿取内容物）
        isPickable = false;

        ReadStackParamsFromPrefab();
    }

    protected override void Start()
    {
        base.Start();
        UpdateVisuals();

        if (forcedHidden && visualRoot != null)
        {
            visualRoot.gameObject.SetActive(false);
        }
    }

    void OnPlateCountChanged(int oldVal, int newVal)
    {
        UpdateVisuals();
    }

    void OnForcedHiddenChanged(bool oldVal, bool newVal)
    {
        if (newVal && visualRoot != null)
        {
            visualRoot.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 从单盘预制体上的 StackableProp 读取堆叠参数。
    /// </summary>
    void ReadStackParamsFromPrefab()
    {
        if (singlePlatePrefab == null) return;
        StackableProp sp = singlePlatePrefab.GetComponent<StackableProp>();
        if (sp == null) return;

        if (stackPrefab == null) stackPrefab = sp.dynamicStackPrefab;
        dispenseLayout = sp.layoutType;
        dispenseGridCols = sp.gridColumns;
        dispenseGridRows = sp.gridRows;
        dispenseGridSpacing = sp.gridSpacing;

        if (dispenseLayout == StackLayout.Grid)
            maxDispensePerStack = sp.gridColumns * sp.gridRows;
        else
            maxDispensePerStack = sp.maxStackCount;

        if (debugLog) Debug.Log($"<color=#8B4513>[DirtyPlateStack]</color> 从 {singlePlatePrefab.name} 读取堆叠参数: max={maxDispensePerStack}, layout={dispenseLayout}");
    }

    public void SetPlateCount(int count)
    {
        if (NetworkServer.active)
        {
            plateCount = Mathf.Max(1, count);
        }
    }

    // 由餐桌在吃完饭时调用，施加终极隐身锁
    public void ForceHideUntilPickedUp()
    {
        if (NetworkServer.active)
        {
            forcedHidden = true;
        }
    }

    public void BindTable(OrderResponse table)
    {
        boundTable = table;
    }

    /// <summary>
    /// 仅服务端：生成分发到玩家手上的网络整堆（短按/长按一致）；数量受 maxDispensePerStack 限制。
    /// </summary>
    [Server]
    public GameObject ServerDispenseItem(bool isLongPress)
    {
        _ = isLongPress; // 短按/长按行为一致，保留参数以兼容 CmdRequestDispenseBox
        if (plateCount <= 0) return null;

        DirtyPlateStack prefabSrc = portableHandStackPrefab;
        if (prefabSrc == null)
        {
            if (debugLog) Debug.LogError("[DirtyPlateStack] portableHandStackPrefab 未设置，无法分发整堆。");
            return null;
        }

        if (singlePlatePrefab == null) return null;

        if (forcedHidden)
            forcedHidden = false;

        int takeCount = Mathf.Min(maxDispensePerStack, plateCount);
        GameObject go = Instantiate(prefabSrc.gameObject, new Vector3(-9999f, -9999f, -9999f), Quaternion.identity);
        DirtyPlateStack spawned = go.GetComponent<DirtyPlateStack>();
        if (spawned == null)
        {
            Destroy(go);
            return null;
        }

        spawned.plateCount = Mathf.Max(1, takeCount);

        plateCount -= takeCount;
        if (boundTable != null)
            boundTable.OnDirtyPlatesDispensed(takeCount);

        NetworkServer.Spawn(go);

        if (debugLog) Debug.Log($"<color=#8B4513>[DirtyPlateStack-Server]</color> 分发网络整堆 x{takeCount}，地上剩余: {plateCount}");

        AfterDispense();
        return go;
    }

    /// <summary>
    /// 每次分发后由 Server 调用：销毁自身。
    /// （Visuals由于SyncVar hook，会在全网自动更新）
    /// </summary>
    [Server]
    void AfterDispense()
    {
        if (plateCount <= 0)
        {
            // 从放置点上解绑
            if (CurrentPlacePoint != null)
            {
                CurrentPlacePoint.ClearOccupant();
            }
            ClearPlaceState();

            if (debugLog) Debug.Log($"<color=#8B4513>[DirtyPlateStack-Server]</color> 所有脏盘子都被拿走了，销毁脏盘堆。");
            NetworkServer.Destroy(gameObject);
        }
        else
        {
            // SyncVar hook 自动更视觉，无需显式 UpdateVisuals
            UpdateVisuals();
        }
    }

    private void UpdateVisuals()
    {
        if (visualRoot == null) return;

        // 清理旧渲染
        foreach (Transform child in visualRoot)
        {
            Destroy(child.gameObject);
        }

        // 绑定了餐桌且仍有「特殊残羹」视觉时：不生成通用盘堆叠模型（残羹由 OrderResponse.activeEatenModels 负责）
        bool useGenericStackVisual =
            singlePlatePrefab != null &&
            (boundTable == null || boundTable.GetActiveEatenModelCount() == 0);

        if (useGenericStackVisual && !forcedHidden)
        {
            visualRoot.gameObject.SetActive(true);
            for (int i = 0; i < plateCount; i++)
            {
                GameObject plate = Instantiate(singlePlatePrefab, visualRoot);
                plate.transform.localPosition = new Vector3(0, i * stackYOffset, 0);
                plate.transform.localRotation = Quaternion.identity;

                StackableProp sp = plate.GetComponent<StackableProp>();
                if (sp != null) Destroy(sp);
                CarryableItem ci = plate.GetComponent<CarryableItem>();
                if (ci != null) Destroy(ci);
                Rigidbody rb = plate.GetComponent<Rigidbody>();
                if (rb != null) Destroy(rb);
                Collider[] cols = plate.GetComponentsInChildren<Collider>();
                foreach (var c in cols) Destroy(c);
            }
        }
        else
        {
            visualRoot.gameObject.SetActive(false);
        }

        // 刷新碰撞体
        if (stackCollider != null)
        {
            float newSizeY = baseColliderSizeY + (plateCount - 1) * stackYOffset;
            float newCenterY = baseColliderCenterY + (plateCount - 1) * stackYOffset * 0.5f;

            stackCollider.size = new Vector3(stackCollider.size.x, newSizeY, stackCollider.size.z);
            stackCollider.center = new Vector3(stackCollider.center.x, newCenterY, stackCollider.center.z);
        }
    }
}
