using UnityEngine;
using TMPro; // 使用 TextMeshPro

public class FPSCounter : MonoBehaviour
{
    public static FPSCounter Instance { get; private set; }

    [Header("UI Reference")]
    public TextMeshProUGUI fpsText;

    [Header("Settings")]
    [Tooltip("刷新频率（秒）：不要每帧刷新 UI，否则会造成额外的性能损耗")]
    public float updateInterval = 0.5f;

    [Tooltip("是否显示 FPS；可由设置菜单与 PlayerPrefs 同步。")]
    [SerializeField] bool showFps = true;

    private float accum = 0f; // 累计的时间
    private int frames = 0;   // 累计的帧数
    private float timeLeft;   // 距离下次刷新的倒计时

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Start()
    {
        showFps = PlayerPrefs.GetInt(GameplaySessionUIUtil.PrefKeyShowFps, 1) == 1;
        ApplyShowFpsVisual();
        timeLeft = updateInterval;
    }

    void Update()
    {
        if (!showFps)
            return;

        // 1. 累计时间和帧数
        // 注意：必须使用 unscaledDeltaTime，这样即使你用 Time.timeScale = 0 暂停了游戏，帧率依然能正常计算
        timeLeft -= Time.unscaledDeltaTime;
        accum += Time.unscaledDeltaTime;
        frames++;

        // 2. 当倒计时结束时，计算并刷新一次 UI
        if (timeLeft <= 0.0)
        {
            float fps = frames / accum;
            
            if (fpsText != null)
            {
                fpsText.text = $"FPS: {Mathf.RoundToInt(fps)}";

                // 3. 动态变色：直观反映性能状态
                if (fps >= 55)
                    fpsText.color = Color.green;      // 流畅
                else if (fps >= 30)
                    fpsText.color = Color.yellow;     // 勉强能玩
                else
                    fpsText.color = Color.red;        // 严重卡顿
            }

            // 4. 重置计时器，准备下一个周期的计算
            timeLeft = updateInterval;
            accum = 0f;
            frames = 0;
        }
    }

    public bool ShowFps => showFps;

    /// <summary>设置菜单 Toggle 调用：写入 PlayerPrefs 并控制 fpsText 显隐。</summary>
    public void SetShowFps(bool value)
    {
        showFps = value;
        PlayerPrefs.SetInt(GameplaySessionUIUtil.PrefKeyShowFps, value ? 1 : 0);
        ApplyShowFpsVisual();
    }

    void ApplyShowFpsVisual()
    {
        if (fpsText != null)
            fpsText.gameObject.SetActive(showFps);
    }
}