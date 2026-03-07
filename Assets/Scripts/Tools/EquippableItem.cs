using UnityEngine;

public class EquippableItem : MonoBehaviour
{
    [Header("Equip Settings")]
    public bool canEquip = true;

    // 以后如果你要给不同道具单独微调装备位置/旋转，
    // 就把数据加在这里，不需要重写整套拿取逻辑。
    public Vector3 equipLocalPosition = Vector3.zero;
    public Vector3 equipLocalEulerAngles = Vector3.zero;

    [Header("Debug")]
    public bool debugLog = true;
}