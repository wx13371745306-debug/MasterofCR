# BinStation（垃圾桶工作台）Unity 配置指南

## 一、代码变更总结

本次改动涉及 3 个文件：

| 文件 | 改动 |
|------|------|
| `Assets/Scripts/Station/BinStation.cs` | **新建** - 垃圾桶工作台脚本 |
| `Assets/Scripts/Station/FryPot.cs` | **修改** - 新增 `CanDump()` 和 `ForceClear()` 公开方法 |
| `Assets/Scripts/Player/PlayerItemInteractor.cs` | **修改** - K 键优先检查 BinStation；持物时高亮 BinStation |

---

## 二、创建 BinStation 游戏对象

### 步骤 1：创建主体

1. 在场景中创建一个空 GameObject，命名为 `BinStation`
2. 放置好位置（垃圾桶应该在的地方）
3. 添加你的垃圾桶模型作为子物体（纯视觉）

### 步骤 2：配置 Station 碰撞体（用于 K 键交互检测）

1. 在 `BinStation` 上添加一个 **Collider**（Box/Sphere/Capsule 均可），设为 **Is Trigger = true**
2. 将 `BinStation` 的 **Layer 设为 Station 层**（即 `PlayerInteractionSensor` 上 `stationMask` 所包含的层）
   - 这是让 Sensor 能检测到这个 Station 的关键

### 步骤 3：挂载 BinStation 脚本

1. 给 `BinStation` 添加 **`BinStation`** 组件
2. Inspector 上会看到以下字段：
   - **Highlight Object**（继承自 BaseStation）：拖入高亮显示用的子物体（可选）
   - **Bin Place Point**：拖入下面创建的 ItemPlacePoint（步骤 4）
   - **Clean Plate Prefab**：拖入干净盘子预制体（和 `DishWashingStation` 上的 `cleanPlatePrefab` 用同一个即可）

### 步骤 4：创建 ItemPlacePoint（用于 J 键放入并销毁物品）

1. 在 `BinStation` 下创建一个子 GameObject，命名为 `BinPlacePoint`
2. 添加 **`ItemPlacePoint`** 组件
3. 添加一个 **Collider**（Box/Sphere），设为 **Is Trigger = true**
4. 将 `BinPlacePoint` 的 **Layer 设为 PlacePoint 层**（即 `PlayerInteractionSensor` 上 `placePointMask` 所包含的层）
5. `ItemPlacePoint` 设置：
   - **Allow Any Category** = `true`（允许任何物品放入）
   - **Attach Point**：可以指向自身或一个子 Transform（物品销毁前会短暂出现在这里）

### 步骤 5：回到 BinStation 组件

- 将步骤 4 创建的 `BinPlacePoint` 拖入 `BinStation` 的 **Bin Place Point** 字段

---

## 三、Inspector 字段速查

### BinStation 组件

| 字段 | 类型 | 说明 |
|------|------|------|
| `highlightObject` | GameObject | 高亮物体，Sensor 瞄准时亮起（可选） |
| `debugLog` | bool | 是否在 Console 打印调试日志 |
| `binPlacePoint` | ItemPlacePoint | 垃圾桶的放置点，J 键放入物品时触发销毁 |
| `cleanPlatePrefab` | GameObject | 空盘子预制体，Dish 转盘子时使用 |

---

## 四、功能说明

### 功能 A：J 键 — 把物品放进垃圾桶（销毁）

- 玩家手持任何物品 → 面对 BinStation 的 PlacePoint → 按 J 放下
- 物品被放置到 PlacePoint 后立即被销毁
- 适用于所有类型的 CarryableItem

### 功能 B：K 键 — 手持锅（FryPot）清空

- 玩家长按 J 端起锅 → 面对 BinStation → 按 K
- 锅内的所有食材、烹饪进度、视觉效果全部清除
- 锅本身留在玩家手中，变回空锅

### 功能 C：K 键 — 手持菜品（Dish）转空盘

- 玩家手持成品菜（带有 `DishRecipeTag` 的物品）→ 面对 BinStation → 按 K
- 菜品被销毁，玩家手中变为一个干净的空盘子（`cleanPlatePrefab`）

---

## 五、常见问题排查

| 问题 | 检查项 |
|------|--------|
| Sensor 检测不到 BinStation | 确认 BinStation 的 Collider 是 Trigger，且 Layer 在 `stationMask` 中 |
| J 键放不上物品 | 确认 BinPlacePoint 的 Collider 是 Trigger，Layer 在 `placePointMask` 中，且 `allowAnyCategory = true` |
| K 键对锅无反应 | 确认锅内确实有食材（`HasAnyIngredient`）或已完成烹饪（`cookingFinished`） |
| K 键对菜品无反应 | 确认菜品预制体上挂有 `DishRecipeTag` 组件 |
| 菜品转盘子后手中是空的 | 确认 BinStation 的 `cleanPlatePrefab` 已配置，且该预制体有 `CarryableItem` |
| 持物时 BinStation 不高亮 | 确认 `highlightObject` 已拖入 |
