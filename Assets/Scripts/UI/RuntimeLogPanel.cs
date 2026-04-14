using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// 运行时捕获 <see cref="Debug.Log"/> 并显示在 ScrollView + 旧版 Text 上（避免 TMP 缺字刷屏）；日志回调可能非主线程，经队列在 Update 中刷新。
/// 挂在你常驻激活的 Canvas 下即可。
/// </summary>
[DisallowMultipleComponent]
public class RuntimeLogPanel : MonoBehaviour
{
    [Header("UI 引用")]
    [Tooltip("整块日志面板的根（可选）；用于显示/隐藏")]
    [SerializeField] GameObject panelRoot;

    [Tooltip("ScrollRect 的 Content 下挂的 UnityEngine.UI.Text（旧版 Text）")]
    [SerializeField] Text logText;

    [Tooltip("可选：用于滚到底部")]
    [SerializeField] ScrollRect scrollRect;

    [Tooltip("可选：点击切换面板显示")]
    [SerializeField] Button toggleButton;

    [Header("行为")]
    [SerializeField] bool startVisible = false;

    [Tooltip("无按钮时可用键盘切换（新输入系统）")]
    [SerializeField] Key toggleKey = Key.F3;

    [Tooltip("是否响应快捷键（关闭则仅按钮）")]
    [SerializeField] bool enableHotkey = true;

    [Tooltip("保留最近多少行（超出则删头部）")]
    [Min(10)] [SerializeField] int maxLines = 250;

    [Tooltip("Error/Exception 时是否附带堆栈（信息量大，联机建议关）")]
    [SerializeField] bool includeStackForErrors = false;

    [Tooltip("新日志后自动滚到底部")]
    [SerializeField] bool autoScrollToBottom = true;

    readonly ConcurrentQueue<string> _pending = new ConcurrentQueue<string>();
    readonly List<string> _lines = new List<string>(256);
    readonly StringBuilder _sb = new StringBuilder(8192);

    void Awake()
    {
        if (panelRoot != null)
            panelRoot.SetActive(startVisible);

        if (toggleButton != null)
            toggleButton.onClick.AddListener(ToggleVisible);
    }

    void OnEnable()
    {
        Application.logMessageReceivedThreaded += HandleLogThreaded;
    }

    void OnDisable()
    {
        Application.logMessageReceivedThreaded -= HandleLogThreaded;
    }

    void HandleLogThreaded(string condition, string stackTrace, LogType type)
    {
        string prefix = $"[{System.DateTime.Now:HH:mm:ss}][{type}] ";
        var line = prefix + condition;
        if (includeStackForErrors && (type == LogType.Error || type == LogType.Exception) && !string.IsNullOrEmpty(stackTrace))
            line += "\n" + stackTrace;

        _pending.Enqueue(line);
    }

    void Update()
    {
        if (enableHotkey && toggleKey != Key.None && Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame)
            ToggleVisible();

        bool any = false;
        while (_pending.TryDequeue(out string line))
        {
            any = true;
            _lines.Add(line);
            while (_lines.Count > maxLines)
                _lines.RemoveAt(0);
        }

        if (!any || logText == null)
            return;

        _sb.Clear();
        for (int i = 0; i < _lines.Count; i++)
        {
            if (i > 0) _sb.Append('\n');
            _sb.Append(_lines[i]);
        }

        logText.text = _sb.ToString();

        if (autoScrollToBottom && scrollRect != null)
            scrollRect.verticalNormalizedPosition = 0f;
    }

    /// <summary>供按钮或外部调用</summary>
    public void ToggleVisible()
    {
        if (panelRoot == null) return;
        panelRoot.SetActive(!panelRoot.activeSelf);
    }

    public void SetVisible(bool visible)
    {
        if (panelRoot != null)
            panelRoot.SetActive(visible);
    }

    /// <summary>清空已显示缓冲（仍会接收后续 Log）</summary>
    public void ClearBuffer()
    {
        _lines.Clear();
        if (logText != null)
            logText.text = string.Empty;
    }
}
