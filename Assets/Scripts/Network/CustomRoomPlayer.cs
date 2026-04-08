using UnityEngine;
using Mirror;
using System;

/// <summary>
/// 自定义网络大厅玩家。负责在正式进入游戏前处理名字的同步与 UI 状态刷新。
/// </summary>
public class CustomRoomPlayer : NetworkRoomPlayer
{
    [Header("Debug Settings")]
    public bool debugLog = false;

    // [SyncVar] 会自动让服务端的变化同步到所有客户端。
    // hook 参数指明当客户端接收到该值改变时，调用指定方法来响应表现层(UI等)。
    [SyncVar(hook = nameof(OnPlayerNameChanged))]
    public string playerName = "Unknown Player";

    // 一旦玩家名发生改变，触发此委托以便 UI 脚本注册监听
    public event Action<string, string> OnNameUpdateEvent;
    
    // 当玩家点击准备或取消准备时触发
    public event Action<bool> OnReadyStateUpdateEvent;

    public override void Start()
    {
        base.Start();
        // 尝试向 LobbyUIManager 注册自身，不管它存不存在都不会报错（弱依赖）
        if (LobbyUIManager.Instance != null)
            LobbyUIManager.Instance.RegisterPlayer(this);
    }

    public override void OnDisable()
    {
        base.OnDisable();
        if (LobbyUIManager.Instance != null)
            LobbyUIManager.Instance.UnregisterPlayer(this);
    }
    
    // 【屏蔽原生UI】防止屏幕左上角打印默认的 RoomPlayer[XX] 列表
    public override void OnGUI() {}

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (debugLog)
            Debug.Log($"[CustomRoomPlayer] OnStartClient 执行。netId: {netId}");

        if (isLocalPlayer)
        {
            // 防跳板与最小化破坏：使用 PlayerPrefs 获取大厅输入的昵称，不破坏原有游戏数据链路
            string storedName = PlayerPrefs.GetString("PlayerLobbyName", $"Player_{netId}");
            
            if (debugLog)
                Debug.Log($"[CustomRoomPlayer] 本地玩家发送其名字给服务端: {storedName}");

            // 执行核心第一步：通过命令赋予服务器处理名字的权限
            CmdSetPlayerName(storedName);
        }
    }
    
    // Command：由客户端远程调用，仅在服务端执行
    [Command]
    private void CmdSetPlayerName(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            newName = $"Client_{netId}";
        }

        if (debugLog)
            Debug.Log($"[CustomRoomPlayer] 服务端接收到名字设置请求: {newName} (来自 connection: {connectionToClient.connectionId})");

        // 在服务端端进行状态修改，状态会自动根据 SyncVar 广播给所有客户端
        playerName = newName;
    }

    // Hook方法：当客户端(也包括自己)通过网络同步收到最新名字时自动触发
    private void OnPlayerNameChanged(string oldName, string newName)
    {
        if (debugLog)
            Debug.Log($"[CustomRoomPlayer] 同步客户端名字变更：旧名字 '{oldName}' -> 新名字 '{newName}'");

        // 利用事件驱动抛出，和 UI 彻底解耦。具体负责表现的 UI 监听到事件后自行刷新。
        OnNameUpdateEvent?.Invoke(oldName, newName);
    }

    public override void ReadyStateChanged(bool oldReadyState, bool newReadyState)
    {
        base.ReadyStateChanged(oldReadyState, newReadyState);
        if (debugLog)
            Debug.Log($"[CustomRoomPlayer] netId: {netId} - 准备状态已更新: {newReadyState}");
            
        OnReadyStateUpdateEvent?.Invoke(newReadyState);
    }
}
