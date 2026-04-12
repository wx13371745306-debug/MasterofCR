using UnityEngine;

public class PlayerAttributes : MonoBehaviour
{
    [Header("Abilities (Multipliers)")]
    [Tooltip("切菜能力基础属性 (乘数)，默认1.0")]
    [SerializeField] float _baseChopSpeedMultiplier = 1.0f;

    [Tooltip("洗碗能力基础属性 (乘数)，默认1.0")]
    [SerializeField] float _baseWashSpeedMultiplier = 1.0f;

    [Header("Cooking")]
    [Tooltip("基础暴击率，默认10%")]
    [SerializeField] float _baseCritRate = 0.1f;

    [Header("饰品加成（运行时由 PlayerAccessoryHolder 写入）")]
    [HideInInspector] public float accessoryChopBonus = 0f;
    [HideInInspector] public float accessoryCritBonus = 0f;
    [HideInInspector] public float accessoryTipRate = 0f;
    [HideInInspector] public bool hasDash = false;
    [HideInInspector] public float originalMass = 1f;

    // 对外属性名与原字段名一致，旧代码无需改动
    public float chopSpeedMultiplier => _baseChopSpeedMultiplier + accessoryChopBonus;
    public float washSpeedMultiplier => _baseWashSpeedMultiplier;
    public float baseCritRate => _baseCritRate + accessoryCritBonus;

    [Header("Debug")]
    public bool debugLog = true;
}
