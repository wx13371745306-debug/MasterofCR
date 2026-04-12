using System.Collections.Generic;
using UnityEngine;
using Mirror;

/// <summary>
/// 局内设置菜单：联机人数统计、玩家名列表（基于客户端已同步的 NetworkIdentity）。
/// </summary>
public static class GameplaySessionUIUtil
{
    public const string PrefKeyShowFps = "Settings_ShowFps";
    public const string PrefKeySensorDebug = "Settings_SensorDebug";

    /// <summary>用于设置面板「房间号」展示：联机为连接地址/主机标识；单机为「单机」。</summary>
    public static string GetRoomDisplayLabel()
    {
        if (!NetworkClient.active && !NetworkServer.active)
            return "单机";

        if (NetworkServer.active && NetworkClient.isConnected)
        {
            var nm = NetworkManager.singleton;
            if (nm != null && !string.IsNullOrEmpty(nm.networkAddress))
                return $"主机 · {nm.networkAddress}";
            return "主机";
        }

        // Mirror 客户端连接无 .address；展示用 NetworkManager 连接时使用的地址（与官方 HUD 一致）
        var n = NetworkManager.singleton;
        if (n != null && !string.IsNullOrEmpty(n.networkAddress))
            return n.networkAddress;

        return "联机中";
    }

    /// <summary>当前局内玩家数（联机：已生成且含 NetworkPlayerNameSync 的实体数；单机：1）。</summary>
    public static int CountPlayersInSession()
    {
        if (!NetworkClient.active)
            return 1;

        int n = 0;
        foreach (var kv in NetworkClient.spawned)
        {
            if (kv.Value == null) continue;
            if (kv.Value.GetComponent<NetworkPlayerNameSync>() != null)
                n++;
        }

        return n > 0 ? n : 1;
    }

    public static int GetMaxPlayersOrDefault()
    {
        if (NetworkManager.singleton is CustomNetworkRoomManager rm)
            return rm.maxConnections;
        return 4;
    }

    /// <summary>按 netId 排序的玩家显示名（与大厅同步名一致）。</summary>
    public static List<(uint netId, string displayName)> GetSortedPlayerEntries()
    {
        var list = new List<(uint netId, string displayName)>();
        if (!NetworkClient.active)
            return list;

        foreach (var kv in NetworkClient.spawned)
        {
            if (kv.Value == null) continue;
            NetworkPlayerNameSync ns = kv.Value.GetComponent<NetworkPlayerNameSync>();
            if (ns == null) continue;
            list.Add((kv.Key, string.IsNullOrEmpty(ns.playerName) ? $"Player {kv.Key}" : ns.playerName));
        }

        list.Sort((a, b) => a.netId.CompareTo(b.netId));
        return list;
    }
}
