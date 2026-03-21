using UnityEngine;
using UnityEditor;
using System.Text;
using System.IO;

public class PrefabFullDataExporter : EditorWindow
{
    private GameObject targetPrefab;
    private bool exportAllProperties = false; // 是否导出所有底层属性（勾选后内容会非常多）

    [MenuItem("Tools/Prefab/导出预制体完整参数")]
    public static void ShowWindow()
    {
        GetWindow<PrefabFullDataExporter>("参数导出器");
    }

    private void OnGUI()
    {
        GUILayout.Label("预制体属性深度导出", EditorStyles.boldLabel);
        targetPrefab = (GameObject)EditorGUILayout.ObjectField("目标预制体", targetPrefab, typeof(GameObject), false);
        
        exportAllProperties = EditorGUILayout.Toggle("导出隐藏/原生属性", exportAllProperties);
        EditorGUILayout.HelpBox("默认只导出可见的公共参数。开启后会包含更多底层数据。", MessageType.Info);

        if (GUILayout.Button("导出详细报告") && targetPrefab != null)
        {
            ExportFullData();
        }
    }

    private void ExportFullData()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"==========================================");
        sb.AppendLine($"预制体详细数据报告: {targetPrefab.name}");
        sb.AppendLine($"生成时间: {System.DateTime.Now}");
        sb.AppendLine($"==========================================\n");

        Traverse(targetPrefab.transform, sb, 0);

        string path = EditorUtility.SaveFilePanel("保存详细报告", "", $"{targetPrefab.name}_FullReport.txt", "txt");
        if (!string.IsNullOrEmpty(path))
        {
            File.WriteAllText(path, sb.ToString());
            AssetDatabase.Refresh();
            Debug.Log($"<color=cyan>[Exporter]</color> 报告已生成: {path}");
        }
    }

    private void Traverse(Transform t, StringBuilder sb, int indent)
    {
        string space = new string(' ', indent * 4);
        sb.AppendLine($"{space}● 物体: {t.name} (Layer: {LayerMask.LayerToName(t.gameObject.layer)})");

        // 获取该物体上所有的组件
        Component[] components = t.GetComponents<Component>();
        foreach (var comp in components)
        {
            if (comp == null) continue;

            string compName = comp.GetType().Name;
            sb.AppendLine($"{space}  |- [组件: {compName}]");

            // 使用 SerializedObject 遍历属性
            SerializedObject so = new SerializedObject(comp);
            SerializedProperty prop = so.GetIterator();

            // 进入属性树
            bool enterChildren = true;
            while (prop.NextVisible(enterChildren)) // 只遍历 Inspector 可见的属性
            {
                enterChildren = false; // 防止重复进入子节点

                // 排除掉一些没用的基础属性
                if (prop.name == "m_GameObject" || prop.name == "m_Script") continue;

                string valueStr = GetPropertyValue(prop);
                if (!string.IsNullOrEmpty(valueStr))
                {
                    sb.AppendLine($"{space}     |-- {prop.displayName} ({prop.name}): {valueStr}");
                }
            }
        }

        sb.AppendLine("");

        foreach (Transform child in t)
        {
            Traverse(child, sb, indent + 1);
        }
    }

    // 根据属性类型获取其字符串表现形式
    private string GetPropertyValue(SerializedProperty prop)
    {
        switch (prop.propertyType)
        {
            case SerializedPropertyType.Integer: return prop.intValue.ToString();
            case SerializedPropertyType.Boolean: return prop.boolValue.ToString();
            case SerializedPropertyType.Float: return prop.floatValue.ToString("F2");
            case SerializedPropertyType.String: return $"\"{prop.stringValue}\"";
            case SerializedPropertyType.Color: return prop.colorValue.ToString();
            case SerializedPropertyType.ObjectReference: 
                return prop.objectReferenceValue != null ? $"[{prop.objectReferenceValue.name}]" : "None";
            case SerializedPropertyType.Enum:
                return prop.enumDisplayNames[prop.enumValueIndex];
            case SerializedPropertyType.Vector3: return prop.vector3Value.ToString();
            case SerializedPropertyType.Rect: return prop.rectValue.ToString();
            default:
                return exportAllProperties ? $"(Type: {prop.propertyType})" : ""; 
        }
    }
}