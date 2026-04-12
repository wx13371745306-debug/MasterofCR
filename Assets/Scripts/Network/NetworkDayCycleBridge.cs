using UnityEngine;
using Mirror;

/// <summary>
/// DayCycleManager 的网络桥接层。
/// 挂在与 DayCycleManager 同一个 GameObject 上（需要有 NetworkIdentity）。
/// Host 端的 DayCycleManager 驱动所有逻辑，本脚本负责把关键状态同步给 Guest。
/// Guest 端的 DayCycleManager.Update 计时被禁用，完全由 SyncVar 驱动。
/// </summary>
public class NetworkDayCycleBridge : NetworkBehaviour
{
    [Header("Debug")]
    public bool debugLog = true;

    [SyncVar(hook = nameof(OnPhaseChanged))]
    private DayCyclePhase syncedPhase = DayCyclePhase.DayZero;

    [SyncVar]
    private int syncedDayIndex;

    [SyncVar]
    private float syncedRemainingClock;

    private DayCycleManager dayCycle;

    /// <summary>联机环境下，当前机器是否为服务端（Host）</summary>
    public bool IsHostAuthority => NetworkServer.active;

    public static NetworkDayCycleBridge Instance { get; private set; }

    void Awake()
    {
        Instance = this;
        dayCycle = GetComponent<DayCycleManager>();
    }

    void Update()
    {
        if (!NetworkClient.active) return;
        if (dayCycle == null) return;

        if (IsHostAuthority)
        {
            syncedPhase = dayCycle.Phase;
            syncedDayIndex = dayCycle.CurrentDayIndex;
            syncedRemainingClock = dayCycle.RemainingClockSeconds;
        }
    }

    /// <summary>Guest 端的 DayCycleManager 不走 Update 计时，但需要持续接收同步的剩余时间。</summary>
    public float GetSyncedRemainingClock() => syncedRemainingClock;

    // ── Guest 请求操作 → Command → Host 执行 → SyncVar 自动广播 ──

    /// <summary>供 UI 按钮调用（替代直接调用 DayCycleManager.RequestStartFirstDay）</summary>
    public void NetworkRequestStartFirstDay()
    {
        if (IsHostAuthority)
        {
            dayCycle.RequestStartFirstDay();
        }
        else
        {
            CmdRequestStartFirstDay();
        }
    }

    [Command(requiresAuthority = false)]
    void CmdRequestStartFirstDay()
    {
        if (debugLog) Debug.Log("[NetworkDayCycleBridge] Guest 请求开始第一天");
        if (dayCycle != null)
            dayCycle.RequestStartFirstDay();
    }

    /// <summary>供 UI 按钮调用（替代直接调用 DayCycleManager.RequestNextDay）</summary>
    public void NetworkRequestNextDay()
    {
        if (IsHostAuthority)
        {
            dayCycle.RequestNextDay();
        }
        else
        {
            CmdRequestNextDay();
        }
    }

    [Command(requiresAuthority = false)]
    void CmdRequestNextDay()
    {
        if (debugLog) Debug.Log("[NetworkDayCycleBridge] Guest 请求下一天");
        if (dayCycle != null)
            dayCycle.RequestNextDay();
    }

    // ── SyncVar Hook：Guest 端收到 phase 变化时，驱动本地 DayCycleManager 进入对应阶段 ──

    void OnPhaseChanged(DayCyclePhase oldPhase, DayCyclePhase newPhase)
    {
        if (IsHostAuthority) return;
        if (dayCycle == null) return;

        if (debugLog)
            Debug.Log($"[NetworkDayCycleBridge] Guest 收到阶段同步: {oldPhase} → {newPhase}");

        dayCycle.ApplyNetworkPhase(newPhase, syncedDayIndex);
    }
}
