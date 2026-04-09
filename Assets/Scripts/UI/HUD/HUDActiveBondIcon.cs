using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 显示在游戏 HUD 中的单独一个羁绊图标。支持鼠标悬停显示说明文案。
/// </summary>
public class HUDActiveBondIcon : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public bool debugLog = false;

    [Header("UI Elements")]
    public Image iconImage;
    [Tooltip("悬浮时显示的提示底板（包含文字）")]
    public GameObject tooltipPanel;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI descriptionText;

    private BondActivationStateSO.BondEntry currentBond;

    public void Setup(BondActivationStateSO.BondEntry bond)
    {
        if (bond == null) return;
        currentBond = bond;

        if (iconImage != null)
        {
            iconImage.sprite = bond.icon;
            iconImage.enabled = bond.icon != null;
        }

        if (nameText != null) nameText.text = bond.displayName;
        if (descriptionText != null) descriptionText.text = bond.description;

        if (tooltipPanel != null) tooltipPanel.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (tooltipPanel != null)
        {
            if (debugLog) Debug.Log($"[HUDActiveBondIcon] 执行了某操作: 鼠标悬停显示 {currentBond?.displayName}");
            tooltipPanel.SetActive(true);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (tooltipPanel != null)
        {
            if (debugLog) Debug.Log($"[HUDActiveBondIcon] 执行了某操作: 鼠标移出隐藏 {currentBond?.displayName}");
            tooltipPanel.SetActive(false);
        }
    }
}
