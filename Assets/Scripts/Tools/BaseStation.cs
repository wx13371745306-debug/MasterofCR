using UnityEngine;

public abstract class BaseStation : MonoBehaviour, IInteractiveStation
{
    [Header("Common")]
    public GameObject highlightObject;
    public bool debugLog = true;

    public virtual void SetSensorHighlight(bool on)
    {
        if (highlightObject != null)
            highlightObject.SetActive(on);
    }

    public virtual bool CanInteract(PlayerItemInteractor interactor)
    {
        return true;
    }

    public abstract void BeginInteract(PlayerItemInteractor interactor);
    public abstract void EndInteract(PlayerItemInteractor interactor);
}