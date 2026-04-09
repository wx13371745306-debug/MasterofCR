using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 文字提示触发器。
/// 挂载到任何需要鼠标悬浮显示文字说明的 UI 元素上即可。
///
/// 【使用方法】
/// 1. 选中目标 UI 元素（Button / Image / 任意带 Raycast Target 的控件）。
/// 2. Add Component → TextTooltipTrigger。
/// 3. 在 Inspector 的 Content Text 文本框中填写要显示的提示内容。
/// </summary>
public class TextTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("鼠标悬浮时显示的文字内容")]
    [TextArea(2, 5)]
    public string contentText;

    /// <summary>鼠标进入时，通知管理器显示文字提示框</summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (TooltipManager.Instance != null && !string.IsNullOrEmpty(contentText))
            TooltipManager.Instance.ShowText(contentText);
    }

    /// <summary>鼠标离开时，通知管理器隐藏所有提示框</summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        if (TooltipManager.Instance != null)
            TooltipManager.Instance.HideAll();
    }
}
