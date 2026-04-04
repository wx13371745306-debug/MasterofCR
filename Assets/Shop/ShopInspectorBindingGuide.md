# 商店 UI 按钮 Inspector 绑定指南

**下单**、**加入购物车**：请在 **Inspector** 里绑定（见下）。  
**购物车行 ±**：由脚本 **`AddListener`**，**不要**在 Inspector 里再绑同一方法，否则会点一次触发两次。

**调试日志**：在挂有 **`ShopUIController`** 的物体（一般为 **`ShopPanel`**）上勾选 **`Enable Shop Debug Logs`** 后，才会在 Console 输出商品卡/购物车行的调试信息；**取消勾选则不输出**。

---

## 1. 下单按钮（`Order Button`）

| 步骤 | 操作 |
|------|------|
| 选中对象 | 场景或预制体里挂有 **Button** 的「下单」物体 |
| 组件 | **Button** → **On Click ()** |
| 点击 **+** | 增加一条事件 |
| **Object** | 拖入挂有 **`ShopUIController`** 的物体（一般为 **`ShopPanel`**） |
| **Function** | 选 **`ShopUIController` → `OnOrderClicked()`** |

---

## 2. 商品卡 · 加入购物车（`ShopProductCard` 预制体）

**代码不再对「加入购物车」`AddListener`**，只触发 **Inspector 绑定的一次**。

| 步骤 | 操作 |
|------|------|
| 打开预制体 | **`ShopProductCard`** |
| 选中 | 「加入购物车」**Button** 物体 |
| **On Click ()** | **Object** → 拖 **`ShopProductCard` 根物体**（挂 **`ShopProductCardView`**） |
| **Function** | **`ShopProductCardView` → `OnAddToCartButtonClicked()`** |

**Inspector 必做：** 在 **`ShopProductCardView`** 上把 **`Add To Cart Button`** 指到该按钮。

**可选：** 拖入 **`Shop UI`**；不拖则从父级自动查找 **`ShopUIController`**。

---

## 3. 购物车行 · 加 / 减（`ShopCartLine` 预制体）

**脚本在 `OnEnable` / `OnDisable` 里对 `Minus` / `Plus` 自动绑定**；**请勿**在 Inspector 再绑 `OnMinus` / `OnPlus`（会重复触发）。

**Inspector 必做：** 在 **`ShopCartLineView`** 上把 **`Minus Button`**、**`Plus Button`** 指到对应子物体。

**行背景挡点击：** 可保留 **`Disable Root Background Raycast`**，避免根 **Image** 挡射线。

---

## 4. 常见问题

**Q：为什么 Function 里选不到 `OnOrderClicked`？**  
A：目标 Object 必须是带 **`ShopUIController`** 的物体；脚本编译成功后再打开下拉列表。

**Q：点击没反应？**  
A：检查 **EventSystem**、**Canvas** 上 **Graphic Raycaster**、按钮 **Interactable**、**ShopPanel** 是否激活。

**Q：`Setup` 有日志，但点 ± 始终没有 `OnMinus` / `OnPlus`？**  
A：见前文 **ScrollRect / 遮挡 / Navigation**；工程里已调用 **`ShopUIButtonUtil.FixForScrollView`**。

**Q：想和别的逻辑一起点按钮？**  
A：在 **On Click ()** 里**多加几条**即可（「加入购物车」勿与代码监听重复；± 勿与脚本重复）。

---

## 5. 代码侧摘要

- **`ShopUIController`**：**`Enable Shop Debug Logs`** 控制商品卡/购物车行调试输出；**`orderButton`** 仍用 Inspector 绑定 **`OnOrderClicked`**。
- **`ShopProductCardView`**：**不**对加入购物车 **`AddListener`**，仅 **`OnAddToCartButtonClicked`** 供 Inspector 调用。
- **`ShopCartLineView`**：对 **±** 代码 **`AddListener` / `RemoveListener`**。
