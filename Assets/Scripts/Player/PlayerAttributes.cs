using UnityEngine;

public class PlayerAttributes : MonoBehaviour
{
    [Header("Abilities (Multipliers)")]
    [Tooltip("切菜能力属性 (乘数)，默认1.0")]
    public float chopSpeedMultiplier = 1.0f;
    
    [Tooltip("洗碗能力属性 (乘数)，默认1.0")]
    public float washSpeedMultiplier = 1.0f;

    [Header("Debug")]
    public bool debugLog = true;
}
