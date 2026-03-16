using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Cooking/Fry Recipe Database")]
public class FryRecipeDatabase : ScriptableObject
{
    [System.Serializable]
    public class IngredientEntry
    {
        public FryIngredientId ingredient;
        public int count = 1;
    }

    [System.Serializable]
    public class FryRecipe
    {
        public string recipeName;
        public bool unlocked = true;
        public List<IngredientEntry> ingredients = new List<IngredientEntry>();
        public GameObject resultPrefab; // 盛出来后拿在手里的 Item

        [Tooltip("烹饪完成后，留在锅里的模型预制体")]
        public GameObject finishedVisualPrefab; 
    }

    public List<FryRecipe> recipes = new List<FryRecipe>();

    public FryRecipe FindMatch(Dictionary<FryIngredientId, int> materialCounts)
    {
        foreach (var recipe in recipes)
        {
            if (!recipe.unlocked) continue;
            bool match = true;
            foreach (var entry in recipe.ingredients)
            {
                if (!materialCounts.TryGetValue(entry.ingredient, out int count) || count != entry.count)
                {
                    match = false;
                    break;
                }
            }
            if (!match) continue;
            if (materialCounts.Count != recipe.ingredients.Count) continue;
            return recipe;
        }
        return null;
    }
}