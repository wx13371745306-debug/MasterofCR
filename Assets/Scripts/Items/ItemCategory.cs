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
    Any = ~0
}