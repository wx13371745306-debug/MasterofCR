using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MenuSelectionUIController : MonoBehaviour
{
    [Header("Data")]
    public MenuSO menuSO;

    [Tooltip("拖入菜谱数据库 SO（FryRecipeDatabase / DrinkRecipeDatabase 等）；支持任意数量的槽位")]
    public List<ScriptableObject> recipeSources = new List<ScriptableObject>();

    [Header("UI - Recipe Grid (左侧已解锁菜谱)")]
    public GameObject selectionPanelRoot;
    public Transform recipeCardContainer;
    public GameObject recipeCardPrefab;

    [Header("UI - Selected List (右侧已选菜谱)")]
    public Transform selectedItemContainer;
    public GameObject selectedItemPrefab;

    [Header("UI - Buttons")]
    public Button confirmButton;
    public Button closeButton;

    [Header("UI - Tutorial Panel")]
    public RecipeTutorialPanel tutorialPanel;

    private readonly HashSet<FryRecipeDatabase.FryRecipe> selected = new HashSet<FryRecipeDatabase.FryRecipe>();

    public bool IsOpen => selectionPanelRoot != null && selectionPanelRoot.activeSelf;

    public void Open()
    {
        Debug.Log($"[MenuSelection] Open() 被调用 | selectionPanelRoot={(selectionPanelRoot != null ? selectionPanelRoot.name : "NULL")}");
        if (selectionPanelRoot == null)
        {
            Debug.LogWarning("[MenuSelection] selectionPanelRoot 未赋值，无法打开面板！");
            return;
        }

        RestoreFromMenuSO();
        selectionPanelRoot.SetActive(true);
        Debug.Log("[MenuSelection] 面板已激活，开始构建菜谱列表");
        RebuildRecipeGrid();
        RebuildSelectedList();
    }

    public void Close()
    {
        Debug.Log("[MenuSelection] Close() 被调用");
        if (selectionPanelRoot == null) return;
        selectionPanelRoot.SetActive(false);
    }

    public void ToggleRecipe(FryRecipeDatabase.FryRecipe recipe)
    {
        if (recipe == null) return;

        if (!selected.Remove(recipe))
        {
            selected.Add(recipe);
            Debug.Log($"[MenuSelection] 选中菜谱: '{recipe.recipeName}' | 当前已选: {selected.Count}");
        }
        else
        {
            Debug.Log($"[MenuSelection] 取消选中: '{recipe.recipeName}' | 当前已选: {selected.Count}");
        }

        RebuildRecipeGrid();
        RebuildSelectedList();
    }

    public bool IsSelected(FryRecipeDatabase.FryRecipe recipe)
    {
        return recipe != null && selected.Contains(recipe);
    }

    public void OnConfirmClicked()
    {
        Debug.Log($"[MenuSelection] 确定按钮被点击 | menuSO={(menuSO != null ? menuSO.name : "NULL")} | 已选数量: {selected.Count}");
        if (menuSO == null)
        {
            Debug.LogWarning("[MenuSelection] menuSO 未赋值，无法保存选择！");
            return;
        }

        menuSO.Clear();
        foreach (var recipe in selected)
        {
            if (recipe != null)
                menuSO.selectedRecipes.Add(recipe);
        }
        Debug.Log($"[MenuSelection] 已将 {menuSO.selectedRecipes.Count} 道菜谱写入 MenuSO");

        Close();
    }

    public void OnCloseClicked()
    {
        Debug.Log("[MenuSelection] 关闭按钮被点击");
        Close();
    }

    public void OpenTutorial(FryRecipeDatabase.FryRecipe recipe)
    {
        Debug.Log($"[MenuSelection] 打开制作方法: '{recipe?.recipeName ?? "NULL"}' | tutorialPanel={(tutorialPanel != null ? "已赋值" : "NULL")}");
        if (tutorialPanel != null)
            tutorialPanel.Open(recipe);
    }

    void RestoreFromMenuSO()
    {
        selected.Clear();
        if (menuSO == null)
        {
            Debug.LogWarning("[MenuSelection] RestoreFromMenuSO: menuSO 为空，跳过恢复");
            return;
        }

        foreach (var r in menuSO.selectedRecipes)
        {
            if (r != null)
                selected.Add(r);
        }
        Debug.Log($"[MenuSelection] 从 MenuSO 恢复了 {selected.Count} 道已选菜谱");
    }

    void RebuildRecipeGrid()
    {
        if (recipeCardContainer == null)
        {
            Debug.LogWarning("[MenuSelection] RebuildRecipeGrid: recipeCardContainer 未赋值！");
            return;
        }
        if (recipeCardPrefab == null)
        {
            Debug.LogWarning("[MenuSelection] RebuildRecipeGrid: recipeCardPrefab 未赋值！");
            return;
        }

        for (int i = recipeCardContainer.childCount - 1; i >= 0; i--)
            Destroy(recipeCardContainer.GetChild(i).gameObject);

        Debug.Log($"[MenuSelection] recipeSources 槽位数量: {recipeSources.Count}");
        int totalCards = 0;

        foreach (var source in recipeSources)
        {
            if (source == null)
            {
                Debug.LogWarning("[MenuSelection] recipeSources 中有一个槽位为空（NULL），已跳过");
                continue;
            }
            var provider = source as IRecipeSource;
            if (provider == null)
            {
                Debug.LogWarning($"[MenuSelection] SO '{source.name}' (类型: {source.GetType().Name}) 未实现 IRecipeSource，已跳过");
                continue;
            }

            var unlocked = provider.GetUnlockedRecipes();
            Debug.Log($"[MenuSelection] 数据源 '{source.name}' (分类: {provider.CategoryName}): 共 {unlocked.Count} 道已解锁菜谱");

            foreach (var recipe in unlocked)
            {
                if (recipe == null) continue;

                GameObject go = Instantiate(recipeCardPrefab, recipeCardContainer);
                go.transform.localScale = Vector3.one;
                var card = go.GetComponent<MenuRecipeCardView>();
                if (card != null)
                    card.Setup(recipe, this);
                else
                    Debug.LogWarning($"[MenuSelection] recipeCardPrefab 上没有找到 MenuRecipeCardView 脚本！");
                totalCards++;
            }
        }
        Debug.Log($"[MenuSelection] 菜谱 Grid 构建完成，共生成 {totalCards} 张卡片");
    }

    void RebuildSelectedList()
    {
        if (selectedItemContainer == null)
        {
            Debug.LogWarning("[MenuSelection] RebuildSelectedList: selectedItemContainer 未赋值！");
            return;
        }
        if (selectedItemPrefab == null)
        {
            Debug.LogWarning("[MenuSelection] RebuildSelectedList: selectedItemPrefab 未赋值！");
            return;
        }

        for (int i = selectedItemContainer.childCount - 1; i >= 0; i--)
            Destroy(selectedItemContainer.GetChild(i).gameObject);

        foreach (var recipe in selected)
        {
            if (recipe == null) continue;

            GameObject go = Instantiate(selectedItemPrefab, selectedItemContainer);
            go.transform.localScale = Vector3.one;
            var view = go.GetComponent<MenuSelectedItemView>();
            if (view != null)
                view.Setup(recipe, this);
            else
                Debug.LogWarning($"[MenuSelection] selectedItemPrefab 上没有找到 MenuSelectedItemView 脚本！");
        }
        Debug.Log($"[MenuSelection] 已选列表刷新完成，共 {selected.Count} 项");
    }
}
