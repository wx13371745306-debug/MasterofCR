using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Day0 开场面板：绑定「开始第一天」按钮到 <see cref="DayCycleManager.RequestStartFirstDay"/>。
/// 面板显示/隐藏可由你在 Inspector 里关联 <see cref="OnEnterDayZero"/>，或自行控制。
/// </summary>
public class DayZeroIntroUI : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private Button startFirstDayButton;
    [SerializeField] private DayCycleManager dayCycle;
    [Tooltip("第一天（周一）关卡配置中计划的顾客总数")]
    [SerializeField] private TextMeshProUGUI firstDayPlannedGuestsText;

    void Start()
    {
        var d = Resolve();
        if (d != null && d.Phase == DayCyclePhase.DayZero && panel != null)
            panel.SetActive(true);
        RefreshFirstDayPlannedGuests();
    }

    void OnEnable()
    {
        var d = Resolve();
        if (d != null)
        {
            d.OnEnterDayZero += ShowPanel;
            d.OnPrepStart += HidePanel;
        }

        if (startFirstDayButton != null)
            startFirstDayButton.onClick.AddListener(OnStartFirstDayClicked);
    }

    void OnDisable()
    {
        var d = Resolve();
        if (d != null)
        {
            d.OnEnterDayZero -= ShowPanel;
            d.OnPrepStart -= HidePanel;
        }

        if (startFirstDayButton != null)
            startFirstDayButton.onClick.RemoveListener(OnStartFirstDayClicked);
    }

    DayCycleManager Resolve() => dayCycle != null ? dayCycle : DayCycleManager.Instance;

    void ShowPanel()
    {
        if (panel != null)
            panel.SetActive(true);
        RefreshFirstDayPlannedGuests();
    }

    void RefreshFirstDayPlannedGuests()
    {
        var d = Resolve();
        if (firstDayPlannedGuestsText != null && d != null)
            firstDayPlannedGuestsText.text = d.GetPlannedGuestCountForDay(0).ToString();
    }

    void HidePanel()
    {
        if (panel != null)
            panel.SetActive(false);
    }

    public void OnStartFirstDayClicked()
    {
        var d = Resolve();
        if (d != null)
            d.RequestStartFirstDay();
    }
}
