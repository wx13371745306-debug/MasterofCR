using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MenuRecipeCardView : MonoBehaviour
{
    [Header("Debug")]
    public bool debugLog = false;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI priceText;
    [SerializeField] private Button cardButton;
    [SerializeField] private Button tutorialButton;

    [Header("选中状态视觉")]
    [SerializeField] private GameObject selectedHighlight;

    private FryRecipeDatabase.FryRecipe recipe;
    private MenuSelectionUIController controller;

    public void Setup(FryRecipeDatabase.FryRecipe recipe, MenuSelectionUIController controller)
    {
        this.recipe = recipe;
        this.controller = controller;

        if (nameText != null)
            nameText.text = recipe.GetDisplayName();
        if (iconImage != null)
        {
            iconImage.sprite = recipe.dishIcon;
            iconImage.enabled = recipe.dishIcon != null;
        }
        if (priceText != null)
            priceText.text = $"${recipe.price}";

        bool isSelected = controller != null && controller.IsSelected(recipe);
        if (debugLog) Debug.Log($"[MenuCard] Setup: '{recipe.GetDisplayName()}' | 价格={recipe.price} | 图标={(recipe.dishIcon != null ? "有" : "无")} | 已选={isSelected}");

        RefreshSelectedVisual();
    }

    public void OnCardClicked()
    {
        if (debugLog) Debug.Log($"[MenuCard] 卡片被点击: '{recipe?.recipeName ?? "NULL"}' | controller={(controller != null ? "有" : "NULL")}");
        if (controller != null && recipe != null)
            controller.ToggleRecipe(recipe);
        else
            Debug.LogWarning("[MenuCard] 点击无效：controller 或 recipe 为空");
    }

    public void OnTutorialClicked()
    {
        if (debugLog) Debug.Log($"[MenuCard] 制作方法按钮被点击: '{recipe?.recipeName ?? "NULL"}'");
        if (controller != null && recipe != null)
            controller.OpenTutorial(recipe);
    }

    void RefreshSelectedVisual()
    {
        if (selectedHighlight != null && controller != null)
            selectedHighlight.SetActive(controller.IsSelected(recipe));
    }
}
