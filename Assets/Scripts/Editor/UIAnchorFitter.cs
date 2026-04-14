using UnityEngine;
using UnityEditor;

public class UIAnchorFitter : Editor
{
    [MenuItem("Tools/游戏开发助手/一键 UI 锚点贴合 (设为等比缩放)")]
    public static void FitAnchors()
    {
        GameObject[] selectedObjects = Selection.gameObjects;

        if (selectedObjects.Length == 0)
        {
            Debug.LogWarning("<color=#FF9900>[UI 助手]</color> 请先在 Hierarchy 中选中你需要等比缩放的 UI 子物体（比如那些按钮）。");
            return;
        }

        foreach (GameObject go in selectedObjects)
        {
            RectTransform t = go.GetComponent<RectTransform>();
            if (t == null || t.parent == null) continue;

            RectTransform pt = t.parent.GetComponent<RectTransform>();
            if (pt == null) continue;

            // 记录 Undo，允许你按 Ctrl+Z 撤销
            Undo.RecordObject(t, "Fit Anchors");

            // 计算当前相对于父级的百分比位置
            Vector2 newAnchorsMin = new Vector2(
                t.anchorMin.x + t.offsetMin.x / pt.rect.width,
                t.anchorMin.y + t.offsetMin.y / pt.rect.height
            );
            Vector2 newAnchorsMax = new Vector2(
                t.anchorMax.x + t.offsetMax.x / pt.rect.width,
                t.anchorMax.y + t.offsetMax.y / pt.rect.height
            );

            // 应用新的锚点，并将偏移量归零
            t.anchorMin = newAnchorsMin;
            t.anchorMax = newAnchorsMax;
            t.offsetMin = t.offsetMax = Vector2.zero;
        }

        Debug.Log($"<color=#00FF00>[UI 助手]</color> 成功！已将 {selectedObjects.Length} 个 UI 元素转换为百分比等比缩放模式！");
    }
}