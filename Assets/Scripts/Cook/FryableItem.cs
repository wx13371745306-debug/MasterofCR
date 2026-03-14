using UnityEngine;

public class FryableItem : MonoBehaviour
{
    public FryIngredientId ingredientId;

    [Header("Cooking Progress")]
    public int baseRequired = 100;
    public int addedRequired = 50;
}