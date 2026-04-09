using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 图片提示触发器。
/// 挂载到任何需要鼠标悬浮显示配方图/说明图的 UI 元素上即可。
///
/// 【使用方法】
/// 1. 选中目标 UI 元素（Button / Image / 任意带 Raycast Target 的控件）。
/// 2. Add Component → ImageTooltipTrigger。
/// 3. 在 Inspector 的 Image To Show 字段中拖入要显示的 Sprite。
/// </summary>
public class ImageTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("鼠标悬浮时显示的图片（Sprite）")]
    public Sprite imageToShow;

    /// <summary>鼠标进入时，如果图片不为空，通知管理器显示图片提示框</summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (TooltipManager.Instance != null && imageToShow != null)
            TooltipManager.Instance.ShowImage(imageToShow);
    }

    /// <summary>鼠标离开时，通知管理器隐藏所有提示框</summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        if (TooltipManager.Instance != null)
            TooltipManager.Instance.HideAll();
    }
}
