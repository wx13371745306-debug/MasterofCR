using UnityEngine;
using UnityEngine.UI;
using TMPro; // 我们默认使用 TextMeshPro 来显示清晰的字体

public class OrderCardUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("显示菜品图片的 Image 组件")]
    public Image dishIcon;
    
    [Tooltip("显示是几号桌点的 Text 组件")]
    public TextMeshProUGUI tableNumberText;

    // 隐藏在面板外，用来记录自己的唯一条形码
    [HideInInspector]
    public string orderId; 

    /// <summary>
    /// 初始化卡片显示
    /// </summary>
    public void Setup(OrderInstance order)
    {
        // 记住自己的 ID
        orderId = order.orderId;

        // 替换菜品图标
        if (dishIcon != null && order.recipe != null && order.recipe.dishIcon != null)
        {
            dishIcon.sprite = order.recipe.dishIcon;
        }

        // 显示桌号
        if (tableNumberText != null)
        {
            tableNumberText.text = order.tableId.ToString();
        }
    }
}