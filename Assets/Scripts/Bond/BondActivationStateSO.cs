using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BondActivationState", menuName = "Cooking/Bond Activation State")]
public class BondActivationStateSO : ScriptableObject
{
    [System.Serializable]
    public class BondEntry
    {
        public RecipeBondTag tag;
        [Tooltip("在 UI 上显示的中文名称，可自由编辑")]
        public string displayName;
        [Tooltip("激活后显示的说明文案")]
        public string description = "该buff已激活";
        [Tooltip("羁绊图标，激活后在 UI 列表中显示此图")]
        public Sprite icon;
        [Tooltip("该羁绊激活后提供的暴击加成（0 表示无加成）")]
        public float critBonus = 0f;
        [Tooltip("运行时由逻辑写入，不要手动勾选")]
        public bool isActive;
    }

    [Header("羁绊槽位（扩展羁绊需在此增加条目）")]
    public List<BondEntry> bonds = new List<BondEntry>();

    /// <summary>按枚举查找对应条目，找不到返回 null。</summary>
    public BondEntry GetEntry(RecipeBondTag tag)
    {
        foreach (var b in bonds)
            if (b.tag == tag) return b;
        return null;
    }

    /// <summary>查询某条羁绊是否激活。</summary>
    public bool IsActive(RecipeBondTag tag)
    {
        var e = GetEntry(tag);
        return e != null && e.isActive;
    }

    /// <summary>返回所有已激活的羁绊。</summary>
    public List<BondEntry> GetActiveBonds()
    {
        var result = new List<BondEntry>();
        foreach (var b in bonds)
            if (b.isActive) result.Add(b);
        return result;
    }

    /// <summary>根据 MenuSO 重新计算羁绊激活状态。</summary>
    public void RefreshFromMenu(MenuSO menu)
    {
        if (menu != null)
            RefreshFromRecipes(menu.selectedRecipes);
        else
            ResetAll();
    }

    /// <summary>
    /// 根据任意菜谱列表重新计算所有羁绊的激活状态。
    /// 仅统计 size != D 的餐食。bondTag 支持 [Flags] 多选。
    /// </summary>
    public void RefreshFromRecipes(IEnumerable<FryRecipeDatabase.FryRecipe> recipes)
    {
        int sichuan = 0, homeCooking = 0, vegetable = 0, meat = 0;
        int totalFood = 0;

        if (recipes != null)
        {
            foreach (var r in recipes)
            {
                if (r == null || r.size == DishSize.D) continue;
                totalFood++;

                if (r.bondTag.HasFlag(RecipeBondTag.Sichuan))    sichuan++;
                if (r.bondTag.HasFlag(RecipeBondTag.HomeCooking)) homeCooking++;
                if (r.bondTag.HasFlag(RecipeBondTag.Vegetable))   vegetable++;
                if (r.bondTag.HasFlag(RecipeBondTag.Meat))        meat++;

                Debug.Log($"[Bond] 统计菜谱 '{r.recipeName}' bondTag={r.bondTag} (川湘:{r.bondTag.HasFlag(RecipeBondTag.Sichuan)} 家常:{r.bondTag.HasFlag(RecipeBondTag.HomeCooking)} 素:{r.bondTag.HasFlag(RecipeBondTag.Vegetable)} 肉:{r.bondTag.HasFlag(RecipeBondTag.Meat)})");
            }
        }

        Debug.Log($"[Bond] 统计结果：餐食总数={totalFood} | 川湘={sichuan} 家常={homeCooking} 素菜={vegetable} 肉菜={meat}");

        foreach (var b in bonds)
        {
            bool wasPreviouslyActive = b.isActive;

            if (b.tag == RecipeBondTag.Sichuan)
                b.isActive = sichuan >= 3;
            else if (b.tag == RecipeBondTag.HomeCooking)
                b.isActive = homeCooking >= 2;
            else if (b.tag == RecipeBondTag.Meat)
                b.isActive = meat > vegetable;
            else if (b.tag == RecipeBondTag.Vegetable)
                b.isActive = vegetable > meat;
            else
                b.isActive = false;

            Debug.Log($"[Bond] 羁绊 '{b.displayName}'(tag={b.tag}): {(b.isActive ? "激活" : "未激活")}{(wasPreviouslyActive != b.isActive ? " ← 状态变更!" : "")}");
        }
    }

    /// <summary>累加所有已激活羁绊的暴击加成。</summary>
    public float GetTotalActiveCritBonus()
    {
        float total = 0f;
        foreach (var b in bonds)
            if (b.isActive) total += b.critBonus;
        return total;
    }

    /// <summary>全部重置为未激活。</summary>
    public void ResetAll()
    {
        foreach (var b in bonds)
            b.isActive = false;
    }
}
