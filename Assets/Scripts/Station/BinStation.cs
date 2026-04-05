using UnityEngine;

public class BinStation : BaseStation
{
    [Header("Bin Settings")]
    [Tooltip("垃圾桶的放置点，任何物品放上来都会被销毁")]
    public ItemPlacePoint binPlacePoint;

    [Tooltip("空盘子预制体，用于将菜品（Dish）转回空盘")]
    public GameObject cleanPlatePrefab;

    protected override void Awake()
    {
        base.Awake();
        if (binPlacePoint != null)
            binPlacePoint.OnItemPlacedEvent += OnItemDroppedIntoBin;
    }

    void OnDestroy()
    {
        if (binPlacePoint != null)
            binPlacePoint.OnItemPlacedEvent -= OnItemDroppedIntoBin;
    }

    void OnItemDroppedIntoBin(CarryableItem item)
    {
        if (item == null) return;

        binPlacePoint.ClearOccupant();

        if (debugLog)
            Debug.Log($"<color=#FF4444>[BinStation]</color> 销毁放入的物品: {item.name}");

        Destroy(item.gameObject);
    }

    public override bool CanInteract(PlayerItemInteractor interactor)
    {
        return false;
    }

    public override void BeginInteract(PlayerItemInteractor interactor) { }
    public override void EndInteract(PlayerItemInteractor interactor) { }

    public bool TryDumpHeldItem(PlayerItemInteractor interactor, CarryableItem heldItem)
    {
        if (interactor == null || heldItem == null) return false;

        FryPot pot = heldItem.GetComponent<FryPot>();
        if (pot != null)
            return TryDumpPot(pot);

        DishRecipeTag dishTag = heldItem.GetComponent<DishRecipeTag>();
        if (dishTag != null)
            return TryConvertDishToPlate(interactor, heldItem);

        return false;
    }

    bool TryDumpPot(FryPot pot)
    {
        if (!pot.CanDump())
        {
            if (debugLog)
                Debug.Log("<color=#FF4444>[BinStation]</color> 锅是空的，无需清空。");
            return false;
        }

        pot.ForceClear();

        if (debugLog)
            Debug.Log("<color=#FF4444>[BinStation]</color> 已清空锅内所有食材和进度。");

        return true;
    }

    bool TryConvertDishToPlate(PlayerItemInteractor interactor, CarryableItem dish)
    {
        if (cleanPlatePrefab == null)
        {
            if (debugLog)
                Debug.LogWarning("<color=#FF4444>[BinStation]</color> cleanPlatePrefab 未配置，无法将菜品转回盘子！");
            return false;
        }

        Transform holdPoint = interactor.GetHoldPoint();
        if (holdPoint == null) return false;

        GameObject plateObj = Instantiate(cleanPlatePrefab);
        CarryableItem plateItem = plateObj.GetComponent<CarryableItem>();

        if (plateItem == null)
        {
            if (debugLog)
                Debug.LogError("<color=#FF4444>[BinStation]</color> cleanPlatePrefab 上缺少 CarryableItem 组件！");
            Destroy(plateObj);
            return false;
        }

        plateItem.BeginHold(holdPoint);
        interactor.ReplaceHeldItem(plateItem);

        if (debugLog)
            Debug.Log($"<color=#FF4444>[BinStation]</color> 菜品 '{dish.name}' 已转回空盘子。");

        Destroy(dish.gameObject);
        return true;
    }
}
