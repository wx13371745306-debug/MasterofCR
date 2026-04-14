using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class OrderCardUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("显示菜品图片的 Image 组件")]
    public Image dishIcon;

    [Tooltip("显示是几号桌点的 Text 组件")]
    public TextMeshProUGUI tableNumberText;

    [Tooltip("显示菜品名称（优先 FryRecipe.displayNameZh）")]
    public TextMeshProUGUI dishNameText;

    [Tooltip("横向排列原料图标的父节点（Layout 由你在预制体中配置）")]
    public RectTransform ingredientIconsRoot;

    [Header("原料格")]
    [Tooltip("每一格原料图标的预制体（需含至少一个 Image 用于显示 Sprite）。未赋值则不显示原料条，不报错。")]
    public GameObject ingredientCellPrefab;

    // 隐藏在面板外，用来记录自己的唯一条形码
    [HideInInspector]
    public string orderId;

    /// <summary>桌面订单条：用桌号 + 槽位生成展示用 OrderInstance（不参与全局核销）。</summary>
    public void SetupForTable(FryRecipeDatabase.FryRecipe recipe, int tableId, int slotIndex)
    {
        if (recipe == null)
            return;
        var pseudo = new OrderInstance(tableId, recipe, $"table_ui_{tableId}_{slotIndex}");
        Setup(pseudo);
    }

    /// <summary>
    /// 初始化卡片显示。联机：Guest 侧 OrderInstance 由 recipeName 还原 FryRecipe，与本字段一致，原料条与 Host 相同。
    /// </summary>
    public void Setup(OrderInstance order)
    {
        orderId = order.orderId;

        if (dishIcon != null && order.recipe != null && order.recipe.dishIcon != null)
            dishIcon.sprite = order.recipe.dishIcon;

        if (tableNumberText != null)
            tableNumberText.text = order.tableId.ToString();

        if (dishNameText != null)
        {
            if (order.recipe != null)
            {
                dishNameText.text = order.recipe.GetDisplayName();
                dishNameText.gameObject.SetActive(!string.IsNullOrWhiteSpace(dishNameText.text));
            }
            else
            {
                dishNameText.text = "";
                dishNameText.gameObject.SetActive(false);
            }
        }

        RebuildIngredientStrip(order.recipe);
    }

    void RebuildIngredientStrip(FryRecipeDatabase.FryRecipe recipe)
    {
        if (ingredientIconsRoot == null)
            return;

        ClearChildren(ingredientIconsRoot);

        if (ingredientCellPrefab == null)
        {
            ingredientIconsRoot.gameObject.SetActive(false);
            return;
        }

        if (recipe == null || recipe.orderCardIngredientDisplays == null)
        {
            ingredientIconsRoot.gameObject.SetActive(false);
            return;
        }

        bool any = false;
        foreach (var entry in recipe.orderCardIngredientDisplays)
        {
            if (entry == null || entry.icon == null)
                continue;
            // 每条配置只显示一个图标；count 仍保留在菜谱数据中供别处使用，此处不展示数量文字、不按数量复制格子
            if (InstantiateCellFromPrefab(entry.icon))
                any = true;
        }

        ingredientIconsRoot.gameObject.SetActive(any);
    }

    /// <summary>实例化一格并设置图标；失败时返回 false（不抛异常）。</summary>
    bool InstantiateCellFromPrefab(Sprite icon)
    {
        if (icon == null || ingredientCellPrefab == null)
            return false;

        GameObject go = Instantiate(ingredientCellPrefab, ingredientIconsRoot);
        if (go == null)
            return false;

        var img = go.GetComponentInChildren<Image>(true);
        if (img != null)
        {
            img.sprite = icon;
            img.preserveAspect = true;
            img.raycastTarget = false;
            return true;
        }

        // 无 Image 时销毁无效实例，避免留下空节点
        Destroy(go);
        return false;
    }

    void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }
}
