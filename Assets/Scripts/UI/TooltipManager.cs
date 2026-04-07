using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 全局悬浮提示框管理器（单例）。
/// 持有"文字面板"和"图片面板"两个子面板，由 Trigger 组件驱动显示/隐藏。
///
/// 【Unity 编辑器配置】
/// 1. 在 Canvas 的 **最底部**（Hierarchy 最下方 = 渲染最前）创建一个空物体，命名为 "TooltipManager"，挂上本脚本。
/// 2. 在 TooltipManager 下创建两个子面板：
///    - TextTooltipPanel：带 Image（背景）+ TextMeshPro - Text (TMP) 子节点
///    - ImageTooltipPanel：带 Image（用于显示配方图/说明图）
/// 3. 每个面板都加一个 CanvasGroup 组件，取消勾选 Blocks Raycasts（防止面板抢夺射线导致闪烁）。
/// 4. Pivot 无需手动设置，脚本会根据鼠标位置自动调整面板展开方向。
/// 5. 把面板和对应的 Text / Image 组件拖入本脚本的序列化字段即可。
/// 
/// </summary>
public class TooltipManager : MonoBehaviour
{
    // ======================== 单例 ========================
    public static TooltipManager Instance { get; private set; }

    // ======================== 序列化字段 ========================

    [Header("文字提示面板")]
    [SerializeField] private RectTransform textPanel;
    [SerializeField] private TextMeshProUGUI textComponent;

    [Header("图片提示面板")]
    [SerializeField] private RectTransform imagePanel;
    [SerializeField] private Image imageComponent;

    [Header("跟随偏移（屏幕像素）")]
    [Tooltip("鼠标指针右下方的偏移量，防止提示框遮挡鼠标")]
    [SerializeField] private Vector2 offset = new Vector2(16f, -16f);

    // ======================== 私有缓存 ========================

    /// <summary>父级 Canvas 的 RectTransform，用于坐标转换</summary>
    private RectTransform canvasRect;

    /// <summary>父级 Canvas 引用，用于判断渲染模式</summary>
    private Canvas parentCanvas;

    // ======================== 生命周期 ========================

    void Awake()
    {
        // 单例注册（场景内单例，不跨场景保留）
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // 缓存父级 Canvas
        parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas != null)
            canvasRect = parentCanvas.transform as RectTransform;

        // 启动时强制隐藏两个面板
        HideAll();
    }

    void OnDestroy()
    {
        // 场景卸载时清理单例引用，避免野指针
        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        // 只要有任意面板处于激活状态，就让它跟随鼠标
        if (textPanel != null && textPanel.gameObject.activeSelf)
            FollowMouse(textPanel);

        if (imagePanel != null && imagePanel.gameObject.activeSelf)
            FollowMouse(imagePanel);
    }

    // ======================== 公开 API ========================

    /// <summary>
    /// 显示文字提示框。激活文字面板、赋值内容，同时隐藏图片面板。
    /// </summary>
    public void ShowText(string content)
    {
        if (textPanel == null || textComponent == null) return;

        textComponent.text = content;
        textPanel.gameObject.SetActive(true);

        if (imagePanel != null)
            imagePanel.gameObject.SetActive(false);
    }

    /// <summary>
    /// 显示图片提示框。激活图片面板、赋值 Sprite，同时隐藏文字面板。
    /// </summary>
    public void ShowImage(Sprite image)
    {
        if (imagePanel == null || imageComponent == null) return;

        imageComponent.sprite = image;
        imageComponent.SetNativeSize();
        imagePanel.gameObject.SetActive(true);

        if (textPanel != null)
            textPanel.gameObject.SetActive(false);
    }

    /// <summary>
    /// 同时隐藏文字面板和图片面板。
    /// </summary>
    public void HideAll()
    {
        if (textPanel != null)
            textPanel.gameObject.SetActive(false);

        if (imagePanel != null)
            imagePanel.gameObject.SetActive(false);
    }

    // ======================== 内部方法 ========================

    /// <summary>
    /// 让指定面板跟随鼠标位置。
    /// 根据鼠标在屏幕上的位置动态调整面板 Pivot，使面板始终朝屏幕内部展开：
    ///   鼠标在左上区域 → Pivot(0,1)，面板向右下展开
    ///   鼠标在右上区域 → Pivot(1,1)，面板向左下展开
    ///   鼠标在左下区域 → Pivot(0,0)，面板向右上展开
    ///   鼠标在右下区域 → Pivot(1,0)，面板向左上展开
    /// </summary>
    private void FollowMouse(RectTransform panel)
    {
        if (canvasRect == null) return;

        Camera cam = (parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            ? null
            : parentCanvas.worldCamera;

        if (Mouse.current == null) return;
        Vector2 mouseScreen = Mouse.current.position.ReadValue();

        // 判断鼠标在屏幕的哪个区域，用面板宽高作为安全边距
        Vector2 panelSize = panel.rect.size;
        float screenW = Screen.width;
        float screenH = Screen.height;

        // 鼠标右侧空间是否放得下面板（考虑偏移）
        bool fitsRight = (mouseScreen.x + Mathf.Abs(offset.x) + panelSize.x * (screenW / canvasRect.rect.size.x)) < screenW;
        // 鼠标上方空间是否放得下面板
        bool fitsAbove = (mouseScreen.y + Mathf.Abs(offset.y) + panelSize.y * (screenH / canvasRect.rect.size.y)) < screenH;

        // 动态设置 Pivot：0 表示面板从该方向展开，1 表示面板向反方向展开
        float pivotX = fitsRight ? 0f : 1f;
        float pivotY = fitsAbove ? 0f : 1f;
        panel.pivot = new Vector2(pivotX, pivotY);

        // 计算偏移方向：根据 Pivot 翻转偏移的正负号
        float ox = fitsRight ? Mathf.Abs(offset.x) : -Mathf.Abs(offset.x);
        float oy = fitsAbove ? Mathf.Abs(offset.y) : -Mathf.Abs(offset.y);

        // 将鼠标屏幕坐标转换为 panel 父物体的本地坐标（保证 localPosition 赋值正确）
        RectTransform parentRect = panel.parent as RectTransform;
        if (parentRect == null) parentRect = canvasRect;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect, mouseScreen, cam, out Vector2 localPoint);

        localPoint += new Vector2(ox, oy);
        panel.localPosition = localPoint;
    }
}
