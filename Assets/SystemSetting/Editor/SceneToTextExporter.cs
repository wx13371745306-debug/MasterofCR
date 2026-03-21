using UnityEngine;
using UnityEditor;
using System.Text;
using System.IO;

public class SceneToTextExporter : EditorWindow
{
    [MenuItem("Tools/Export Scene for AI")]
    public static void ExportScene()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"--- Scene Structure Export ({System.DateTime.Now}) ---");
        
        // 获取当前场景所有根物体
        GameObject[] rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        
        foreach (GameObject obj in rootObjects)
        {
            ExportObject(obj, sb, 0);
        }

        string path = EditorUtility.SaveFilePanel("Save Scene Export", "", "SceneContext.txt", "txt");
        if (!string.IsNullOrEmpty(path))
        {
            File.WriteAllText(path, sb.ToString());
            Debug.Log("Scene context exported to: " + path);
        }
    }

    private static void ExportObject(GameObject obj, StringBuilder sb, int indent)
    {
        string space = new string(' ', indent * 2);
        sb.AppendLine($"{space}- [GO] {obj.name} (Active: {obj.activeSelf})");

        // 获取该物体上挂载的所有组件
        Component[] components = obj.GetComponents<Component>();
        foreach (var comp in components)
        {
            if (comp == null) continue;
            sb.AppendLine($"{space}  # [Comp] {comp.GetType().Name}");
            
            // 如果是常用的组件，可以记录关键参数（可选）
            if (comp is Transform t)
                sb.AppendLine($"{space}    Pos: {t.localPosition}, Rot: {t.localEulerAngles}");
        }

        // 递归处理子物体
        foreach (Transform child in obj.transform)
        {
            ExportObject(child.gameObject, sb, indent + 1);
        }
    }
}