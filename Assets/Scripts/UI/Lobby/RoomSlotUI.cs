using UnityEngine;
using TMPro;

/// <summary>
/// 具体到每个固定的玩家槽位 (PlayerSlot_1~4)。
/// 负责自身显隐与状态刷新。
/// </summary>
public class RoomSlotUI : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI playerNameText;
    public TextMeshProUGUI readyStateText;
    
    private CustomRoomPlayer boundPlayer;

    private void OnDestroy()
    {
        Clear();
    }

    public void BindPlayer(CustomRoomPlayer player)
    {
        Clear(); // 先清空旧绑定
        
        boundPlayer = player;
        gameObject.SetActive(true); // 激活当前槽位

        boundPlayer.OnNameUpdateEvent += OnNameUpdated;
        boundPlayer.OnReadyStateUpdateEvent += OnReadyUpdated;

        // 刷新初始数据
        OnNameUpdated(null, boundPlayer.playerName);
        OnReadyUpdated(boundPlayer.readyToBegin);
    }

    public void Clear()
    {
        if (boundPlayer != null)
        {
            boundPlayer.OnNameUpdateEvent -= OnNameUpdated;
            boundPlayer.OnReadyStateUpdateEvent -= OnReadyUpdated;
        }
        boundPlayer = null;
        
        // 此槽位无人时整体隐藏
        gameObject.SetActive(false); 
    }

    private void OnNameUpdated(string oldName, string newName)
    {
        if (playerNameText != null)
        {
            if (boundPlayer != null && boundPlayer.isLocalPlayer)
                playerNameText.text = $"<color=#FFE100>{newName} (You)</color>";
            else
                playerNameText.text = newName;
        }
    }

    private void OnReadyUpdated(bool isReady)
    {
        if (readyStateText != null)
        {
            readyStateText.text = isReady ? "<color=#00FF00>已准备</color>" : "<color=#AAAAAA>等待中</color>";
        }
    }
}
