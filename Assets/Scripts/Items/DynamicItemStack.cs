using System.Collections.Generic;
using UnityEngine;

public enum StackLayout
{
    Vertical,
    Grid // 可以以后扩展，先支持简单的
}

/// <summary>
/// 【动态堆】运行时创建的通用堆叠容器，继承自 CarryableItem。
/// 不再动态创建 ItemPlacePoint 子物体。
/// 往 Stack 里添加物品由 PlayerItemInteractor 直接调用 PushItem() 完成。
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class DynamicItemStack : CarryableItem
{
    [Header("Dynamic Stack Settings")]
    public int maxCapacity = 5;
    public StackLayout layout = StackLayout.Vertical;
    public float stackOffset = 0.05f;

    [Header("Grid Layout")]
    public int gridCols = 2;
    public int gridRows = 2;
    public float gridSpacing = 0.15f;

    [Tooltip("存放所有实际物品的游戏体")]
    public Transform visualRoot;

    // 内部维护的列表
    private List<CarryableItem> stackedItems = new List<CarryableItem>();
    private BoxCollider stackCollider;

    // 基础碰撞体高度/中心，用于垒高时调整大小
    private float baseColliderSizeY = 0.05f;
    private float baseColliderCenterY = 0.025f;

    public int Count => stackedItems.Count;
    public bool IsFull => stackedItems.Count >= maxCapacity;

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
    }

    /// <summary>
    /// 当玩家从无到有首次创建这个 Stack 时，调用此方法进行初始化。
    /// 把初始的两个物体装进来，设置容量限制等。
    /// </summary>
    public void Initialize(CarryableItem itemA, CarryableItem itemB, int maxCap, StackLayout layoutType, float offset, int cols = 1, int rows = 1, float spacing = 0.15f)
    {
        this.categories = itemA.categories;
        this.maxCapacity = maxCap;
        this.layout = layoutType;
        this.stackOffset = offset;
        this.gridCols = cols;
        this.gridRows = rows;
        this.gridSpacing = spacing;
        this.isUsable = false;

        PushItemSilently(itemA);
        PushItemSilently(itemB);
        UpdateVisuals();

        if (debugLog) Debug.Log($"<color=#00FFFF>[DynamicItemStack]</color> 成功创建了一个新的 Stack '{name}'，当前容量：{stackedItems.Count}/{maxCapacity}，类别：{categories}");
    }

    /// <summary>
    /// 【SupplyBox 专用】从预制体批量生成 N 个物品直接塞入 Stack。
    /// 物品从不出现在物理世界中，直接进入 Stack 内部，避免碰撞弹飞。
    /// </summary>
    public void InitializeFromPrefab(GameObject itemPrefab, int count, int maxCap, StackLayout layoutType, float offset, int cols = 1, int rows = 1, float spacing = 0.15f)
    {
        if (itemPrefab == null) return;

        // 先从预制体读取类别信息
        CarryableItem templateItem = itemPrefab.GetComponent<CarryableItem>();
        if (templateItem != null) this.categories = templateItem.categories;

        this.maxCapacity = maxCap;
        this.layout = layoutType;
        this.stackOffset = offset;
        this.gridCols = cols;
        this.gridRows = rows;
        this.gridSpacing = spacing;
        this.isUsable = false;

        for (int i = 0; i < count; i++)
        {
            // 在一个远离场景的位置实例化，避免瞬间的物理碰撞
            GameObject obj = Instantiate(itemPrefab, Vector3.one * -9999f, Quaternion.identity);
            CarryableItem item = obj.GetComponent<CarryableItem>();
            if (item != null)
            {
                PushItemSilently(item);
            }
            else
            {
                Destroy(obj);
            }
        }

        UpdateVisuals();

        if (debugLog) Debug.Log($"<color=#00FFFF>[DynamicItemStack]</color> 从预制体批量创建了 Stack '{name}'，数量：{stackedItems.Count}/{maxCapacity}，类别：{categories}");
    }

    /// <summary>
    /// 外部接口：尝试向堆中推入一个物品。
    /// 由 PlayerItemInteractor 在 TryEndHold 中调用。
    /// 传入 sensor 以便将被吸收的物品从检测列表中注销（防止幽灵残留）。
    /// 返回 true 表示成功吸收。
    /// </summary>
    public bool PushItem(CarryableItem item, PlayerInteractionSensor sensor = null)
    {
        if (item == null) return false;

        if (stackedItems.Count >= maxCapacity)
        {
            if (debugLog) Debug.LogWarning($"<color=#FF9900>[DynamicItemStack]</color> {name} 容量已满 ({stackedItems.Count}/{maxCapacity})，无法再堆叠 {item.name}！");
            return false;
        }

        if (!item.HasAnyCategory(categories))
        {
            if (debugLog) Debug.LogWarning($"<color=#FF0000>[DynamicItemStack]</color> 类别不匹配！Stack 是 {categories}，物品是 {item.categories}");
            return false;
        }

        if (debugLog) Debug.Log($"<color=#00FFFF>[DynamicItemStack]</color> 吸收了新的物体: {item.name}，当前数量: {stackedItems.Count + 1}");

        PushItemSilently(item);
        UpdateVisuals();

        return true;
    }

    /// <summary>
    /// 检查一个物品能否被推入此堆。
    /// </summary>
    public bool CanAccept(CarryableItem item)
    {
        if (item == null) return false;
        if (stackedItems.Count >= maxCapacity) return false;
        if (!item.HasAnyCategory(categories)) return false;
        return true;
    }

    private void PushItemSilently(CarryableItem item)
    {
        if (item == null) return;

        if (item.CurrentPlacePoint != null)
            item.CurrentPlacePoint.ClearOccupant();
        item.ClearPlaceState();

        item.transform.SetParent(visualRoot, false);
        
        // 【修复抖动】彻底禁用 Rigidbody，而不是仅设置 isKinematic
        if (item.rb != null)
        {
            item.rb.isKinematic = true;
            item.rb.useGravity = false;
            item.rb.linearVelocity = Vector3.zero;
            item.rb.angularVelocity = Vector3.zero;
            item.rb.detectCollisions = false; // 彻底禁用碰撞检测
        }

        // 关掉它的碰撞体
        if (item.itemColliders != null)
        {
            foreach (var c in item.itemColliders)
            {
                if (c != null) c.enabled = false;
            }
        }

        // 禁用 StackableProp，防止堆内物品仍被当作独立堆叠目标
        StackableProp stackable = item.GetComponent<StackableProp>();
        if (stackable != null) stackable.enabled = false;

        stackedItems.Add(item);
    }

    /// <summary>
    /// 玩家单按 J 键从中拿走一个。
    /// 返回吐出来的那个物体；如果里面只剩1个了，就会返回仅剩的那个，然后 Stack 就地销毁。
    /// </summary>
    public CarryableItem PopItem()
    {
        if (stackedItems.Count == 0) return null;

        // 取出最后一个
        int lastIndex = stackedItems.Count - 1;
        CarryableItem popped = stackedItems[lastIndex];
        stackedItems.RemoveAt(lastIndex);

        if (debugLog) Debug.Log($"<color=#00FFFF>[DynamicItemStack]</color> 从堆中抽出了一个: {popped.name}，剩余数量: {stackedItems.Count}");

        // 恢复被抽出的物体的碰撞体状态
        RestoreItemPhysics(popped);

        // 把被抽出来的物体的父节点解除，放归自由
        popped.transform.SetParent(null);

        // 如果抽完后只剩 1 个了，说明这个堆没必要存在了，把最后那个也释放出来
        if (stackedItems.Count == 1)
        {
            CarryableItem lastOne = stackedItems[0];
            stackedItems.Clear();

            RestoreItemPhysics(lastOne);
            lastOne.transform.SetParent(null);
            
            // 先尝试把最后那个塞到这堆刚才呆着的 PlacePoint 里
            ItemPlacePoint pointToInherit = CurrentPlacePoint;
            
            if (pointToInherit != null)
            {
                ClearPlaceState(); // 当前 Stack 让出这个点
                lastOne.ForcePlaceAtStart(pointToInherit);
            }
            else
            {
                lastOne.DropToGround();
            }

            if (debugLog) Debug.Log($"<color=#FF00FF>[DynamicItemStack]</color> 堆破裂，释放最后1个物体: {lastOne.name}，Stack 将被销毁。");

            // 销毁自己
            Destroy(this.gameObject);
        }
        else
        {
            // 如果还有多个，就更新排布
            UpdateVisuals();
        }

        return popped;
    }

    private void RestoreItemPhysics(CarryableItem item)
    {
        if (item == null) return;
        // 【修复抖动】重新启用碰撞检测
        if (item.rb != null)
        {
            item.rb.detectCollisions = true;
        }
        if (item.itemColliders != null)
        {
            foreach (var c in item.itemColliders)
            {
                if (c != null) c.enabled = true;
            }
        }

        StackableProp stackable = item.GetComponent<StackableProp>();
        if (stackable != null) stackable.enabled = true;
    }

    private void UpdateVisuals()
    {
        if (visualRoot == null) return;

        for (int i = 0; i < stackedItems.Count; i++)
        {
            CarryableItem item = stackedItems[i];
            
            if (layout == StackLayout.Vertical)
            {
                item.transform.localPosition = new Vector3(0, i * stackOffset, 0);
            }
            else if (layout == StackLayout.Grid)
            {
                // 计算当前物品在方阵中的 行号 和 列号
                int col = i % gridCols;
                int row = i / gridCols;

                // 居中偏移：让整个方阵以原点为中心
                float totalWidth = (gridCols - 1) * gridSpacing;
                float totalDepth = (gridRows - 1) * gridSpacing;
                float offsetX = -totalWidth * 0.5f + col * gridSpacing;
                float offsetZ = -totalDepth * 0.5f + row * gridSpacing;

                item.transform.localPosition = new Vector3(offsetX, 0, offsetZ);
            }
            
            item.transform.localRotation = Quaternion.identity;
        }

        // 动态调整整堆的碰撞盒子大小
        if (stackCollider != null)
        {
            if (layout == StackLayout.Vertical)
            {
                float newSizeY = baseColliderSizeY + (stackedItems.Count - 1) * stackOffset;
                float newCenterY = baseColliderCenterY + (stackedItems.Count - 1) * stackOffset * 0.5f;
                stackCollider.size = new Vector3(stackCollider.size.x, newSizeY, stackCollider.size.z);
                stackCollider.center = new Vector3(stackCollider.center.x, newCenterY, stackCollider.center.z);
            }
            else if (layout == StackLayout.Grid)
            {
                float totalWidth = (gridCols - 1) * gridSpacing + 0.2f;  // 加一点边距
                float totalDepth = (gridRows - 1) * gridSpacing + 0.2f;
                stackCollider.size = new Vector3(totalWidth, baseColliderSizeY, totalDepth);
                stackCollider.center = new Vector3(0, baseColliderCenterY, 0);
            }
        }
    }
}
