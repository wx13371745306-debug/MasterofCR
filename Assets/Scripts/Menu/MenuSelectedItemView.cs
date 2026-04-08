using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MenuSelectedItemView : MonoBehaviour
{
    [Header("Debug")]
    public bool debugLog = false;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private Image iconImage;
    [SerializeField] private Button removeButton;

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
    }

    public void OnRemoveClicked()
    {
        if (debugLog) Debug.Log($"[MenuSelected] 移除按钮被点击: '{recipe?.recipeName ?? "NULL"}'");
        if (controller != null && recipe != null)
            controller.ToggleRecipe(recipe);
        else
            Debug.LogWarning("[MenuSelected] 移除无效：controller 或 recipe 为空");
    }
}
