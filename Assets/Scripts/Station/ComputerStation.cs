using UnityEngine;

/// <summary>
/// 电脑/终端：玩家在 Sensor 范围内空手按 K 打开商店与菜单。本体需带 Collider（触发器），
/// 所在 Layer 需在 PlayerInteractionSensor 的 Station Mask 内；无需 CarryableItem / ItemPlacePoint。
/// 玩家打开电脑后仍可自由移动，离开一定距离后自动关闭电脑。
/// </summary>
public class ComputerStation : BaseStation
{
    [Header("Computer UI Controller")]
    [Tooltip("场景中新的包含了商店和菜单按钮的电脑主面板控制类")]
    public ComputerPanelUIController panelController;

    [Header("Auto-Close")]
    [Tooltip("玩家离电脑超过此距离时自动关闭电脑界面")]
    [SerializeField] private float autoCloseDistance = 5f;

    private Transform trackedPlayer;

    public override bool CanInteract(PlayerItemInteractor interactor)
    {
        if (interactor == null) return false;
        if (interactor.IsHoldingItem()) return false;
        return panelController != null;
    }

    public override void BeginInteract(PlayerItemInteractor interactor)
    {
        cachedInteractor = interactor;
        if (panelController != null)
        {
            if (debugLog) Debug.Log("[ComputerStation] 执行了某操作: 开始玩家交互打开电脑主面板");
            trackedPlayer = interactor != null ? interactor.transform.root : null;
            // 对于旧版有 playerMoveFallback 需求的话可以在此处理，但因为 ComputerStation 原先是没有 playerMoveOverride 给出去的。
            // 故传入 null。
            panelController.Open(null);
        }
    }

    public override void EndInteract(PlayerItemInteractor interactor)
    {
        // 松开 K 不关闭电脑界面；由距离检测或 UI 上的关闭按钮关闭。
    }

    /// <summary>由 UI 按钮调用：关闭商店/菜单子面板并显示电脑初始主界面。</summary>
    public void ReturnToComputerHome()
    {
        if (panelController != null)
            panelController.ReturnToComputerHome();
    }

    private void Update()
    {
        if (panelController == null || trackedPlayer == null) return;

        // 如果用户在商店子界面或者菜单子界面中，那 IsOpen 检测可能要深入一点。
        // 为了稳定兼容如果电脑面板或它的子集被展示，即不清除trackedPlayer。
        bool isAnyPanelOpen = panelController.IsOpen 
                           || (panelController.shopUI != null && panelController.shopUI.IsOpen)
                           || (panelController.menuUI != null && panelController.menuUI.IsOpen);

        if (!isAnyPanelOpen)
        {
            trackedPlayer = null;
            return;
        }

        float dist = Vector3.Distance(trackedPlayer.position, transform.position);
        if (dist > autoCloseDistance)
        {
            if (debugLog) Debug.Log($"[ComputerStation] 执行了某操作: 玩家距离过远（{dist} > {autoCloseDistance}），强制关闭电脑面板");
            
            // 兜底全关闭
            if (panelController.IsOpen) panelController.Close();
            if (panelController.shopUI != null && panelController.shopUI.IsOpen) panelController.shopUI.Close();
            if (panelController.menuUI != null && panelController.menuUI.IsOpen) panelController.menuUI.Close();
            
            trackedPlayer = null;
        }
    }
}
