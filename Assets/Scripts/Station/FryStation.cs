using UnityEngine;

public class FryStation : BaseStation
{
    [Header("Station Settings")]
    public ItemPlacePoint potPlacePoint;
    public float frySpeed = 20f;

    [Header("Visual Effects")]
    [Tooltip("在此处拖入你的煎炸特效预制体或场景中的子物体")]
    public GameObject fryEffect; 


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
        // 改为全自动后，玩家不再需要对其按交互键来炒菜
        return false;
    }

    public override void BeginInteract(PlayerItemInteractor interactor)
    {
        // 空实现
    }

    public override void EndInteract(PlayerItemInteractor interactor)
    {
        // 空实现
    }

    void Update()
    {
        FryPot pot = CurrentPot();

        // 如果没有锅，或者锅不能接收进度（没有食材或已做完）
        if (pot == null || !pot.CanReceiveProgress())
        {
            // 关闭特效
            if (fryEffect != null && fryEffect.activeSelf)
            {
                fryEffect.SetActive(false);
            }
            return;
        }

        // 走到这里说明锅里有食材且未熟
        // 开启特效
        if (fryEffect != null && !fryEffect.activeSelf)
        {
            fryEffect.SetActive(true);
        }

        // 自动增加进度
        pot.AddProgress(frySpeed * Time.deltaTime);
    }
}