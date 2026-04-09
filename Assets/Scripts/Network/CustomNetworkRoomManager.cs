using UnityEngine;
using Mirror;

/// <summary>
/// 自定义网络房间管理器，管理客户端连接与场景切换
/// </summary>
public class CustomNetworkRoomManager : NetworkRoomManager
{
    [Header("Debug Settings")]
    public bool debugLog = false;

    // 【屏蔽原生UI】防止屏幕上出现 Mirror 默认的灰色方块和连通信息
    public override void OnGUI() {}

    public override void OnRoomServerConnect(NetworkConnectionToClient conn)
    {
        base.OnRoomServerConnect(conn);
        if (debugLog)
            Debug.Log($"[CustomNetworkRoomManager] 客户端连接: {conn.address}");
    }

    public override void OnRoomServerDisconnect(NetworkConnectionToClient conn)
    {
        if (debugLog)
            Debug.Log($"[CustomNetworkRoomManager] 客户端断开连接: {conn.address}");
        base.OnRoomServerDisconnect(conn);
    }

    public override void OnRoomStartServer()
    {
        base.OnRoomStartServer();
        if (debugLog)
            Debug.Log("[CustomNetworkRoomManager] 房间服务器启动");
    }

    public override bool OnRoomServerSceneLoadedForPlayer(NetworkConnectionToClient conn, GameObject roomPlayer, GameObject gamePlayer)
    {
        if (debugLog)
            Debug.Log($"[CustomNetworkRoomManager] 正在为此连接从 RoomPlayer 提取数据并转交至 GamePlayer...");
        
        // 【Step 3 核心逻辑挂载点】已实现：在此提取 roomPlayer 持有的信息(如 playerName)，初始化给 gamePlayer
        CustomRoomPlayer roomPlayerScript = roomPlayer.GetComponent<CustomRoomPlayer>();
        NetworkPlayerNameSync nameSync = gamePlayer.GetComponent<NetworkPlayerNameSync>();

        if (roomPlayerScript != null && nameSync != null)
        {
            nameSync.playerName = roomPlayerScript.playerName;
        }
        else
        {
            Debug.LogWarning("[CustomNetworkRoomManager] 提取名字失败：未能找到 CustomRoomPlayer 或 NetworkPlayerNameSync 组件！");
        }
        
        return base.OnRoomServerSceneLoadedForPlayer(conn, roomPlayer, gamePlayer);
    }

    public override void OnRoomClientConnect()
    {
        base.OnRoomClientConnect();
        if (debugLog)
            Debug.Log("[CustomNetworkRoomManager] 本地客户端成功连接服务器");
    }

    public override void OnRoomClientDisconnect()
    {
        base.OnRoomClientDisconnect();
        if (debugLog)
            Debug.Log("[CustomNetworkRoomManager] 本地客户端断开连接");
    }

    // 【新增修复】拦截 Mirror 在所有人准备后默认触发的「全自动跳场景」！
    // 将切场景的主动权完全交给房主点击的 "开始游戏" 按钮，解决 Scene change is already in progress 报错。
    public override void OnRoomServerPlayersReady()
    {
        // 留空，什么都不做！不调用 base.OnRoomServerPlayersReady()
        if (debugLog)
            Debug.Log("[CustomNetworkRoomManager] 所有人都准备好了，等待房主手动点击开始游戏...");
    }
}
