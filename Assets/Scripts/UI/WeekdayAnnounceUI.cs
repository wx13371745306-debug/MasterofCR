using TMPro;
using UnityEngine;

/// <summary>
/// 准备阶段开始时显示「周一」等文案，持续 showDuration 后隐藏。
/// </summary>
public class WeekdayAnnounceUI : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private TextMeshProUGUI weekdayText;
    [SerializeField] private float showDuration = 2f;
    [SerializeField] private DayCycleManager dayCycle;

    float hideAt;
    bool showing;

    void OnEnable()
    {
        var d = dayCycle != null ? dayCycle : DayCycleManager.Instance;
        if (d != null)
            d.OnPrepStart += OnPrepStart;
    }

    void OnDisable()
    {
        var d = dayCycle != null ? dayCycle : DayCycleManager.Instance;
        if (d != null)
            d.OnPrepStart -= OnPrepStart;
    }

    void OnPrepStart()
    {
        var d = dayCycle != null ? dayCycle : DayCycleManager.Instance;
        if (weekdayText != null && d != null)
            weekdayText.text = d.CurrentWeekdayName;
        if (panel != null)
            panel.SetActive(true);
        showing = true;
        hideAt = Time.time + Mathf.Max(0.1f, showDuration);
    }

    void Update()
    {
        if (!showing) return;
        if (Time.time >= hideAt && panel != null)
        {
            panel.SetActive(false);
            showing = false;
        }
    }
}
