using UnityEngine;

public class ItemPlacePoint : MonoBehaviour
{
    [Header("Refs")]
    public Transform attachPoint;
    public InteractableHighlight sensorHighlight;

    [Header("Placement Rules")]
    public bool allowAnyCategory = true;
    public ItemCategory allowedCategories = ItemCategory.None;

    // 【新增事件】：当有物体合法放上来时触发，取代 Update 轮询！
    public event System.Action<CarryableItem> OnItemPlacedEvent;

    public CarryableItem CurrentItem => currentItem;
    private CarryableItem currentItem;

    void Awake()
    {
        if (attachPoint == null) attachPoint = transform;
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
        item.InternalSetPlacedState(this); // 通知物体改变自身状态
        
        OnItemPlacedEvent?.Invoke(item); // 广播事件（比如通知锅把食材吃掉）
        return true;
    }

    public void ClearOccupant()
    {
        if (currentItem != null)
        {
            currentItem.ClearPlaceState();
            currentItem = null;
        }
    }

    public void SetSensorHighlight(bool on)
    {
        if (sensorHighlight != null) sensorHighlight.SetHighlighted(on);
    }
}