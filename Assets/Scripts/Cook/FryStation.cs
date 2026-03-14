using UnityEngine;

public class FryStation : BaseStation
{
    public ItemPlacePoint potPlacePoint;
    public float frySpeed = 20f;

    private bool isInteracting = false;

    FryPot CurrentPot()
    {
        if (potPlacePoint == null) return null;

        CarryableItem item = potPlacePoint.CurrentItem;
        if (item == null) return null;

        return item.GetComponent<FryPot>();
    }

    public override bool CanInteract(PlayerItemInteractor interactor)
    {
        FryPot pot = CurrentPot();
        if (pot == null) return false;

        // 没有食材、已经做完，都不该继续炒
        if (!pot.CanReceiveProgress()) return false;

        return true;
    }

    public override void BeginInteract(PlayerItemInteractor interactor)
    {
        isInteracting = true;
    }

    public override void EndInteract(PlayerItemInteractor interactor)
    {
        isInteracting = false;
    }

    void Update()
    {
        if (!isInteracting) return;

        FryPot pot = CurrentPot();
        if (pot == null) return;

        pot.AddProgress(frySpeed * Time.deltaTime);
    }
}