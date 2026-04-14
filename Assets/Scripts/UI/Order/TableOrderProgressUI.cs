using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 桌面进度 UI：呼叫点餐/点菜读条/等菜不耐烦/本桌订单卡片/加钱飘字。
/// 由 <see cref="OrderResponse.OnTableProgressUiSync"/> 驱动刷新（SyncVar/SyncList），非整帧轮询。
/// </summary>
public class TableOrderProgressUI : MonoBehaviour
{
    [Header("UI References")]
    public Canvas canvas;
    [Tooltip("等待点菜阶段：呼叫点餐（Image 颜色会随耐心变红）")]
    public GameObject callIconObj;
    [Tooltip("为空则从 callIconObj 上取 Image")]
    public Image callIconImage;
    [Tooltip("点菜读条（Ordering 阶段）")]
    public GameObject progressBarObj;
    public Image fillImage;
    public GameObject waitingFoodImpatientIconObj;
    [Tooltip("为空则从 waitingFoodImpatientIconObj 上取 Image")]
    public Image waitingFoodImpatientImage;

    [Header("Money Popup")]
    [Tooltip("显示获得金钱的整个物体")]
    public GameObject moneyPopupObj;
    [Tooltip("用来显示 +$ 00.00 的文本（TextMeshPro 组件）")]
    public TMPro.TMP_Text moneyPopupText;

    [Header("Patience Color (Normalized)")]
    [Tooltip("耐心百分比低于此值时开始向红色过渡 (0.0 - 1.0)")]
    public float stressStartRatio = 0.5f;
    [Tooltip("耐心百分比低于等于此值时完全变为 stress 色 (0.0 - 1.0)")]
    public float stressEndRatio = 0.1f;
    public Color patienceStressColor = new Color(1f, 0.15f, 0.15f, 1f);

    [Header("Target")]
    public OrderResponse tableResponse;

    [Header("Settings")]
    public bool alwaysFaceCamera = true;

    [Header("本桌订单列表")]
    [Tooltip("挂载订单卡片的容器（建议 Horizontal Layout Group）")]
    public RectTransform orderCardContainer;
    [Tooltip("与全局订单相同的 OrderCard 预制体（挂 OrderCardUI）")]
    public GameObject orderCardPrefab;
    [Tooltip("为 true 时：等上菜阶段只要本桌有未上菜品就显示 Canvas（不必等到不耐烦），便于看订单条")]
    public bool extendCanvasWhenWaitingForFoodWithOrders = true;

    [Header("Debug")]
    public bool debugLog;

    private Camera mainCamera;
    private Color callIconBaseColor = Color.white;
    private Color foodImpatientBaseColor = Color.white;
    private bool cachedBaseColors;

    private float currentPopupMoney;
    private Color originalPopupColor = Color.white;
    private Coroutine moneyPopupCoroutine;

    void OnEnable()
    {
        EnsureTableResponse();
        if (tableResponse != null)
            tableResponse.OnTableProgressUiSync += HandleTableProgressSync;
    }

    void OnDisable()
    {
        if (tableResponse != null)
            tableResponse.OnTableProgressUiSync -= HandleTableProgressSync;
    }

    void Start()
    {
        EnsureTableResponse();
        mainCamera = Camera.main;

        if (canvas != null)
            canvas.enabled = false;

        if (moneyPopupText != null)
        {
            originalPopupColor = moneyPopupText.color;
            if (moneyPopupObj == null)
                moneyPopupText.gameObject.SetActive(false);
        }

        if (moneyPopupObj != null)
            moneyPopupObj.SetActive(false);

        CacheBaseColorsIfNeeded();
        RefreshAll();
    }

    void EnsureTableResponse()
    {
        if (tableResponse == null)
            tableResponse = transform.parent.GetComponentInChildren<OrderResponse>();
    }

    void HandleTableProgressSync()
    {
        RefreshAll();
    }

    /// <summary>外部数据同步后统一刷新（也可供调试调用）。</summary>
    public void RefreshAll()
    {
        if (tableResponse == null)
            return;

        CacheBaseColorsIfNeeded();

        RefreshCanvasActive();

        if (canvas == null || !canvas.enabled)
            return;

        RefreshOrderingAndPatienceWidgets();
        RebuildTableOrderCards();
    }

    void RefreshCanvasActive()
    {
        var state = tableResponse.currentState;
        bool showCall = tableResponse.ShouldShowWaitingToOrderCallIcon;
        bool showFoodImpatient = tableResponse.ShouldShowWaitingFoodImpatientIcon;
        bool popupActive = moneyPopupCoroutine != null;

        int orderCount = tableResponse.GetCurrentOrder() != null ? tableResponse.GetCurrentOrder().Count : 0;
        bool showForPendingOrders = extendCanvasWhenWaitingForFoodWithOrders
            && state == OrderResponse.TableState.WaitingForFood
            && orderCount > 0;

        bool shouldShowCanvas =
            state == OrderResponse.TableState.WaitingToOrder ||
            state == OrderResponse.TableState.Ordering ||
            (state == OrderResponse.TableState.WaitingForFood && showFoodImpatient) ||
            showForPendingOrders ||
            popupActive;

        if (canvas.enabled != shouldShowCanvas)
        {
            canvas.enabled = shouldShowCanvas;
            if (debugLog)
                Debug.Log($"[TableUI] Canvas: {canvas.enabled}, 状态: {state}, 本桌订单数: {orderCount}");
        }
    }

    void RefreshOrderingAndPatienceWidgets()
    {
        var state = tableResponse.currentState;
        bool showCall = tableResponse.ShouldShowWaitingToOrderCallIcon;
        bool showFoodImpatient = tableResponse.ShouldShowWaitingFoodImpatientIcon;

        switch (state)
        {
            case OrderResponse.TableState.WaitingToOrder:
                if (progressBarObj != null && progressBarObj.activeSelf)
                    progressBarObj.SetActive(false);
                if (callIconObj != null && callIconObj.activeSelf != showCall)
                    callIconObj.SetActive(showCall);

                float maxOrder = tableResponse.GetDisplayMaxPatienceOrder();
                float orderRatio = maxOrder > 0f ? (tableResponse.currentPatienceOrder / maxOrder) : 0f;
                ApplyPatienceTintRatio(callIconImage, callIconBaseColor, orderRatio);

                if (waitingFoodImpatientIconObj != null && waitingFoodImpatientIconObj.activeSelf)
                    waitingFoodImpatientIconObj.SetActive(false);
                if (waitingFoodImpatientImage != null)
                    waitingFoodImpatientImage.color = foodImpatientBaseColor;
                break;

            case OrderResponse.TableState.Ordering:
                if (callIconObj != null && callIconObj.activeSelf)
                    callIconObj.SetActive(false);
                if (callIconImage != null)
                    callIconImage.color = callIconBaseColor;
                if (progressBarObj != null && !progressBarObj.activeSelf)
                    progressBarObj.SetActive(true);
                if (fillImage != null)
                    fillImage.fillAmount = tableResponse.GetOrderProgressNormalized();
                if (waitingFoodImpatientIconObj != null && waitingFoodImpatientIconObj.activeSelf)
                    waitingFoodImpatientIconObj.SetActive(false);
                if (waitingFoodImpatientImage != null)
                    waitingFoodImpatientImage.color = foodImpatientBaseColor;
                break;

            case OrderResponse.TableState.WaitingForFood:
                if (callIconObj != null && callIconObj.activeSelf)
                    callIconObj.SetActive(false);
                if (callIconImage != null)
                    callIconImage.color = callIconBaseColor;
                if (progressBarObj != null && progressBarObj.activeSelf)
                    progressBarObj.SetActive(false);
                if (waitingFoodImpatientIconObj != null && waitingFoodImpatientIconObj.activeSelf != showFoodImpatient)
                    waitingFoodImpatientIconObj.SetActive(showFoodImpatient);
                if (showFoodImpatient)
                {
                    float maxFood = tableResponse.GetDisplayMaxPatienceFood();
                    float foodRatio = maxFood > 0f ? (tableResponse.currentPatienceFood / maxFood) : 0f;
                    ApplyPatienceTintRatio(waitingFoodImpatientImage, foodImpatientBaseColor, foodRatio);
                }
                else if (waitingFoodImpatientImage != null)
                    waitingFoodImpatientImage.color = foodImpatientBaseColor;
                break;

            default:
                if (progressBarObj != null && progressBarObj.activeSelf)
                    progressBarObj.SetActive(false);
                if (callIconObj != null && callIconObj.activeSelf)
                    callIconObj.SetActive(false);
                if (waitingFoodImpatientIconObj != null && waitingFoodImpatientIconObj.activeSelf)
                    waitingFoodImpatientIconObj.SetActive(false);
                break;
        }
    }

    void RebuildTableOrderCards()
    {
        if (orderCardContainer == null || orderCardPrefab == null || tableResponse == null)
            return;

        for (int i = orderCardContainer.childCount - 1; i >= 0; i--)
            Destroy(orderCardContainer.GetChild(i).gameObject);

        var list = tableResponse.GetCurrentOrder();
        if (list == null || list.Count == 0)
            return;

        int tableId = tableResponse.tableId;
        for (int i = 0; i < list.Count; i++)
        {
            var recipe = list[i];
            if (recipe == null)
                continue;

            GameObject cardObj = Instantiate(orderCardPrefab, orderCardContainer);
            var cardUi = cardObj.GetComponent<OrderCardUI>();
            if (cardUi != null)
                cardUi.SetupForTable(recipe, tableId, i);
        }
    }

    void CacheBaseColorsIfNeeded()
    {
        if (cachedBaseColors)
            return;

        if (callIconImage == null && callIconObj != null)
        {
            callIconImage = callIconObj.GetComponent<Image>();
            if (callIconImage == null)
                callIconImage = callIconObj.GetComponentInChildren<Image>(true);
        }
        if (callIconImage != null)
            callIconBaseColor = callIconImage.color;

        if (waitingFoodImpatientImage == null && waitingFoodImpatientIconObj != null)
        {
            waitingFoodImpatientImage = waitingFoodImpatientIconObj.GetComponent<Image>();
            if (waitingFoodImpatientImage == null)
                waitingFoodImpatientImage = waitingFoodImpatientIconObj.GetComponentInChildren<Image>(true);
        }
        if (waitingFoodImpatientImage != null)
            foodImpatientBaseColor = waitingFoodImpatientImage.color;

        cachedBaseColors = true;
    }

    static float PatienceStress01(float patienceRatio, float startRatio, float endRatio)
    {
        return Mathf.Clamp01(Mathf.InverseLerp(startRatio, endRatio, patienceRatio));
    }

    public void ApplyPatienceTintRatio(Image img, Color baseCol, float patienceRatio)
    {
        if (img == null)
            return;
        float t = PatienceStress01(patienceRatio, stressStartRatio, stressEndRatio);
        Color target = patienceStressColor;
        target.a = baseCol.a;
        img.color = Color.Lerp(baseCol, target, t);
    }

    /// <summary>当玩家上菜获得金钱时由 Rpc 或单机逻辑调用。</summary>
    public void ShowMoneyEarned(int amount)
    {
        if (debugLog)
            Debug.Log($"[TableUI] ShowMoneyEarned amount={amount}");

        if (moneyPopupText == null && moneyPopupObj != null)
            moneyPopupText = moneyPopupObj.GetComponent<TMPro.TMP_Text>();

        if (moneyPopupText == null)
        {
            if (debugLog)
                Debug.LogWarning("[TableUI] moneyPopupText 为空，无法显示加钱");
            return;
        }

        if (moneyPopupCoroutine != null)
            StopCoroutine(moneyPopupCoroutine);
        else
            currentPopupMoney = 0f;

        currentPopupMoney += amount;

        moneyPopupText.text = $"+$ {currentPopupMoney:0.00}";
        Color c = originalPopupColor;
        c.a = 1f;
        moneyPopupText.color = c;

        if (moneyPopupObj != null)
            moneyPopupObj.SetActive(true);
        else
            moneyPopupText.gameObject.SetActive(true);

        moneyPopupCoroutine = StartCoroutine(MoneyPopupFadeRoutine());
        RefreshCanvasActive();
    }

    IEnumerator MoneyPopupFadeRoutine()
    {
        const float duration = 3f;
        float popupTimer = duration;
        while (popupTimer > 0f)
        {
            popupTimer -= Time.deltaTime;
            if (moneyPopupText != null)
            {
                Color c = originalPopupColor;
                c.a = Mathf.Clamp01(popupTimer / duration);
                moneyPopupText.color = c;
            }
            yield return null;
        }

        if (moneyPopupObj != null)
            moneyPopupObj.SetActive(false);
        else if (moneyPopupText != null)
            moneyPopupText.gameObject.SetActive(false);

        currentPopupMoney = 0f;
        moneyPopupCoroutine = null;
        RefreshCanvasActive();
    }

    void LateUpdate()
    {
        if (!alwaysFaceCamera || mainCamera == null)
            return;
        transform.rotation = mainCamera.transform.rotation;
    }
}
