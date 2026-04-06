using UnityEngine;

public class ItemPlacePoint : MonoBehaviour
{
    [Header("Refs")]
    public Transform attachPoint;
    public InteractableHighlight sensorHighlight;

    [Header("Placement Rules")]
    public bool allowAnyCategory = true;
    public ItemCategory allowedCategories = ItemCategory.None;

    [Header("Linked Carryable")]
    [Tooltip("当此放置点被占用时，将该 CarryableItem 设为不可拿取；清空时恢复可拿取")]
    public CarryableItem linkedCarryable;

    [Header("Layer Switch On Occupy")]
    [Tooltip("勾选后，放置物品时将指定 Collider 的 Layer 切换为 Uninteractive，取走时恢复原始 Layer")]
    public bool switchColliderLayerOnOccupy = false;
    [Tooltip("被控制 Layer 的目标 Collider（不填则不生效）")]
    public Collider targetCollider;

    // 【新增事件】：当有物体合法放上来时触发，取代 Update 轮询！
    public event System.Action<CarryableItem> OnItemPlacedEvent;

    public CarryableItem CurrentItem => currentItem;
    private CarryableItem currentItem;
    private int targetColliderOriginalLayer;

    void Awake()
    {
        if (attachPoint == null) attachPoint = transform;
        if (targetCollider != null)
            targetColliderOriginalLayer = targetCollider.gameObject.layer;
        SetSensorHighlight(false);
    }

    public bool CanPlace(CarryableItem item)
    {
        if (item == null || (currentItem != null && currentItem != item)) return false;
        if (allowAnyCategory) return true;
        return item.HasAnyCategory(allowedCategories);
    }

    // 【核心重构】：所有放置动作统一由这个函数主导，打破了原来的循环依赖
    public bool TryAcceptItem(CarryableItem item)
    {
        if (!CanPlace(item)) return false;

        if (currentItem != null && currentItem != item)
            ClearOccupant(); // 挤走旧的

        currentItem = item;
        item.InternalSetPlacedState(this);
        
        if (linkedCarryable != null) linkedCarryable.isPickable = false;
        SetTargetColliderLayer(true);

        OnItemPlacedEvent?.Invoke(item);
        return true;
    }

    public void ClearOccupant()
    {
        if (currentItem != null)
        {
            currentItem.ClearPlaceState();
            currentItem = null;
        }

        if (linkedCarryable != null) linkedCarryable.isPickable = true;
        SetTargetColliderLayer(false);
    }

    private void SetTargetColliderLayer(bool occupied)
    {
        if (!switchColliderLayerOnOccupy || targetCollider == null) return;

        if (occupied)
        {
            int uninteractiveLayer = LayerMask.NameToLayer("Uninteractive");
            if (uninteractiveLayer != -1)
                targetCollider.gameObject.layer = uninteractiveLayer;
        }
        else
        {
            targetCollider.gameObject.layer = targetColliderOriginalLayer;
        }
    }

    public void SetSensorHighlight(bool on)
    {
        if (sensorHighlight != null) sensorHighlight.SetHighlighted(on);
    }
}