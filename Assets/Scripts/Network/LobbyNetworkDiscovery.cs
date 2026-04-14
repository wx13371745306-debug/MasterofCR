using System.Net;
using Mirror;
using Mirror.Discovery;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 仅在房间场景（仍可加入大厅）时响应局域网发现请求；已进入游戏场景时不回复，避免列表里出现「可加入」的假象。
/// </summary>
public class LobbyNetworkDiscovery : NetworkDiscovery
{
    [Header("联机诊断")]
    [Tooltip("开启后输出局域网发现：为何不回包、成功回包（节流，避免刷屏）")]
    public bool diagLog = true;

    /// <summary>忽略请求日志节流（秒）</summary>
    const float IgnoreLogInterval = 2f;

    /// <summary>成功响应日志节流（秒）</summary>
    const float ReplyLogInterval = 2f;

    float _lastIgnoreLogTime = -999f;
    float _lastReplyLogTime = -999f;

    /// <remarks>
    /// Mirror 的 <see cref="ServerResponse"/> 是 struct，<see cref="NetworkDiscovery.ProcessRequest"/> 不能返回 null；
    /// 改为在 <see cref="NetworkDiscoveryBase{ServerRequest, ServerResponse}.ProcessClientRequest"/> 层短路。
    /// </remarks>
    protected override void ProcessClientRequest(ServerRequest request, IPEndPoint endpoint)
    {
        NetworkRoomManager rm = NetworkManager.singleton as NetworkRoomManager;
        if (rm != null && !string.IsNullOrEmpty(rm.RoomScene) && !Utils.IsSceneActive(rm.RoomScene))
        {
            if (diagLog && Time.unscaledTime - _lastIgnoreLogTime >= IgnoreLogInterval)
            {
                _lastIgnoreLogTime = Time.unscaledTime;
                Scene s = SceneManager.GetActiveScene();
                Debug.Log(
                    "[LobbyDiscovery] 忽略发现请求：当前激活场景不是 RoomScene（主机不会对局域网广播回包）。\n" +
                    $"  activeScene.name={s.name} activeScene.path={s.path}\n" +
                    $"  RoomScene(配置)={rm.RoomScene}  IsSceneActive(RoomScene)={Utils.IsSceneActive(rm.RoomScene)}\n" +
                    $"  请求来源 endpoint={endpoint}");
            }
            return;
        }

        base.ProcessClientRequest(request, endpoint);
    }

    protected override ServerResponse ProcessRequest(ServerRequest request, IPEndPoint endpoint)
    {
        ServerResponse response = base.ProcessRequest(request, endpoint);
        if (diagLog && Time.unscaledTime - _lastReplyLogTime >= ReplyLogInterval)
        {
            _lastReplyLogTime = Time.unscaledTime;
            Debug.Log(
                "[LobbyDiscovery] 已响应局域网发现请求。\n" +
                $"  serverId={response.serverId} uri={response.uri} 请求来源={endpoint}");
        }
        return response;
    }
}
