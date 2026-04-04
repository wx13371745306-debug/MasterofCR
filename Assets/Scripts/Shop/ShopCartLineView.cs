using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopCartLineView : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI quantityText;
    [SerializeField] private TextMeshProUGUI linePriceText;
    [SerializeField] private Button minusButton;
    [SerializeField] private Button plusButton;

    [Header("逻辑（可选：不填则自动从父级查找 ShopUIController）")]
    [SerializeField] private ShopUIController shopUI;

    [Tooltip("行背景 Image 若铺满且开启 Raycast，可能挡住子按钮；开启则在本行 Awake 时关闭根 Image 的 Raycast Target")]
    [SerializeField] private bool disableRootBackgroundRaycast = true;

    private string itemId;
    private int unitPrice;
    private bool debugLogsEnabled;

    void Awake()
    {
        ResolveShopUI();

        if (disableRootBackgroundRaycast)
        {
            var bg = GetComponent<Image>();
            if (bg != null)
                bg.raycastTarget = false;
        }

        DisableRaycastOnChildTexts(minusButton != null ? minusButton.transform : null);
        DisableRaycastOnChildTexts(plusButton != null ? plusButton.transform : null);

        ShopUIButtonUtil.FixForScrollView(minusButton);
        ShopUIButtonUtil.FixForScrollView(plusButton);
    }

    void OnEnable()
    {
        if (minusButton != null)
            minusButton.onClick.AddListener(OnMinusButtonClicked);
        if (plusButton != null)
            plusButton.onClick.AddListener(OnPlusButtonClicked);
    }

    void OnDisable()
    {
        if (minusButton != null)
            minusButton.onClick.RemoveListener(OnMinusButtonClicked);
        if (plusButton != null)
            plusButton.onClick.RemoveListener(OnPlusButtonClicked);
    }

    void ResolveShopUI()
    {
        if (shopUI == null)
            shopUI = GetComponentInParent<ShopUIController>();
    }

    static void DisableRaycastOnChildTexts(Transform buttonRoot)
    {
        if (buttonRoot == null) return;
        foreach (var tmp in buttonRoot.GetComponentsInChildren<TMP_Text>(true))
            tmp.raycastTarget = false;
    }

    public void OnMinusButtonClicked()
    {
        ResolveShopUI();
        if (debugLogsEnabled)
            Debug.Log($"[ShopCartLine] OnMinus itemId={itemId ?? "(empty)"} shopUI={(shopUI != null)}");

        if (shopUI == null || string.IsNullOrWhiteSpace(itemId))
        {
            if (debugLogsEnabled)
                Debug.LogWarning("[ShopCartLine] OnMinus 跳过：未找到 ShopUIController 或 itemId 为空。");
            return;
        }
        shopUI.AdjustQuantity(itemId, -1);
    }

    public void OnPlusButtonClicked()
    {
        ResolveShopUI();
        if (debugLogsEnabled)
            Debug.Log($"[ShopCartLine] OnPlus itemId={itemId ?? "(empty)"} shopUI={(shopUI != null)}");

        if (shopUI == null || string.IsNullOrWhiteSpace(itemId))
        {
            if (debugLogsEnabled)
                Debug.LogWarning("[ShopCartLine] OnPlus 跳过：未找到 ShopUIController 或 itemId 为空。");
            return;
        }
        shopUI.AdjustQuantity(itemId, 1);
    }

    public void Setup(string id, string displayName, int unitPrice, int quantity, bool debugLogs = false)
    {
        debugLogsEnabled = debugLogs;
        ResolveShopUI();
        itemId = id;
        this.unitPrice = unitPrice;

        if (nameText != null)
            nameText.text = displayName;

        RefreshQuantityDisplay(quantity);

        if (debugLogsEnabled)
        {
            Debug.Log($"[ShopCartLine] Setup itemId={itemId} qty={quantity} ±已绑定 minus={(minusButton != null)} plus={(plusButton != null)}");
        }
    }

    public void RefreshQuantityDisplay(int quantity)
    {
        if (quantityText != null)
            quantityText.text = quantity.ToString();
        if (linePriceText != null)
            linePriceText.text = ShopPriceFormat.FormatLineTotal(unitPrice, quantity);
    }
}
