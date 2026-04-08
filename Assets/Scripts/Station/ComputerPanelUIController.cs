using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 电脑引导主面板，用于分配玩家是要打开商店还是菜单。
/// </summary>
public class ComputerPanelUIController : MonoBehaviour
{
    public bool debugLog = false;

    [Header("UI References")]
    [Tooltip("此控制器的面板根节点")]
    public GameObject panelRoot;
    public Button shopButton;
    public Button menuButton;
    public Button closeButton;

    [Header("Linked Systems")]
    public ShopUIController shopUI;
    public MenuSelectionUIController menuUI;

    private PlayerMoveRB activeLockedPlayerMove;
    private CursorLockMode savedLockMode;
    private bool savedCursorVisible;

    public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

    void Start()
    {
        if (shopButton != null) shopButton.onClick.AddListener(OnShopButtonClicked);
        if (menuButton != null) menuButton.onClick.AddListener(OnMenuButtonClicked);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
    }

    /// <param name="playerMoveOverride">打开时锁死的人，可传空</param>
    public void Open(PlayerMoveRB playerMoveOverride = null)
    {
        if (panelRoot == null) return;
        
        if (debugLog) Debug.Log("[ComputerPanelUIController] 执行了某操作: 打开引导面板");

        // 仅由电脑第一次打开时锁定处理光标状态
        savedLockMode = Cursor.lockState;
        savedCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        activeLockedPlayerMove = playerMoveOverride;
        if (activeLockedPlayerMove != null)
        {
            activeLockedPlayerMove.SetHorizontalPositionLocked(true);
        }

        panelRoot.SetActive(true);
    }

    public void Close()
    {
        if (panelRoot == null || !panelRoot.activeSelf) return;

        if (debugLog) Debug.Log("[ComputerPanelUIController] 执行了某操作: 关闭引导面板");

        panelRoot.SetActive(false);

        // 防御性关闭子菜单（实际上应该由子菜单自身管理并回到本系统，但这里兜底保证不卡死）
        if (shopUI != null && shopUI.IsOpen) shopUI.Close();
        if (menuUI != null && menuUI.IsOpen) menuUI.Close();

        if (activeLockedPlayerMove != null)
        {
            activeLockedPlayerMove.SetHorizontalPositionLocked(false);
            activeLockedPlayerMove = null;
        }

        Cursor.lockState = savedLockMode;
        Cursor.visible = savedCursorVisible;
    }

    private void OnShopButtonClicked()
    {
        if (shopUI == null) return;
        if (debugLog) Debug.Log("[ComputerPanelUIController] 执行了某操作: 打开商店UI");
        
        // 传递 false 给 shopUI 锁定位置选项，因为本主版已经处理了玩家位置和鼠标锁定
        shopUI.Open(false, null);
        panelRoot.SetActive(false); 
    }

    private void OnMenuButtonClicked()
    {
        if (menuUI == null) return;

        // 判定现在的阶段
        DayCyclePhase phase = DayCycleManager.Instance != null ? DayCycleManager.Instance.Phase : DayCyclePhase.DayZero;
        
        // Phase 判断：包含 DayZero 和 Prep 以及 Closing (打烊阶段)
        if (phase == DayCyclePhase.DayZero || phase == DayCyclePhase.Prep || phase == DayCyclePhase.Closing || phase == DayCyclePhase.NextDayTransition)
        {
            if (debugLog) Debug.Log("[ComputerPanelUIController] 执行了某操作: 打烊阶段允许编辑，打开菜单UI");
            menuUI.Open();
            panelRoot.SetActive(false); 
        }
        else
        {
            if (debugLog) Debug.Log($"[ComputerPanelUIController] 执行了某操作: 当前阶段 {phase} 拒绝了菜单操作请求");
            Debug.LogWarning("只有在打烊准备阶段才可以编辑菜单！");
            
            // TODO: 这里可以加上 UI 文字提示反馈给玩家
        }
    }
}
