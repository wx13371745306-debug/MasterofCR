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

    [Header("下一天 校验")]
    [Tooltip("菜单数据资产，用于检查玩家是否已选了至少一道菜")]
    [SerializeField] private MenuSO menuSO;
    [Tooltip("配送队列，用于检查玩家是否有已下单的待交付货物")]
    [SerializeField] private ShopDeliveryQueue shopDeliveryQueue;
    [Tooltip("弹窗 UI 控制器")]
    [SerializeField] private NextDayConfirmUI confirmUI;

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
        // ========== 校验 Step 1：菜单是否为空 ==========
        if (menuSO == null || menuSO.selectedRecipes == null || menuSO.selectedRecipes.Count == 0)
        {
            if (confirmUI != null)
                confirmUI.ShowWarning("请至少选择一道菜才能开始下一天！");
            else
                Debug.LogWarning("[ClosingSummaryUI] 菜单为空且 confirmUI 未赋值，无法弹窗！");
            return;
        }

        // ========== 校验 Step 2：是否有已下单的待交付货物 ==========
        bool hasOrders = shopDeliveryQueue != null && shopDeliveryQueue.HasPendingOrders;

        if (!hasOrders)
        {
            if (confirmUI != null)
                confirmUI.ShowConfirm("你还没有下单购买任何食材，确定要开始下一天吗？", DoNextDay);
            else
                DoNextDay();
            return;
        }

        // ========== 两项都满足，直接下一天 ==========
        DoNextDay();
    }

    void DoNextDay()
    {
        var d = ResolveDayCycle();
        if (d != null)
            d.RequestNextDay();
    }
}

