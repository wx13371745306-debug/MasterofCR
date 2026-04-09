#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 一次性生成商店目录资源与 UI 预制体。菜单：Tools / Shop / Setup Shop Assets
/// </summary>
public static class ShopUISetup
{
    const string ShopFolder = "Assets/Shop";
    const string CatalogPath = ShopFolder + "/ShopItemCatalog.asset";
    const string ProductCardPrefabPath = ShopFolder + "/ShopProductCard.prefab";
    const string CartLinePrefabPath = ShopFolder + "/ShopCartLine.prefab";
    const string ShopCanvasPrefabPath = ShopFolder + "/ShopCanvas.prefab";

    [MenuItem("Tools/Shop/Setup Shop Assets")]
    static void SetupAll()
    {
        EnsureFolder(ShopFolder);
        CreateCatalogIfNeeded();
        CreateProductCardPrefab();
        CreateCartLinePrefab();
        CreateShopCanvasPrefab();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[ShopUISetup] 完成。请将 ShopCanvas 预制体拖入场景，执行 Tools/Shop/Add Shop Delivery To Scene 添加交货点，并在 ShopCanvas 上绑定 ShopDeliverySpawner。");
    }

    [MenuItem("Tools/Shop/Add Shop Delivery To Scene")]
    static void AddDeliveryToScene()
    {
        var root = new GameObject("ShopDelivery");
        var anchor = new GameObject("DeliveryAnchor");
        anchor.transform.SetParent(root.transform);
        anchor.transform.localPosition = Vector3.zero;

        var spawner = root.AddComponent<ShopDeliverySpawner>();
        var so = new SerializedObject(spawner);
        so.FindProperty("anchor").objectReferenceValue = anchor.transform;
        so.FindProperty("cellSize").vector3Value = new Vector3(0.35f, 0.35f, 0.35f);
        so.FindProperty("batchOffset").vector3Value = new Vector3(1.05f, 0f, 0f);
        so.ApplyModifiedPropertiesWithoutUndo();

        Undo.RegisterCreatedObjectUndo(root, "Add Shop Delivery");
        Selection.activeGameObject = root;
        Debug.Log("[ShopUISetup] 已创建 ShopDelivery。请将 ShopUIController.deliverySpawner 指向该物体上的 ShopDeliverySpawner。");
    }

    static void EnsureFolder(string assetPath)
    {
        if (AssetDatabase.IsValidFolder(assetPath)) return;
        var parent = Path.GetDirectoryName(assetPath)?.Replace("\\", "/");
        var name = Path.GetFileName(assetPath);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    static void CreateCatalogIfNeeded()
    {
        if (File.Exists(CatalogPath.Replace("Assets/", Application.dataPath + "/")))
        {
            Debug.Log("[ShopUISetup] ShopItemCatalog 已存在，跳过。");
            return;
        }

        var catalog = ScriptableObject.CreateInstance<ShopItemCatalog>();
        var tomato = new ShopItemCatalog.ShopItemEntry
        {
            itemId = "TomatoChunk",
            displayName = "番茄块",
            icon = null,
            unitPrice = 5,
            worldPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Ingredient/TomatoChunk/TomatoChunk.prefab"),
            unlocked = true
        };
        var eggs = new ShopItemCatalog.ShopItemEntry
        {
            itemId = "Eggs",
            displayName = "鸡蛋",
            icon = null,
            unitPrice = 3,
            worldPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Ingredient/Eggs/Eggs.prefab"),
            unlocked = true
        };
        catalog.items = new System.Collections.Generic.List<ShopItemCatalog.ShopItemEntry> { tomato, eggs };

        AssetDatabase.CreateAsset(catalog, CatalogPath);
    }

    static void CreateProductCardPrefab()
    {
        var root = new GameObject("ShopProductCard", typeof(RectTransform));
        var rt = root.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(220, 140);

        var bg = root.AddComponent<Image>();
        bg.color = new Color(0.15f, 0.15f, 0.18f, 0.95f);

        var nameGo = CreateTMP("Name", root.transform, "商品名", 16, TextAlignmentOptions.TopLeft);
        var nameRt = nameGo.GetComponent<RectTransform>();
        nameRt.anchorMin = new Vector2(0, 0.45f);
        nameRt.anchorMax = new Vector2(1, 1);
        nameRt.offsetMin = new Vector2(8, 0);
        nameRt.offsetMax = new Vector2(-8, -8);

        var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGo.transform.SetParent(root.transform, false);
        var iconRt = iconGo.GetComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0, 0.1f);
        iconRt.anchorMax = new Vector2(0.45f, 0.45f);
        iconRt.offsetMin = new Vector2(8, 4);
        iconRt.offsetMax = new Vector2(-4, -4);

        var priceGo = CreateTMP("Price", root.transform, "0", 18, TextAlignmentOptions.MidlineRight);
        var priceRt = priceGo.GetComponent<RectTransform>();
        priceRt.anchorMin = new Vector2(0.45f, 0.1f);
        priceRt.anchorMax = new Vector2(1, 0.45f);
        priceRt.offsetMin = new Vector2(4, 4);
        priceRt.offsetMax = new Vector2(-8, -4);

        var btnGo = new GameObject("AddButton", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGo.transform.SetParent(root.transform, false);
        var btnRt = btnGo.GetComponent<RectTransform>();
        btnRt.anchorMin = new Vector2(0.1f, 0);
        btnRt.anchorMax = new Vector2(0.9f, 0.28f);
        btnRt.offsetMin = new Vector2(8, 6);
        btnRt.offsetMax = new Vector2(-8, 4);
        btnGo.GetComponent<Image>().color = new Color(0.2f, 0.55f, 0.35f, 1f);
        var btnText = CreateTMP("Text", btnGo.transform, "加入购物车", 14, TextAlignmentOptions.Midline);
        var btnTextRt = btnText.GetComponent<RectTransform>();
        btnTextRt.anchorMin = Vector2.zero;
        btnTextRt.anchorMax = Vector2.one;
        btnTextRt.offsetMin = Vector2.zero;
        btnTextRt.offsetMax = Vector2.zero;

        var card = root.AddComponent<ShopProductCardView>();
        var so = new SerializedObject(card);
        so.FindProperty("nameText").objectReferenceValue = nameGo.GetComponent<TextMeshProUGUI>();
        so.FindProperty("iconImage").objectReferenceValue = iconGo.GetComponent<Image>();
        so.FindProperty("priceText").objectReferenceValue = priceGo.GetComponent<TextMeshProUGUI>();
        so.FindProperty("addToCartButton").objectReferenceValue = btnGo.GetComponent<Button>();
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, ProductCardPrefabPath);
        Object.DestroyImmediate(root);
    }

    static void CreateCartLinePrefab()
    {
        var root = new GameObject("ShopCartLine", typeof(RectTransform));
        var rt = root.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(400, 36);

        var bg = root.AddComponent<Image>();
        bg.color = new Color(0.12f, 0.12f, 0.14f, 0.9f);

        var nameGo = CreateTMP("Name", root.transform, "名称", 15, TextAlignmentOptions.MidlineLeft);
        var nameRt = nameGo.GetComponent<RectTransform>();
        nameRt.anchorMin = new Vector2(0, 0);
        nameRt.anchorMax = new Vector2(0.35f, 1);
        nameRt.offsetMin = new Vector2(8, 2);
        nameRt.offsetMax = new Vector2(-4, -2);

        var minusGo = CreateButton("Minus", root.transform, "-");
        var minusRt = minusGo.GetComponent<RectTransform>();
        minusRt.anchorMin = new Vector2(0.35f, 0.15f);
        minusRt.anchorMax = new Vector2(0.42f, 0.85f);
        minusRt.offsetMin = Vector2.zero;
        minusRt.offsetMax = Vector2.zero;

        var qtyGo = CreateTMP("Qty", root.transform, "1", 16, TextAlignmentOptions.Midline);
        var qtyRt = qtyGo.GetComponent<RectTransform>();
        qtyRt.anchorMin = new Vector2(0.42f, 0);
        qtyRt.anchorMax = new Vector2(0.5f, 1);
        qtyRt.offsetMin = Vector2.zero;
        qtyRt.offsetMax = Vector2.zero;

        var plusGo = CreateButton("Plus", root.transform, "+");
        var plusRt = plusGo.GetComponent<RectTransform>();
        plusRt.anchorMin = new Vector2(0.5f, 0.15f);
        plusRt.anchorMax = new Vector2(0.57f, 0.85f);
        plusRt.offsetMin = Vector2.zero;
        plusRt.offsetMax = Vector2.zero;

        var priceGo = CreateTMP("LinePrice", root.transform, "0", 15, TextAlignmentOptions.MidlineRight);
        var priceRt = priceGo.GetComponent<RectTransform>();
        priceRt.anchorMin = new Vector2(0.57f, 0);
        priceRt.anchorMax = new Vector2(1, 1);
        priceRt.offsetMin = new Vector2(4, 2);
        priceRt.offsetMax = new Vector2(-8, -2);

        var line = root.AddComponent<ShopCartLineView>();
        var so = new SerializedObject(line);
        so.FindProperty("nameText").objectReferenceValue = nameGo.GetComponent<TextMeshProUGUI>();
        so.FindProperty("quantityText").objectReferenceValue = qtyGo.GetComponent<TextMeshProUGUI>();
        so.FindProperty("linePriceText").objectReferenceValue = priceGo.GetComponent<TextMeshProUGUI>();
        so.FindProperty("minusButton").objectReferenceValue = minusGo.GetComponent<Button>();
        so.FindProperty("plusButton").objectReferenceValue = plusGo.GetComponent<Button>();
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, CartLinePrefabPath);
        Object.DestroyImmediate(root);
    }

    static GameObject CreateButton(string name, Transform parent, string label)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = new Color(0.25f, 0.25f, 0.3f, 1f);
        var t = CreateTMP("Text", go.transform, label, 18, TextAlignmentOptions.Midline);
        var tr = t.GetComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = Vector2.zero;
        tr.offsetMax = Vector2.zero;
        return go;
    }

    static GameObject CreateTMP(string name, Transform parent, string text, float size, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.color = Color.white;
        return go;
    }

    static void CreateShopCanvasPrefab()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<ShopItemCatalog>(CatalogPath);
        var productCard = AssetDatabase.LoadAssetAtPath<GameObject>(ProductCardPrefabPath);
        var cartLine = AssetDatabase.LoadAssetAtPath<GameObject>(CartLinePrefabPath);

        var canvasGo = new GameObject("ShopCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        var panel = new GameObject("ShopPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasGo.transform, false);
        var panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = Vector2.zero;
        panelRt.anchorMax = Vector2.one;
        panelRt.offsetMin = new Vector2(80, 60);
        panelRt.offsetMax = new Vector2(-80, -60);
        panel.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.1f, 0.92f);

        var leftScroll = CreateScroll("ProductScroll", panel.transform);
        var leftRt = leftScroll.GetComponent<RectTransform>();
        leftRt.anchorMin = new Vector2(0, 0.12f);
        leftRt.anchorMax = new Vector2(0.48f, 0.95f);
        leftRt.offsetMin = new Vector2(16, 8);
        leftRt.offsetMax = new Vector2(-8, -8);
        var leftContent = leftScroll.transform.Find("Viewport/Content");
        var grid = leftContent.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(230, 150);
        grid.spacing = new Vector2(10, 10);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;
        grid.childAlignment = TextAnchor.UpperCenter;

        var rightScroll = CreateScroll("CartScroll", panel.transform);
        var rightRt = rightScroll.GetComponent<RectTransform>();
        rightRt.anchorMin = new Vector2(0.52f, 0.12f);
        rightRt.anchorMax = new Vector2(1, 0.95f);
        rightRt.offsetMin = new Vector2(8, 8);
        rightRt.offsetMax = new Vector2(-16, -8);
        var rightContent = rightScroll.transform.Find("Viewport/Content");
        var vlg = rightContent.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.childControlHeight = false;
        vlg.childControlWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.spacing = 4;
        vlg.padding = new RectOffset(4, 4, 4, 4);

        var bottom = new GameObject("CheckoutBar", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        bottom.transform.SetParent(panel.transform, false);
        var bottomRt = bottom.GetComponent<RectTransform>();
        bottomRt.anchorMin = new Vector2(0, 0);
        bottomRt.anchorMax = new Vector2(1, 0.12f);
        bottomRt.offsetMin = new Vector2(16, 12);
        bottomRt.offsetMax = new Vector2(-16, -8);
        var h = bottom.GetComponent<HorizontalLayoutGroup>();
        h.childAlignment = TextAnchor.MiddleLeft;
        h.spacing = 24;
        h.padding = new RectOffset(8, 8, 4, 4);
        h.childControlWidth = false;
        h.childControlHeight = true;

        var totalLabel = CreateTMP("TotalLabel", bottom.transform, "总价：", 20, TextAlignmentOptions.MidlineLeft);
        totalLabel.GetComponent<RectTransform>().sizeDelta = new Vector2(80, 40);
        var totalVal = CreateTMP("TotalValue", bottom.transform, "0", 22, TextAlignmentOptions.MidlineLeft);
        totalVal.GetComponent<RectTransform>().sizeDelta = new Vector2(120, 40);

        var balLabel = CreateTMP("BalLabel", bottom.transform, "购买后余额：", 20, TextAlignmentOptions.MidlineLeft);
        balLabel.GetComponent<RectTransform>().sizeDelta = new Vector2(160, 40);
        var balVal = CreateTMP("BalValue", bottom.transform, "0", 22, TextAlignmentOptions.MidlineLeft);
        balVal.GetComponent<RectTransform>().sizeDelta = new Vector2(120, 40);

        var err = CreateTMP("OrderError", bottom.transform, "", 16, TextAlignmentOptions.MidlineLeft);
        err.GetComponent<TextMeshProUGUI>().color = new Color(1f, 0.4f, 0.35f);
        err.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 40);

        var orderBtnGo = CreateButton("OrderButton", bottom.transform, "下单");
        orderBtnGo.GetComponent<RectTransform>().sizeDelta = new Vector2(140, 44);
        orderBtnGo.GetComponent<Image>().color = new Color(0.25f, 0.45f, 0.75f, 1f);

        var shopUI = panel.AddComponent<ShopUIController>();
        var shopSo = new SerializedObject(shopUI);
        shopSo.FindProperty("catalog").objectReferenceValue = catalog;
        shopSo.FindProperty("shopPanelRoot").objectReferenceValue = panel;
        shopSo.FindProperty("productCardContainer").objectReferenceValue = leftContent;
        shopSo.FindProperty("productCardPrefab").objectReferenceValue = productCard;
        shopSo.FindProperty("cartLineContainer").objectReferenceValue = rightContent;
        shopSo.FindProperty("cartLinePrefab").objectReferenceValue = cartLine;
        shopSo.FindProperty("totalPriceText").objectReferenceValue = totalVal.GetComponent<TextMeshProUGUI>();
        shopSo.FindProperty("balanceAfterText").objectReferenceValue = balVal.GetComponent<TextMeshProUGUI>();
        shopSo.FindProperty("orderButton").objectReferenceValue = orderBtnGo.GetComponent<Button>();
        shopSo.FindProperty("orderErrorText").objectReferenceValue = err.GetComponent<TextMeshProUGUI>();
        shopSo.FindProperty("deliverySpawner").objectReferenceValue = null;
        shopSo.FindProperty("unlockCursorWhenOpen").boolValue = true;
        shopSo.ApplyModifiedPropertiesWithoutUndo();

        var input = canvasGo.AddComponent<ShopInputToggle>();
        var inputSo = new SerializedObject(input);
        inputSo.FindProperty("shopUI").objectReferenceValue = shopUI;
        inputSo.ApplyModifiedPropertiesWithoutUndo();

        panel.SetActive(false);

        PrefabUtility.SaveAsPrefabAsset(canvasGo, ShopCanvasPrefabPath);
        Object.DestroyImmediate(canvasGo);
    }

    static GameObject CreateScroll(string name, Transform parent)
    {
        var scrollGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        scrollGo.transform.SetParent(parent, false);
        scrollGo.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.12f, 1f);

        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(scrollGo.transform, false);
        var vpRt = viewport.GetComponent<RectTransform>();
        vpRt.anchorMin = Vector2.zero;
        vpRt.anchorMax = Vector2.one;
        vpRt.offsetMin = new Vector2(4, 4);
        vpRt.offsetMax = new Vector2(-4, -4);
        viewport.GetComponent<Image>().color = new Color(0, 0, 0, 0.01f);
        viewport.GetComponent<Mask>().showMaskGraphic = false;

        var content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(viewport.transform, false);
        var cRt = content.GetComponent<RectTransform>();
        cRt.anchorMin = new Vector2(0, 1);
        cRt.anchorMax = new Vector2(1, 1);
        cRt.pivot = new Vector2(0.5f, 1);
        cRt.anchoredPosition = Vector2.zero;
        cRt.sizeDelta = new Vector2(0, 300);

        var sr = scrollGo.GetComponent<ScrollRect>();
        sr.viewport = vpRt;
        sr.content = cRt;
        sr.horizontal = false;
        sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Clamped;

        return scrollGo;
    }
}
#endif
