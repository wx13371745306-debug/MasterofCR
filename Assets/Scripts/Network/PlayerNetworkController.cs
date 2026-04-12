using UnityEngine;
using Mirror;

/// <summary>
/// 玩家网络的本地总控中枢 (Step 3 & 4 核心安全保障)。
/// 用于剥夺远端玩家的输入权限，绑定本地玩家的摄像机，并充当网络交互请求的唯一代理源。
/// 严格满足最小化破坏原则（Bypass策略）。
/// </summary>
public class PlayerNetworkController : NetworkBehaviour
{
    [Header("Debug Settings")]
    public bool debugLog = true;

    private PlayerMoveRB moveRB;
    private PlayerItemInteractor interactor;

    private void Awake()
    {
        moveRB = GetComponent<PlayerMoveRB>();
        interactor = GetComponent<PlayerItemInteractor>();
    }

    private void Start()
    {
        // 如果是单机（无NetworkManager活跃），放行所有逻辑。
        if (!NetworkClient.active && !NetworkServer.active) 
            return;

        // 如果是联网模式：只给“本地玩家自己”保留组件。剥夺“其他远程镜像”的逻辑控制组件。
        // 这从源头上阻断了其他客户端或主机模拟玩家时触发本地键盘输入事件！
        if (!isLocalPlayer)
        {
            if (debugLog)
                Debug.Log($"<color=#FF0000>[PlayerNetworkController]</color> 剥夺远端玩家({netId})的控制权限与输入组件。");
                
            if (moveRB != null) moveRB.enabled = false;
            // 若保留物理特性可将 rigidbody 转为 kinematic，阻止远程外力
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;

            if (interactor != null) interactor.enabled = false;
        }
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        
        if (debugLog)
            Debug.Log($"<color=#00FF00>[PlayerNetworkController]</color> 本地玩家({netId})创建完成！正在绑定专属摄像机...");

        // 统一在网络世界中绑定全局摄像机给本地玩家的实体
        CameraFollowRig camRig = UnityEngine.Object.FindAnyObjectByType<CameraFollowRig>();
        if (camRig != null)
        {
            camRig.target = this.transform;
            camRig.alwaysFollow = true; 
            
            if (debugLog) Debug.Log("[PlayerNetworkController] 摄像机绑定成功！");
        }
        else
        {
            Debug.LogWarning("[PlayerNetworkController] 出错：未能找到场景中的 CameraFollowRig！请确保存在。");
        }

        // 将本地玩家注入 DayCycleManager，使每日传送（TeleportPlayerToSpawn）在联机和假单机模式下都能正常工作
        if (DayCycleManager.Instance != null)
        {
            DayCycleManager.Instance.SetPlayerObject(this.gameObject);

            // 同时注入出生点：优先用场景中的 NetworkStartPosition
            NetworkStartPosition nsp = UnityEngine.Object.FindAnyObjectByType<NetworkStartPosition>();
            if (nsp != null)
                DayCycleManager.Instance.SetPlayerSpawnPoint(nsp.transform);

            if (debugLog) Debug.Log("[PlayerNetworkController] 已将玩家引用和出生点注入 DayCycleManager。");
        }
    }

    // =========================================================
    // 【Step 4：交互接管区（代理方法）】
    // 下面将存放用于物品拿取与放下的核心 Command 方法，未来扩展
    // =========================================================
    
    [Command]
    public void CmdRequestPickUp(GameObject targetItemObj, bool isLongPress)
    {
        if (targetItemObj == null) return;
        
        // 服务端进行状态检测（判重等...）
        CarryableItem item = targetItemObj.GetComponent<CarryableItem>();
        if (item == null)
        {
            if (debugLog) Debug.Log($"[Server] 拒绝玩家 {netId} 收取物体，目标无效。");
            return;
        }

        // 饰品：与客户端 CanBePickedUp(holdPoint) 对齐，装备栏已满则拒绝（主机上 Holder 槽位与客户端一致）
        var accessoryHolder = GetComponent<PlayerAccessoryHolder>();
        if (item is AccessoryItem && accessoryHolder != null && !accessoryHolder.CanEquip())
        {
            if (debugLog) Debug.Log($"[Server] 拒绝玩家 {netId} 拾取饰品：装备栏已满。");
            return;
        }

        if (!item.CanBePickedUp())
        {
            if (debugLog) Debug.Log($"[Server] 拒绝玩家 {netId} 收取物体，目标已被他人取走或锁定。");
            return;
        }

        if (debugLog) Debug.Log($"[Server] 批准了玩家 {netId} 收取物体 {item.name}。下发同播。");

        // ItemPlacePoint 非网络组件：若不先在服务端 ClearOccupant，服务端仍认为槽位被占，
        // 洗碗机出口库存（OnOccupantCleared）等逻辑永远不会在服务端执行，联机下第二位玩家无法拿到下一盘等。
        if (item.CurrentPlacePoint != null)
            item.CurrentPlacePoint.ClearOccupant();
        
        // 移交物权
        NetworkIdentity itemIdentity = targetItemObj.GetComponent<NetworkIdentity>();
        if (itemIdentity != null)
        {
            // 先剥夺其他可能死锁的物权
            itemIdentity.RemoveClientAuthority();
            itemIdentity.AssignClientAuthority(connectionToClient);
        }

        // 下发同步，所有端执行视觉跟随或全套拾取动作
        RpcAcknowledgePickUp(targetItemObj, isLongPress);
    }

    [Command]
    public void CmdRequestDispenseBox(GameObject stationObj, bool isLongPress)
    {
        if (stationObj == null) return;

        GameObject generatedObj = null;

        SupplyBox supplyBox = stationObj.GetComponent<SupplyBox>();
        if (supplyBox != null)
        {
            generatedObj = supplyBox.ServerDispenseItem(isLongPress);
        }
        else
        {
            DirtyPlateStack dirtyStack = stationObj.GetComponent<DirtyPlateStack>();
            if (dirtyStack != null)
            {
                generatedObj = dirtyStack.ServerDispenseItem(isLongPress);
            }
        }

        if (generatedObj != null)
        {
            NetworkIdentity itemIdentity = generatedObj.GetComponent<NetworkIdentity>();
            if (itemIdentity != null)
            {
                itemIdentity.AssignClientAuthority(connectionToClient);
            }
            
            // 下发拾取动作指令给该玩家，如同他刚刚拾取了这个刚生成的物体
            RpcAcknowledgePickUp(generatedObj, isLongPress);
        }
    }

    /// <summary>
    /// 待清桌阶段从餐桌收走全部脏盘（服务端生成网络 DirtyPlateStack + 清桌 SyncVar）。
    /// </summary>
    [Command]
    public void CmdRequestTakeAllDirtyPlatesFromTable(uint orderResponseNetId)
    {
        if (!NetworkServer.spawned.TryGetValue(orderResponseNetId, out NetworkIdentity ni)) return;

        OrderResponse table = ni.GetComponent<OrderResponse>();
        if (table == null) return;

        table.ServerTakeAllDirtyPlatesFor(this);
    }

    /// <param name="stationRootNetId">带 NetworkIdentity 的洗碗机根（或任意祖先上的 NI），Mirror Command 不能传无 NI 的 GameObject。</param>
    [Command]
    public void CmdRequestDispenseCleanPlate(uint stationRootNetId, bool isLongPress)
    {
        if (!NetworkServer.spawned.TryGetValue(stationRootNetId, out NetworkIdentity ni)) return;

        CleanPlateDispenserStation dispenser = ni.GetComponentInChildren<CleanPlateDispenserStation>(true);
        if (dispenser == null) return;

        GameObject generatedObj = dispenser.ServerDispenseOne();
        if (generatedObj == null) return;

        NetworkIdentity itemIdentity = generatedObj.GetComponent<NetworkIdentity>();
        if (itemIdentity != null)
            itemIdentity.AssignClientAuthority(connectionToClient);

        RpcAcknowledgePickUp(generatedObj, isLongPress);
    }

    [ClientRpc]
    private void RpcAcknowledgePickUp(GameObject targetItemObj, bool isLongPress)
    {
        if (targetItemObj == null) return;
        
        CarryableItem item = targetItemObj.GetComponent<CarryableItem>();
        if (item == null) return;

        if (isLocalPlayer)
        {
            // 【本地客户端】：执行全套原始逻辑并赋值
            if (interactor != null)
            {
                item.BeginHold(interactor.GetHoldPoint());

                // 饰品拾取后会在 BeginHold 中自动装备，不应设为手持物
                AccessoryItem acc = item as AccessoryItem;
                if (acc != null && acc.wasAutoEquipped)
                {
                    acc.wasAutoEquipped = false;
                }
                else
                {
                    interactor.ReplaceHeldItem(item);
                }

                if (debugLog) Debug.Log($"<color=#FF00FF>[本地同播]</color> 成功获取物权并拿起 {item.name}");
            }
        }
        else
        {
            AccessoryItem remoteAcc = item as AccessoryItem;
            if (remoteAcc != null)
            {
                var holder = GetComponent<PlayerAccessoryHolder>();
                if (holder != null)
                    holder.RemoteVisualEquip(remoteAcc);
                if (debugLog) Debug.Log($"<color=#FF00FF>[远端同播]</color> 饰品 {item.name} 远端视觉挂载到玩家 {netId}");
                return;
            }

            if (interactor != null && interactor.GetHoldPoint() != null)
            {
                // 与本地 BeginHold 对齐：关 NT、物理、HeldItem 层、state，否则远端只见 Transform 不见层/状态，Host 可能看不到或与 NT 冲突
                item.BeginHold(interactor.GetHoldPoint());
                if (debugLog) Debug.Log($"<color=#FF00FF>[远端同播]</color> BeginHold 同步远端手持：{item.name} → 玩家 {netId}");
            }
        }
    }

    /// <summary>放下：丢地</summary>
    public const byte ReleaseDrop = 0;
    /// <summary>放下：放到 ItemPlacePoint</summary>
    public const byte ReleasePlace = 1;
    /// <summary>放下：推入已有 DynamicItemStack</summary>
    public const byte ReleasePushStack = 2;
    /// <summary>放下：两物品合并为新 Stack（仅服务端 Instantiate + Spawn）</summary>
    public const byte ReleaseMergeStack = 3;
    /// <summary>放下：放入冰箱</summary>
    public const byte ReleaseFridge = 4;

    /// <summary>
    /// 服务端权威放下/放置/堆叠/入冰箱。
    /// <para><paramref name="auxNetObj"/>：有 NetworkIdentity 的辅助目标（DynamicItemStack / CarryableItem），用于堆叠。</para>
    /// <para><paramref name="auxWorldPos"/>：无 NetworkIdentity 的目标世界坐标（ItemPlacePoint / FridgeStation），服务端按位置匹配。</para>
    /// </summary>
    [Command]
    public void CmdRequestReleaseHeld(byte releaseKind, GameObject itemObj, GameObject auxNetObj, Vector3 auxWorldPos)
    {
        if (itemObj == null) return;

        NetworkIdentity itemNi = itemObj.GetComponent<NetworkIdentity>();
        if (itemNi == null) return;
        if (itemNi.connectionToClient != connectionToClient)
        {
            if (debugLog)
                Debug.LogWarning($"[CmdRequestReleaseHeld] 拒绝：物品 netId={itemNi.netId} 的 connection 与发起者不符 (itemConn={itemNi.connectionToClient}, cmdConn={connectionToClient})。");
            return;
        }

        CarryableItem item = itemObj.GetComponent<CarryableItem>();
        if (item == null) return;

        if (item is DirtyPlateStack && (releaseKind == ReleasePushStack || releaseKind == ReleaseMergeStack))
        {
            if (debugLog) Debug.Log("[CmdRequestReleaseHeld] 拒绝：脏盘整堆不可合并或推入其它 Stack。");
            return;
        }

        bool ok = false;

        switch (releaseKind)
        {
            case ReleaseDrop:
                item.DropToGround();
                ok = true;
                break;

            case ReleasePlace:
            {
                ItemPlacePoint pp = FindNearestComponent<ItemPlacePoint>(auxWorldPos, 0.5f);
                if (pp == null)
                {
                    if (debugLog) Debug.LogWarning($"[CmdRequestReleaseHeld] ReleasePlace 未找到 ItemPlacePoint near {auxWorldPos}");
                    break;
                }
                if (!pp.CanPlace(item))
                {
                    if (debugLog) Debug.LogWarning($"[CmdRequestReleaseHeld] CanPlace=false: item={item.name} point={pp.name}");
                    break;
                }
                if (pp.TryAcceptItem(item))
                    ok = true;
                break;
            }

            case ReleasePushStack:
                if (auxNetObj != null)
                {
                    DynamicItemStack stack = auxNetObj.GetComponent<DynamicItemStack>();
                    if (stack != null && item.GetComponent<StackableProp>() != null && stack.PushItem(item, null))
                        ok = true;
                }
                break;

            case ReleaseMergeStack:
                if (auxNetObj != null)
                {
                    StackableProp targetProp = auxNetObj.GetComponent<StackableProp>();
                    if (targetProp != null && item.GetComponent<StackableProp>() != null)
                    {
                        DynamicItemStack created = targetProp.MergeIntoStack(item, null);
                        ok = created != null;
                    }
                }
                if (ok)
                    RpcApplyReleaseHeld(releaseKind, itemObj, auxWorldPos);
                return;

            case ReleaseFridge:
            {
                FridgeStation fridge = FindNearestComponent<FridgeStation>(auxWorldPos, 3f);
                if (fridge != null && fridge.CanAcceptItem())
                    ok = fridge.ServerTryStoreFromAuthority(item);
                break;
            }
        }

        if (!ok) return;

        itemNi.RemoveClientAuthority();
        RpcApplyReleaseHeld(releaseKind, itemObj, auxWorldPos);
    }

    /// <summary>按世界坐标在场景中查找最近的指定组件（用于 PlacePoint / FridgeStation 等无 NetworkIdentity 的对象）。</summary>
    static T FindNearestComponent<T>(Vector3 worldPos, float maxDist) where T : Component
    {
        float bestSqr = maxDist * maxDist;
        T best = null;
        foreach (var c in FindObjectsByType<T>(FindObjectsSortMode.None))
        {
            float sqr = (c.transform.position - worldPos).sqrMagnitude;
            if (sqr < bestSqr) { bestSqr = sqr; best = c; }
        }
        return best;
    }

    /// <summary>
    /// 所有客户端统一：从手上脱离物品。
    /// 服务端（Host）已在 Cmd 中完成实际逻辑；纯客户端（Guest）根据 releaseKind 镜像执行放置/丢地等表现。
    /// </summary>
    [ClientRpc]
    void RpcApplyReleaseHeld(byte releaseKind, GameObject itemObj, Vector3 auxWorldPos)
    {
        // 发起放下的玩家在所有端（含远端观察）都应清空 held，否则他端仍显示菜粘在手上
        if (interactor != null)
            interactor.ReplaceHeldItem(null);

        if (NetworkServer.active)
            return;

        CarryableItem item = itemObj != null ? itemObj.GetComponent<CarryableItem>() : null;
        if (item == null) return;

        switch (releaseKind)
        {
            case ReleasePlace:
            {
                ItemPlacePoint pp = FindNearestComponent<ItemPlacePoint>(auxWorldPos, 0.5f);
                if (pp != null)
                {
                    item.SetNetworkTransformSync(false);
                    pp.TryAcceptItem(item);
                }
                else
                {
                    item.transform.SetParent(null);
                    item.SetNetworkTransformSync(true);
                }
                break;
            }

            case ReleaseDrop:
            default:
            {
                item.DropToGround();
                item.SetNetworkTransformSync(true);
                break;
            }
        }
    }

    /// <param name="stationTypeName">
    /// 子物体上具体 <see cref="BaseStation"/> 派生类的 <see cref="System.Type.Name"/>（同一 NI 根下多个 Station 时必填，如 DishWashingStation / CleanPlateDispenserStation）。
    /// 可空：仅当根下只有一个 BaseStation 时安全。
    /// </param>
    [Command]
    public void CmdSetStationInteractState(GameObject stationObj, bool isInteracting, string stationTypeName)
    {
        if (stationObj == null)
        {
            if (debugLog) Debug.LogWarning("[StationK/Cmd] stationObj 为 null，忽略。");
            return;
        }

        BaseStation station = ResolveBaseStationOnRoot(stationObj, stationTypeName);
        if (station == null)
        {
            if (debugLog)
                Debug.LogWarning($"[StationK/Cmd] 在 {stationObj.name} 上无法解析 BaseStation（stationTypeName={stationTypeName ?? "null"}）。");
            return;
        }

        PlayerItemInteractor pi = GetComponent<PlayerItemInteractor>();
        if (pi == null)
        {
            if (debugLog) Debug.LogWarning("[StationK/Cmd] PlayerItemInteractor 缺失。");
            return;
        }

        if (debugLog)
            Debug.Log($"[StationK/Cmd] Server 执行 {(isInteracting ? "BeginInteract" : "EndInteract")} | station={station.GetType().Name} on {station.gameObject.name} | playerNetId={netId}");

        if (isInteracting)
            station.BeginInteract(pi);
        else
            station.EndInteract(pi);

        RpcSetStationInteractState(stationObj, isInteracting, netId);
    }

    /// <summary>
    /// 同一 NetworkIdentity 根下可能挂多个 <see cref="BaseStation"/>（如洗碗机：洗碗池 + 干净盘发放），不能只用 GetComponentInChildren 取第一个。
    /// </summary>
    static BaseStation ResolveBaseStationOnRoot(GameObject stationRoot, string stationTypeName)
    {
        BaseStation[] all = stationRoot.GetComponentsInChildren<BaseStation>(true);
        if (all == null || all.Length == 0)
            return null;

        if (!string.IsNullOrEmpty(stationTypeName))
        {
            foreach (BaseStation bs in all)
            {
                if (bs == null) continue;
                if (bs.GetType().Name == stationTypeName || bs.GetType().FullName == stationTypeName)
                    return bs;
            }
            return null;
        }

        if (all.Length == 1)
            return all[0];

        // 未传类型且多个 Station：保留旧行为取第一个，但易错；调用方应始终传 stationTypeName
        return all[0];
    }

    /// <summary>
    /// 服务端生成可拾取物后：移交客户端权威并 Rpc 执行本地拿起（与 CmdRequestDispenseBox 等一致）。
    /// </summary>
    public void ServerAssignPickupToCaller(GameObject spawnedItem, bool isLongPress)
    {
        if (!NetworkServer.active || spawnedItem == null) return;

        NetworkIdentity itemIdentity = spawnedItem.GetComponent<NetworkIdentity>();
        if (itemIdentity != null)
        {
            itemIdentity.RemoveClientAuthority();
            itemIdentity.AssignClientAuthority(connectionToClient);
        }

        RpcAcknowledgePickUp(spawnedItem, isLongPress);
    }

    [ClientRpc]
    void RpcSetStationInteractState(GameObject stationObj, bool isInteracting, uint playerNetId)
    {
        // 权威交互已在 Command 内于服务端执行；勿再 BeginInteract/EndInteract，避免 Host 双次调用。
        // 若日后需全客户端纯表现（与 SyncVar 无关），可在此扩展。
    }

    // =========================================================
    // 【垃圾桶倒锅：仅服务端 ForceClear，FryPotNetworkSync 会镜像到各端】
    // =========================================================

    [Command]
    public void CmdRequestFryPotDump(GameObject potItemObj)
    {
        if (potItemObj == null) return;

        NetworkIdentity ni = potItemObj.GetComponent<NetworkIdentity>();
        if (ni == null) return;
        if (ni.connectionToClient != connectionToClient)
        {
            if (debugLog)
                Debug.LogWarning($"[CmdRequestFryPotDump] 拒绝：锅 netId={ni.netId} 的权威与发起者不符。");
            return;
        }

        FryPot pot = potItemObj.GetComponent<FryPot>();
        if (pot == null) return;
        if (!pot.CanDump())
        {
            if (debugLog) Debug.Log($"[CmdRequestFryPotDump] CanDump=false，跳过。");
            return;
        }

        pot.ForceClear();
        if (debugLog) Debug.Log($"[CmdRequestFryPotDump] Server 已清空锅 {potItemObj.name}");
    }

    // =========================================================
    // 【盛菜系统：由 Server 执行 Pot → Plate 装盘】
    // =========================================================

    [Command]
    public void CmdRequestPotServe(GameObject potItemObj, GameObject plateObj)
    {
        if (potItemObj == null || plateObj == null)
        {
            if (debugLog) Debug.Log($"<color=#FF0000>[盛菜Cmd]</color> 参数为空 pot={potItemObj} plate={plateObj}");
            return;
        }

        FryPot pot = potItemObj.GetComponent<FryPot>();
        CarryableItem plate = plateObj.GetComponent<CarryableItem>();
        if (pot == null || plate == null || !pot.CanServe())
        {
            if (debugLog) Debug.Log($"<color=#FF0000>[盛菜Cmd]</color> 条件不满足 pot={pot != null} plate={plate != null} canServe={pot?.CanServe()}");
            return;
        }

        if (debugLog) Debug.Log($"<color=#00FF00>[盛菜Cmd]</color> Server 执行盛菜 pot={potItemObj.name} plate={plateObj.name}");

        PlayerItemInteractor pi = GetComponent<PlayerItemInteractor>();
        if (pi == null) return;

        GameObject dishObj = pi.ExecutePotToPlateServeLocal(pot, plate);
        if (dishObj == null) return;

        NetworkIdentity dishNi = dishObj.GetComponent<NetworkIdentity>();
        if (dishNi != null)
        {
            // 无客户端权威时放下会触发 CmdRequestReleaseHeld 拒绝
            dishNi.AssignClientAuthority(connectionToClient);
            RpcApplyDishQuality(dishNi.netId, (byte)pot.LastServedQuality);
        }
    }

    /// <summary>盘子工具（K）对锅盛菜：仅服务端执行，避免客户端 Serve/Destroy 与权威错乱。</summary>
    [Command]
    public void CmdRequestPlateToolServe(GameObject potItemObj, GameObject plateObj)
    {
        if (potItemObj == null || plateObj == null) return;

        FryPot pot = potItemObj.GetComponent<FryPot>();
        CarryableItem plate = plateObj.GetComponent<CarryableItem>();
        if (pot == null || plate == null || !pot.CanServe()) return;

        NetworkIdentity plateNi = plateObj.GetComponent<NetworkIdentity>();
        if (plateNi != null && plateNi.connectionToClient != connectionToClient)
        {
            if (debugLog)
                Debug.LogWarning($"[CmdRequestPlateToolServe] 盘子权威不符 netId={plateNi.netId}");
            return;
        }

        GameObject dishPrefab = pot.Serve();
        if (dishPrefab == null) return;

        DishQuality servedQuality = pot.LastServedQuality;

        NetworkServer.Destroy(plateObj);

        GameObject newDishObj = Object.Instantiate(dishPrefab);
        NetworkIdentity dishNi = newDishObj.GetComponent<NetworkIdentity>();
        if (dishNi == null)
        {
            if (debugLog) Debug.LogError("[CmdRequestPlateToolServe] Dish 预制体缺少 NetworkIdentity。");
            Object.Destroy(newDishObj);
            return;
        }

        NetworkServer.Spawn(newDishObj);
        dishNi.AssignClientAuthority(connectionToClient);
        RpcApplyDishQuality(dishNi.netId, (byte)servedQuality);
        RpcAcknowledgePickUp(newDishObj, false);
    }

    /// <summary>各客户端对 Dish 应用品质子物体显隐（服务端 Instantiate 仅发生在 Host 进程，Guest 需 Rpc）。</summary>
    [ClientRpc]
    void RpcApplyDishQuality(uint dishNetId, byte qualityByte)
    {
        if (!NetworkClient.spawned.TryGetValue(dishNetId, out NetworkIdentity ni)) return;
        DishQualityTag tag = ni.GetComponent<DishQualityTag>();
        if (tag == null) return;
        tag.ApplyQuality((DishQuality)qualityByte);
    }

    // =========================================================
    // 【饰品系统：卸下饰品网络同步】
    // =========================================================

    [Command]
    public void CmdUnequipAllAccessories()
    {
        RpcUnequipAllAccessories();
    }

    [ClientRpc]
    private void RpcUnequipAllAccessories()
    {
        var holder = GetComponent<PlayerAccessoryHolder>();
        if (holder != null)
            holder.ExecuteUnequipAll();
    }
}
