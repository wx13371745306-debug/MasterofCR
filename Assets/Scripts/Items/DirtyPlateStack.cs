using UnityEngine;

/// <summary>
/// 餐桌吃完饭后生成的脏盘子堆（纯视觉 + 数量追踪）。
/// 玩家对着它：
///   - 点按 J：拿走一个脏盘子
///   - 长按 J：拿走一堆脏盘子（DynamicItemStack）
/// 当所有盘子被拿完后，自动销毁自身。
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class DirtyPlateStack : CarryableItem
{
    [Header("Dirty Plate Stack")]
    public int plateCount = 1;
    [Tooltip("代表单个脏盘子的预制体（必须有 CarryableItem + StackableProp）")]
    public GameObject singlePlatePrefab;
    [Tooltip("通用的 DynamicItemStack 预制体")]
    public GameObject stackPrefab;
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
        plateCount = Mathf.Max(1, count);
        UpdateVisuals();
    }

    // 由餐桌在吃完饭时调用，施加终极隐身锁
    public void ForceHideUntilPickedUp()
    {
        forcedHidden = true;
        if (visualRoot != null) visualRoot.gameObject.SetActive(false);
    }

    public void BindTable(OrderResponse table)
    {
        boundTable = table;
    }

    /// <summary>
    /// 玩家对着脏盘堆按 J 时由 PlayerItemInteractor 调用。
    /// isLongPress = false: 拿一个脏盘子
    /// isLongPress = true:  拿一堆脏盘子 (DynamicItemStack)
    /// </summary>
    public bool TryDispenseToPlayer(PlayerItemInteractor interactor, bool isLongPress)
    {
        if (plateCount <= 0 || interactor.IsHoldingItem()) return false;
        if (singlePlatePrefab == null) return false;

        // 解除隐身锁；若桌上仍用「特殊残羹」展示，则不要打开通用 visualRoot，避免叠出第二套盘子模型
        if (forcedHidden)
        {
            forcedHidden = false;
        }

        if (isLongPress && stackPrefab != null && plateCount >= 2)
        {
            return DispenseStack(interactor);
        }
        else
        {
            return DispenseSingle(interactor);
        }
    }

    bool DispenseSingle(PlayerItemInteractor interactor)
    {
        // 在远离场景的地方实例化，避免物理碰撞
        GameObject obj = Instantiate(singlePlatePrefab, Vector3.one * -9999f, Quaternion.identity);
        CarryableItem item = obj.GetComponent<CarryableItem>();
        if (item == null)
        {
            Destroy(obj);
            return false;
        }

        item.BeginHold(interactor.GetHoldPoint());
        interactor.ReplaceHeldItem(item);
        plateCount--;
        boundTable?.OnDirtyPlatesDispensed(1);

        if (debugLog) Debug.Log($"<color=#8B4513>[DirtyPlateStack]</color> 拿取了一个脏盘子，剩余: {plateCount}");

        AfterDispense();
        return true;
    }

    bool DispenseStack(PlayerItemInteractor interactor)
    {
        int dispenseCount = Mathf.Min(maxDispensePerStack, plateCount);

        if (dispenseCount <= 1)
            return DispenseSingle(interactor);

        GameObject stackObj = Instantiate(stackPrefab, Vector3.one * -9999f, Quaternion.identity);
        DynamicItemStack stack = stackObj.GetComponent<DynamicItemStack>();
        if (stack == null)
        {
            Debug.LogError("[DirtyPlateStack] Stack 预制体上没有 DynamicItemStack 组件!");
            Destroy(stackObj);
            return false;
        }

        stack.InitializeFromPrefab(singlePlatePrefab, dispenseCount,
            maxDispensePerStack, dispenseLayout, stackYOffset,
            dispenseGridCols, dispenseGridRows, dispenseGridSpacing);

        stack.BeginHold(interactor.GetHoldPoint());
        interactor.ReplaceHeldItem(stack);
        plateCount -= dispenseCount;
        boundTable?.OnDirtyPlatesDispensed(dispenseCount);

        if (debugLog) Debug.Log($"<color=#8B4513>[DirtyPlateStack]</color> 拿取了一个脏盘Stack({dispenseCount}个)，剩余: {plateCount}");

        AfterDispense();
        return true;
    }

    /// <summary>
    /// 每次分发后调用：更新视觉。如果盘子全拿完了，销毁自身。
    /// </summary>
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

            if (debugLog) Debug.Log($"<color=#8B4513>[DirtyPlateStack]</color> 所有脏盘子都被拿走了，销毁脏盘堆。");
            Destroy(gameObject);
        }
        else
        {
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

        if (useGenericStackVisual)
        {
            visualRoot.gameObject.SetActive(true);
            for (int i = 0; i < plateCount; i++)
            {
                GameObject plate = Instantiate(singlePlatePrefab, visualRoot);
                plate.transform.localPosition = new Vector3(0, i * stackYOffset, 0);
                plate.transform.localRotation = Quaternion.identity;

                CarryableItem ci = plate.GetComponent<CarryableItem>();
                if (ci != null) Destroy(ci);
                StackableProp sp = plate.GetComponent<StackableProp>();
                if (sp != null) Destroy(sp);
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
