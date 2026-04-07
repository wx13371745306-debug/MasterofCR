using System.Collections.Generic;
using UnityEngine;

public class OrderGenerator : MonoBehaviour
{
    [Header("Refs")]
    public MenuSO menuSO;
    public FryRecipeDatabase recipeDatabase;
    public DrinkRecipeDatabase drinkRecipeDatabase;
    public GameConfigSO gameConfig;

    public List<FryRecipeDatabase.FryRecipe> GenerateOrder(int min, int max, DishPlaceSystem system, int customerCount = 1)
    {
        var order = new List<FryRecipeDatabase.FryRecipe>();

        if (recipeDatabase == null)
        {
            Debug.LogWarning("[OrderGenerator] 无法生成订单：未配置 recipeDatabase。");
            return order;
        }

        if (system == null) 
        {
            Debug.LogWarning("[OrderGenerator] 无法生成订单：未提供 DishPlaceSystem 约束。");
            return order;
        }

        // 1. 获取这桌当前可用的所有槽位
        List<DishSize> remainingSlots = system.GetSlotSizes();
        if (remainingSlots.Count == 0) 
        {
            Debug.LogWarning($"[OrderGenerator] 无法生成订单：该桌子没有可用的槽位。");
            return order;
        }

        // 2. 确定要生成几个菜（仅餐食）
        int finalMin = Mathf.Max(1, min);
        int finalMax = Mathf.Max(finalMin, max);
        int targetCount = Random.Range(finalMin, finalMax + 1);
        Debug.Log($"[OrderGenerator] 开始生成订单，目标菜品数：{targetCount}。可用槽位数量：{remainingSlots.Count}。");

        // 3. 准备餐食题库：优先从 MenuSO 读取，fallback 到数据库全部已解锁菜谱
        var candidates = new List<FryRecipeDatabase.FryRecipe>();
        if (menuSO != null && menuSO.selectedRecipes.Count > 0)
        {
            candidates.AddRange(menuSO.GetFoodRecipes());
        }
        else
        {
            foreach (var r in recipeDatabase.recipes)
            {
                if (r != null && r.unlocked)
                    candidates.Add(r);
            }
        }

        if (candidates.Count == 0) return order;

        // 4. 抽取餐食
        int safety = 0;
        while (order.Count < targetCount && remainingSlots.Count > 0 && safety < 500)
        {
            safety++;

            int idx = Random.Range(0, candidates.Count);
            var pick = candidates[idx];

            if (!TryConsumeSlotFor(pick.size, remainingSlots))
            {
                Debug.Log($"[OrderGenerator] 尝试添加 {pick.recipeName}({pick.size}) 失败：没有合适的空余槽位。");
                continue; 
            }

            Debug.Log($"[OrderGenerator] 成功添加 {pick.recipeName}({pick.size})，剩余可用槽位：{remainingSlots.Count}");
            order.Add(pick);
        }

        // 5. 每位顾客独立掷骰：是否追加饮料
        if (drinkRecipeDatabase != null && gameConfig != null)
        {
            float baseProbability = gameConfig.drinkOrderProbability;
            float probability = baseProbability;

            bool bridgeOk = BondRuntimeBridge.Instance != null && BondRuntimeBridge.Instance.State != null;
            bool sichuanActive = bridgeOk && BondRuntimeBridge.Instance.State.IsActive(RecipeBondTag.Sichuan);
            Debug.Log($"[OrderGenerator] 饮料判定 | Bridge={bridgeOk} SichuanActive={sichuanActive} baseProbability={baseProbability:P0}");

            if (sichuanActive)
            {
                probability = Mathf.Clamp01(probability + 0.4f);
                Debug.Log($"[OrderGenerator] 川湘羁绊生效，饮料概率 {baseProbability:P0} → {probability:P0}");
            }
            List<FryRecipeDatabase.FryRecipe> drinkCandidates;
            if (menuSO != null && menuSO.selectedRecipes.Count > 0)
                drinkCandidates = menuSO.GetDrinkRecipes();
            else
                drinkCandidates = drinkRecipeDatabase.GetUnlockedRecipes();

            for (int c = 0; c < customerCount; c++)
            {
                float roll = Random.value;
                if (roll >= probability) continue;

                if (drinkCandidates.Count == 0)
                {
                    Debug.LogWarning("[OrderGenerator] 饮料菜谱中没有已解锁的饮料。");
                    break;
                }

                var drinkPick = drinkCandidates[Random.Range(0, drinkCandidates.Count)];
                if (TryConsumeSlotFor(DishSize.D, remainingSlots))
                {
                    order.Add(drinkPick);
                    Debug.Log($"[OrderGenerator] 顾客 {c + 1}/{customerCount} 加点了饮料: {drinkPick.recipeName}");
                }
                else
                {
                    Debug.Log($"[OrderGenerator] 顾客 {c + 1}/{customerCount} 想点饮料但没有可用的 D 槽位，后续顾客跳过。");
                    break;
                }
            }
        }

        Debug.Log($"[OrderGenerator] 订单生成完毕，最终菜品数：{order.Count}。");
        return order;
    }

    private static bool TryConsumeSlotFor(DishSize dishSize, List<DishSize> remainingSlots)
    {
        int bestIndex = -1;
        int bestRank = int.MaxValue;

        for (int i = 0; i < remainingSlots.Count; i++)
        {
            DishSize slot = remainingSlots[i];
            if (!SlotCanFit(slot, dishSize)) continue;

            // 找最“门当户对”的槽位（紧凑原则）
            int rank = SlotRank(slot);
            if (rank < bestRank)
            {
                bestRank = rank;
                bestIndex = i;
            }
        }

        if (bestIndex < 0) return false;
        
        // 找到了合适的槽位，从可用清单里划掉它，避免下一个菜又占这个位置
        remainingSlots.RemoveAt(bestIndex);
        return true;
    }

    private static bool SlotCanFit(DishSize slot, DishSize dish)
    {
        if (dish == DishSize.D) return slot == DishSize.D;
        if (slot == DishSize.D) return false;

        if (dish == DishSize.L) return slot == DishSize.L;
        if (dish == DishSize.M) return slot == DishSize.M || slot == DishSize.L;
        return slot == DishSize.S || slot == DishSize.M || slot == DishSize.L;
    }

    private static int SlotRank(DishSize slot)
    {
        // 排序逻辑，越小的槽位优先级越高，优先被消耗
        switch (slot)
        {
            case DishSize.S: return 1;
            case DishSize.M: return 2;
            case DishSize.L: return 3;
            case DishSize.D: return 4;
            default: return 99;
        }
    }
}