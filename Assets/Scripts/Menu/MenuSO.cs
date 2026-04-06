using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Menu", menuName = "Cooking/Menu")]
public class MenuSO : ScriptableObject
{
    [Header("玩家选择的菜单")]
    public List<FryRecipeDatabase.FryRecipe> selectedRecipes = new List<FryRecipeDatabase.FryRecipe>();

    public List<FryRecipeDatabase.FryRecipe> GetFoodRecipes()
    {
        var result = new List<FryRecipeDatabase.FryRecipe>();
        foreach (var r in selectedRecipes)
        {
            if (r != null && r.size != DishSize.D)
                result.Add(r);
        }
        return result;
    }

    public List<FryRecipeDatabase.FryRecipe> GetDrinkRecipes()
    {
        var result = new List<FryRecipeDatabase.FryRecipe>();
        foreach (var r in selectedRecipes)
        {
            if (r != null && r.size == DishSize.D)
                result.Add(r);
        }
        return result;
    }

    public void Clear()
    {
        selectedRecipes.Clear();
    }
}
