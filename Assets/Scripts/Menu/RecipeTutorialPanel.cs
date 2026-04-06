using UnityEngine;
using UnityEngine.UI;

public class RecipeTutorialPanel : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Button closeButton;

    public void Open(FryRecipeDatabase.FryRecipe recipe)
    {
        Debug.Log($"[Tutorial] Open: '{recipe?.recipeName ?? "NULL"}' | panelRoot={(panelRoot != null ? panelRoot.name : "NULL")}");
        if (panelRoot == null)
        {
            Debug.LogWarning("[Tutorial] panelRoot 未赋值，无法打开制作方法面板！");
            return;
        }
        panelRoot.SetActive(true);
    }

    public void Close()
    {
        Debug.Log("[Tutorial] Close 被调用");
        if (panelRoot == null) return;
        panelRoot.SetActive(false);
    }

    public void OnCloseClicked()
    {
        Debug.Log("[Tutorial] 关闭按钮被点击");
        Close();
    }
}
