using UnityEngine;

public enum DishQuality { Normal, Critical, Flawed }

/// <summary>
/// 挂在每道菜的 Dish 预制体上，持有三种品质的子模型引用。
/// 盛菜时由 FryPot 的品质结果调用 ApplyQuality 激活对应模型。
/// </summary>
public class DishQualityTag : MonoBehaviour
{
    [Header("品质子模型（初始均隐藏，结算时激活对应的）")]
    public GameObject normalObj;
    public GameObject criticalObj;
    public GameObject flawedObj;

    [Header("视觉效果")]
    [Tooltip("激活后模型绕 Y 轴旋转速度（度/秒），0 则不旋转")]
    public float rotateSpeed = 30f;

    [Header("Debug")]
    public bool debugLog = false;

    [HideInInspector] public DishQuality quality = DishQuality.Normal;

    private GameObject activeVisual;

    public float PriceMultiplier => quality switch
    {
        DishQuality.Critical => 2.0f,
        DishQuality.Flawed   => 0.5f,
        _                    => 1.0f,
    };

    void Awake()
    {
        if (normalObj != null) normalObj.SetActive(false);
        if (criticalObj != null) criticalObj.SetActive(false);
        if (flawedObj != null) flawedObj.SetActive(false);
    }

    void Update()
    {
        if (activeVisual != null && activeVisual.activeSelf && rotateSpeed != 0f)
            activeVisual.transform.Rotate(0f, rotateSpeed * Time.deltaTime, 0f, Space.Self);
    }

    public void ApplyQuality(DishQuality q)
    {
        quality = q;

        if (normalObj != null) normalObj.SetActive(false);
        if (criticalObj != null) criticalObj.SetActive(false);
        if (flawedObj != null) flawedObj.SetActive(false);
        activeVisual = null;

        switch (q)
        {
            case DishQuality.Critical:
                if (criticalObj != null) { criticalObj.SetActive(true); activeVisual = criticalObj; }
                else if (debugLog) Debug.Log($"[DishQualityTag] {name}: criticalObj 未配置，暴击菜无法显示专属模型。");
                break;
            case DishQuality.Flawed:
                if (flawedObj != null) { flawedObj.SetActive(true); activeVisual = flawedObj; }
                else if (debugLog) Debug.Log($"[DishQualityTag] {name}: flawedObj 未配置，瑕疵菜无法显示专属模型。");
                break;
            default:
                if (normalObj != null) { normalObj.SetActive(true); activeVisual = normalObj; }
                else if (debugLog) Debug.Log($"[DishQualityTag] {name}: normalObj 未配置，普通菜无法显示专属模型。");
                break;
        }

        if (debugLog) Debug.Log($"[DishQualityTag] {name}: 品质={q}, 倍率={PriceMultiplier}x");
    }
}
