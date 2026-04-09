using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 两个 Icon：1) 等待点菜 — 颜色随耐心在「开始发慌」到「彻底发慌」之间变红；2) 等上菜耐心低时 — 同色规则，耐心回升则颜色恢复。
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

    [Header("Debug")]
    public bool debugLog;

    private Camera mainCamera;
    private Color callIconBaseColor = Color.white;
    private Color foodImpatientBaseColor = Color.white;
    private bool cachedBaseColors;

    // 金钱弹窗控制变量
    private float popupTimer = 0f;
    private float currentPopupMoney = 0f;
    private Color originalPopupColor = Color.white;

    void Start()
    {
        if (tableResponse == null)
            tableResponse = transform.parent.GetComponentInChildren<OrderResponse>();

        mainCamera = Camera.main;

        if (canvas != null) canvas.enabled = false;
        
        if (moneyPopupText != null)
        {
            originalPopupColor = moneyPopupText.color;
            if (moneyPopupObj == null)
            {
                moneyPopupText.gameObject.SetActive(false); // 没绑 Obj 就直接关 Text 的游戏对象
            }
        }
        
        if (moneyPopupObj != null) moneyPopupObj.SetActive(false);

        CacheBaseColorsIfNeeded();
    }

    void CacheBaseColorsIfNeeded()
    {
        if (cachedBaseColors) return;

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

    /// <summary>耐心比例从高到低：t=0 原色，t=1 全红（在 stressStartRatio 与 stressEndRatio 之间插值）。</summary>
    static float PatienceStress01(float patienceRatio, float startRatio, float endRatio)
    {
        // InverseLerp: 当 patienceRatio 从 startRatio 下降到 endRatio 时，返回 0 到 1
        return Mathf.Clamp01(Mathf.InverseLerp(startRatio, endRatio, patienceRatio));
    }

    /// <summary>
    /// 【外部接口】允许其他系统主动施加基于耐心比例的变色效果
    /// </summary>
    public void ApplyPatienceTintRatio(Image img, Color baseCol, float patienceRatio)
    {
        if (img == null) return;
        float t = PatienceStress01(patienceRatio, stressStartRatio, stressEndRatio);
        Color target = patienceStressColor;
        target.a = baseCol.a;
        img.color = Color.Lerp(baseCol, target, t);
    }
    /// <summary>
    /// 当玩家上菜获得金钱时调用，显示弹窗并重置计时器。
    /// </summary>
    public void ShowMoneyEarned(int amount)
    {
        if (debugLog) Debug.Log($"[TableUI] ShowMoneyEarned 被调用! amount={amount}");

        if (moneyPopupText == null && moneyPopupObj != null)
             moneyPopupText = moneyPopupObj.GetComponent<TMPro.TMP_Text>();
             
        if (moneyPopupText == null)
        {
             if (debugLog) Debug.LogWarning("[TableUI] 尝试显示金钱，但 moneyPopupText 仍为空！");
             return;
        }
        
        // 如果当前没有弹窗，保存一个纯净的颜色，并重置累计金钱
        if (popupTimer <= 0f) {
             currentPopupMoney = 0f;
        }

        currentPopupMoney += amount;
        popupTimer = 3f; // 重新设定3秒倒计时

        // 显示文本
        moneyPopupText.text = $"+$ {currentPopupMoney:0.00}";
        if (debugLog) Debug.Log($"[TableUI] 金钱文本已更新为: {moneyPopupText.text}");
        
        // 重置透明度为完全不透明
        Color c = originalPopupColor;
        c.a = 1f;
        moneyPopupText.color = c;
        
        if (moneyPopupObj != null) 
            moneyPopupObj.SetActive(true);
        else 
            moneyPopupText.gameObject.SetActive(true); // 如果没有专门绑 obj，就把纯文字节点激活
    }

    void LateUpdate()
    {
        if (tableResponse == null) return;
        if (canvas == null) return;

        CacheBaseColorsIfNeeded();

        var state = tableResponse.currentState;
        bool showCall = tableResponse.ShouldShowWaitingToOrderCallIcon;
        bool showFoodImpatient = tableResponse.ShouldShowWaitingFoodImpatientIcon;
        bool isShowingPopup = popupTimer > 0f;

        bool shouldShowCanvas =
            state == OrderResponse.TableState.WaitingToOrder ||
            state == OrderResponse.TableState.Ordering ||
            (state == OrderResponse.TableState.WaitingForFood && showFoodImpatient) ||
            isShowingPopup; // 即使在别的阶段，只要弹窗时间还在，就显示Canvas

        if (canvas.enabled != shouldShowCanvas)
        {
            canvas.enabled = shouldShowCanvas;
            if (debugLog) Debug.Log($"[TableUI] Canvas: {canvas.enabled}, 状态: {state}");
        }

        if (!canvas.enabled) return;

        switch (state)
        {
            case OrderResponse.TableState.WaitingToOrder:
                if (progressBarObj != null && progressBarObj.activeSelf) progressBarObj.SetActive(false);
                if (callIconObj != null && callIconObj.activeSelf != showCall) callIconObj.SetActive(showCall);
                
                float orderRatio = tableResponse.effectiveMaxPatienceOrder > 0f ? (tableResponse.currentPatienceOrder / tableResponse.effectiveMaxPatienceOrder) : 0f;
                ApplyPatienceTintRatio(callIconImage, callIconBaseColor, orderRatio);
                
                if (waitingFoodImpatientIconObj != null && waitingFoodImpatientIconObj.activeSelf)
                    waitingFoodImpatientIconObj.SetActive(false);
                if (waitingFoodImpatientImage != null)
                    waitingFoodImpatientImage.color = foodImpatientBaseColor;
                break;

            case OrderResponse.TableState.Ordering:
                if (callIconObj != null && callIconObj.activeSelf) callIconObj.SetActive(false);
                if (callIconImage != null) callIconImage.color = callIconBaseColor;
                if (progressBarObj != null && !progressBarObj.activeSelf) progressBarObj.SetActive(true);
                if (fillImage != null) fillImage.fillAmount = tableResponse.GetOrderProgressNormalized();
                if (waitingFoodImpatientIconObj != null && waitingFoodImpatientIconObj.activeSelf)
                    waitingFoodImpatientIconObj.SetActive(false);
                if (waitingFoodImpatientImage != null)
                    waitingFoodImpatientImage.color = foodImpatientBaseColor;
                break;

            case OrderResponse.TableState.WaitingForFood:
                if (callIconObj != null && callIconObj.activeSelf) callIconObj.SetActive(false);
                if (callIconImage != null) callIconImage.color = callIconBaseColor;
                if (progressBarObj != null && progressBarObj.activeSelf) progressBarObj.SetActive(false);
                if (waitingFoodImpatientIconObj != null && waitingFoodImpatientIconObj.activeSelf != showFoodImpatient)
                    waitingFoodImpatientIconObj.SetActive(showFoodImpatient);
                if (showFoodImpatient)
                {
                    float foodRatio = tableResponse.effectiveMaxPatienceFood > 0f ? (tableResponse.currentPatienceFood / tableResponse.effectiveMaxPatienceFood) : 0f;
                    ApplyPatienceTintRatio(waitingFoodImpatientImage, foodImpatientBaseColor, foodRatio);
                }
                else if (waitingFoodImpatientImage != null)
                    waitingFoodImpatientImage.color = foodImpatientBaseColor;
                break;
        }

        // 弹窗消失逻辑
        if (popupTimer > 0f)
        {
            popupTimer -= Time.deltaTime;
            
            if (moneyPopupText != null)
            {
                Color c = originalPopupColor;
                c.a = Mathf.Clamp01(popupTimer / 3f); // 从 1 慢慢降到 0
                moneyPopupText.color = c;
            }

            if (popupTimer <= 0f)
            {
                if (moneyPopupObj != null) moneyPopupObj.SetActive(false);
                else if (moneyPopupText != null) moneyPopupText.gameObject.SetActive(false);
            }
        }

        if (alwaysFaceCamera && mainCamera != null)
            transform.rotation = mainCamera.transform.rotation;
    }
}
