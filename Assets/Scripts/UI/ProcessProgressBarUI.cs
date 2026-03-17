using UnityEngine;
using UnityEngine.UI;

public class ProcessProgressBarUI : MonoBehaviour
{
    [Header("UI References")]
    public Canvas canvas;
    public Image fillImage;
    
    [Header("Settings")]
    public bool alwaysFaceCamera = true;
    public bool hideWhenEmpty = true; // 进度为0或完成时隐藏

    private IProcessable processable;
    private Camera mainCamera;

    void Start()
    {
        // 寻找自身或父物体上的 IProcessable 接口 (例如 ChopProcessable)
        processable = GetComponentInParent<IProcessable>();
        mainCamera = Camera.main;

        // 初始状态下隐藏进度条
        if (canvas != null && hideWhenEmpty)
        {
            canvas.gameObject.SetActive(false);
        }
    }

    void LateUpdate()
    {
        if (processable == null || canvas == null || fillImage == null) return;

        // 判断是否需要显示进度条 (有进度且未完成)
        bool hasProgress = processable.CurrentProgress > 0;
        bool isComplete = processable.IsComplete;
        bool shouldShow = hasProgress && !isComplete;

        // 控制 Canvas 的显隐
        if (hideWhenEmpty && canvas.gameObject.activeSelf != shouldShow)
        {
            canvas.gameObject.SetActive(shouldShow);
        }

        // 如果处于显示状态，更新进度和朝向
        if (canvas.gameObject.activeSelf)
        {
            // 1. 更新进度条填充比例
            fillImage.fillAmount = processable.NormalizedProgress;

            // 2. 广告牌效果：始终朝向摄像机
            if (alwaysFaceCamera && mainCamera != null)
            {
                // 让 UI 的旋转与主摄像机的旋转保持一致
                // 这样无论你怎么走、怎么旋转食材，UI 永远正对着镜头
                transform.rotation = mainCamera.transform.rotation;
            }
        }
    }
}