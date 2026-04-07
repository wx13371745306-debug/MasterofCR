/// <summary>
/// 菜谱羁绊标签（每道菜可同时归属多个羁绊类型）。
/// 使用 [Flags] 支持多选，Inspector 中可勾选多个。
/// 扩展新羁绊时在此追加枚举值（必须是 2 的幂），并在 BondActivationStateSO 中增加对应槽位。
/// </summary>
[System.Flags]
public enum RecipeBondTag
{
    None        = 0,
    Sichuan     = 1 << 0,   // 川湘菜
    HomeCooking = 1 << 1,   // 家常菜
    Vegetable   = 1 << 2,   // 素菜
    Meat        = 1 << 3    // 肉菜
}
