#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

public static class FindBrokenUIObjects
{
    [MenuItem("Tools/查找损坏的 UI 对象")]
    static void Find()
    {
        var allTransforms = GameObject.FindObjectsByType<RectTransform>(FindObjectsSortMode.None);
        foreach (var rt in allTransforms)
        {
            var components = rt.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] == null)
                {
                    Debug.LogError($"[损坏组件] 物体 '{rt.name}' (路径: {GetPath(rt)}) 的第 {i} 个组件是 null (Missing Script)", rt.gameObject);
                }
            }
        }
        Debug.Log("[扫描完成] 如果没有输出错误，说明当前场景中没有 Missing 组件");
    }

    static string GetPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
}
#endif