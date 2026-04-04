using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 打烊阶段显示当日统计与「下一天」按钮；新一天准备开始时隐藏。
/// </summary>
public class ClosingSummaryUI : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private DayCycleManager dayCycle;
    [SerializeField] private DayStatsTracker statsTracker;

    [SerializeField] private TextMeshProUGUI guestsServedText;
    [SerializeField] private TextMeshProUGUI guestsFailedText;
    [SerializeField] private TextMeshProUGUI orderDishText;
    [SerializeField] private TextMeshProUGUI orderDrinkText;
    [SerializeField] private TextMeshProUGUI extraDishText;
    [SerializeField] private TextMeshProUGUI extraDrinkText;
    [SerializeField] private TextMeshProUGUI revenueText;
    [SerializeField] private TextMeshProUGUI footfallText;
    [SerializeField] private TextMeshProUGUI badReviewRateText;
    [Tooltip("显示下一日关卡配置中计划的顾客总数（各波 groupSize 之和）")]
    [SerializeField] private TextMeshProUGUI nextDayPlannedGuestsText;
    [SerializeField] private Button nextDayButton;

    void Start()
    {
        var d = ResolveDayCycle();
        if (d != null && d.Phase == DayCyclePhase.Closing && panel != null)
        {
            panel.SetActive(true);
            RefreshTexts();
        }
    }

    void OnEnable()
    {
        var d = ResolveDayCycle();
        if (d != null)
        {
            d.OnEnterClosing += OnEnterClosing;
            d.OnPrepStart += OnPrepStartHide;
        }

        if (nextDayButton != null)
            nextDayButton.onClick.AddListener(OnNextDayClicked);
    }

    void OnDisable()
    {
        var d = ResolveDayCycle();
        if (d != null)
        {
            d.OnEnterClosing -= OnEnterClosing;
            d.OnPrepStart -= OnPrepStartHide;
        }

        if (nextDayButton != null)
            nextDayButton.onClick.RemoveListener(OnNextDayClicked);
    }

    DayCycleManager ResolveDayCycle() => dayCycle != null ? dayCycle : DayCycleManager.Instance;

    void OnEnterClosing()
    {
        if (panel != null)
            panel.SetActive(true);
        RefreshTexts();
    }

    void OnPrepStartHide()
    {
        if (panel != null)
            panel.SetActive(false);
    }

    void RefreshTexts()
    {
        var d = ResolveDayCycle();
        if (d != null && nextDayPlannedGuestsText != null)
            nextDayPlannedGuestsText.text = d.GetPlannedGuestCountForNextDay().ToString();

        var s = statsTracker != null ? statsTracker.Current : null;
        if (s == null) return;

        Set(guestsServedText, s.guestsServed);
        Set(guestsFailedText, s.guestsFailed);
        Set(orderDishText, s.orderDishCount);
        Set(orderDrinkText, s.orderDrinkCount);
        Set(extraDishText, s.extraDishCount);
        Set(extraDrinkText, s.extraDrinkCount);
        Set(revenueText, s.revenue);
        Set(footfallText, s.footfall);

        if (badReviewRateText != null)
        {
            float rate = s.footfall <= 0 ? 0f : (s.guestsFailed / (float)s.footfall) * 100f;
            badReviewRateText.text = rate.ToString("F2") + "%";
        }
    }

    static void Set(TextMeshProUGUI tmp, int value)
    {
        if (tmp != null)
            tmp.text = value.ToString();
    }

    void OnNextDayClicked()
    {
        var d = ResolveDayCycle();
        if (d != null)
            d.RequestNextDay();
    }
}
