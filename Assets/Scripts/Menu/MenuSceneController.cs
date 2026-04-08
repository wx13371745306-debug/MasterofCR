using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MenuSceneController : MonoBehaviour
{
    [Header("Debug")]
    public bool debugLog = false;

    [Header("Data")]
    public MenuSO menuSO;
    [Tooltip("羁绊状态 SO，进入主菜单时重置")]
    public BondActivationStateSO bondState;

    [Header("UI")]
    public Button selectMenuButton;
    public Button startGameButton;
    public TextMeshProUGUI errorText;

    [Header("References")]
    public MenuSelectionUIController menuSelectionUI;
    [Tooltip("主界面上的羁绊列表 UI（可选），选菜后刷新")]
    public BondListUIController mainBondListUI;

    [Header("Settings")]
    [Tooltip("进入游戏前要求的最低菜谱数量")]
    [Min(1)]
    public int minRecipes = 1;

    [Tooltip("游戏场景名称")]
    public string gameSceneName = "SampleScene";

    void Start()
    {
        if (menuSO != null)
        {
            menuSO.Clear();
            if (debugLog) Debug.Log("[MenuScene] MenuSO 已重置为空");
        }
        if (bondState != null)
        {
            bondState.ResetAll();
            if (debugLog) Debug.Log("[MenuScene] BondActivationStateSO 已重置为全部未激活");
        }
        RefreshMainBondUI();

        if (menuSelectionUI != null)
            menuSelectionUI.OnPanelClosed += OnSelectionPanelClosed;
    }

    void OnDestroy()
    {
        if (menuSelectionUI != null)
            menuSelectionUI.OnPanelClosed -= OnSelectionPanelClosed;
    }

    void OnSelectionPanelClosed()
    {
        if (debugLog) Debug.Log("[MenuScene] 选菜面板关闭，刷新主界面羁绊显示");
        RefreshMainBondUI();
    }

    public void OnSelectMenuClicked()
    {
        if (debugLog) Debug.Log($"[MenuScene] 选菜按钮被点击 | menuSelectionUI={(menuSelectionUI != null ? menuSelectionUI.name : "NULL")}");
        if (menuSelectionUI != null)
            menuSelectionUI.Open();
        else
            Debug.LogWarning("[MenuScene] menuSelectionUI 未赋值，无法打开选菜界面！请在 Inspector 中拖入引用。");
        ClearError();
    }

    public void OnStartGameClicked()
    {
        if (debugLog) Debug.Log($"[MenuScene] 开始游戏按钮被点击 | menuSO={(menuSO != null ? menuSO.name : "NULL")}");
        if (menuSO == null)
        {
            ShowError("未配置 MenuSO");
            Debug.LogWarning("[MenuScene] menuSO 未赋值！请在 Inspector 中拖入 MenuSO 资产。");
            return;
        }

        if (debugLog) Debug.Log($"[MenuScene] 当前已选菜谱数: {menuSO.selectedRecipes.Count}, 最低要求: {minRecipes}");
        if (menuSO.selectedRecipes.Count < minRecipes)
        {
            ShowError($"请至少选择 {minRecipes} 道菜谱后再开始游戏");
            return;
        }

        if (debugLog) Debug.Log($"[MenuScene] 验证通过，正在加载场景: {gameSceneName}");
        SceneManager.LoadScene(gameSceneName);
    }

    void RefreshMainBondUI()
    {
        if (mainBondListUI != null)
            mainBondListUI.Refresh();
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
