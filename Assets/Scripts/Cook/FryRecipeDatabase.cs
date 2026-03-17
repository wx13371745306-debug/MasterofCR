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
        public GameObject resultPrefab; 
        [Tooltip("烹饪完成后，留在锅里的模型预制体")]
        public GameObject finishedVisualPrefab; 
    }

    [Header("Recipes")]
    public List<FryRecipe> recipes = new List<FryRecipe>();

    // --- 新增：失败兜底配方 ---
    [Header("Fallback")]
    [Tooltip("当放入的食材不满足任何配方时，默认生成的失败菜品配方")]
    public FryRecipe failedDishRecipe; 

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
            
            return recipe; // 找到正常配方，返回
        }

        // --- 核心修改：如果没有匹配项，不再返回 null，而是返回失败配方 ---
        return failedDishRecipe; 
    }
}