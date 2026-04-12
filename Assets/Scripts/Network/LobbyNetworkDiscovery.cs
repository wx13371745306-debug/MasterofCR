using System.Net;
using Mirror;
using Mirror.Discovery;

/// <summary>
/// 仅在房间场景（仍可加入大厅）时响应局域网发现请求；已进入游戏场景时不回复，避免列表里出现「可加入」的假象。
/// </summary>
public class LobbyNetworkDiscovery : NetworkDiscovery
{
    /// <remarks>
    /// Mirror 的 <see cref="ServerResponse"/> 是 struct，<see cref="NetworkDiscovery.ProcessRequest"/> 不能返回 null；
    /// 改为在 <see cref="NetworkDiscoveryBase{ServerRequest, ServerResponse}.ProcessClientRequest"/> 层短路。
    /// </remarks>
    protected override void ProcessClientRequest(ServerRequest request, IPEndPoint endpoint)
    {
        NetworkRoomManager rm = NetworkManager.singleton as NetworkRoomManager;
        if (rm != null && !string.IsNullOrEmpty(rm.RoomScene) && !Utils.IsSceneActive(rm.RoomScene))
            return;

        base.ProcessClientRequest(request, endpoint);
    }
}
