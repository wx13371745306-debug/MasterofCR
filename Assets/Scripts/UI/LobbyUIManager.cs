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

    // 缓存发现的服务器房间以及当前房间内的玩家
    private readonly Dictionary<long, ServerResponse> discoveredServers = new Dictionary<long, ServerResponse>();
    private readonly List<CustomRoomPlayer> roomPlayers = new List<CustomRoomPlayer>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        // 如果没有拖拽 Discovery，主动去场景里找找
        if (networkDiscovery == null)
            networkDiscovery = FindAnyObjectByType<NetworkDiscovery>();

        SwitchToPanel(mainMenuPanel);
        if (playerNameInput != null)
            playerNameInput.text = PlayerPrefs.GetString("PlayerLobbyName", "Player");
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

            // 2. 房主逻辑：检查所有人是否都准备了
            if (isHost && startGameBtn != null)
            {
                // 如果不仅自己一个人，且全部准备好了
                bool allReady = nrm.allPlayersReady && nrm.roomSlots.Count > 0;
                startGameBtn.interactable = allReady;
            }

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
        SwitchToPanel(lobbyPanel);

        // 激活多人的时候，大厅开启搜索局域网
        discoveredServers.Clear();
        foreach (Transform child in roomListContent) Destroy(child.gameObject); // 清理旧 UI 数据
        
        if (networkDiscovery != null)
            networkDiscovery.StartDiscovery();
    }

    public void OnClickExitGame() { Application.Quit(); }

    // =========================================================
    // 【Lobby_Panel 事件】
    // =========================================================
    public void OnClickBackToMenu()
    {
        if (networkDiscovery != null)
            networkDiscovery.StopDiscovery();
            
        SwitchToPanel(mainMenuPanel);
    }

    public void OnClickCreateRoom()
    {
        SwitchToPanel(roomPanel);
        if (roomIDText != null) 
            roomIDText.text = $"Room: {Random.Range(1000, 9999)}";

        // 作为 Host 建立房间并广播让客机听到
        NetworkManager.singleton.StartHost();
        if (networkDiscovery != null)
            networkDiscovery.AdvertiseServer();
    }

    // NetworkDiscovery 引擎扫描到网络上别人广播的信息后会调用这里
    public void OnDiscoveredServer(ServerResponse info)
    {
        if (discoveredServers.ContainsKey(info.serverId)) return; // 避免重复添加

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
        if (networkDiscovery != null)
            networkDiscovery.StopDiscovery();

        // 告知管理器目标服务端 IP 并且连接
        NetworkManager.singleton.StartClient(info.uri);
        SwitchToPanel(roomPanel);
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
        if (nrm != null && NetworkServer.active)
        {
            // 这是 Mirror 的启动游戏跳转指令
            nrm.ServerChangeScene(nrm.GameplayScene);
        }
    }

    public void OnClickLeaveRoom()
    {
        if (NetworkServer.active && NetworkClient.isConnected) 
            NetworkManager.singleton.StopHost();    // 房主解散
        else if (NetworkClient.isConnected)
            NetworkManager.singleton.StopClient();  // 客机退出

        // 重置清槽，回到主菜单 
        foreach (var slot in playerSlots) if (slot != null) slot.Clear();
        roomPlayers.Clear();
        SwitchToPanel(mainMenuPanel);
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
