/// <summary>商店内价格展示：前缀 $ + 保留两位小数（单价、行价、总价均为整数金额时显示为 $5.00）。</summary>
public static class ShopPriceFormat
{
    public static string Format(int amount)
    {
        return $"${amount:F2}";
    }

    public static string FormatLineTotal(int unitPrice, int quantity)
    {
        return $"${unitPrice * quantity:F2}";
    }
}
