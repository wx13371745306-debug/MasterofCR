using UnityEngine;

/// <summary>
/// 电脑/终端：玩家在 Sensor 范围内空手按 K 打开商店。本体需带 Collider（触发器），
/// 所在 Layer 需在 PlayerInteractionSensor 的 Station Mask 内；无需 CarryableItem / ItemPlacePoint。
/// 玩家打开商店后仍可自由移动，离开一定距离后自动关闭商店。
/// </summary>
public class ComputerStation : BaseStation
{
    [Header("Shop")]
    [Tooltip("场景中的 ShopUIController（一般挂在 ShopPanel 上）")]
    public ShopUIController shopUI;

    [Header("Auto-Close")]
    [Tooltip("玩家离电脑超过此距离时自动关闭商店")]
    [SerializeField] private float autoCloseDistance = 5f;

    private Transform trackedPlayer;

    public override bool CanInteract(PlayerItemInteractor interactor)
    {
        if (interactor == null) return false;
        if (interactor.IsHoldingItem()) return false;
        return shopUI != null;
    }

    public override void BeginInteract(PlayerItemInteractor interactor)
    {
        cachedInteractor = interactor;
        if (shopUI != null)
        {
            trackedPlayer = interactor != null ? interactor.transform.root : null;
            shopUI.Open(false, null);
        }
    }

    public override void EndInteract(PlayerItemInteractor interactor)
    {
        // 松开 K 不关闭商店；由距离检测或商店 UI 上的关闭按钮关闭。
    }

    private void Update()
    {
        if (shopUI == null || trackedPlayer == null) return;

        if (!shopUI.IsOpen)
        {
            trackedPlayer = null;
            return;
        }

        float dist = Vector3.Distance(trackedPlayer.position, transform.position);
        if (dist > autoCloseDistance)
        {
            shopUI.Close();
            trackedPlayer = null;
        }
    }
}
