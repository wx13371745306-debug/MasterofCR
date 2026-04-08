using UnityEngine;
using Mirror;
using TMPro;

/// <summary>
/// 负责在游戏中同步与展示各名玩家昵称的数据实体。
/// 此组件专为局域网改造 Step 3 设计，满足数据下发和解耦呈现。
/// </summary>
public class NetworkPlayerNameSync : NetworkBehaviour
{
    [Header("UI Reference")]
    [Tooltip("指向当前玩家挂载的 World Space 版 Canvas 中的 TextMeshPro 文本")]
    public TextMeshProUGUI nameText; 

    [Header("Debug")]
    public bool debugLog = false;

    // SyncVar 会由服务端推送到所有连接了的客机
    // hook 定义了当这个值发生变更时（对于任何一台客户端）要执行的本地方法。
    [SyncVar(hook = nameof(OnNameChanged))]
    public string playerName = "Unknown Player";

    public override void OnStartClient()
    {
        base.OnStartClient();
        
        // 客机实体刚生成时，主动应用一次当前已同步的内容（防止 Hook 未及时触发引发的一开始白板错乱）
        UpdateNameDisplay(playerName);

        if (debugLog)
            Debug.Log($"[NetworkPlayerNameSync] OnStartClient 实体创建，当前名字被初始赋为: {playerName}");
    }

    private void OnNameChanged(string oldName, string newName)
    {
        if (debugLog)
            Debug.Log($"[NetworkPlayerNameSync] 检测到网络名变动: '{oldName}' -> '{newName}'");

        UpdateNameDisplay(newName);
    }

    private void UpdateNameDisplay(string txt)
    {
        if (nameText != null)
        {
            nameText.text = txt;
        }
    }
}
