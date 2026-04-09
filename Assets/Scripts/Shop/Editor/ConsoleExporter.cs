using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Text;

[InitializeOnLoad]
public class ConsoleExporter
{
    // 用于在内存中缓存日志
    private static List<string> logHistory = new List<string>();
    // 线程锁，防止多线程日志写入冲突
    private static readonly object lockObj = new object();

    // 静态构造函数：每次代码编译或编辑器打开时自动执行
    static ConsoleExporter()
    {
        // 先注销再注册，防止重复订阅
        Application.logMessageReceivedThreaded -= OnLogMessage;
        Application.logMessageReceivedThreaded += OnLogMessage;
    }

    private static void OnLogMessage(string condition, string stackTrace, LogType type)
    {
        lock (lockObj)
        {
            // 限制最大缓存数量，防止内存泄漏（保留最近 10000 条）
            if (logHistory.Count > 10000) logHistory.RemoveAt(0);

            // 格式化日志
            string time = System.DateTime.Now.ToString("HH:mm:ss");
            string logEntry = $"[{time}] [{type}] {condition}";

            // 如果是报错或警告，附加上详细的堆栈追踪信息
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Warning)
            {
                logEntry += $"\n{stackTrace}";
            }
            
            logHistory.Add(logEntry);
        }
    }

    [MenuItem("Tools/游戏开发助手/一键复制当前运行日志")]
    public static void CopyLogsToClipboard()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=========================================");
        sb.AppendLine("          Unity 游戏运行日志提取         ");
        sb.AppendLine($"          提取时间: {System.DateTime.Now}");
        sb.AppendLine("=========================================\n");

        lock (lockObj)
        {
            if (logHistory.Count == 0)
            {
                Debug.LogWarning("<color=#FF9900>[ConsoleExporter]</color> 当前没有任何日志缓存哦！");
                return;
            }


            foreach (var log in logHistory)
            {
                sb.AppendLine(log);
            }
        }

        // ==========================================
        // 核心修改点：直接写入系统剪贴板
        // ==========================================
        EditorGUIUtility.systemCopyBuffer = sb.ToString();
        
        Debug.Log($"<color=#00FF00>[ConsoleExporter]</color> 成功！已将 {logHistory.Count} 条完整日志复制到剪贴板，快去粘贴给 Cursor 吧！");
    }

    [MenuItem("Tools/游戏开发助手/清空后台日志缓存")]
    public static void ClearLogs()
    {
        lock (lockObj)
        {
            logHistory.Clear();
        }
        Debug.Log("<color=#00FF00>[ConsoleExporter]</color> 后台日志缓存已清空！");
    }
}