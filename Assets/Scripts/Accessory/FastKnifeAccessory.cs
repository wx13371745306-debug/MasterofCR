using UnityEngine;

/// <summary>
/// 快刀饰品：装备后增加切菜速度。
/// </summary>
public class FastKnifeAccessory : AccessoryItem
{
    [Header("快刀 - 切菜速度加成")]
    [Tooltip("装备后增加的切菜速度乘数（加法叠加到 chopSpeedMultiplier）")]
    [SerializeField] float chopSpeedBonus = 2.0f;

    public override void OnEquipped(PlayerAttributes attrs)
    {
        if (attrs == null) return;
        attrs.accessoryChopBonus += chopSpeedBonus;
        if (debugLog)
            Debug.Log($"<color=#00FFFF>[FastKnifeAccessory]</color> 切菜速度 +{chopSpeedBonus}，当前总乘数: {attrs.chopSpeedMultiplier}");
    }

    public override void OnUnequipped(PlayerAttributes attrs)
    {
        if (attrs == null) return;
        attrs.accessoryChopBonus -= chopSpeedBonus;
        if (debugLog)
            Debug.Log($"<color=#00FFFF>[FastKnifeAccessory]</color> 切菜速度 -{chopSpeedBonus}，当前总乘数: {attrs.chopSpeedMultiplier}");
    }
}
