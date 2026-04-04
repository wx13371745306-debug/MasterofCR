using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopProductCardView : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI priceText;
    [SerializeField] private Button addToCartButton;

    [Header("逻辑（可选：不填则自动从父级查找 ShopUIController）")]
    [SerializeField] private ShopUIController shopUI;

    private string itemId;
    private bool debugLogsEnabled;

    void Awake()
    {
        if (shopUI == null)
            shopUI = GetComponentInParent<ShopUIController>();
        if (addToCartButton != null)
        {
            DisableRaycastOnChildTexts(addToCartButton.transform);
            ShopUIButtonUtil.FixForScrollView(addToCartButton);
        }
    }

    static void DisableRaycastOnChildTexts(Transform buttonRoot)
    {
        if (buttonRoot == null) return;
        foreach (var tmp in buttonRoot.GetComponentsInChildren<TMP_Text>(true))
            tmp.raycastTarget = false;
    }

    /// <summary>仅在 Inspector 里把「加入购物车」Button 的 On Click 绑到此方法；代码不再 AddListener，避免触发两次。</summary>
    public void OnAddToCartButtonClicked()
    {
        if (shopUI == null)
            shopUI = GetComponentInParent<ShopUIController>();
        if (debugLogsEnabled)
            Debug.Log($"[ShopProductCard] OnAddToCart itemId={itemId ?? "(empty)"} shopUI={(shopUI != null)}");

        if (shopUI == null || string.IsNullOrWhiteSpace(itemId)) return;
        shopUI.AdjustQuantity(itemId, 1);
    }

    public void Setup(ShopItemCatalog.ShopItemEntry entry, bool debugLogs = false)
    {
        if (entry == null) return;

        debugLogsEnabled = debugLogs;
        itemId = entry.itemId;

        if (nameText != null)
            nameText.text = entry.displayName;
        if (iconImage != null)
        {
            iconImage.sprite = entry.icon;
            iconImage.enabled = entry.icon != null;
        }
        if (priceText != null)
            priceText.text = ShopPriceFormat.Format(entry.unitPrice);

        if (addToCartButton != null)
            addToCartButton.interactable = entry.unlocked && !string.IsNullOrWhiteSpace(entry.itemId);

        if (debugLogsEnabled)
            Debug.Log($"[ShopProductCard] Setup itemId={itemId}");
    }
}
