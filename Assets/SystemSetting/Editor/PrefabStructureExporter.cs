using UnityEngine;
using UnityEditor;
using System.Text;

public class PrefabExporter : Editor
{
    [MenuItem("Tools/游戏开发助手/一键导出选中预制体参数 (复制到剪贴板)")]
    public static void ExportPrefabToClipboard()
    {
        // 获取当前选中的 GameObject（支持在 Project 窗口选中 Prefab，或在 Hierarchy 中选中物体）
        GameObject selectedGO = Selection.activeGameObject;

        if (selectedGO == null)
        {
            Debug.LogWarning("<color=#FF9900>[PrefabExporter]</color> 导出失败：请先选中一个 GameObject 或 Prefab！");
            return;
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=========================================");
        sb.AppendLine($"# Prefab / GameObject: {selectedGO.name}");
        sb.AppendLine($"# 导出时间: {System.DateTime.Now}");
        sb.AppendLine("=========================================\n");

        // 从根节点开始深度遍历
        TraverseGameObject(selectedGO, sb, 0);

        // 写入剪贴板
        EditorGUIUtility.systemCopyBuffer = sb.ToString();

        Debug.Log($"<color=#00FF00>[PrefabExporter]</color> 成功！<b>{selectedGO.name}</b> 的结构与参数已复制到系统剪贴板！");
    }

    private static void TraverseGameObject(GameObject go, StringBuilder sb, int indentLevel)
    {
        string indent = new string(' ', indentLevel * 4);
        sb.AppendLine($"{indent}▶ [{go.name}]");

        // 1. 读取该物体上的所有组件
        Component[] components = go.GetComponents<Component>();
        foreach (Component comp in components)
        {
            if (comp == null)
            {
                sb.AppendLine($"{indent}    - (Missing Script)");
                continue;
            }

            sb.AppendLine($"{indent}    - ({comp.GetType().Name})");
            DumpComponentProperties(comp, sb, indentLevel + 1);
        }

        // 2. 递归遍历所有子物体
        foreach (Transform child in go.transform)
        {
            TraverseGameObject(child.gameObject, sb, indentLevel + 1);
        }
    }

    private static void DumpComponentProperties(Component comp, StringBuilder sb, int indentLevel)
    {
        string indent = new string(' ', indentLevel * 4);
        SerializedObject so = new SerializedObject(comp);
        SerializedProperty prop = so.GetIterator();

        bool enterChildren = true;

        while (prop.NextVisible(enterChildren))
        {
            // 对于普通的属性，我们不需要深入其底层的 struct 细节，除非它是我们要展开的 Generic 类型
            enterChildren = false; 

            // 屏蔽掉一些毫无意义的 Unity 内部属性，让导出的代码更干净
            if (prop.name == "m_ObjectHideFlags" || 
                prop.name == "m_CorrespondingSourceObject" || 
                prop.name == "m_PrefabInstance" || 
                prop.name == "m_PrefabAsset" || 
                prop.name == "m_Script")
            {
                continue;
            }

            // 安全获取属性值
            string valueStr = GetPropertyValueSafe(prop);
            sb.AppendLine($"{indent}  {prop.name}: {valueStr}");
        }
    }

    private static string GetPropertyValueSafe(SerializedProperty prop)
    {
        try
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer: return prop.intValue.ToString();
                case SerializedPropertyType.Boolean: return prop.boolValue ? "True" : "False";
                case SerializedPropertyType.Float: return prop.floatValue.ToString("F3");
                case SerializedPropertyType.String: return string.IsNullOrEmpty(prop.stringValue) ? "\"\"" : $"\"{prop.stringValue}\"";
                case SerializedPropertyType.Color: return prop.colorValue.ToString();
                case SerializedPropertyType.ObjectReference: return prop.objectReferenceValue != null ? prop.objectReferenceValue.name : "null";
                case SerializedPropertyType.LayerMask: return prop.intValue.ToString();
                case SerializedPropertyType.Vector2: return prop.vector2Value.ToString();
                case SerializedPropertyType.Vector3: return prop.vector3Value.ToString();
                case SerializedPropertyType.Vector4: return prop.vector4Value.ToString();
                case SerializedPropertyType.Rect: return prop.rectValue.ToString();
                case SerializedPropertyType.ArraySize: return $"[Array Size: {prop.intValue}]";
                case SerializedPropertyType.Character: return $"'{((char)prop.intValue)}'";
                case SerializedPropertyType.AnimationCurve: return "[AnimationCurve]";
                case SerializedPropertyType.Bounds: return prop.boundsValue.ToString();
                
                // 【核心修复点】：针对枚举类型的越界安全检查
                case SerializedPropertyType.Enum:
                    if (prop.enumValueIndex >= 0 && prop.enumValueIndex < prop.enumDisplayNames.Length)
                    {
                        return prop.enumDisplayNames[prop.enumValueIndex];
                    }
                    return $"[Invalid Enum Index: {prop.enumValueIndex}]";

                // 其他通用类型（如 struct, array），直接返回其类型名
                case SerializedPropertyType.Generic:
                    return prop.isArray ? "[Array/List]" : "[Generic Struct]";

                default:
                    return $"[{prop.propertyType.ToString()}]";
            }
        }
        catch (System.Exception ex)
        {
            // 无论发生什么底层错误，直接截断捕获，绝不崩溃
            return $"[读取错误: {ex.Message}]";
        }
    }
}