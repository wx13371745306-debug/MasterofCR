using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MenuSceneController : MonoBehaviour
{
    [Header("Data")]
    public MenuSO menuSO;

    [Header("UI")]
    public Button selectMenuButton;
    public Button startGameButton;
    public TextMeshProUGUI errorText;

    [Header("References")]
    public MenuSelectionUIController menuSelectionUI;

    [Header("Settings")]
    [Tooltip("进入游戏前要求的最低菜谱数量")]
    [Min(1)]
    public int minRecipes = 1;

    [Tooltip("游戏场景名称")]
    public string gameSceneName = "SampleScene";

    public void OnSelectMenuClicked()
    {
        Debug.Log($"[MenuScene] 选菜按钮被点击 | menuSelectionUI={(menuSelectionUI != null ? menuSelectionUI.name : "NULL")}");
        if (menuSelectionUI != null)
            menuSelectionUI.Open();
        else
            Debug.LogWarning("[MenuScene] menuSelectionUI 未赋值，无法打开选菜界面！请在 Inspector 中拖入引用。");
        ClearError();
    }

    public void OnStartGameClicked()
    {
        Debug.Log($"[MenuScene] 开始游戏按钮被点击 | menuSO={(menuSO != null ? menuSO.name : "NULL")}");
        if (menuSO == null)
        {
            ShowError("未配置 MenuSO");
            Debug.LogWarning("[MenuScene] menuSO 未赋值！请在 Inspector 中拖入 MenuSO 资产。");
            return;
        }

        Debug.Log($"[MenuScene] 当前已选菜谱数: {menuSO.selectedRecipes.Count}, 最低要求: {minRecipes}");
        if (menuSO.selectedRecipes.Count < minRecipes)
        {
            ShowError($"请至少选择 {minRecipes} 道菜谱后再开始游戏");
            return;
        }

        Debug.Log($"[MenuScene] 验证通过，正在加载场景: {gameSceneName}");
        SceneManager.LoadScene(gameSceneName);
    }

    void ShowError(string msg)
    {
        if (errorText != null)
            errorText.text = msg;
    }

    void ClearError()
    {
        if (errorText != null)
            errorText.text = string.Empty;
    }
}
