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

    // Interactor 叫我亮我就亮
    public virtual void SetSensorHighlight(bool on)
    {
        isSensorTargeted = on;

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