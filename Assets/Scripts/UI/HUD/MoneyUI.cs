using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Mirror;
using TMPro; // 如果你使用的是 TextMeshPro
// using UnityEngine.UI; // 如果你使用的是旧版普通 Text，请取消注释这行，并把下面的 TextMeshProUGUI 改成 Text

public class MoneyUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("拖入显示金钱的 Text 组件")]
    public TextMeshProUGUI moneyText;

    [Header("Settings")]
    [Tooltip("金钱显示的前缀，比如 '$' 或 '金币: '")]
    public string prefix = "$ ";

    [Header("二级菜单")]
    [Tooltip("初始请设为隐藏（取消勾选或 SetActive false）；主界面按钮只负责打开，退出房间按钮放在此面板内。")]
    [SerializeField] GameObject secondaryMenuPanel;

    [Header("二级菜单 · 房间与玩家列表")]
    [Tooltip("显示当前房间号/连接信息（见 GameplaySessionUIUtil.GetRoomDisplayLabel）")]
    [SerializeField] TextMeshProUGUI roomIdText;
    [Tooltip("可选：显示本局人数 如 2/4；不拖则只依赖下方玩家行")]
    [SerializeField] TextMeshProUGUI sessionPlayerCountText;
    [Tooltip("玩家行父节点（建议 Vertical Layout Group）")]
    [SerializeField] Transform sessionRosterLayout;
    [Tooltip("单名玩家行预制体（挂 SessionPlayerRowUI，含姓名+延迟 TMP）")]
    [SerializeField] GameObject sessionPlayerRowPrefab;

    [Header("二级菜单 · 设置项（可选，不拖则对应功能跳过）")]
    [SerializeField] FPSCounter fpsCounter;
    [SerializeField] Toggle toggleShowFps;
    [SerializeField] Toggle toggleSensorDebug;

    readonly List<GameObject> _rosterRowObjects = new List<GameObject>();
    readonly List<SessionPlayerRowUI> _sessionPlayerRows = new List<SessionPlayerRowUI>();

    void Update()
    {
        if (Keyboard.current == null) return;
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
            ToggleSecondaryMenu();

        if (secondaryMenuPanel != null && secondaryMenuPanel.activeInHierarchy)
        {
            for (int i = 0; i < _sessionPlayerRows.Count; i++)
            {
                if (_sessionPlayerRows[i] != null)
                    _sessionPlayerRows[i].RefreshPingDisplay();
            }
        }
    }

    void OnEnable()
    {
        MoneyManager.OnMoneyChanged += UpdateMoneyDisplay;

        if (MoneyManager.Instance != null)
        {
            UpdateMoneyDisplay(MoneyManager.Instance.CurrentMoney);
        }
    }

    void OnDisable()
    {
        MoneyManager.OnMoneyChanged -= UpdateMoneyDisplay;
    }

    private void UpdateMoneyDisplay(int newAmount)
    {
        if (moneyText != null)
        {
            moneyText.text = prefix + newAmount.ToString();
        }
    }

    /// <summary>主 HUD 上的入口按钮绑定：开关联动（再点一次关闭，与 Esc 一致）。</summary>
    public void OpenSecondaryMenu()
    {
        ToggleSecondaryMenu();
    }

    /// <summary>面板内「返回游戏」等按钮可绑定；Esc 关闭时也会调用同一套逻辑。</summary>
    public void CloseSecondaryMenu()
    {
        if (secondaryMenuPanel != null)
            secondaryMenuPanel.SetActive(false);
    }

    /// <summary>Esc 开关联动：隐藏则打开，已打开则关闭。</summary>
    public void ToggleSecondaryMenu()
    {
        if (secondaryMenuPanel == null) return;
        bool willOpen = !secondaryMenuPanel.activeSelf;
        secondaryMenuPanel.SetActive(willOpen);
        if (willOpen)
            RefreshSecondaryPanelContent();
    }

    /// <summary>
    /// 供二级菜单内按钮绑定：断开联机并回到 NetworkManager 配置的 Offline 场景。
    /// </summary>
    public void OnClickLeaveRoom()
    {
        if (NetworkManager.singleton == null) return;

        if (NetworkServer.active && NetworkClient.isConnected)
            NetworkManager.singleton.StopHost();
        else if (NetworkClient.isConnected)
            NetworkManager.singleton.StopClient();
    }

    /// <summary>【Toggle 绑定】显示 FPS。</summary>
    public void OnToggleShowFps(bool on)
    {
        FPSCounter counter = fpsCounter != null ? fpsCounter : FPSCounter.Instance;
        if (counter != null)
            counter.SetShowFps(on);
        else
            PlayerPrefs.SetInt(GameplaySessionUIUtil.PrefKeyShowFps, on ? 1 : 0);
    }

    /// <summary>【Toggle 绑定】PlayerInteractionSensor 的 IMGUI 调试窗口。</summary>
    public void OnToggleSensorDebug(bool on)
    {
        PlayerPrefs.SetInt(GameplaySessionUIUtil.PrefKeySensorDebug, on ? 1 : 0);
        PlayerInteractionSensor sensor = FindLocalPlayerSensor();
        if (sensor != null)
            sensor.debugLog = on;
    }

    void RefreshSecondaryPanelContent()
    {
        if (fpsCounter == null)
            fpsCounter = FPSCounter.Instance;

        if (toggleShowFps != null)
        {
            bool on = fpsCounter != null ? fpsCounter.ShowFps : PlayerPrefs.GetInt(GameplaySessionUIUtil.PrefKeyShowFps, 1) == 1;
            toggleShowFps.SetIsOnWithoutNotify(on);
        }

        if (toggleSensorDebug != null)
        {
            PlayerInteractionSensor sensor = FindLocalPlayerSensor();
            bool on = sensor != null
                ? sensor.debugLog
                : PlayerPrefs.GetInt(GameplaySessionUIUtil.PrefKeySensorDebug, 0) == 1;
            toggleSensorDebug.SetIsOnWithoutNotify(on);
        }

        if (roomIdText != null)
            roomIdText.text = GameplaySessionUIUtil.GetRoomDisplayLabel();

        RebuildSessionRoster();
    }

    void RebuildSessionRoster()
    {
        foreach (GameObject go in _rosterRowObjects)
        {
            if (go != null)
                Destroy(go);
        }

        _rosterRowObjects.Clear();
        _sessionPlayerRows.Clear();

        int maxP = GameplaySessionUIUtil.GetMaxPlayersOrDefault();
        int cur = GameplaySessionUIUtil.CountPlayersInSession();

        if (sessionPlayerCountText != null)
            sessionPlayerCountText.text = $"{cur}/{maxP}";

        if (sessionRosterLayout == null || sessionPlayerRowPrefab == null)
            return;

        if (!NetworkClient.active)
        {
            GameObject row = Instantiate(sessionPlayerRowPrefab, sessionRosterLayout);
            _rosterRowObjects.Add(row);
            SessionPlayerRowUI ui = row.GetComponent<SessionPlayerRowUI>();
            if (ui != null)
            {
                ui.Bind("本地", isLocalPlayer: true);
                _sessionPlayerRows.Add(ui);
            }
            return;
        }

        List<(uint netId, string displayName)> players = GameplaySessionUIUtil.GetSortedPlayerEntries();
        uint localId = 0;
        if (NetworkClient.localPlayer != null)
            localId = NetworkClient.localPlayer.netId;

        foreach (var entry in players)
        {
            GameObject rowGo = Instantiate(sessionPlayerRowPrefab, sessionRosterLayout);
            _rosterRowObjects.Add(rowGo);
            SessionPlayerRowUI row = rowGo.GetComponent<SessionPlayerRowUI>();
            if (row == null) continue;

            bool isLocal = entry.netId == localId;
            row.Bind(entry.displayName, isLocal);
            _sessionPlayerRows.Add(row);
        }
    }

    static PlayerInteractionSensor FindLocalPlayerSensor()
    {
        if (NetworkClient.active && NetworkClient.localPlayer != null)
        {
            PlayerInteractionSensor s = NetworkClient.localPlayer.GetComponentInChildren<PlayerInteractionSensor>(true);
            if (s != null) return s;
        }

        return Object.FindAnyObjectByType<PlayerInteractionSensor>(FindObjectsInactive.Include);
    }
}
