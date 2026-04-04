using UnityEngine;

/// <summary>
/// 挂在 GlobalManagers 上；由 OrderResponse / DayCycleManager 写入当日统计。
/// </summary>
public class DayStatsTracker : MonoBehaviour
{
    public static DayStatsTracker Instance { get; private set; }

    [SerializeField] private DayRunStats current = new DayRunStats();

    public DayRunStats Current => current;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void ClearDay() => current.Clear();

    public void RegisterGuestsServed(int count)
    {
        if (count <= 0) return;
        current.guestsServed += count;
    }

    public void RegisterGuestsFailed(int count)
    {
        if (count <= 0) return;
        current.guestsFailed += count;
    }

    public void RegisterPlacedItem(bool isCorrectOrder, bool isDrink)
    {
        if (isCorrectOrder)
        {
            if (isDrink) current.orderDrinkCount++;
            else current.orderDishCount++;
        }
        else
        {
            if (isDrink) current.extraDrinkCount++;
            else current.extraDishCount++;
        }
    }

    public void RegisterRevenue(int amount)
    {
        if (amount == 0) return;
        current.revenue += amount;
    }

    public void RegisterFootfall(int count)
    {
        if (count <= 0) return;
        current.footfall += count;
    }
}
