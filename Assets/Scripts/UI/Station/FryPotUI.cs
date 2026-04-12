using UnityEngine;
using UnityEngine.UI;

public class FryPotUI : MonoBehaviour
{
    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    [Header("UI References")]
    public Canvas canvas;
    public Image fillImage;

    [Header("Settings")]
    public bool alwaysFaceCamera = true;
    public bool hideWhenEmpty = true;

    [Header("糊菜警告")]
    [Tooltip("危险阶段且锅在煎炸台上时显示，Emission 在双色间闪烁")]
    public GameObject burnWarningMotionTarget;
    [Tooltip("若为空则从 burnWarningMotionTarget 上取 Renderer（含子物体）")]
    public Renderer burnWarningRenderer;
    [Tooltip("Emission 闪烁一端的 HDR 颜色（白）")]
    public Color burnEmissionColorA = Color.white;
    [Tooltip("Emission 闪烁另一端（红）")]
    public Color burnEmissionColorB = new Color(1f, 0f, 0f, 1f);
    [Tooltip("Emission 来回切换的速度")]
    public float burnEmissionFlashSpeed = 2f;

    private FryPot pot;
    private Camera mainCamera;
    private Color normalFillColor;
    private MaterialPropertyBlock emissionMpb;

    void Start()
    {
        pot = GetComponentInParent<FryPot>();
        mainCamera = Camera.main;

        if (fillImage != null)
            normalFillColor = fillImage.color;

        if (canvas != null && hideWhenEmpty)
            canvas.gameObject.SetActive(false);

        if (burnWarningRenderer == null && burnWarningMotionTarget != null)
            burnWarningRenderer = burnWarningMotionTarget.GetComponentInChildren<Renderer>(true);

        if (burnWarningMotionTarget != null)
            burnWarningMotionTarget.SetActive(false);

        ApplyBurnEmission(Color.black);
    }

    void LateUpdate()
    {
        if (pot == null || canvas == null || fillImage == null) return;

        bool hasIngredient = pot.HasAnyIngredient();
        bool isBurn = pot.IsBurnCountdown;

        bool shouldShow = hasIngredient || isBurn;
        if (pot.cookingFinished)
            shouldShow = false;

        if (!shouldShow)
            ResetBurnWarningVisual();

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
            ResetBurnWarningVisual();
        }

        if (alwaysFaceCamera && mainCamera != null)
            transform.rotation = mainCamera.transform.rotation;
    }

    void UpdateCookingUI()
    {
        if (!fillImage.gameObject.activeSelf)
            fillImage.gameObject.SetActive(true);

        float progress = 0f;
        if (pot.requiredProgress > 0.001f)
            progress = Mathf.Clamp01(pot.currentProgress / pot.requiredProgress);

        fillImage.fillAmount = progress;
        fillImage.color = normalFillColor;
    }

    void UpdateBurnUI()
    {
        if (fillImage.gameObject.activeSelf)
            fillImage.gameObject.SetActive(false);

        float ratio = pot.BurnRatio;
        float safeRatio = pot.BurnSafeRatio;
        bool inDanger = ratio > safeRatio;
        bool showDanger = inDanger && pot.ReceivesStationHeat;

        if (!showDanger || burnWarningMotionTarget == null)
        {
            SetBurnWarningActive(false);
            ApplyBurnEmission(Color.black);
            return;
        }

        SetBurnWarningActive(true);
        float t = Mathf.PingPong(Time.time * burnEmissionFlashSpeed, 1f);
        ApplyBurnEmission(Color.Lerp(burnEmissionColorA, burnEmissionColorB, t));
    }

    void ResetBurnWarningVisual()
    {
        SetBurnWarningActive(false);
        ApplyBurnEmission(Color.black);
    }

    void SetBurnWarningActive(bool on)
    {
        if (burnWarningMotionTarget == null) return;
        if (burnWarningMotionTarget.activeSelf != on)
            burnWarningMotionTarget.SetActive(on);
    }

    void ApplyBurnEmission(Color c)
    {
        if (burnWarningRenderer == null) return;
        if (emissionMpb == null)
            emissionMpb = new MaterialPropertyBlock();
        burnWarningRenderer.GetPropertyBlock(emissionMpb);
        emissionMpb.SetColor(EmissionColorId, c);
        burnWarningRenderer.SetPropertyBlock(emissionMpb);
    }
}
