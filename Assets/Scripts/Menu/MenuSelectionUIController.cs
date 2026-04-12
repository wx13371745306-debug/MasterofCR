using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MenuSelectionUIController : MonoBehaviour
{
    [Header("Debug")]
    public bool debugLog = false;

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

    [Header("ComputerStation · 回到主界面")]
    [Tooltip("拖入 ComputerPanelUIController；「返回电脑主界面」按钮绑定 OnReturnToComputerHomeClicked")]
    [SerializeField] private ComputerPanelUIController computerPanel;

    [Header("羁绊")]
    [Tooltip("羁绊状态 SO，选菜变化时实时刷新")]
    public BondActivationStateSO bondState;
    [Tooltip("羁绊列表 UI，选菜变化时实时刷新（可选）")]
    public BondListUIController bondListUI;

    private readonly HashSet<FryRecipeDatabase.FryRecipe> selected = new HashSet<FryRecipeDatabase.FryRecipe>();

    /// <summary>选菜面板关闭（确认或取消）时触发，供主界面刷新羁绊等。</summary>
    public event Action OnPanelClosed;

    public bool IsOpen => selectionPanelRoot != null && selectionPanelRoot.activeSelf;

    public void Open()
    {
        if (debugLog) Debug.Log($"[MenuSelection] Open() 被调用 | selectionPanelRoot={(selectionPanelRoot != null ? selectionPanelRoot.name : "NULL")}");
        if (selectionPanelRoot == null)
        {
            Debug.LogWarning("[MenuSelection] selectionPanelRoot 未赋值，无法打开面板！");
            return;
        }

        RestoreFromMenuSO();
        selectionPanelRoot.SetActive(true);
        if (debugLog) Debug.Log("[MenuSelection] 面板已激活，开始构建菜谱列表");
        RebuildRecipeGrid();
        RebuildSelectedList();
    }

    public void Close()
    {
        if (debugLog) Debug.Log("[MenuSelection] Close() 被调用");
        if (selectionPanelRoot == null) return;
        selectionPanelRoot.SetActive(false);
        OnPanelClosed?.Invoke();
    }

    public void ToggleRecipe(FryRecipeDatabase.FryRecipe recipe)
    {
        if (recipe == null) return;

        if (!selected.Remove(recipe))
        {
            selected.Add(recipe);
            if (debugLog) Debug.Log($"[MenuSelection] 选中菜谱: '{recipe.recipeName}' | 当前已选: {selected.Count}");
        }
        else
        {
            if (debugLog) Debug.Log($"[MenuSelection] 取消选中: '{recipe.recipeName}' | 当前已选: {selected.Count}");
        }

        RebuildRecipeGrid();
        RebuildSelectedList();
        RefreshBonds();
    }

    public bool IsSelected(FryRecipeDatabase.FryRecipe recipe)
    {
        return recipe != null && selected.Contains(recipe);
    }

    public static event Action OnMenuConfirmed;

    public void OnConfirmClicked()
    {
        if (debugLog) Debug.Log($"[MenuSelection] 确定按钮被点击 | menuSO={(menuSO != null ? menuSO.name : "NULL")} | 已选数量: {selected.Count}");
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
        if (debugLog) Debug.Log($"[MenuSelection] 已将 {menuSO.selectedRecipes.Count} 道菜谱写入 MenuSO");

        RefreshBonds();

        // 【新增：如果运行中调用这】为了在游戏进行中也能读取，强制 RuntimeBridge 重计羁绊
        if (BondRuntimeBridge.Instance != null)
        {
            BondRuntimeBridge.Instance.Refresh();
        }

        // 联网时将菜谱同步给所有端
        var shopBridge = NetworkShopBridge.Instance;
        if (shopBridge != null && Mirror.NetworkClient.active)
        {
            var names = new System.Collections.Generic.List<string>();
            foreach (var r in menuSO.selectedRecipes)
            {
                if (r != null && !string.IsNullOrEmpty(r.recipeName))
                    names.Add(r.recipeName);
            }
            shopBridge.NetworkSyncMenu(names);
        }

        OnMenuConfirmed?.Invoke();

        Close();
    }

    public void OnCloseClicked()
    {
        if (debugLog) Debug.Log("[MenuSelection] 关闭按钮被点击");
        Close();
    }

    /// <summary>关闭选菜界面并回到电脑主界面；需在 Inspector 中赋值 computerPanel。</summary>
    public void OnReturnToComputerHomeClicked()
    {
        if (computerPanel != null)
            computerPanel.ReturnToComputerHome();
        else
            OnCloseClicked();
    }

    public void OpenTutorial(FryRecipeDatabase.FryRecipe recipe)
    {
        if (debugLog) Debug.Log($"[MenuSelection] 打开制作方法: '{recipe?.recipeName ?? "NULL"}' | tutorialPanel={(tutorialPanel != null ? "已赋值" : "NULL")}");
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
        if (debugLog) Debug.Log($"[MenuSelection] 从 MenuSO 恢复了 {selected.Count} 道已选菜谱");
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

        if (debugLog) Debug.Log($"[MenuSelection] recipeSources 槽位数量: {recipeSources.Count}");
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
            if (debugLog) Debug.Log($"[MenuSelection] 数据源 '{source.name}' (分类: {provider.CategoryName}): 共 {unlocked.Count} 道已解锁菜谱");

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
        if (debugLog) Debug.Log($"[MenuSelection] 菜谱 Grid 构建完成，共生成 {totalCards} 张卡片");
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
        if (debugLog) Debug.Log($"[MenuSelection] 已选列表刷新完成，共 {selected.Count} 项");
    }

    void RefreshBonds()
    {
        if (debugLog) Debug.Log($"[MenuSelection] RefreshBonds | bondState={(bondState != null ? bondState.name : "NULL")} | bondListUI={(bondListUI != null ? "已赋值" : "NULL")} | 当前已选: {selected.Count}");
        if (bondState != null)
        {
            bondState.RefreshFromRecipes(selected);
            var active = bondState.GetActiveBonds();
            if (debugLog) Debug.Log($"[MenuSelection] 羁绊刷新完成，激活数量: {active.Count}");
            foreach (var b in active)
                if (debugLog) Debug.Log($"[MenuSelection]   已激活: '{b.displayName}' (tag={b.tag})");
        }
        else
        {
            Debug.LogWarning("[MenuSelection] bondState 未赋值，无法刷新羁绊！请在 Inspector 中拖入 BondActivationStateSO。");
        }

        if (bondListUI != null)
            bondListUI.Refresh();
        else
            Debug.LogWarning("[MenuSelection] bondListUI 未赋值，羁绊 UI 不会刷新。");
    }
}
