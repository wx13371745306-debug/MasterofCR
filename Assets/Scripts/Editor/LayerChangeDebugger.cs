using UnityEngine;

public class LayerChangeDebugger : MonoBehaviour
{
    private int lastLayer;
    private string lastLayerName;

    void Awake()
    {
        lastLayer = gameObject.layer;
        lastLayerName = LayerMask.LayerToName(lastLayer);
        Debug.Log($"<color=#00FFFF>[LayerDebug]</color> {name} 初始 Layer: <b>{lastLayerName}</b> ({lastLayer}) | Frame {Time.frameCount}");
    }

    void Update()
    {
        CheckLayerChange("Update");
    }

    void LateUpdate()
    {
        CheckLayerChange("LateUpdate");
    }

    private void CheckLayerChange(string phase)
    {
        int current = gameObject.layer;
        if (current == lastLayer) return;

        string currentName = LayerMask.LayerToName(current);
        string stackTrace = System.Environment.StackTrace;

        Debug.LogWarning(
            $"<color=#FF4444>[LayerDebug]</color> <b>{name}</b> 的 Layer 被改变了!\n" +
            $"  旧 Layer: <b>{lastLayerName}</b> ({lastLayer})\n" +
            $"  新 Layer: <b>{currentName}</b> ({current})\n" +
            $"  检测阶段: {phase} | Frame: {Time.frameCount} | Time: {Time.time:F3}s\n" +
            $"  调用堆栈:\n{stackTrace}");

        lastLayer = current;
        lastLayerName = currentName;
    }
}
