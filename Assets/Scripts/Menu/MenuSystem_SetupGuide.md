# 选菜系统 - Unity Editor 配置指南

---

## 一、创建 ScriptableObject 资产

### 1.1 创建 MenuSO 资产

1. 在 Project 窗口中进入 `Assets/SO/` 文件夹
2. 右键 -> Create -> Cooking -> Menu
3. 将资产命名为 `Menu`（最终路径：`Assets/SO/Menu.asset`）
4. 此资产**不需要**在 Inspector 中预填任何数据，运行时由选菜系统自动写入

---

## 二、创建预制体

### 2.1 MenuRecipeCard 预制体（菜谱卡片）

创建一个 UI 预制体，挂载 `MenuRecipeCardView` 脚本。

**推荐层级结构：**

```
MenuRecipeCard (GameObject)
├── CardButton (Button)              -- 整张卡片可点击，用于切换选中状态
│   ├── IconImage (Image)            -- 菜品图标
│   ├── NameText (TextMeshProUGUI)   -- 菜品名称
│   └── PriceText (TextMeshProUGUI)  -- 价格显示
├── SelectedHighlight (GameObject)   -- 选中时的高亮边框/遮罩（默认关闭）
└── TutorialButton (Button)          -- "制作方法" 按钮
```

**Inspector 配置 `MenuRecipeCardView`：**

| 字段 | 拖入对象 |
|------|---------|
| `nameText` | NameText (TextMeshProUGUI) |
| `iconImage` | IconImage (Image) |
| `priceText` | PriceText (TextMeshProUGUI) |
| `cardButton` | CardButton (Button) |
| `tutorialButton` | TutorialButton (Button) |
| `selectedHighlight` | SelectedHighlight (GameObject) |

**Button 事件绑定：**

- `CardButton` -> On Click -> 拖入自身 `MenuRecipeCardView` -> 选择 `OnCardClicked()`
- `TutorialButton` -> On Click -> 拖入自身 `MenuRecipeCardView` -> 选择 `OnTutorialClicked()`

**注意：** `SelectedHighlight` 默认应设为**未激活**（取消勾选 Inspector 顶部的 checkbox）。

---

### 2.2 MenuSelectedItem 预制体（右侧已选条目）

创建一个 UI 预制体，挂载 `MenuSelectedItemView` 脚本。

**推荐层级结构：**

```
MenuSelectedItem (GameObject)
├── IconImage (Image)                -- 菜品小图标
├── NameText (TextMeshProUGUI)       -- 菜品名称
└── RemoveButton (Button)            -- X 移除按钮
```

**Inspector 配置 `MenuSelectedItemView`：**

| 字段 | 拖入对象 |
|------|---------|
| `nameText` | NameText (TextMeshProUGUI) |
| `iconImage` | IconImage (Image) |
| `removeButton` | RemoveButton (Button) |

**Button 事件绑定：**

- `RemoveButton` -> On Click -> 拖入自身 `MenuSelectedItemView` -> 选择 `OnRemoveClicked()`

---

## 三、新建场景并搭建 UI

### 3.1 创建场景

1. File -> New Scene -> 选择 Basic 模板
2. Ctrl+S 保存到 `Assets/Scenes/MenuScene.unity`

### 3.2 添加到 Build Settings

1. File -> Build Settings
2. 点击 Add Open Scenes，将 `MenuScene` 加入
3. **拖拽调整顺序**，确保 `MenuScene` 在 `SampleScene` **之前**（索引更小）

```
0 : Scenes/MenuScene      <-- 游戏启动首先加载
1 : Scenes/SampleScene     <-- 游戏主场景
```

### 3.3 搭建 Canvas 层级

在 MenuScene 中创建以下 Hierarchy 结构：

```
MenuCanvas (Canvas)                              -- 挂载 MenuSceneController
├── CanvasScaler                                 -- UI Scale Mode: Scale With Screen Size
│                                                   Reference Resolution: 1920x1080
│                                                   Screen Match Mode: Match Width Or Height (0.5)
├── GraphicRaycaster
│
├── MainButtons (空 GameObject，居中布局)
│   ├── SelectMenuButton (Button)                -- "选菜" 按钮
│   ├── StartGameButton (Button)                 -- "开始游戏" 按钮
│   └── ErrorText (TextMeshProUGUI)              -- 错误提示文本（默认为空）
│
├── SelectionPanel (GameObject)                  -- 选菜界面根节点（默认关闭）
│   │                                               挂载 MenuSelectionUIController
│   ├── Background (Image)                       -- 半透明背景遮罩
│   │
│   ├── LeftPanel (空 GameObject)                -- 左侧：已解锁菜谱区域
│   │   ├── Title (TextMeshProUGUI)              -- 标题："已解锁菜谱"
│   │   └── RecipeCardContainer (空 GameObject)  -- 挂载 Grid Layout Group
│   │       │                                       Cell Size: 根据卡片大小调整（如 200x280）
│   │       │                                       Spacing: (10, 10)
│   │       │                                       Constraint: Flexible
│   │       └── (运行时动态生成 MenuRecipeCard)
│   │
│   ├── RightPanel (空 GameObject)               -- 右侧：已选菜谱区域
│   │   ├── Title (TextMeshProUGUI)              -- 标题："已选菜谱"
│   │   ├── SelectedItemContainer (空 GameObject)-- 挂载 Grid Layout Group
│   │   │   └── (运行时动态生成 MenuSelectedItem)
│   │   └── ConfirmButton (Button)               -- "确定" 按钮
│   │
│   └── CloseButton (Button)                     -- 右上角 "X" 关闭按钮
│
└── TutorialPanel (GameObject)                   -- 制作方法面板（默认关闭）
    │                                               挂载 RecipeTutorialPanel
    ├── PanelRoot (GameObject)                   -- panelRoot 指向此节点（默认关闭）
    │   ├── Background (Image)
    │   ├── Content (空 GameObject)              -- 教程内容（后续填充）
    │   └── CloseTutorialButton (Button)         -- 右上角 "X" 关闭
    └── (RecipeTutorialPanel 脚本挂在 TutorialPanel 上)
```

**提示：** 如果左侧或右侧菜谱数量可能很多，可以在 `LeftPanel` / `RightPanel` 下增加 `Scroll View`，将 `RecipeCardContainer` / `SelectedItemContainer` 放入 Scroll View 的 Content 中。

---

## 四、挂载脚本并配置 Inspector

### 4.1 MenuSceneController（挂在 MenuCanvas 上）

| 字段 | 拖入对象 |
|------|---------|
| `menuSO` | `Assets/SO/Menu.asset` |
| `selectMenuButton` | MainButtons/SelectMenuButton |
| `startGameButton` | MainButtons/StartGameButton |
| `errorText` | MainButtons/ErrorText |
| `menuSelectionUI` | SelectionPanel (上面的 MenuSelectionUIController) |
| `minRecipes` | `1`（至少选 1 道菜才能开始游戏） |
| `gameSceneName` | `SampleScene` |

**Button 事件绑定：**

- `SelectMenuButton` -> On Click -> 拖入 MenuCanvas -> 选择 `MenuSceneController.OnSelectMenuClicked()`
- `StartGameButton` -> On Click -> 拖入 MenuCanvas -> 选择 `MenuSceneController.OnStartGameClicked()`

---

### 4.2 MenuSelectionUIController（挂在 SelectionPanel 上）

| 字段 | 拖入对象 |
|------|---------|
| `menuSO` | `Assets/SO/Menu.asset`（同一个资产） |
| `recipeSources` | 列表大小设为 2：<br>Element 0 = `Assets/SO/recipe.asset`（FryRecipeDatabase）<br>Element 1 = `Assets/DirnkConfig.asset`（DrinkRecipeDatabase） |
| `selectionPanelRoot` | SelectionPanel 自身 |
| `recipeCardContainer` | LeftPanel/RecipeCardContainer |
| `recipeCardPrefab` | MenuRecipeCard 预制体 |
| `selectedItemContainer` | RightPanel/SelectedItemContainer |
| `selectedItemPrefab` | MenuSelectedItem 预制体 |
| `confirmButton` | RightPanel/ConfirmButton |
| `closeButton` | CloseButton |
| `tutorialPanel` | TutorialPanel (RecipeTutorialPanel) |

**Button 事件绑定：**

- `ConfirmButton` -> On Click -> 拖入 SelectionPanel -> 选择 `MenuSelectionUIController.OnConfirmClicked()`
- `CloseButton` -> On Click -> 拖入 SelectionPanel -> 选择 `MenuSelectionUIController.OnCloseClicked()`

**重要：recipeSources 槽位说明**

`recipeSources` 是一个 `List<ScriptableObject>` 列表。你需要把实现了 `IRecipeSource` 接口的 SO 资产拖进去：
- 当前项目中有 2 个：`FryRecipeDatabase` 和 `DrinkRecipeDatabase` 各一个
- 未来新增菜谱类型（如 BakeRecipeDatabase），只需让新 SO 实现 `IRecipeSource`，然后在此列表中增加一个槽位即可

> 注意：请确认你的 FryRecipeDatabase 资产路径。项目中 SO 资产位于 `Assets/SO/` 目录下，饮品资产可能在 `Assets/DirnkConfig.asset`（根目录）。请在 Project 窗口中搜索 `t:FryRecipeDatabase` 和 `t:DrinkRecipeDatabase` 确认。

---

### 4.3 RecipeTutorialPanel（挂在 TutorialPanel 上）

| 字段 | 拖入对象 |
|------|---------|
| `panelRoot` | TutorialPanel/PanelRoot |
| `closeButton` | PanelRoot/CloseTutorialButton |

**Button 事件绑定：**

- `CloseTutorialButton` -> On Click -> 拖入 TutorialPanel -> 选择 `RecipeTutorialPanel.OnCloseClicked()`

---

## 五、游戏场景 (SampleScene) 配置

### 5.1 OrderGenerator 新增字段

在 SampleScene 中找到挂载了 `OrderGenerator` 的 GameObject：

| 字段 | 拖入对象 |
|------|---------|
| `menuSO` | `Assets/SO/Menu.asset`（与 MenuScene 中使用**同一个**资产） |

其余字段（`recipeDatabase`、`drinkRecipeDatabase`、`gameConfig`）保持原样不动。

> MenuSO 作为 fallback 机制：如果 `menuSO` 为空或其中没有选择任何菜谱，OrderGenerator 会自动退回到原始的数据库逻辑。

---

## 六、默认状态检查清单

配置完成后，确认以下初始状态：

| 项目 | 初始状态 |
|------|---------|
| `SelectionPanel` | **未激活**（默认关闭，点击"选菜"按钮后打开） |
| `TutorialPanel/PanelRoot` | **未激活**（默认关闭，点击"制作方法"后打开） |
| `MenuRecipeCard` 预制体中的 `SelectedHighlight` | **未激活**（运行时根据选中状态动态开关） |
| `ErrorText` | 文本内容为**空字符串** |
| `Menu.asset` (MenuSO) | `selectedRecipes` 列表为**空**（不要预填数据） |

---

## 七、运行流程验证

1. 运行 MenuScene
2. 点击"选菜"按钮 -> SelectionPanel 显示
3. 左侧 Grid 应列出所有 `unlocked = true` 的菜谱（来自 FryRecipeDatabase + DrinkRecipeDatabase）
4. 点击卡片 -> 卡片高亮 + 右侧出现已选条目
5. 再次点击同一卡片 -> 取消选中 + 右侧移除
6. 右侧点击 X 按钮 -> 同样取消选中
7. 点击"确定" -> 面板关闭，`Menu.asset` 的 `selectedRecipes` 被写入
8. 点击"开始游戏" -> 如果已选菜谱 >= `minRecipes`，加载 SampleScene
9. 在 SampleScene 中，顾客只会点 MenuSO 中选择的菜品

---

## 八、快速查找资产路径

| 资产 | 搜索方式 |
|------|---------|
| FryRecipeDatabase 资产 | Project 窗口搜索 `t:FryRecipeDatabase` |
| DrinkRecipeDatabase 资产 | Project 窗口搜索 `t:DrinkRecipeDatabase` |
| MenuSO 资产 | 右键 `Assets/SO/` -> Create -> Cooking -> Menu |
| 所有脚本 | 位于 `Assets/Scripts/Menu/` 目录下 |
