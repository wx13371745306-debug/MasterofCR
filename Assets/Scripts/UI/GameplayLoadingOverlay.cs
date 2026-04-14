using System.IO;
using UnityEngine;

/// <summary>
/// 挂在与 <see cref="NetworkManager"/> 同一常驻物体（如 NetworkRoomManager）上，
/// 在 Inspector 中拖入全屏黑底 Panel；由代码在切往游戏场景时显示、本地玩家就绪后隐藏。
/// </summary>
public class GameplayLoadingOverlay : MonoBehaviour
{
    public static GameplayLoadingOverlay Instance { get; private set; }

    [Tooltip("全屏遮罩根物体（含黑底 + 「正在加入」文案），默认应设为 Inactive）")]
    [SerializeField] GameObject overlayRoot;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[GameplayLoadingOverlay] 重复实例，将销毁多余组件。");
            Destroy(this);
            return;
        }

        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>显示「正在加入」遮罩。</summary>
    public void Show()
    {
        if (overlayRoot != null)
            overlayRoot.SetActive(true);
    }

    /// <summary>隐藏遮罩（断线、本地玩家已进入游戏时调用）。</summary>
    public void Hide()
    {
        if (overlayRoot != null)
            overlayRoot.SetActive(false);
    }

    /// <summary>供玩家脚本静态调用，无实例时安全跳过。</summary>
    public static void TryHide()
    {
        if (Instance != null)
            Instance.Hide();
    }

    /// <summary>
    /// 与 NetworkRoomManager.GameplayScene（可能为短名或带路径）及 Mirror 下发的 newSceneName 比对。
    /// </summary>
    public static bool NamesMatchGameplay(string gameplaySceneConfigured, string newSceneNameFromNetwork)
    {
        if (string.IsNullOrEmpty(gameplaySceneConfigured) || string.IsNullOrEmpty(newSceneNameFromNetwork))
            return false;

        string a = Path.GetFileNameWithoutExtension(gameplaySceneConfigured.Replace('\\', '/'));
        string b = Path.GetFileNameWithoutExtension(newSceneNameFromNetwork.Replace('\\', '/'));
        if (a == b)
            return true;

        return newSceneNameFromNetwork == gameplaySceneConfigured
               || newSceneNameFromNetwork.EndsWith(a + ".unity")
               || gameplaySceneConfigured.EndsWith(b + ".unity");
    }
}
