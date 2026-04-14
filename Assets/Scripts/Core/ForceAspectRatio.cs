using UnityEngine;

[ExecuteAlways] // 加上这个标签，在编辑器里不运行游戏也能实时看到黑边效果
[RequireComponent(typeof(Camera))]
public class ForceAspectRatio : MonoBehaviour
{
    [Header("目标比例 (例如 16:9)")]
    public Vector2 targetAspect = new Vector2(16f, 9f);

    private Camera cam;
    private int lastScreenWidth = 0;
    private int lastScreenHeight = 0;

    void Start()
    {
        cam = GetComponent<Camera>();
        ApplyLetterbox();
    }

    void Update()
    {
        // 只有在屏幕/窗口大小发生变化时才重新计算，节省性能
        if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
        {
            ApplyLetterbox();
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
        }
    }

    private void ApplyLetterbox()
    {
        if (cam == null) return;

        // 计算目标宽高比和当前屏幕宽高比
        float targetRatio = targetAspect.x / targetAspect.y;
        float windowRatio = (float)Screen.width / (float)Screen.height;
        
        // 计算当前屏幕相对于目标比例的高度缩放值
        float scaleHeight = windowRatio / targetRatio;

        if (scaleHeight < 1.0f)
        {
            // 屏幕比较方（比如 4:3），或者窗口被拖窄了
            // 需要在上下加黑边 (Letterbox)
            Rect rect = cam.rect;
            rect.width = 1.0f;
            rect.height = scaleHeight;
            rect.x = 0;
            rect.y = (1.0f - scaleHeight) / 2.0f;
            cam.rect = rect;
        }
        else
        {
            // 屏幕比较宽（比如 21:9 带鱼屏），或者窗口被拖宽了
            // 需要在左右加黑边 (Pillarbox)
            float scaleWidth = 1.0f / scaleHeight;
            Rect rect = cam.rect;
            rect.width = scaleWidth;
            rect.height = 1.0f;
            rect.x = (1.0f - scaleWidth) / 2.0f;
            rect.y = 0;
            cam.rect = rect;
        }
    }
}