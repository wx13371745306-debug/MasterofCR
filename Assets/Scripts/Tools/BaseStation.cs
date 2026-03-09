using UnityEngine;

public abstract class BaseStation : MonoBehaviour, IInteractiveStation
{
    [Header("Common")]
    public GameObject highlightObject;
    public bool debugLog = true;

    protected bool isSensorTargeted = false;
    protected PlayerItemInteractor cachedInteractor;

    protected virtual void Awake()
    {
        if (cachedInteractor == null)
            cachedInteractor = FindObjectOfType<PlayerItemInteractor>();
    }

    public virtual void SetSensorHighlight(bool on)
    {
        isSensorTargeted = on;

        if (highlightObject != null)
            highlightObject.SetActive(on);

        OnSensorHighlightChanged();
    }

    protected virtual void OnSensorHighlightChanged()
    {
    }

    public virtual bool CanInteract(PlayerItemInteractor interactor)
    {
        return true;
    }

    public abstract void BeginInteract(PlayerItemInteractor interactor);
    public abstract void EndInteract(PlayerItemInteractor interactor);
}