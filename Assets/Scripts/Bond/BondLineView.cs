using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 羁绊列表中的单行 UI：可显示图标、名称、说明三项内容。
/// 三个字段均为可选——在预制体中绑定了哪个就显示哪个，未绑定的自动隐藏。
/// 鼠标悬浮时，通过全局 TooltipManager 显示对应羁绊的名称与说明。
/// 挂在 BondLine 预制体上，由 BondListUIController 生成并赋值。
/// </summary>
public class BondLineView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI descriptionText;

    /// <summary>缓存 Setup 时从 BondEntry 拿到的提示文字，悬浮时显示</summary>
    private string tooltipContent;

    /// <summary>
    /// 初始化此行。三个参数与三个 UI 元素一一对应，
    /// 只有字段已绑定且数据有效时才显示，否则隐藏对应的 GameObject。
    /// 同时缓存 displayName + description 作为悬浮提示内容。
    /// </summary>
    public void Setup(Sprite icon, string displayName, string description)
    {
        SetupIcon(icon);
        SetupText(nameText, displayName);
        SetupText(descriptionText, description);

        bool hasName = !string.IsNullOrEmpty(displayName);
        bool hasDesc = !string.IsNullOrEmpty(description);

        if (hasName && hasDesc)
            tooltipContent = displayName + "\n" + description;
        else if (hasName)
            tooltipContent = displayName;
        else if (hasDesc)
            tooltipContent = description;
        else
            tooltipContent = string.Empty;

        Debug.Log($"[BondLineView] Setup 完成 | 物体='{name}' | tooltipContent='{tooltipContent}'");
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        Debug.Log($"[BondLineView] OnPointerEnter 触发 | 物体='{name}' | tooltipContent='{tooltipContent}' | TooltipManager.Instance={(TooltipManager.Instance != null ? "存在" : "NULL")}");

        if (TooltipManager.Instance == null)
        {
            Debug.LogWarning("[BondLineView] TooltipManager.Instance 为 null！请确认场景中存在挂载了 TooltipManager 脚本的物体。");
            return;
        }
        if (string.IsNullOrEmpty(tooltipContent))
        {
            Debug.LogWarning("[BondLineView] tooltipContent 为空，不显示提示框。请检查 BondEntry 的 displayName / description 是否填写。");
            return;
        }
        TooltipManager.Instance.ShowText(tooltipContent);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Debug.Log($"[BondLineView] OnPointerExit 触发 | 物体='{name}'");

        if (TooltipManager.Instance != null)
            TooltipManager.Instance.HideAll();
    }

    private void SetupIcon(Sprite icon)
    {
        if (iconImage == null) return;

        bool show = icon != null;
        iconImage.gameObject.SetActive(show);
        if (show)
            iconImage.sprite = icon;
    }

    private void SetupText(TextMeshProUGUI tmp, string content)
    {
        if (tmp == null) return;

        bool show = !string.IsNullOrEmpty(content);
        tmp.gameObject.SetActive(show);
        if (show)
            tmp.text = content;
    }
}
