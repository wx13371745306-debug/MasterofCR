using UnityEngine;

[CreateAssetMenu(menuName = "Order/Table Class Data")]
public class TableClassData : ScriptableObject
{
    [Min(1)] public int minDishes = 1;
    [Min(1)] public int maxDishes = 3;
}
