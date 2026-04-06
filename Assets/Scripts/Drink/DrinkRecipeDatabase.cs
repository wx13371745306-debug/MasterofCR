using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "DrinkRecipeDatabase", menuName = "Cooking/Drink Recipe Database")]
public class DrinkRecipeDatabase : ScriptableObject, IRecipeSource
{
    public string CategoryName => "饮品";
    [Header("Drink Recipes")]
    public List<FryRecipeDatabase.FryRecipe> recipes = new List<FryRecipeDatabase.FryRecipe>();

    public FryRecipeDatabase.FryRecipe FindByName(string targetRecipeName)
    {
        if (string.IsNullOrEmpty(targetRecipeName)) return null;

        foreach (var r in recipes)
        {
            if (r != null && r.recipeName == targetRecipeName)
                return r;
        }
        return null;
    }

    public List<FryRecipeDatabase.FryRecipe> GetUnlockedRecipes()
    {
        var result = new List<FryRecipeDatabase.FryRecipe>();
        foreach (var r in recipes)
        {
            if (r != null && r.unlocked)
                result.Add(r);
        }
        return result;
    }
}
