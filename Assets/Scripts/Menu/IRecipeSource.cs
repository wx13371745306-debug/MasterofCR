using System.Collections.Generic;

public interface IRecipeSource
{
    string CategoryName { get; }
    List<FryRecipeDatabase.FryRecipe> GetUnlockedRecipes();
}
