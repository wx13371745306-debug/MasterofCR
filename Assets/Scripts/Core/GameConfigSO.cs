using UnityEngine;

[CreateAssetMenu(fileName = "GameConfig", menuName = "Cooking/Game Config")]
public class GameConfigSO : ScriptableObject
{
    [Header("饮料系统")]
    [Tooltip("每位顾客点饮料的概率（0 = 从不点，1 = 必定点）")]
    [Range(0f, 1f)]
    public float drinkOrderProbability = 0.1f;
}
