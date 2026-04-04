using UnityEngine;

/// <summary>
/// 电脑/终端：玩家在 Sensor 范围内空手按 K 打开商店。本体需带 Collider（触发器），
/// 所在 Layer 需在 PlayerInteractionSensor 的 Station Mask 内；无需 CarryableItem / ItemPlacePoint。
/// </summary>
public class ComputerStation : BaseStation
{
    [Header("Shop")]
    [Tooltip("场景中的 ShopUIController（一般挂在 ShopPanel 上）")]
    public ShopUIController shopUI;

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
            PlayerMoveRB move = interactor != null ? interactor.GetComponentInParent<PlayerMoveRB>() : null;
            shopUI.Open(true, move);
        }
    }

    public override void EndInteract(PlayerItemInteractor interactor)
    {
        // 松开 K 不关闭商店；由商店 UI 上的关闭按钮调用 ShopUIController.Close。
    }
}
