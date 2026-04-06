using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Cooking/Fry Recipe Database")]
public class FryRecipeDatabase : ScriptableObject, IRecipeSource
{
    public string CategoryName => "炒菜";
    [System.Serializable]
    public class IngredientEntry
    {
        [Tooltip("与食材预制体上 FryIngredientTag 的 ID 一致，例如 TomatoChunk")]
        public string ingredientId = "";
        public int count = 1;
    }

    [System.Serializable]
    public class FryRecipe
    {
        [Tooltip("内部标识，需与 DishRecipeTag、订单逻辑中的 recipeName 一致")]
        public string recipeName;
        [Tooltip("菜单卡等 UI 显示用中文名；留空则使用 recipeName")]
        public string displayNameZh = "";
        public bool unlocked = true;

        /// <summary>菜单与订单 UI 展示用名称（优先中文）。</summary>
        public string GetDisplayName()
        {
            return string.IsNullOrWhiteSpace(displayNameZh) ? recipeName : displayNameZh.Trim();
        }
        public List<IngredientEntry> ingredients = new List<IngredientEntry>();
        public GameObject resultPrefab; 
        [Tooltip("烹饪完成后，留在锅里的模型预制体")]
        public GameObject finishedVisualPrefab; 
        [Tooltip("吃完后的空盘子预制体")]
        public GameObject eatenPrefab;

        [Header("订单与结算")]
        // 菜品占用的桌面槽位类型：S/M/L 为餐食，D 为饮品
        public DishSize size;
        // 订单 UI 显示用图标（Sprite）
        public Sprite dishIcon;
        // 结算价格（匹配成功加钱）
        public int price = 50;
        // 用餐耗时（桌子进入用餐倒计时使用）
        public float eatTime = 10f;
    }

    [Header("Recipes")]
    public List<FryRecipe> recipes = new List<FryRecipe>();

    // --- 新增：失败兜底配方 ---
    [Header("Fallback")]
    [Tooltip("当放入的食材不满足任何配方时，默认生成的失败菜品配方")]
    public FryRecipe failedDishRecipe; 

    public FryRecipe FindMatch(Dictionary<string, int> materialCounts)
    {
        foreach (var recipe in recipes)
        {
            if (!recipe.unlocked)
            {
                Debug.Log($"[FryRecipeDB] 跳过未解锁菜谱: '{recipe.recipeName}'");
                continue;
            }
            if (recipe.ingredients == null)
            {
                Debug.Log($"[FryRecipeDB] 跳过无食材列表的菜谱: '{recipe.recipeName}'");
                continue;
            }

            Debug.Log($"[FryRecipeDB] 正在比对菜谱 '{recipe.recipeName}' (需要{recipe.ingredients.Count}种食材)");

            bool match = true;
            foreach (var entry in recipe.ingredients)
            {
                string id = FryIngredientTag.NormalizeId(entry.ingredientId);
                if (string.IsNullOrEmpty(id))
                {
                    Debug.LogWarning($"[FryRecipeDB]   菜谱 '{recipe.recipeName}' 含空的 ingredientId，跳过此菜谱");
                    match = false;
                    break;
                }

                bool found = materialCounts.TryGetValue(id, out int count);
                if (!found)
                {
                    Debug.Log($"[FryRecipeDB]   需要 '{id}'×{entry.count} → 锅里没有此食材，不匹配");
                    match = false;
                    break;
                }
                if (count != entry.count)
                {
                    Debug.Log($"[FryRecipeDB]   需要 '{id}'×{entry.count} → 锅里有 '{id}'×{count}，数量不匹配");
                    match = false;
                    break;
                }
                Debug.Log($"[FryRecipeDB]   需要 '{id}'×{entry.count} → 匹配 ✓");
            }

            if (!match) continue;

            if (materialCounts.Count != recipe.ingredients.Count)
            {
                Debug.Log($"[FryRecipeDB]   食材种类数不一致: 锅里{materialCounts.Count}种 vs 菜谱{recipe.ingredients.Count}种，不匹配");
                continue;
            }

            Debug.Log($"[FryRecipeDB] ★ 匹配成功: '{recipe.recipeName}'");
            return recipe;
        }

        Debug.Log($"[FryRecipeDB] 无匹配菜谱，返回兜底: '{(failedDishRecipe != null ? failedDishRecipe.recipeName : "null")}'");
        return failedDishRecipe;
    }

    public List<FryRecipe> GetUnlockedRecipes()
    {
        var result = new List<FryRecipe>();
        foreach (var r in recipes)
        {
            if (r != null && r.unlocked)
                result.Add(r);
        }
        return result;
    }

    public FryRecipe FindByName(string targetRecipeName)
    {
        if (string.IsNullOrEmpty(targetRecipeName)) return null;

        foreach (var r in recipes)
        {
            if (r == null) continue;
            if (r.recipeName == targetRecipeName) return r;
        }

        if (failedDishRecipe != null && failedDishRecipe.recipeName == targetRecipeName)
            return failedDishRecipe;

        return null;
    }
}
