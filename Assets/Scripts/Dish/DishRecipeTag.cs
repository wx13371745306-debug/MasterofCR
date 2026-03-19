using UnityEngine;

public class DishRecipeTag : MonoBehaviour
{
    [Tooltip("必须与 FryRecipeDatabase 中的 recipeName 完全一致")]
    public string recipeName;
}
