using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Mirror.Discovery;

/// <summary>
/// 当网络上搜索到主机房间时，大厅克隆出来的这个记录条目 UI 控制。
/// 附带点击“加入”的回调传递。
/// </summary>
public class RoomLineUIEntry : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI roomIDText;
    public TextMeshProUGUI playerCountText;

    [Tooltip("局内展示模式时隐藏加入按钮；不拖则自动查找子物体上的第一个 Button。")]
    [SerializeField] Button joinButton;

    private ServerResponse cachedServerInfo;
    private LobbyUIManager cachedUIManager;

    void Awake()
    {
        if (joinButton == null)
            joinButton = GetComponentInChildren<Button>(true);
    }

    public void Setup(ServerResponse info, LobbyUIManager manager)
    {
        cachedServerInfo = info;
        cachedUIManager = manager;

        // 根据你传入或定制的 Discovery 数据解包。这里做一个示例：
        if (roomIDText != null)
            roomIDText.text = $"Room: {info.uri.Host}"; // 默认利用 IP/主机名代替
            
        // 如果你的 ServerResponse 扩充了当前的游玩人数（默认为 -1 需开发者自己实现载荷）
        // 这里就简单地用 ?/4 代替。
        if (playerCountText != null)
            playerCountText.text = "?/4";

        SetJoinButtonVisible(true);
    }

    /// <summary>
    /// 局内设置：与大厅同款布局，仅展示文案（隐藏加入）。
    /// </summary>
    public void SetupAsGameplayRow(string leftTitle, string rightText, bool showJoinButton)
    {
        cachedServerInfo = default;
        cachedUIManager = null;

        if (roomIDText != null)
            roomIDText.text = leftTitle ?? "";
        if (playerCountText != null)
            playerCountText.text = rightText ?? "";

        SetJoinButtonVisible(showJoinButton);
    }

    void SetJoinButtonVisible(bool visible)
    {
        if (joinButton != null)
            joinButton.gameObject.SetActive(visible);
    }

    /// <summary>
    /// 【UI 绑定】供预制体上的“JoinRoom_Button”调用的点击事件
    /// </summary>
    public void OnClickJoin()
    {
        if (cachedUIManager != null)
        {
            cachedUIManager.JoinDiscoveredRoom(cachedServerInfo);
        }
    }
}
