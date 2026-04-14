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
2. **联机**：在垃圾桶**根物体**（或包含本脚本的祖先）上挂 **`NetworkIdentity`**，否则「成品菜 → 空盘」无法发 `Command`（会打 Error Log）。
3. Inspector 上会看到以下字段：
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
| `cleanPlatePrefab` | GameObject | 空盘子预制体，Dish 转盘子时使用；**联机必须带 `NetworkIdentity`**（与洗碗机发放盘子一致） |

---

## 四、功能说明

### ItemCategory.BinProtected（策划配置）

- 在 **`CarryableItem` 的 Categories** 中勾选 **`BinProtected`** 的预制体：面向 BinStation 按 **J / K** 无玩法效果（输入被吞掉）；也 **不能** 通过垃圾桶槽位被销毁。
- **锅**、**成品菜**（`DishRecipeTag`）仍以 **组件** 判定，不要求勾 BinProtected。
- 若希望 **空盘子** 也不能扔桶，请在空盘预制体上勾选 **`BinProtected`**。

### 功能 A：J 键 — 把物品放进垃圾桶（销毁）

- 玩家手持 **未受保护** 的物品 → 面对 BinStation 的 PlacePoint → 按 J 放下 → 销毁。
- **受保护**（锅 / 成品菜 / `BinProtected`）：无法放入槽位销毁，放下会被取消。

### 功能 B：J / K — 手持锅（FryPot）

- 有内容可清空时：清空锅（联机走 `CmdRequestFryPotDump`）。
- **空锅**：无状态变化，仅 Console 日志（`debugLog` 开启时）。

### 功能 C：J / K — 手持成品菜（`DishRecipeTag`）转空盘

- 转为 `cleanPlatePrefab` 空盘（与 `BinProtected` 并存时 **优先** 走成品菜逻辑）。

---

## 五、常见问题排查

| 问题 | 检查项 |
|------|--------|
| Sensor 检测不到 BinStation | 确认 BinStation 的 Collider 是 Trigger，且 Layer 在 `stationMask` 中 |
| J 键放不上物品 | 确认 BinPlacePoint 的 Collider 是 Trigger，Layer 在 `placePointMask` 中，且 `allowAnyCategory = true` |
| 锅无法清空 | 确认 `CanDump()` 为真（有食材、已出菜或糊菜倒计时等，见 `FryPot`） |
| 成品菜无反应 | 确认预制体挂有 `DishRecipeTag`；`cleanPlatePrefab` 已配置 |
| 某道具仍被桶销毁 | 在预制体 `CarryableItem` 上勾选 **BinProtected** |
| 持物时 BinStation 不高亮 | 确认 `highlightObject` 已拖入 |
