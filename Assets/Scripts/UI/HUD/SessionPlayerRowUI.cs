using UnityEngine;
using TMPro;
using Mirror;

/// <summary>
/// 局内设置面板中「单名玩家」一行：姓名 + 延迟（本机行显示 NetworkTime RTT，他机无同步时为占位）。
/// 挂在你新建的预制体根物体上，拖好 TMP 引用。
/// </summary>
public class SessionPlayerRowUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] TextMeshProUGUI playerNameText;
    [SerializeField] TextMeshProUGUI latencyText;

    [Tooltip("他机或未就绪时延迟栏显示")]
    [SerializeField] string remoteLatencyPlaceholder = "—";

    bool _isLocalPlayerRow;

    public void Bind(string displayName, bool isLocalPlayer)
    {
        _isLocalPlayerRow = isLocalPlayer;

        if (playerNameText != null)
            playerNameText.text = isLocalPlayer ? $"{displayName}（你）" : displayName;

        if (!isLocalPlayer)
        {
            if (latencyText != null)
                latencyText.text = remoteLatencyPlaceholder;
            return;
        }

        RefreshPingDisplay();
    }

    /// <summary>仅本机行会更新延迟；菜单打开期间由 MoneyUI 每帧调用以刷新 RTT。</summary>
    public void RefreshPingDisplay()
    {
        if (latencyText == null || !_isLocalPlayerRow) return;

        if (NetworkClient.active)
        {
            double ms = NetworkTime.rtt * 1000.0;
            if (ms >= 0 && ms < 60000)
                latencyText.text = $"{Mathf.Round((float)ms)} ms";
            else
                latencyText.text = remoteLatencyPlaceholder;
        }
        else
            latencyText.text = remoteLatencyPlaceholder;
    }
}
