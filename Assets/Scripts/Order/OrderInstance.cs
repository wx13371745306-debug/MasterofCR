using System;

/// <summary>
/// 独立的订单实例。
/// 即使同一个桌子点了两盘一样的菜，它们也会生成两个不同的 OrderInstance，拥有不同的 orderId。
/// 这样在删除（上菜）时，就能精准做到“上哪个消哪个”，绝不会误删。
/// </summary>
[Serializable]
public class OrderInstance
{
    public string orderId;      // 唯一标识符 (GUID)
    public int tableId;         // 是几号桌点的
    public FryRecipeDatabase.FryRecipe recipe; // 具体的菜品配方数据

    // 构造函数，在生成订单时调用
    public OrderInstance(int tableId, FryRecipeDatabase.FryRecipe recipe)
    {
        // 自动生成一个全球唯一的字符串作为 ID，例如 "d3f4b...a2"
        this.orderId = Guid.NewGuid().ToString(); 
        this.tableId = tableId;
        this.recipe = recipe;
    }
}