using UnityEngine;
using UnityEngine.UI;

public class FryPotUI : MonoBehaviour
{
    [Header("UI References")]
    public Canvas canvas;
    public Image fillImage;

    [Header("Settings")]
    public bool alwaysFaceCamera = true;
    public bool hideWhenEmpty = true;

    [Header("糊菜闪烁")]
    [Tooltip("闪烁时的警告色")]
    public Color burnWarningColor = Color.red;
    [Tooltip("闪烁的起始速度（Hz）")]
    public float burnFlashBaseSpeed = 2f;
    [Tooltip("倒计时结束时闪烁速度的倍率")]
    public float burnFlashMaxSpeedMul = 8f;

    private FryPot pot;
    private Camera mainCamera;
    private Color normalFillColor;
    private float flashAccum;

    void Start()
    {
        pot = GetComponentInParent<FryPot>();
        mainCamera = Camera.main;

        if (fillImage != null)
            normalFillColor = fillImage.color;

        if (canvas != null && hideWhenEmpty)
            canvas.gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        if (pot == null || canvas == null || fillImage == null) return;

        bool hasIngredient = pot.HasAnyIngredient();
        bool isBurn = pot.IsBurnCountdown;

        bool shouldShow = hasIngredient || isBurn;
        if (pot.cookingFinished)
            shouldShow = false;

        if (hideWhenEmpty && canvas.gameObject.activeSelf != shouldShow)
            canvas.gameObject.SetActive(shouldShow);

        if (!canvas.gameObject.activeSelf) return;

        if (isBurn)
        {
            UpdateBurnUI();
        }
        else
        {
            UpdateCookingUI();
            flashAccum = 0f;
        }

        if (alwaysFaceCamera && mainCamera != null)
            transform.rotation = mainCamera.transform.rotation;
    }

    void UpdateCookingUI()
    {
        float progress = 0f;
        if (pot.requiredProgress > 0.001f)
            progress = Mathf.Clamp01(pot.currentProgress / pot.requiredProgress);

        fillImage.fillAmount = progress;
        fillImage.color = normalFillColor;
    }

    void UpdateBurnUI()
    {
        float ratio = pot.BurnRatio;
        fillImage.fillAmount = 1f - ratio;

        float safeRatio = pot.BurnSafeRatio;
        if (ratio <= safeRatio)
        {
            fillImage.color = normalFillColor;
            flashAccum = 0f;
            return;
        }

        // 危险阶段：蓝红闪烁，速度随 ratio 递增
        float dangerProgress = Mathf.InverseLerp(safeRatio, 1f, ratio);
        float speed = Mathf.Lerp(burnFlashBaseSpeed, burnFlashBaseSpeed * burnFlashMaxSpeedMul, dangerProgress);
        flashAccum += Time.deltaTime * speed;

        float t = Mathf.PingPong(flashAccum, 1f);
        fillImage.color = Color.Lerp(normalFillColor, burnWarningColor, t);
    }
}