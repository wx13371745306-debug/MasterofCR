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
        if (item == null || !item.CanBePickedUp())
        {
            if (debugLog) Debug.Log($"[Server] 拒绝玩家 {netId} 收取物体，目标已被他人取走或锁定。");
            return;
        }

        if (debugLog) Debug.Log($"[Server] 批准了玩家 {netId} 收取物体 {item.name}。下发同播。");
        
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
                interactor.ReplaceHeldItem(item);
                if (debugLog) Debug.Log($"<color=#FF00FF>[本地同播]</color> 成功获取物权并拿起 {item.name}");
            }
        }
        else
        {
            // 【远端客户端】：表现层强制跟随对方玩家的手（避免调用完整 BeginHold 带来额外复杂物理碰撞开关错乱）
            if (interactor != null && interactor.GetHoldPoint() != null)
            {
                item.transform.SetParent(interactor.GetHoldPoint(), false);
                if (item.poseConfig != null)
                {
                    item.transform.localPosition = item.poseConfig.holdLocalPosition;
                    item.transform.localRotation = Quaternion.Euler(item.poseConfig.holdLocalEulerAngles);
                }
                else
                {
                    item.transform.localPosition = Vector3.zero;
                    item.transform.localRotation = Quaternion.identity;
                }
                if (debugLog) Debug.Log($"<color=#FF00FF>[远端同播]</color> 幽灵避免：仅视觉上将 {item.name} 塞入了玩家 {netId} 掌中");
            }
        }
    }

    [Command]
    public void CmdSetStationInteractState(GameObject stationObj, bool isInteracting)
    {
        if (stationObj == null) return;
        BaseStation station = stationObj.GetComponentInChildren<BaseStation>();
        if (station != null)
        {
            // 在服务器模拟开启或关闭互动
            // 注意：由于服务器没有本地的 PlayerItemInteractor 实例传入拿在手上的道具，暂传 null
            if (isInteracting)
                station.BeginInteract(null);
            else
                station.EndInteract(null);
        }
    }
}
