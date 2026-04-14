using UnityEngine;
using Mirror;

/// <summary>
/// 垃圾桶：J/K 判定顺序为 FryPot（含空锅吞输入）→ DishRecipeTag 转空盘 → BinProtected 吞输入无效果；
/// 其它物体放入 <see cref="binPlacePoint"/> 会销毁（受保护物见 <see cref="ShouldSuppressTrashPlacement"/>）。
/// </summary>
public class BinStation : BaseStation
{
    [Header("Bin Settings")]
    [Tooltip("垃圾桶的放置点，任何物品放上来都会被销毁")]
    public ItemPlacePoint binPlacePoint;

    [Tooltip("空盘子预制体，用于将菜品（Dish）转回空盘")]
    public GameObject cleanPlatePrefab;

    protected override void Awake()
    {
        base.Awake();
        if (binPlacePoint != null)
            binPlacePoint.OnItemPlacedEvent += OnItemDroppedIntoBin;
    }


    void OnDestroy()
    {
        if (binPlacePoint != null)
            binPlacePoint.OnItemPlacedEvent -= OnItemDroppedIntoBin;
    }

    void OnItemDroppedIntoBin(CarryableItem item)
    {
        if (item == null) return;

        if (ShouldSuppressTrashPlacement(item))
        {
            Debug.LogWarning(
                $"[BinStation] 受保护物品不应经槽位销毁: {item.name}。已清除槽位并掉落地面，请检查 ShouldSuppressReleaseIntoBin。",
                this);
            binPlacePoint.ClearOccupant();
            item.DropToGround();
            return;
        }

        binPlacePoint.ClearOccupant();

        if (debugLog)
            Debug.Log($"<color=#FF4444>[BinStation]</color> 销毁放入的物品: {item.name}");

        if (NetworkServer.active)
            NetworkServer.Destroy(item.gameObject);
        else
            Destroy(item.gameObject);
    }

    public override bool CanInteract(PlayerItemInteractor interactor)
    {
        return false;
    }

    public override void BeginInteract(PlayerItemInteractor interactor) { }
    public override void EndInteract(PlayerItemInteractor interactor) { }

    /// <summary>
    /// 手持物不可被「放进垃圾桶槽位销毁」：锅、成品菜组件、或勾选 <see cref="ItemCategory.BinProtected"/>。
    /// </summary>
    public static bool ShouldSuppressTrashPlacement(CarryableItem item)
    {
        if (item == null) return false;
        if (item.GetComponent<FryPot>() != null) return true;
        if (item.GetComponent<DishRecipeTag>() != null) return true;
        return item.HasBinProtectedCategory();
    }

    /// <summary>
    /// 面对本 BinStation：先 <see cref="TryDumpHeldItem"/>（锅/菜）；再处理 <see cref="ItemCategory.BinProtected"/>。
    /// </summary>
    public bool TryHandleFacing(PlayerItemInteractor interactor, CarryableItem heldItem)
    {
        if (TryDumpHeldItem(interactor, heldItem))
            return true;
        // BinProtected 优先于 Accessory 短路，避免饰品等仍需「面对 Bin 无反应」
        if (heldItem.HasBinProtectedCategory())
        {
            if (debugLog)
                Debug.Log("<color=#FF4444>[BinStation]</color> BinProtected 物品，已忽略本次 J/K。", this);
            return true;
        }
        if (heldItem is AccessoryItem) return false;
        return false;
    }

    public bool TryDumpHeldItem(PlayerItemInteractor interactor, CarryableItem heldItem)
    {
        if (interactor == null || heldItem == null) return false;
        if (heldItem is AccessoryItem) return false;

        FryPot pot = heldItem.GetComponent<FryPot>();
        if (pot != null)
            return TryDumpPot(pot, heldItem, interactor);

        // 与 BinProtected 并存时优先成品菜逻辑（勿在成品菜上叠 BinProtected）
        DishRecipeTag dishTag = heldItem.GetComponent<DishRecipeTag>();
        if (dishTag != null)
            return TryConvertDishToPlate(interactor, heldItem);

        return false;
    }

    bool TryDumpPot(FryPot pot, CarryableItem heldItem, PlayerItemInteractor interactor)
    {
        if (!pot.CanDump())
        {
            if (debugLog)
                Debug.Log("<color=#FF4444>[BinStation]</color> 空锅，无内容可清空。", this);
            return true;
        }

        if (NetworkClient.active)
        {
            PlayerNetworkController net = interactor != null ? interactor.GetComponent<PlayerNetworkController>() : null;
            if (net != null)
            {
                net.CmdRequestFryPotDump(heldItem.gameObject);
                if (debugLog)
                    Debug.Log("<color=#FF4444>[BinStation]</color> 已发起 CmdRequestFryPotDump。", this);
                return true;
            }
            if (debugLog)
                Debug.LogWarning("<color=#FF4444>[BinStation]</color> 无 PlayerNetworkController，无法倒锅。", this);
            return false;
        }

        pot.ForceClear();

        if (debugLog)
            Debug.Log("<color=#FF4444>[BinStation]</color> 已清空锅内所有食材和进度。", this);

        return true;
    }

    bool TryConvertDishToPlate(PlayerItemInteractor interactor, CarryableItem dish)
    {
        if (cleanPlatePrefab == null)
        {
            if (debugLog)
                Debug.LogWarning("<color=#FF4444>[BinStation]</color> cleanPlatePrefab 未配置，无法将菜品转回盘子！");
            return false;
        }

        Transform holdPoint = interactor.GetHoldPoint();
        if (holdPoint == null) return false;

        // 联机：仅服务端生成/销毁，由 RpcAcknowledgePickUp 同步各端手持
        if (NetworkClient.active)
        {
            PlayerNetworkController net = interactor.GetComponent<PlayerNetworkController>();
            if (net == null)
            {
                if (debugLog)
                    Debug.LogWarning("<color=#FF4444>[BinStation]</color> 无 PlayerNetworkController，无法联机转盘子。");
                return false;
            }

            NetworkIdentity binRoot = TryGetNetworkIdentityInParents(transform);
            if (binRoot == null)
            {
                Debug.LogError(
                    "<color=#FF4444>[BinStation]</color> 联机需要本物体位于带 NetworkIdentity 的层级下（向上查找无 NI）。请在垃圾桶根挂 NetworkIdentity。",
                    this);
                return false;
            }

            net.CmdRequestBinDishToPlate(binRoot.netId, dish.gameObject);
            if (debugLog)
                Debug.Log("<color=#FF4444>[BinStation]</color> 已发起 CmdRequestBinDishToPlate。", this);
            return true;
        }

        // 单机 / 无 Mirror 客户端
        GameObject plateObj = Instantiate(cleanPlatePrefab);
        CarryableItem plateItem = plateObj.GetComponent<CarryableItem>();

        if (plateItem == null)
        {
            if (debugLog)
                Debug.LogError("<color=#FF4444>[BinStation]</color> cleanPlatePrefab 上缺少 CarryableItem 组件！");
            Destroy(plateObj);
            return false;
        }

        if (NetworkServer.active)
            NetworkServer.Spawn(plateObj);

        plateItem.BeginHold(holdPoint);
        interactor.ReplaceHeldItem(plateItem);

        if (debugLog)
            Debug.Log($"<color=#FF4444>[BinStation]</color> 菜品 '{dish.name}' 已转回空盘子。");

        if (NetworkServer.active)
            NetworkServer.Destroy(dish.gameObject);
        else
            Destroy(dish.gameObject);
        return true;
    }

    static NetworkIdentity TryGetNetworkIdentityInParents(Transform start)
    {
        Transform t = start;
        while (t != null)
        {
            NetworkIdentity ni = t.GetComponent<NetworkIdentity>();
            if (ni != null) return ni;
            t = t.parent;
        }
        return null;
    }
}
