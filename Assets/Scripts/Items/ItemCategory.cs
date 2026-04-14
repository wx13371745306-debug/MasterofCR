using System;

[Flags]
public enum ItemCategory
{
    None = 0,
    Ingredient = 1 << 0,
    Tool = 1 << 1,
    Dish = 1 << 2,
    Container = 1 << 3,
    StationLike = 1 << 4,
    ContainerContent = 1 << 5,
    DirtyPlate = 1 << 6,
    CleanPlate = 1 << 7,
    Item = 1 << 8,
    Accessory = 1 << 9,
    /// <summary>标记为不可当垃圾处理；需在预制体上勾选。锅/成品菜仍以组件逻辑为准。</summary>
    BinProtected = 1 << 10,
    Any = ~0
}