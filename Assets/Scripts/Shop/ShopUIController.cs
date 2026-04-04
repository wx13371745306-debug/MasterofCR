using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopUIController : MonoBehaviour
{
    [Header("Data")]
    public ShopItemCatalog catalog;

    [Header("UI")]
    public GameObject shopPanelRoot;
    public Transform productCardContainer;
    public GameObject productCardPrefab;
    public Transform cartLineContainer;
    public GameObject cartLinePrefab;
    public TextMeshProUGUI totalPriceText;
    public TextMeshProUGUI balanceAfterText;
    public Button orderButton;
    public TextMeshProUGUI orderErrorText;

    [Header("Delivery")]
    public ShopDeliverySpawner deliverySpawner;

    [Header("Cursor")]
    public bool unlockCursorWhenOpen = true;

    [Header("Debug")]
    [Tooltip("勾选后在 Console 输出商品卡/购物车行调试日志；取消勾选则不输出")]
    [SerializeField] private bool enableShopDebugLogs;

    private readonly Dictionary<string, int> cart = new Dictionary<string, int>(StringComparer.Ordinal);

    private CursorLockMode savedLockMode;
    private bool savedCursorVisible;

    public bool IsOpen => shopPanelRoot != null && shopPanelRoot.activeSelf;

    void OnEnable()
    {
        MoneyManager.OnMoneyChanged += OnMoneyChanged;
    }

    void OnDisable()
    {
        MoneyManager.OnMoneyChanged -= OnMoneyChanged;
    }

    void OnMoneyChanged(int _)
    {
        RefreshTotals();
    }

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    public void Open()
    {
        if (shopPanelRoot == null) return;

        if (unlockCursorWhenOpen)
        {
            savedLockMode = Cursor.lockState;
            savedCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        shopPanelRoot.SetActive(true);
        RebuildProductGrid();
        RefreshCartUI();
        RefreshTotals();
        ClearError();
    }

    public void Close()
    {
        if (shopPanelRoot == null) return;

        shopPanelRoot.SetActive(false);

        if (unlockCursorWhenOpen)
        {
            Cursor.lockState = savedLockMode;
            Cursor.visible = savedCursorVisible;
        }
    }

    void RebuildProductGrid()
    {
        if (productCardContainer == null || productCardPrefab == null || catalog == null) return;

        for (int i = productCardContainer.childCount - 1; i >= 0; i--)
            Destroy(productCardContainer.GetChild(i).gameObject);

        foreach (var entry in catalog.items)
        {
            if (entry == null || !entry.unlocked) continue;
            if (string.IsNullOrWhiteSpace(entry.itemId)) continue;

            GameObject go = Instantiate(productCardPrefab, productCardContainer);
            go.transform.localScale = Vector3.one;
            var card = go.GetComponent<ShopProductCardView>();
            if (card != null)
                card.Setup(entry, enableShopDebugLogs);
        }
    }

    public void AdjustQuantity(string id, int delta)
    {
        if (string.IsNullOrWhiteSpace(id)) return;

        if (catalog == null || catalog.GetEntry(id) == null)
        {
            ShowError("商品未在商店目录中");
            return;
        }

        if (!cart.TryGetValue(id, out int qty))
            qty = 0;
        qty += delta;
        if (qty <= 0)
            cart.Remove(id);
        else
            cart[id] = qty;

        RefreshCartUI();
        RefreshTotals();
        ClearError();
    }

    void RefreshCartUI()
    {
        if (cartLineContainer == null || cartLinePrefab == null) return;

        for (int i = cartLineContainer.childCount - 1; i >= 0; i--)
            Destroy(cartLineContainer.GetChild(i).gameObject);

        var keys = new List<string>(cart.Keys);
        keys.Sort(StringComparer.Ordinal);

        foreach (var id in keys)
        {
            ShopItemCatalog.ShopItemEntry entry = catalog != null ? catalog.GetEntry(id) : null;
            if (entry == null) continue;

            GameObject go = Instantiate(cartLinePrefab, cartLineContainer);
            go.transform.localScale = Vector3.one;
            var line = go.GetComponent<ShopCartLineView>();
            if (line != null)
                line.Setup(id, entry.displayName, entry.unitPrice, cart[id], enableShopDebugLogs);
        }

        RefreshTotals();
    }

    int ComputeTotal()
    {
        int total = 0;
        foreach (var kv in cart)
        {
            ShopItemCatalog.ShopItemEntry e = catalog != null ? catalog.GetEntry(kv.Key) : null;
            if (e == null) continue;
            total += e.unitPrice * kv.Value;
        }
        return total;
    }

    void RefreshTotals()
    {
        int total = ComputeTotal();
        if (totalPriceText != null)
            totalPriceText.text = ShopPriceFormat.Format(total);

        int balance = MoneyManager.Instance != null ? MoneyManager.Instance.CurrentMoney : 0;
        if (balanceAfterText != null)
            balanceAfterText.text = ShopPriceFormat.Format(balance - total);

        if (orderButton != null)
            orderButton.interactable = cart.Count > 0 && total > 0;
    }

    public void OnOrderClicked()
    {
        int total = ComputeTotal();
        if (total <= 0 || cart.Count == 0)
        {
            ShowError("购物车为空");
            return;
        }

        if (MoneyManager.Instance == null)
        {
            ShowError("MoneyManager 未找到");
            return;
        }

        if (deliverySpawner == null)
        {
            ShowError("交货点未配置");
            return;
        }

        if (!MoneyManager.Instance.TrySpendMoney(total))
        {
            ShowError("余额不足");
            return;
        }

        deliverySpawner.SpawnPurchases(cart, catalog);

        cart.Clear();
        Close();
    }

    void ShowError(string msg)
    {
        if (orderErrorText != null)
            orderErrorText.text = msg;
        Debug.LogWarning("[ShopUI] " + msg);
    }

    void ClearError()
    {
        if (orderErrorText != null)
            orderErrorText.text = string.Empty;
    }
}
