using UnityEngine;
using Mirror;

/// <summary>
/// 干净盘出口「发放台」：与 SupplyBox 思路类似，玩家面对出口按 J 向服务端请求生成一个网络盘子并交到手上；
/// 不负责洗碗逻辑，库存与预制体均引用同一场景内的 <see cref="DishWashingStation"/>。
/// 请在出口区域单独挂 Collider（stationMask 可检测），与洗碗交互（K）分区。
/// </summary>
public class CleanPlateDispenserStation : BaseStation
{
    [Header("Refs")]
    [Tooltip("同预制体/场景内的洗碗池（库存 syncCleanPlateCount、cleanPlatePrefab 来源）")]
    public DishWashingStation dishWashing;

    protected override void Awake()
    {
        base.Awake();
        if (dishWashing == null)
            dishWashing = transform.root.GetComponentInChildren<DishWashingStation>(true);
        if (dishWashing == null)
            dishWashing = FindFirstObjectByType<DishWashingStation>();
    }

    public bool CanDispense()
    {
        return dishWashing != null && dishWashing.CleanPlateStock > 0;
    }

    public override bool CanInteract(PlayerItemInteractor interactor)
    {
        return CanDispense();
    }

    public override void BeginInteract(PlayerItemInteractor interactor) { }

    public override void EndInteract(PlayerItemInteractor interactor) { }

    /// <summary>仅服务端：扣库存并生成盘子网络物体。</summary>
    public GameObject ServerDispenseOne()
    {
        if (!NetworkServer.active || dishWashing == null) return null;
        return dishWashing.ServerTryDispenseCleanPlate();
    }
}
