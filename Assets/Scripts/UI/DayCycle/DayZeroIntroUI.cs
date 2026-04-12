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

    [Header("开始第一天 校验")]
    [Tooltip("菜单数据资产，用于检查玩家是否已选了至少一道菜")]
    [SerializeField] private MenuSO menuSO;
    [Tooltip("配送队列，用于检查玩家是否有已下单的待交付货物")]
    [SerializeField] private ShopDeliveryQueue shopDeliveryQueue;
    [Tooltip("弹窗 UI 控制器（与打烊面板共用同一个）")]
    [SerializeField] private NextDayConfirmUI confirmUI;

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
        // ========== 校验 Step 1：菜单是否为空 ==========
        if (menuSO == null || menuSO.selectedRecipes == null || menuSO.selectedRecipes.Count == 0)
        {
            if (confirmUI != null)
                confirmUI.ShowWarning("请至少选择一道菜才能开始第一天！");
            else
                Debug.LogWarning("[DayZeroIntroUI] 菜单为空且 confirmUI 未赋值，无法弹窗！");
            return;
        }

        // ========== 校验 Step 2：是否有已下单的待交付货物 ==========
        bool hasOrders = NetworkShopBridge.HasPendingOrdersForUiValidation(shopDeliveryQueue);

        if (!hasOrders)
        {
            if (confirmUI != null)
                confirmUI.ShowConfirm("你还没有下单购买任何食材，确定要开始第一天吗？", DoStartFirstDay);
            else
                DoStartFirstDay();
            return;
        }

        // ========== 两项都满足，直接开始 ==========
        DoStartFirstDay();
    }

    void DoStartFirstDay()
    {
        var bridge = NetworkDayCycleBridge.Instance;
        if (bridge != null && Mirror.NetworkClient.active)
        {
            bridge.NetworkRequestStartFirstDay();
            return;
        }

        var d = Resolve();
        if (d != null)
            d.RequestStartFirstDay();
    }
}
