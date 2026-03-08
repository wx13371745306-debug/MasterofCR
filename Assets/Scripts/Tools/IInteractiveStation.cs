using UnityEngine;

public interface IInteractiveStation
{
    bool CanInteract(PlayerItemInteractor interactor);
    void BeginInteract(PlayerItemInteractor interactor);
    void EndInteract(PlayerItemInteractor interactor);
    void SetSensorHighlight(bool on);
}