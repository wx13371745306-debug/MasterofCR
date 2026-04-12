using UnityEngine;

/// <summary>
/// 蝴蝶结饰品：上菜时额外获得菜品总价一定比例的小费。
/// 小费基于暴击后的最终结算价格。
/// </summary>
public class BowTieAccessory : AccessoryItem
{
    [Header("蝴蝶结 - 小费加成")]
    [Tooltip("上菜时额外获得的小费比例（0.1 = 10%）")]
    [SerializeField] float tipRate = 0.1f;

    public override void OnEquipped(PlayerAttributes attrs)
    {
        if (attrs == null) return;
        attrs.accessoryTipRate += tipRate;
        if (debugLog)
            Debug.Log($"<color=#FF69B4>[BowTieAccessory]</color> 小费率 +{tipRate}，当前总小费率: {attrs.accessoryTipRate}");
    }

    public override void OnUnequipped(PlayerAttributes attrs)
    {
        if (attrs == null) return;
        attrs.accessoryTipRate -= tipRate;
        if (debugLog)
            Debug.Log($"<color=#FF69B4>[BowTieAccessory]</color> 小费率 -{tipRate}，当前总小费率: {attrs.accessoryTipRate}");
    }
}
