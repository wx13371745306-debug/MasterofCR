using UnityEngine;

/// <summary>
/// 运动鞋饰品：装备后玩家可以按 Shift 冲刺，同时降低物理质量避免撞飞别人。
/// 冲刺逻辑由 PlayerMoveRB 根据 PlayerAttributes.hasDash 驱动。
/// </summary>
public class SneakerAccessory : AccessoryItem
{
    [Header("运动鞋 - 冲刺配置")]
    [Tooltip("装备后玩家的物理质量（越低越不容易撞飞别人）")]
    [SerializeField] float dashMassOverride = 0.5f;

    public override void OnEquipped(PlayerAttributes attrs)
    {
        if (attrs == null) return;

        Rigidbody playerRb = attrs.GetComponent<Rigidbody>();
        if (playerRb != null)
        {
            attrs.originalMass = playerRb.mass;
            playerRb.mass = dashMassOverride;
        }

        attrs.hasDash = true;

        if (debugLog)
            Debug.Log($"<color=#00FF7F>[SneakerAccessory]</color> 冲刺已启用，质量 {attrs.originalMass} → {dashMassOverride}");
    }

    public override void OnUnequipped(PlayerAttributes attrs)
    {
        if (attrs == null) return;

        attrs.hasDash = false;

        Rigidbody playerRb = attrs.GetComponent<Rigidbody>();
        if (playerRb != null)
            playerRb.mass = attrs.originalMass;

        if (debugLog)
            Debug.Log($"<color=#00FF7F>[SneakerAccessory]</color> 冲刺已禁用，质量恢复为 {attrs.originalMass}");
    }
}
