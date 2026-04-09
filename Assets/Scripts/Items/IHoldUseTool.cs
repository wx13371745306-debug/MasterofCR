public interface IHoldUseTool
{
    bool TryUse(PlayerItemInteractor interactor, PlayerInteractionSensor sensor, CarryableItem selfItem);
}