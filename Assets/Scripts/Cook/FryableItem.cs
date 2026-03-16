using UnityEngine;

public class FryableItem : MonoBehaviour
{
    public FryIngredientId ingredientId;

    [Header("Cooking Progress")]
    public int baseRequired = 100;
    public int addedRequired = 50;

    [Header("Visuals")]
    [Tooltip("当该食材被放入锅中后，在锅内生成的视觉模型预制体（通常不带Collider）")]
    public GameObject visualInPotPrefab;
}