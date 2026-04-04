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
    [Tooltip("等上菜且耐心低于 OrderResponse.impatientThreshold 时显示")]
    public GameObject waitingFoodImpatientIconObj;
    [Tooltip("为空则从 waitingFoodImpatientIconObj 上取 Image")]
    public Image waitingFoodImpatientImage;

    [Header("Patience Color")]
    [Tooltip("耐心高于此值：保持原色；从此值开始向红色过渡")]
    public float patienceColorStressStart = 50f;
    [Tooltip("耐心低于等于此值：完全变为 stress 色")]
    public float patienceColorStressEnd = 10f;
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

    void Start()
    {
        if (tableResponse == null)
            tableResponse = transform.parent.GetComponentInChildren<OrderResponse>();

        mainCamera = Camera.main;

        if (canvas != null) canvas.enabled = false;

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

    /// <summary>耐心从高到低：t=0 原色，t=1 全红（在 stressStart 与 stressEnd 之间插值）。</summary>
    static float PatienceStress01(float patience, float stressStart, float stressEnd)
    {
        return Mathf.Clamp01(Mathf.InverseLerp(stressStart, stressEnd, patience));
    }

    void ApplyPatienceTint(Image img, Color baseCol, float patience)
    {
        if (img == null) return;
        float t = PatienceStress01(patience, patienceColorStressStart, patienceColorStressEnd);
        Color target = patienceStressColor;
        target.a = baseCol.a;
        img.color = Color.Lerp(baseCol, target, t);
    }

    void LateUpdate()
    {
        if (tableResponse == null) return;
        if (canvas == null) return;

        CacheBaseColorsIfNeeded();

        var state = tableResponse.currentState;
        bool showCall = tableResponse.ShouldShowWaitingToOrderCallIcon;
        bool showFoodImpatient = tableResponse.ShouldShowWaitingFoodImpatientIcon;

        bool shouldShowCanvas =
            state == OrderResponse.TableState.WaitingToOrder ||
            state == OrderResponse.TableState.Ordering ||
            (state == OrderResponse.TableState.WaitingForFood && showFoodImpatient);

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
                ApplyPatienceTint(callIconImage, callIconBaseColor, tableResponse.currentPatienceOrder);
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
                    ApplyPatienceTint(waitingFoodImpatientImage, foodImpatientBaseColor, tableResponse.currentPatienceFood);
                else if (waitingFoodImpatientImage != null)
                    waitingFoodImpatientImage.color = foodImpatientBaseColor;
                break;
        }

        if (alwaysFaceCamera && mainCamera != null)
            transform.rotation = mainCamera.transform.rotation;
    }
}
