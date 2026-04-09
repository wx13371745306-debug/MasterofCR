using UnityEngine;
using Mirror;

public abstract class BaseStation : NetworkBehaviour, IInteractiveStation
{
    [Header("Common")]
    public GameObject highlightObject;
    public bool debugLog = true;

    protected bool isSensorTargeted = false;
    protected PlayerItemInteractor cachedInteractor;

    // 获取当前互动的玩家属性
    public PlayerAttributes CurrentPlayerAttributes
    {
        get
        {
            if (cachedInteractor != null)
                return cachedInteractor.GetComponent<PlayerAttributes>();
            return null;
        }
    }

    protected virtual void Awake()
    {
        if (cachedInteractor == null)
            cachedInteractor = FindFirstObjectByType<PlayerItemInteractor>();
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