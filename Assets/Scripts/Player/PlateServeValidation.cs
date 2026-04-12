using UnityEngine;

/// <summary>
/// 持锅装盘：判定场景中的 CarryableItem 是否可作为「空盘」目标（与 Sensor 触发器内候选共用）。
/// </summary>
public static class PlateServeValidation
{
    public static bool IsValidEmptyPlate(CarryableItem item)
    {
        if (item == null) return false;
        if (item.GetComponent<PlateItem>() == null) return false;
        if (item.GetComponent<DishRecipeTag>() != null) return false;
        if (item is DynamicItemStack) return false;
        if (item.GetComponentInParent<DirtyPlateStack>() != null) return false;
        if (item.GetComponentInParent<SupplyBox>() != null) return false;
        return true;
    }
}
