using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 通用弹窗控制器：支持"纯警告"（单按钮关闭）和"确认询问"（双按钮）两种模式。
/// 采用隐藏面板方式，默认 SetActive(false)，需要时激活。
/// </summary>
public class NextDayConfirmUI : MonoBehaviour
{
    [Header("Warning Panel (菜单为空时弹出)")]
    [Tooltip("纯警告面板的根节点（默认隐藏）")]
    public GameObject warningPanel;
    [Tooltip("警告文字")]
    public TextMeshProUGUI warningText;
    [Tooltip("警告面板的关闭按钮")]
    public Button warningCloseBtn;

    [Header("Confirm Panel (购物车为空时弹出)")]
    [Tooltip("确认面板的根节点（默认隐藏）")]
    public GameObject confirmPanel;
    [Tooltip("确认面板的提示文字")]
    public TextMeshProUGUI confirmText;
    [Tooltip("\"确定\" 按钮")]
    public Button confirmYesBtn;
    [Tooltip("\"取消\" 按钮")]
    public Button confirmNoBtn;

    [Header("Debug")]
    public bool debugLog = false;

    private Action pendingConfirmAction;

    void Start()
    {
        // 确保两个面板默认隐藏
        HideAll();

        // 绑定按钮事件
        if (warningCloseBtn != null)
            warningCloseBtn.onClick.AddListener(HideAll);

        if (confirmYesBtn != null)
            confirmYesBtn.onClick.AddListener(OnConfirmYes);

        if (confirmNoBtn != null)
            confirmNoBtn.onClick.AddListener(HideAll);
    }

    /// <summary>
    /// 显示纯警告弹窗（单按钮关闭，不允许继续操作）。
    /// </summary>
    public void ShowWarning(string message)
    {
        HideAll();
        if (warningPanel != null)
        {
            if (warningText != null) warningText.text = message;
            warningPanel.SetActive(true);
        }
        if (debugLog) Debug.Log($"[NextDayConfirmUI] 显示警告: {message}");
    }

    /// <summary>
    /// 显示确认弹窗（双按钮：确定/取消）。
    /// 用户点击"确定"时执行传入的回调。
    /// </summary>
    public void ShowConfirm(string message, Action onConfirm)
    {
        HideAll();
        pendingConfirmAction = onConfirm;
        if (confirmPanel != null)
        {
            if (confirmText != null) confirmText.text = message;
            confirmPanel.SetActive(true);
        }
        if (debugLog) Debug.Log($"[NextDayConfirmUI] 显示确认弹窗: {message}");
    }

    /// <summary>关闭所有弹窗。</summary>
    public void HideAll()
    {
        if (warningPanel != null) warningPanel.SetActive(false);
        if (confirmPanel != null) confirmPanel.SetActive(false);
        pendingConfirmAction = null;
    }

    private void OnConfirmYes()
    {
        if (debugLog) Debug.Log("[NextDayConfirmUI] 用户点击了确定");
        Action action = pendingConfirmAction;
        HideAll();
        action?.Invoke();
    }
}
