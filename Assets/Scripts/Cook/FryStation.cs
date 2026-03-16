using UnityEngine;

public class FryStation : BaseStation
{
    [Header("Station Settings")]
    public ItemPlacePoint potPlacePoint;
    public float frySpeed = 20f;

    [Header("Visual Effects")]
    [Tooltip("在此处拖入你的煎炸特效预制体或场景中的子物体")]
    public GameObject fryEffect; 

    private bool isInteracting = false;

    protected override void Awake()
    {
        base.Awake();
        // 初始化时确保特效是关闭的
        if (fryEffect != null)
        {
            fryEffect.SetActive(false);
        }
    }

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

        // 玩家开始互动：激活特效
        // 由于勾选了 Play on Awake，SetActive(true) 会自动触发播放
        if (fryEffect != null)
        {
            fryEffect.SetActive(true);
        }
    }

    public override void EndInteract(PlayerItemInteractor interactor)
    {
        isInteracting = false;

        // 玩家停止互动：关闭特效
        if (fryEffect != null)
        {
            fryEffect.SetActive(false);
        }
    }

    void Update()
    {
        // 如果互动的过程中由于某种原因（如锅被拿走）导致无法互动，也应关闭特效
        if (!isInteracting) 
        {
            return;
        }

        FryPot pot = CurrentPot();
        if (pot == null || !pot.CanReceiveProgress())
        {
            // 安全检查：如果锅被中途拿走或烹饪完成，强制结束特效
            if (fryEffect != null && fryEffect.activeSelf) fryEffect.SetActive(false);
            return;
        }

        pot.AddProgress(frySpeed * Time.deltaTime);
    }
}