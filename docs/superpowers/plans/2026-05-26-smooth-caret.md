# 平滑光标（Smooth Caret）实现计划

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让字帖模式 / 临摹模式 / 普通跟打模式 的光标和滚动从"瞬时跳"改为"平滑过渡"，参考 monkeytype 的多通道动画方案，提升打字时视觉跟随感。

**参考实现:** `E:/nas同步/打字/monkeytype/frontend/src/ts/elements/caret.ts` 与 `frontend/src/ts/test/test-ui.ts:941` `scrollTape()`。

**Tech Stack:** WPF, C#, `System.Windows.Media.Animation`, PowerShell 冒烟测试。

---

## 现状摘要

| 模式 | 光标元素 | 当前定位 | 当前滚动 |
|---|---|---|---|
| 普通跟打（MainWindow） | 无独立光标，背景色 + `TbxInput` 原生光标 | — | `ScrollToVerticalOffset` 瞬时（阈值 `0.8*行高`） |
| 字帖（CopybookMode） | `Border _cursor`（Canvas overlay） | `Canvas.SetLeft/SetTop` 瞬时（`UpdatePosition()`，CopybookMode.cs:1147） | 同上 |
| 临摹（TracingMode） | `Border _cursor`（定位到镜像行） | `Canvas.SetLeft/SetTop` 瞬时（TracingMode.cs:1045） | 同上，且滚动后二次 `UpdatePosition` |
| 练单（WinTrainer） | 纯展示窗口 `fld`，无打字交互 | — | — |

闪烁动画：字帖 / 临摹都用 `DoubleAnimation(1→0, 500ms, AutoReverse, Forever)` on `OpacityProperty`。

---

## 设计原则

1. **monkeytype 的 3 通道** 在 WPF 里简化为 2 通道：
   - **位置通道**：`Canvas.Left/Top` 上的 `DoubleAnimation`（取代 `Canvas.SetLeft/SetTop`）
   - **滚动通道**：`ScrollViewer.VerticalOffset` 上的 `DoubleAnimation`（取代 `ScrollToVerticalOffset`）
   - 无需 monkeytype 的横向滚动通道（TypeSunny 显示区强制换行）。
2. **新动画必须取消旧动画**：每次击键前 `_cursor.BeginAnimation(prop, null)` 清掉未结束的旧动画，否则连打时会越积越歪。
3. **可配置速度**：参考 monkeytype 的 `off / slow / medium / fast`，对应时长 `0 / 150 / 100 / 85` ms。默认 `medium`。
4. **页面切换、Reset、首次定位** 不走动画（瞬时 `SetPosition`），避免大跨度滑行。
5. **光标动画时长与滚动动画时长一致** 都用 125ms（monkeytype 也是这个值），缓动 `CubicEase{InOut}`，多通道视觉同步。
6. **背景色 vs 光标的时序**：背景色（已打字符）保留瞬时刷新；光标动画跟在后面"追"过去，符合直觉。
7. **Canvas overlay 不跟随 ScrollViewer 滚动** 是 WPF 的固有问题，目前靠 `ScDisplay.ScrollChanged → UpdatePosition` 每次重算。改造时保留这一逻辑，但确保滚动动画期间也每帧触发（`CompositionTarget.Rendering`）。

---

## Chunk 1: 配置与默认值

### Task 1: 加配置项

**Files:**
- Modify: `Config/Config.cs`
- Modify: `WinConfig/WinConfig.xaml.cs`
- Test: `Tests/ScorePanelPresentationTests.ps1` 或新建 `Tests/SmoothCaretConfigTests.ps1`

- [ ] 加失败测试：`显示` 分类应包含 `平滑光标` 配置项。
- [ ] 加失败测试：默认值应为 `medium`。
- [ ] 在 `Config.cs` 添加键 `平滑光标`，类型为下拉枚举（`关闭 / 慢 / 中 / 快`），默认 `中`。
- [ ] 在 `WinConfig` 注册到 `显示` 分类。
- [ ] 加失败测试：`显示` 分类应包含 `平滑滚动` 配置项，默认 `是`（与光标分离，可独立开关——monkeytype 也是分开的 `smoothCaret` 和 `smoothLineScroll`）。
- [ ] 添加键 `平滑滚动`，默认 `是`。
- [ ] 运行测试通过。

---

## Chunk 2: 抽取统一的 SmoothCaret 类

### Task 2: 新增 `UI/SmoothCaret.cs`

**Files:**
- Create: `UI/SmoothCaret.cs`
- Test: `Tests/SmoothCaretTests.cs`（新建，编译期单元测试）

类设计参考 `monkeytype/frontend/src/ts/elements/caret.ts:31` 的 `Caret` 类。

- [ ] 加单元测试：构造一个 `SmoothCaret`，调用 `AnimatePosition(100, 200)`，验证最终 `Canvas.Left == 100 && Canvas.Top == 200`（用 `Storyboard.SkipToFill()` 或调整 `Duration=0` 跳过动画）。
- [ ] 加单元测试：连续两次 `AnimatePosition`，第二次必须取消第一次（验证旧动画被 `null` 替换）。
- [ ] 加单元测试：`SetPosition(x, y)` 瞬时生效，不发起动画。
- [ ] 加单元测试：`StopBlinking()` 后 `Opacity == 1`。
- [ ] 实现 `SmoothCaret` 类，字段：
  - `Border Element` — 光标外观
  - `SmoothCaretSpeed Speed` — 枚举，从 `Config` 读取
  - 内部记录当前正在跑的位置动画（用于取消）
- [ ] 实现 `AnimatePosition(double x, double y, double? height = null)`：
  - 速度 `off` → 走 `SetPosition`
  - 时长 = 速度档位映射（slow=150, medium=100, fast=85）
  - 用 `CubicEase{EasingMode=EaseInOut}`
  - 用 attached property 动画 `Canvas.LeftProperty` / `Canvas.TopProperty`
  - 调用前先 `Element.BeginAnimation(Canvas.LeftProperty, null)` 清掉旧动画
  - 必要时同步动画 `HeightProperty`（换行时字号可能变）
- [ ] 实现 `SetPosition(double x, double y)`：清动画 + 直接赋值。
- [ ] 实现 `StartBlinking() / StopBlinking() / UpdateBlinkingAnimation()`（迁移 CopybookMode.cs:129-132 的逻辑，集中到这里）。
- [ ] 实现 `ApplyForeground(Brush)`、`Show()`、`Hide()`。
- [ ] 编译期测试通过。

### Task 3: 字帖模式接入 SmoothCaret

**Files:**
- Modify: `UI/Modes/CopybookMode.cs`

- [ ] 加冒烟测试：字帖模式打字时光标位置变化应触发动画（验证 `Element.GetAnimationBaseValue` ≠ 当前值）。Tests/CopybookSmoothCaretTests.ps1。
- [ ] 替换 `_cursor = new Border()` 与闪烁逻辑为 `_cursor = new SmoothCaret(...)`。
- [ ] `UpdatePosition()`（CopybookMode.cs:1147）：把 `Canvas.SetLeft/SetTop(_cursor, ...)` 改为 `_cursor.AnimatePosition(x - 2, y + padTop, lineHeight)`。
- [ ] `ScheduleFinalVisualsAndStop()`（CopybookMode.cs:1107）：结束时光标定位用 `SetPosition`（瞬时，不要滑过去）。
- [ ] `Reset()`（CopybookMode.cs:251）和 `Enable()` 首次定位也用 `SetPosition`。
- [ ] `OnDisplayScrollChanged`：滚动期间继续走 `SetPosition`（避免动画叠加滚动补偿造成抖动）。详见 Chunk 4。
- [ ] 冒烟测试通过。

### Task 4: 临摹模式接入 SmoothCaret

**Files:**
- Modify: `UI/Modes/TracingMode.cs`

- [ ] 加冒烟测试：临摹模式光标动画。Tests/TracingSmoothCaretTests.ps1。
- [ ] 同 Task 3 的替换，但定位参考是 `_mirrorBlocks[_currentIndex]`（TracingMode.cs:1054）不变。
- [ ] **额外注意**：临摹模式 `ScrollToCurrentChar()`（TracingMode.cs:1112）会用 `BeginInvoke(Render)` 二次 `UpdatePosition`——这是滚动后的坐标修正。改为 `SetPosition` 调用（不要二次动画），避免和首次动画叠加。
- [ ] 冒烟测试通过。

---

## Chunk 3: 平滑滚动

### Task 5: ScrollViewer 平滑动画

**Files:**
- Create: `Utils/SmoothScrollHelper.cs`
- Modify: `UI/MainWindow.xaml.cs`（`SmoothScrollTo`，约第 926 行）
- Test: `Tests/SmoothScrollTests.cs`

WPF 的 `ScrollViewer.VerticalOffset` 是只读依赖属性，不能直接 `BeginAnimation`。两种实现：

**方案 A（推荐）**：自定义 attached property，setter 内部调用 `ScrollToVerticalOffset`，让 `DoubleAnimation` 动画这个 attached property。

**方案 B**：`CompositionTarget.Rendering` 每帧插值。简单但耦合到全局事件，不推荐。

- [ ] 加单元测试：`SmoothScrollHelper.AnimateScrollTo(scrollViewer, 100, 125ms)`，等动画结束后 `VerticalOffset == 100`。
- [ ] 加单元测试：连续两次调用，第二次取消第一次。
- [ ] 实现 attached property `VerticalOffsetProperty`，setter 内 `((ScrollViewer)d).ScrollToVerticalOffset((double)e.NewValue)`。
- [ ] 实现 `AnimateScrollTo(ScrollViewer sv, double target, int durationMs, EasingFunctionBase ease)`：
  - 先 `sv.BeginAnimation(VerticalOffsetProperty, null)` 清旧动画
  - 起 `DoubleAnimation(from=sv.VerticalOffset, to=target, duration)`
  - `Config["平滑滚动"] == "否"` → 直接 `ScrollToVerticalOffset`
- [ ] 修改 `MainWindow.SmoothScrollTo`（MainWindow.xaml.cs:926）：把内部的 `ScrollToVerticalOffset` 换成 `SmoothScrollHelper.AnimateScrollTo`，时长 125ms，缓动 `CubicEase{InOut}`，并保留 `forceScroll` 与阈值判定。
- [ ] 验证字帖（CopybookMode.cs:1216 `ScrollToCurrentChar`）、临摹（TracingMode.cs:1112）调用该方法后也变成平滑。
- [ ] 单元测试通过。

### Task 6: 滚动动画期间同步光标坐标

**Files:**
- Modify: `UI/Modes/CopybookMode.cs`、`UI/Modes/TracingMode.cs`

Canvas overlay 不在 ScrollViewer 内，滚动时光标的 Canvas 坐标需要每帧重算。

- [ ] 加冒烟测试：滚动动画期间，光标的 `Canvas.Top` 应随 `ScrollViewer.VerticalOffset` 变化（采样 3 个时间点）。Tests/SmoothCaretScrollSyncTests.ps1。
- [ ] 在字帖 / 临摹的 `Enable()` 中订阅 `CompositionTarget.Rendering`，但**仅在滚动动画进行中**（用一个 `_isScrollAnimating` 标志）。
- [ ] `AnimateScrollTo` 开始时设标志 = true，`Completed` 回调清零。
- [ ] 渲染回调内：如果标志为 true，调 `_cursor.SetPosition(...)`（瞬时，跟随每一帧的滚动位置）。
- [ ] `Disable()` 中取消订阅。
- [ ] 冒烟测试通过。

**或者更优雅**：把 `_overlay` Canvas 放进 ScrollViewer 内，作为 `TbDispay` 的兄弟节点。这样滚动天然同步。但要重排 XAML 结构，风险较大——**先用方案 A 做完，结构调整列为后续优化**。

---

## Chunk 4: 普通跟打模式可见光标（可选/二期）

普通模式当前没有独立光标，靠背景色暗示位置。是否要加可见的"待打位置"光标参考 monkeytype，由用户决定。

### Task 7: 普通模式加 Canvas overlay 与光标

**Files:**
- Modify: `UI/MainWindow.xaml`（在 `BdDisplay` 的 Grid 内加 Canvas）
- Modify: `UI/MainWindow.xaml.cs`
- Modify: `Config/Config.cs`（加配置开关 `显示当前位置光标`，默认 `否`，避免侵入老用户体验）

- [ ] 加配置项 `显示当前位置光标` 与对应失败测试。
- [ ] 加冒烟测试：开启该配置后，主显示区出现 `Border` 元素且随击键动画移动。Tests/MainModeSmoothCaretTests.ps1。
- [ ] XAML：在 `BdDisplay` 的 `Grid` 内（`ScDisplay` 旁边）加 `Canvas x:Name="CvCaret"`，`IsHitTestVisible=False`，`Panel.ZIndex=8`。
- [ ] 创建 `SmoothCaret`，添加到 `CvCaret`。
- [ ] 在 `CalcResultsAfterTextChanged`（MainWindow.xaml.cs 约 770 行附近）算完 `nextToType` 后：
  - 若 `_copybookMode/_tracingMode` 激活则跳过（它们自己处理）
  - 否则定位到 `TextInfo.Blocks[nextToType - PageStartIndex]`，调 `AnimatePosition`
- [ ] 滚动同步：参考 Task 6 的标志位方案。
- [ ] 换页时 `SetPosition` 瞬时。
- [ ] 冒烟测试通过。

---

## Chunk 5: 收尾

### Task 8: 配置变更实时生效

**Files:**
- Modify: `UI/SmoothCaret.cs`
- Modify: `UI/MainWindow.xaml.cs`（配置变更回调）

- [ ] 加冒烟测试：改 `平滑光标` 设置后，已存在的光标实例速度立即更新。
- [ ] `SmoothCaret` 提供 `RefreshSpeedFromConfig()`，从 `Config` 重读。
- [ ] 在 `WinConfig` 的 `Apply` 回调里调用所有活动 caret 的 `RefreshSpeedFromConfig`。
- [ ] 同样地，`平滑滚动` 切换为"否"时立即生效（下次滚动用 `ScrollToVerticalOffset`）。
- [ ] 冒烟测试通过。

### Task 9: 性能与边界

**Files:**
- Tests: `Tests/SmoothCaretPerformanceTests.ps1`（可选）

- [ ] 高频连打（≥10 字/秒）时不能积压动画——验证连续 50 次 `AnimatePosition` 后 `Element.GetCurrentValue` 与最终目标一致。
- [ ] 字号 Ctrl+滚轮改变时光标 `Height` 立即跟新（已在 CopybookMode.cs:1135 处理，验证不被动画破坏）。
- [ ] 隐藏窗口（最小化）期间不消耗动画 CPU——`CompositionTarget.Rendering` 仅在可见 + 滚动中订阅。
- [ ] IME 候选框定位仍然准确（`_inputCapture` 的 `Canvas.SetLeft/SetTop` 保持瞬时，**不要**走光标动画通道）。
- [ ] 冒烟通过。

### Task 10: 文档与 changelog

**Files:**
- Modify: `Version/version.txt`
- Modify: 主页 `README.md`（如有功能列表）

- [ ] changelog 加一条：`feat: 平滑光标与平滑滚动（默认开启，设置-显示中可调档/关闭）`。
- [ ] README 提到新功能。

---

## 验收清单

- [ ] 字帖模式：击键时光标视觉滑动 ~100ms，换行时光标和文字同步上滑。
- [ ] 临摹模式：同上，且光标停在镜像行而非原文行（不变）。
- [ ] 普通模式：滚动平滑（无背景色和滚动错位）；如开启位置光标，跟着击键平滑移动。
- [ ] `平滑光标=关闭` 时立刻恢复瞬时跳。
- [ ] `平滑滚动=否` 时立刻恢复瞬时滚动。
- [ ] 连打 / IME / 换页 / Reset 无残影、无积压、无错位。
- [ ] WinTrainer 不受影响。

---

## 文件清单

新增：
- `UI/SmoothCaret.cs`
- `Utils/SmoothScrollHelper.cs`
- `Tests/SmoothCaretTests.cs`
- `Tests/SmoothScrollTests.cs`
- `Tests/CopybookSmoothCaretTests.ps1`
- `Tests/TracingSmoothCaretTests.ps1`
- `Tests/SmoothCaretScrollSyncTests.ps1`
- `Tests/MainModeSmoothCaretTests.ps1`（Task 7 启用时）

修改：
- `Config/Config.cs`
- `WinConfig/WinConfig.xaml.cs`
- `UI/MainWindow.xaml`（仅 Task 7 启用时）
- `UI/MainWindow.xaml.cs`
- `UI/Modes/CopybookMode.cs`
- `UI/Modes/TracingMode.cs`
- `Version/version.txt`
