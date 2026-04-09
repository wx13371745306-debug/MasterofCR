using UnityEngine;
using TMPro;

/// <summary>
/// 具体某个玩家在大厅/房间条目内的 UI 显示控制。
/// 挂载在你做好的代表玩家状态的一小格 UI 预制体上（包含名字文本和准备状态文本）。
/// </summary>
public class RoomPlayerUIEntry : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("显示玩家名字的 UI 文本部件")]
    public TextMeshProUGUI playerNameText;
    
    [Tooltip("显示是否已准备好的文字或高亮图部件")]
    public TextMeshProUGUI readyStateText;
    
    // 绑定的数据源
    private CustomRoomPlayer attachedPlayer;

    public void Setup(CustomRoomPlayer player)
    {
        attachedPlayer = player;
        
        // 挂载数据变动事件监听器
        attachedPlayer.OnNameUpdateEvent += RefreshName;
        attachedPlayer.OnReadyStateUpdateEvent += RefreshReadyState;
        
        // 初始化初始显示
        RefreshName(null, attachedPlayer.playerName);
        RefreshReadyState(attachedPlayer.readyToBegin);
    }
    
    private void OnDestroy()
    {
        // UI 被销毁时，切记注销事件监听，防止内存泄漏空指针
        if (attachedPlayer != null)
        {
            attachedPlayer.OnNameUpdateEvent -= RefreshName;
            attachedPlayer.OnReadyStateUpdateEvent -= RefreshReadyState;
        }
    }

    private void RefreshName(string oldName, string newName)
    {
        if (playerNameText != null)
        {
            // 你也可以在这里加入一些颜色富文本，比如如果这是自己，就高亮为黄色
            if (attachedPlayer != null && attachedPlayer.isLocalPlayer)
                playerNameText.text = $"<color=#FFE100>{newName} (You)</color>";
            else
                playerNameText.text = newName;
        }
    }

    private void RefreshReadyState(bool isReady)
    {
        if (readyStateText != null)
        {
            readyStateText.text = isReady ? "<color=#00FF00>V 已准备</color>" : "<color=#AAAAAA>等待中</color>";
        }
    }
}
