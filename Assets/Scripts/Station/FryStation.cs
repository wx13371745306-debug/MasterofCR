using Mirror;
using UnityEngine;

public class FryStation : BaseStation
{
    [Header("Station Settings")]
    public ItemPlacePoint potPlacePoint;
    public float frySpeed = 20f;

    [Header("Visual Effects")]
    [Tooltip("在此处拖入你的煎炸特效预制体或场景中的子物体")]
    public GameObject fryEffect;

    FryPot _lastPot;

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
        // 煎炸模拟仅服务端权威；客户端由 FryPotNetworkSync 镜像状态
        if (!NetworkServer.active) return;

        FryPot pot = CurrentPot();

        if (_lastPot != null && _lastPot != pot)
            _lastPot.NotifyStationHeat(this, false);
        _lastPot = pot;

        if (pot != null)
            pot.NotifyStationHeat(this, true);

        if (pot == null)
        {
            if (fryEffect != null && fryEffect.activeSelf)
                fryEffect.SetActive(false);
            return;
        }

        if (pot.CanReceiveProgress())
        {
            if (fryEffect != null && !fryEffect.activeSelf)
                fryEffect.SetActive(true);
            pot.AddProgress(frySpeed * Time.deltaTime);
        }
        else if (fryEffect != null && fryEffect.activeSelf)
        {
            fryEffect.SetActive(false);
        }

        // 仅在锅置于本站上时推进糊菜倒计时
        if (pot.IsBurnCountdown)
            pot.AdvanceBurn(Time.deltaTime);
    }
}