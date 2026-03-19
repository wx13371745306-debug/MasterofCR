using System.Collections.Generic;
using UnityEngine;

public class OrderGenerator : MonoBehaviour
{
    [Header("Refs")]
    public FryRecipeDatabase recipeDatabase;

    // 核心生成方法，由每个桌子的 OrderResponse 调用
    public List<FryRecipeDatabase.FryRecipe> GenerateOrder(int min, int max, DishPlaceSystem system)
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

        // 2. 确定要生成几个菜
        int finalMin = Mathf.Max(1, min);
        int finalMax = Mathf.Max(finalMin, max);
        int targetCount = Random.Range(finalMin, finalMax + 1);
        Debug.Log($"[OrderGenerator] 开始生成订单，目标菜品数：{targetCount}。可用槽位数量：{remainingSlots.Count}。");

        // 3. 准备题库（只选已解锁的）
        var candidates = new List<FryRecipeDatabase.FryRecipe>();
        foreach (var r in recipeDatabase.recipes)
        {
            if (r != null && r.unlocked)
            {
                candidates.Add(r);
            }
        }

        if (candidates.Count == 0) return order;

        // 4. 开始抽卡（加入了防止死循环的安全退出机制）
        int safety = 0;
        while (order.Count < targetCount && remainingSlots.Count > 0 && safety < 500)
        {
            safety++;

            // 随机抽一道菜（不从 candidates 里 Remove，允许点两份一样的菜）
            int idx = Random.Range(0, candidates.Count);
            var pick = candidates[idx];

            // 尝试把这道菜塞进槽位里
            if (!TryConsumeSlotFor(pick.size, remainingSlots))
            {
                Debug.Log($"[OrderGenerator] 尝试添加 {pick.recipeName}({pick.size}) 失败：没有合适的空余槽位。");
                // 如果塞不进去（比如抽到了大菜，但桌上只有小槽了），就跳过，重抽
                continue; 
            }

            Debug.Log($"[OrderGenerator] 成功添加 {pick.recipeName}({pick.size})，剩余可用槽位：{remainingSlots.Count}");
            order.Add(pick);
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