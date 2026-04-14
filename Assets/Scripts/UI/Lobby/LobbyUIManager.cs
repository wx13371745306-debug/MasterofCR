using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using Mirror.Discovery;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class LobbyUIManager : MonoBehaviour
{
    public static LobbyUIManager Instance { get; private set; }

    [Header("Core References")]
    public NetworkDiscovery networkDiscovery;
    [Tooltip("如果是单机模式，直接跳转此场景(通过 SceneManager)")]
    public string singlePlayerSceneName = "GameplayScene"; // 替换成你真实的厨房场景名

    [Header("Panels")]
    public GameObject mainMenuPanel;
    public GameObject lobbyPanel;
    public GameObject roomPanel;

    [Header("Main Menu UI")]
    public TMP_InputField playerNameInput;

    [Header("Lobby UI (Room List)")]
    public Transform roomListContent;
    public GameObject roomLinePrefab;

    [Header("Room UI")]
    public TextMeshProUGUI roomIDText;
    public RoomSlotUI[] playerSlots = new RoomSlotUI[4]; // 严格对应四大固定槽位
    
    public GameObject clientControls;
    public Button readyBtn;
    public Button cancelReadyBtn;

    public GameObject hostControls;
    public Button startGameBtn;

    [Header("联机诊断")]
    [Tooltip("输出大厅/发现/断线相关快照，用于排查「搜不到房间」")]
    public bool discoveryDiagLog = true;

    // 缓存发现的服务器房间以及当前房间内的玩家
    private readonly Dictionary<long, ServerResponse> discoveredServers = new Dictionary<long, ServerResponse>();
    private readonly List<CustomRoomPlayer> roomPlayers = new List<CustomRoomPlayer>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        ResolveNetworkDiscoveryReference();

        SwitchToPanel(mainMenuPanel);
        if (playerNameInput != null)
            playerNameInput.text = PlayerPrefs.GetString("PlayerLobbyName", "Player");

        LogDiscoveryDiag("Start");
    }

    /// <summary>
    /// 优先绑定到 <see cref="NetworkManager.singleton"/> 上的 Discovery。
    /// 当 DDOL 已有一份 NM、场景再次加载导致第二份 NM 被 Mirror 整物体 Destroy 时，
    /// Inspector 里拖的引用可能指向已销毁物体；此处避免 StartDiscovery / AdvertiseServer 一直为空。
    /// </summary>
    void ResolveNetworkDiscoveryReference()
    {
        NetworkManager nm = NetworkManager.singleton;
        NetworkDiscovery fromSingleton = null;
        if (nm != null)
        {
            fromSingleton = nm.GetComponent<NetworkDiscovery>();
            if (fromSingleton == null)
                fromSingleton = nm.GetComponentInChildren<NetworkDiscovery>(true);
        }

        if (fromSingleton != null)
        {
            networkDiscovery = fromSingleton;
            return;
        }

        if (networkDiscovery != null)
            return;

        networkDiscovery = FindAnyObjectByType<NetworkDiscovery>();
    }

    private void Update()
    {
        // ============================================
        // 动态状态机：根据网络状态和房主身份更新各种按钮显示
        // ============================================
        if (roomPanel.activeInHierarchy && NetworkClient.active)
        {
            CustomNetworkRoomManager nrm = NetworkManager.singleton as CustomNetworkRoomManager;
            if (nrm == null) return;

            // 1. 判断自己是否是房主 (Host)
            bool isHost = NetworkServer.active;
            
            if (hostControls != null) hostControls.SetActive(isHost);
            if (clientControls != null) clientControls.SetActive(!isHost);

            // 非 Host 不显示「开始游戏」（避免按钮未挂在 hostControls 下时仍可见）
            if (startGameBtn != null)
                startGameBtn.gameObject.SetActive(isHost);

            // 2. 房主逻辑：检查房间内每一名玩家是否都已准备（不依赖 Mirror 的 minPlayers / allPlayersReady 启发式）
            if (isHost && startGameBtn != null)
                startGameBtn.interactable = AreAllRoomPlayersReady(nrm);

            // 3. 客机逻辑：判断自己的准备状态来切换按钮
            if (!isHost && readyBtn != null && cancelReadyBtn != null)
            {
                CustomRoomPlayer myPlayer = GetLocalPlayer();
                if (myPlayer != null)
                {
                    readyBtn.gameObject.SetActive(!myPlayer.readyToBegin);
                    cancelReadyBtn.gameObject.SetActive(myPlayer.readyToBegin);
                }
            }
        }
    }

    // =========================================================
    // 【面板流转方法】
    // =========================================================
    private void SwitchToPanel(GameObject targetPanel)
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(targetPanel == mainMenuPanel);
        if (lobbyPanel != null) lobbyPanel.SetActive(targetPanel == lobbyPanel);
        if (roomPanel != null) roomPanel.SetActive(targetPanel == roomPanel);
    }

    private void SaveName()
    {
        if (playerNameInput != null && !string.IsNullOrWhiteSpace(playerNameInput.text))
        {
            PlayerPrefs.SetString("PlayerLobbyName", playerNameInput.text);
            PlayerPrefs.Save();
        }
    }

    // =========================================================
    // 【MainMenu_Panel 事件】
    // =========================================================
    public void OnClickSinglePlayer()
    {
        SaveName();

        // 【假装单机模式】：启动一台只有自己的本地 Host 服务器。
        // 这样所有 NetworkIdentity 物体都会被 Mirror 正确管理，场景中的厨具、桌子全部正常显示。
        CustomNetworkRoomManager nrm = NetworkManager.singleton as CustomNetworkRoomManager;
        if (nrm != null)
        {
            nrm.StartHost();
            StartCoroutine(AutoStartSinglePlayerRoutine(nrm));
        }
        else
        {
            Debug.LogWarning("[LobbyUI] 未找到 CustomNetworkRoomManager，将直接加载场景");
            SceneManager.LoadScene(singlePlayerSceneName);
        }
    }

    /// <summary>
    /// 协程：等待 Mirror 完成 Host 初始化，自动标记本地玩家为"已准备"，然后跳转到游戏场景。
    /// </summary>
    private IEnumerator AutoStartSinglePlayerRoutine(CustomNetworkRoomManager nrm)
    {
        // 等两帧，确保 Mirror 完成 Host 连接和 RoomPlayer 的创建
        yield return null;
        yield return null;

        // 自动将本地 RoomPlayer 标记为"已准备"
        // 否则 NetworkRoomManager.ServerChangeScene 会因 allPlayersReady==false 而静默拒绝跳转
        NetworkRoomPlayer roomPlayer = NetworkClient.localPlayer != null 
            ? NetworkClient.localPlayer.GetComponent<NetworkRoomPlayer>() 
            : null;

        if (roomPlayer != null)
        {
            roomPlayer.CmdChangeReadyState(true);
            yield return null; // 再等一帧让 ready 状态在服务端生效
        }

        nrm.ServerChangeScene(nrm.GameplayScene);
    }

    public void OnClickMultiplayer()
    {
        SaveName();
        ResolveNetworkDiscoveryReference();
        LogDiscoveryDiag("OnClickMultiplayer(进入大厅前)");
        SwitchToPanel(lobbyPanel);

        // 激活多人的时候，大厅开启搜索局域网
        discoveredServers.Clear();
        foreach (Transform child in roomListContent) Destroy(child.gameObject); // 清理旧 UI 数据
        
        if (networkDiscovery != null)
        {
            networkDiscovery.StartDiscovery();
            LogDiscoveryDiag("OnClickMultiplayer(StartDiscovery 已调用)");
            StartCoroutine(DiagDiscoveryAfterFrames("OnClickMultiplayer", 4));
        }
        else
            Debug.LogWarning("[LobbyDiag] networkDiscovery 为空，无法 StartDiscovery");
    }

    public void OnClickExitGame() { Application.Quit(); }

    // =========================================================
    // 【Lobby_Panel 事件】
    // =========================================================
    public void OnClickBackToMenu()
    {
        ResolveNetworkDiscoveryReference();
        LogDiscoveryDiag("OnClickBackToMenu(停发现前)");
        if (networkDiscovery != null)
            networkDiscovery.StopDiscovery();
            
        SwitchToPanel(mainMenuPanel);
        LogDiscoveryDiag("OnClickBackToMenu(回主菜单后)");
    }

    /// <summary>【大厅 UI 绑定】清空房间列表缓存与列表项，并重新发起局域网发现。</summary>
    public void OnClickRefreshRoomList()
    {
        if (lobbyPanel == null || !lobbyPanel.activeInHierarchy)
            return;

        ResolveNetworkDiscoveryReference();
        discoveredServers.Clear();
        if (roomListContent != null)
        {
            foreach (Transform child in roomListContent)
                Destroy(child.gameObject);
        }

        if (networkDiscovery != null)
        {
            networkDiscovery.StartDiscovery();
            LogDiscoveryDiag("OnClickRefreshRoomList(StartDiscovery 后)");
            StartCoroutine(DiagDiscoveryAfterFrames("OnClickRefreshRoomList", 4));
        }
    }

    public void OnClickCreateRoom()
    {
        ResolveNetworkDiscoveryReference();
        LogDiscoveryDiag("OnClickCreateRoom(StartHost 前)");
        SwitchToPanel(roomPanel);
        if (roomIDText != null) 
            roomIDText.text = $"Room: {Random.Range(1000, 9999)}";

        // 作为 Host 建立房间并广播让客机听到
        NetworkManager.singleton.StartHost();
        ResolveNetworkDiscoveryReference();
        if (networkDiscovery != null)
        {
            try
            {
                networkDiscovery.AdvertiseServer();
                LogDiscoveryDiag("OnClickCreateRoom(AdvertiseServer 成功)");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[LobbyDiag] AdvertiseServer 抛错（常见：UDP 端口被占用）: {ex.Message}\n{ex}");
            }
        }
        else
            Debug.LogWarning("[LobbyDiag] networkDiscovery 为空，无法 AdvertiseServer");

        StartCoroutine(DiagDiscoveryAfterFrames("OnClickCreateRoom", 4));
    }

    // NetworkDiscovery 引擎扫描到网络上别人广播的信息后会调用这里
    public void OnDiscoveredServer(ServerResponse info)
    {
        if (discoveredServers.ContainsKey(info.serverId))
        {
            if (discoveryDiagLog)
                Debug.Log($"[LobbyDiag] OnDiscoveredServer 跳过重复 serverId={info.serverId} uri={info.uri}");
            return; // 避免重复添加
        }

        if (discoveryDiagLog)
            Debug.Log($"[LobbyDiag] OnDiscoveredServer 新房间 serverId={info.serverId} uri={info.uri} 当前已缓存数={discoveredServers.Count}");

        discoveredServers[info.serverId] = info;

        // 实例化一个横条
        if (roomLinePrefab != null && roomListContent != null)
        {
            GameObject entryObj = Instantiate(roomLinePrefab, roomListContent);
            RoomLineUIEntry lineScript = entryObj.GetComponent<RoomLineUIEntry>();
            if (lineScript != null)
            {
                lineScript.Setup(info, this);
            }
        }
    }

    public void JoinDiscoveredRoom(ServerResponse info)
    {
        ResolveNetworkDiscoveryReference();
        LogDiscoveryDiag("JoinDiscoveredRoom(连接前)");
        if (discoveryDiagLog)
            Debug.Log($"[LobbyDiag] JoinDiscoveredRoom 目标 uri={info.uri} serverId={info.serverId}");

        if (networkDiscovery != null)
            networkDiscovery.StopDiscovery();

        // 告知管理器目标服务端 IP 并且连接
        NetworkManager.singleton.StartClient(info.uri);
        SwitchToPanel(roomPanel);
        LogDiscoveryDiag("JoinDiscoveredRoom(StartClient 已调用)");
    }

    // =========================================================
    // 【Room_Panel 事件】
    // =========================================================
    public void OnClickReady() { ToggleReady(true); }
    public void OnClickCancelReady() { ToggleReady(false); }

    // 【新增】合并的智能切换方法，只需在一个按钮上的 OnClick 绑定这一个方法即可
    public void OnClickToggleReadyState()
    {
        CustomRoomPlayer p = GetLocalPlayer();
        if (p != null) p.CmdChangeReadyState(!p.readyToBegin);
    }

    private void ToggleReady(bool isReady)
    {
        CustomRoomPlayer p = GetLocalPlayer();
        if (p != null) p.CmdChangeReadyState(isReady);
    }

    public void OnClickStartGame()
    {
        CustomNetworkRoomManager nrm = NetworkManager.singleton as CustomNetworkRoomManager;
        if (nrm == null || !NetworkServer.active) return;

        if (!AreAllRoomPlayersReady(nrm))
        {
            Debug.LogWarning("[LobbyUI] 无法开始游戏：房间内仍有玩家未准备。");
            return;
        }

        nrm.ServerChangeScene(nrm.GameplayScene);
    }

    /// <summary>
    /// Mirror 的 allPlayersReady 在 minPlayers 较小时可能只需部分玩家就绪；联机房间要求「全员准备」才能开局。
    /// </summary>
    static bool AreAllRoomPlayersReady(NetworkRoomManager nrm)
    {
        if (nrm == null || nrm.roomSlots == null || nrm.roomSlots.Count == 0)
            return false;

        foreach (NetworkRoomPlayer rp in nrm.roomSlots)
        {
            if (rp == null || !rp.readyToBegin)
                return false;
        }

        return true;
    }

    public void OnClickLeaveRoom()
    {
        LogDiscoveryDiag("OnClickLeaveRoom(调用 StopHost/StopClient 前)");
        if (NetworkServer.active && NetworkClient.isConnected)
            NetworkManager.singleton.StopHost();    // 房主解散（全员断线）
        else if (NetworkClient.isConnected)
            NetworkManager.singleton.StopClient();  // 客机主动退出

        // StopHost/StopClient 会触发 Mirror 的 OnRoomClientDisconnect；此处再同步清一次，避免回调顺序导致漏清
        ResetLocalLobbyAfterDisconnect();
    }

    /// <summary>
    /// 本地客户端与服务器断开后恢复大厅默认状态：停发现、清房间列表缓存、清槽位、回主菜单。
    /// 供 <see cref="OnClickLeaveRoom"/> 与 <see cref="CustomNetworkRoomManager.OnRoomClientDisconnect"/> 共用
    /// （房主 StopHost 后其它玩家仅能通过断线回调收到）。
    /// </summary>
    public void ResetLocalLobbyAfterDisconnect()
    {
        LogDiscoveryDiag("ResetLocalLobbyAfterDisconnect(开始)");
        ResolveNetworkDiscoveryReference();
        if (networkDiscovery != null)
            networkDiscovery.StopDiscovery();

        discoveredServers.Clear();
        if (roomListContent != null)
        {
            foreach (Transform child in roomListContent)
                Destroy(child.gameObject);
        }

        foreach (var slot in playerSlots)
        {
            if (slot != null) slot.Clear();
        }
        roomPlayers.Clear();

        SwitchToPanel(mainMenuPanel);
        LogDiscoveryDiag("ResetLocalLobbyAfterDisconnect(结束，已回主菜单；需再次点「多人」才会 StartDiscovery)");
    }

    /// <summary>
    /// 输出当前场景与 Mirror 客户端/服务端状态，用于对照 Mirror 文档中「联网时 StopDiscovery」等行为。
    /// </summary>
    void LogDiscoveryDiag(string tag)
    {
        if (!discoveryDiagLog) return;

        Scene s = SceneManager.GetActiveScene();
        var nrm = NetworkManager.singleton as NetworkRoomManager;
        string roomSceneInfo = nrm != null
            ? $"RoomScene={nrm.RoomScene} IsRoomSceneActive={Utils.IsSceneActive(nrm.RoomScene)} GameplayScene={nrm.GameplayScene}"
            : "NetworkRoomManager=null";

        Debug.Log(
            $"[LobbyDiag][{tag}]\n" +
            $"  activeScene: name={s.name} path={s.path}\n" +
            $"  {roomSceneInfo}\n" +
            $"  NetworkServer.active={NetworkServer.active} NetworkClient.active={NetworkClient.active} NetworkClient.isConnected={NetworkClient.isConnected}\n" +
            $"  discoveredServers.Count={discoveredServers.Count}");
    }

    /// <summary>
    /// 延迟数帧再快照：给 Mirror 完成 Host/Client 与场景切换后再看 isConnected，便于发现「仍显示已连接导致搜不到」的竞态。
    /// </summary>
    IEnumerator DiagDiscoveryAfterFrames(string tag, int frames)
    {
        for (int i = 0; i < frames; i++)
            yield return null;

        LogDiscoveryDiag($"{tag}(延迟{frames}帧后)");

        if (discoveryDiagLog && lobbyPanel != null && lobbyPanel.activeInHierarchy && NetworkClient.isConnected)
        {
            Debug.LogWarning(
                $"[LobbyDiag][{tag}] 大厅已打开但 NetworkClient.isConnected==true。" +
                "Mirror 的 NetworkDiscovery 在广播时会因此 StopDiscovery()，可能导致列表一直为空。若与此相关，请检查断线顺序或延后 StartDiscovery。");
        }
    }

    // =========================================================
    // 【固定槽位核心更新逻辑：供 CustomRoomPlayer 注册调用】
    // =========================================================
    public void RegisterPlayer(CustomRoomPlayer player)
    {
        if (!roomPlayers.Contains(player))
        {
            roomPlayers.Add(player);
            UpdatePlayerSlots();
        }
    }

    public void UnregisterPlayer(CustomRoomPlayer player)
    {
        roomPlayers.Remove(player);
        UpdatePlayerSlots();
    }

    public void UpdatePlayerSlots()
    {
        // 将当前房间内的人按照 Mirror 分配的 Id 索引挨个装进这 4 个固定槽子里
        for (int i = 0; i < playerSlots.Length; i++)
        {
            if (playerSlots[i] == null) continue;

            if (i < roomPlayers.Count)
            {
                playerSlots[i].BindPlayer(roomPlayers[i]);
            }
            else
            {
                playerSlots[i].Clear();
            }
        }
    }

    private CustomRoomPlayer GetLocalPlayer()
    {
        foreach (var p in roomPlayers)
        {
            if (p.isLocalPlayer) return p;
        }
        return null;
    }
}
