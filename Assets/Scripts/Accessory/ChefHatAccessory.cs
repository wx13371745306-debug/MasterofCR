using UnityEngine;

/// <summary>
/// 厨师帽饰品：装备后增加暴击率。
/// </summary>
public class ChefHatAccessory : AccessoryItem
{
    [Header("厨师帽 - 暴击加成")]
    [Tooltip("装备后增加的暴击率（加法叠加到 baseCritRate）")]
    [SerializeField] float critRateBonus = 0.9f;

    public override void OnEquipped(PlayerAttributes attrs)
    {
        if (attrs == null) return;
        attrs.accessoryCritBonus += critRateBonus;
        if (debugLog)
            Debug.Log($"<color=#FFD700>[ChefHatAccessory]</color> 暴击率 +{critRateBonus}，当前总暴击率: {attrs.baseCritRate}");
    }

    public override void OnUnequipped(PlayerAttributes attrs)
    {
        if (attrs == null) return;
        attrs.accessoryCritBonus -= critRateBonus;
        if (debugLog)
            Debug.Log($"<color=#FFD700>[ChefHatAccessory]</color> 暴击率 -{critRateBonus}，当前总暴击率: {attrs.baseCritRate}");
    }
}
