using UnityEngine;
using Mirror;

/// <summary>
/// 单机模式下的玩家生成器。
/// 仅在无任何 Mirror 网络连接时生效，联机模式下此脚本完全静默。
/// 挂载在游戏场景中的一个空物体上即可。
/// </summary>
public class SinglePlayerSpawner : MonoBehaviour
{
    [Header("Player Settings")]
    [Tooltip("玩家预制体（与 NetworkRoomManager 中配置的 GamePlayer 完全相同的预制体）")]
    public GameObject playerPrefab;

    [Tooltip("指定出生点位置。若为空则尝试使用场景中的 NetworkStartPosition")]
    public Transform spawnPoint;

    [Header("Debug")]
    public bool debugLog = true;

    void Start()
    {
        // 核心判断：如果 Mirror 的网络层处于活跃状态（无论服务端还是客户端），说明是联机模式，直接撤退
        if (NetworkClient.active || NetworkServer.active)
        {
            if (debugLog) Debug.Log("[SinglePlayerSpawner] 检测到联机模式，本脚本不执行任何操作。");
            return;
        }

        if (playerPrefab == null)
        {
            Debug.LogError("[SinglePlayerSpawner] playerPrefab 未赋值！请在 Inspector 中拖入玩家预制体。");
            return;
        }

        // 确定出生坐标：优先使用手动指定的 spawnPoint，其次搜索场景中的 NetworkStartPosition
        Vector3 spawnPos = Vector3.zero;
        Quaternion spawnRot = Quaternion.identity;

        if (spawnPoint != null)
        {
            spawnPos = spawnPoint.position;
            spawnRot = spawnPoint.rotation;
        }
        else
        {
            NetworkStartPosition nsp = FindAnyObjectByType<NetworkStartPosition>();
            if (nsp != null)
            {
                spawnPos = nsp.transform.position;
                spawnRot = nsp.transform.rotation;
            }
            else if (debugLog)
            {
                Debug.LogWarning("[SinglePlayerSpawner] 场景中未找到 NetworkStartPosition，玩家将在原点生成。");
            }
        }

        // 实例化玩家
        GameObject player = Instantiate(playerPrefab, spawnPos, spawnRot);
        player.name = "Player (SinglePlayer)";

        if (debugLog) Debug.Log($"[SinglePlayerSpawner] 单机模式：玩家已在 {spawnPos} 生成。");

        // 禁用玩家身上不需要的网络组件（防止 NetworkIdentity 在单机下报警告）
        DisableNetworkComponents(player);

        // 将生成的玩家注入 DayCycleManager，使每日传送功能正常工作
        if (DayCycleManager.Instance != null)
        {
            DayCycleManager.Instance.SetPlayerObject(player);

            // 同时注入出生点，使每日传送的目标位置与初始出生位置一致
            Transform spawnTarget = spawnPoint;
            if (spawnTarget == null)
            {
                NetworkStartPosition nsp = FindAnyObjectByType<NetworkStartPosition>();
                if (nsp != null) spawnTarget = nsp.transform;
            }
            if (spawnTarget != null)
                DayCycleManager.Instance.SetPlayerSpawnPoint(spawnTarget);

            if (debugLog) Debug.Log("[SinglePlayerSpawner] 已将玩家引用和出生点注入 DayCycleManager。");
        }

        // 绑定摄像机
        CameraFollowRig camRig = FindAnyObjectByType<CameraFollowRig>();
        if (camRig != null)
        {
            camRig.target = player.transform;
            camRig.alwaysFollow = true;
            if (debugLog) Debug.Log("[SinglePlayerSpawner] 摄像机已绑定到单机玩家。");
        }
    }

    /// <summary>
    /// 禁用玩家预制体上挂载的所有 Mirror 网络相关组件，防止单机下报错。
    /// </summary>
    void DisableNetworkComponents(GameObject player)
    {
        // 禁用 PlayerNetworkController（它在 Start 中会检测网络状态，但安全起见还是关掉）
        var netCtrl = player.GetComponent<PlayerNetworkController>();
        if (netCtrl != null) netCtrl.enabled = false;

        // 禁用 NetworkPlayerNameSync
        var nameSync = player.GetComponent<NetworkPlayerNameSync>();
        if (nameSync != null) nameSync.enabled = false;

        // 禁用 NetworkIdentity（必须放在最后，因为其他 NetworkBehaviour 依赖它）
        var netId = player.GetComponent<NetworkIdentity>();
        if (netId != null) netId.enabled = false;
    }
}
