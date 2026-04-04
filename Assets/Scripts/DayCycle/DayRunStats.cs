using System;

[Serializable]
public class DayRunStats
{
    public int guestsServed;
    public int guestsFailed;
    public int orderDishCount;
    public int orderDrinkCount;
    public int extraDishCount;
    public int extraDrinkCount;
    public int revenue;
    /// <summary>当日实际生成到场景中的顾客人数（按 AI 数量计）。</summary>
    public int footfall;

    public void Clear()
    {
        guestsServed = 0;
        guestsFailed = 0;
        orderDishCount = 0;
        orderDrinkCount = 0;
        extraDishCount = 0;
        extraDrinkCount = 0;
        revenue = 0;
        footfall = 0;
    }
}
