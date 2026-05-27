# 平滑光标（Smooth Caret）实现计划

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development only when subagents are available **and the user has authorized delegation**; otherwise use superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax for tracking.

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
   - **位置通道**：`Canvas.Left/Top` 保存基准位置，`TranslateTransform.X/Y` 承载视觉位移动画
   - **滚动通道**：自定义 attached property 驱动 `ScrollViewer.ScrollToVerticalOffset` 的 `DoubleAnimation`
   - 无需 monkeytype 的横向滚动通道（TypeSunny 显示区强制换行）。
2. **新动画必须取消旧动画**：每次击键前取消旧动画，并先固定当前视觉值，避免连打时回跳或积压。
3. **动态速度优先，设置语义拆开**：`平滑光标` 只做开关；`光标动画模式` 为 `动态 / 固定`；固定模式读取 `固定动画时长`，动态模式根据最近输入间隔在 `连打 / 正常输入 / 停顿后` 三个用户配置时长之间映射并做平滑，避免连打时动画积压或慢打时过短。
4. **页面切换、Reset、首次定位** 不走动画（瞬时 `SetPosition`），避免大跨度滑行。
5. **光标与滚动时长分离**：光标按开关、动态/固定模式和对应毫秒值控制；滚动固定配置在 `SmoothScrollTo`。monkeytype 实际也是光标 `smoothCaret` 与行滚动 `smoothLineScroll` 独立控制。
6. **背景色跟随输入节奏淡入**：打对/打错背景不再瞬时闪变，而是使用与当前光标相同的动态时长淡入；动画只作用在背景刷子的 opacity，不能让文字本身变透明。
7. **Canvas overlay 不跟随 ScrollViewer 滚动** 是 WPF 的固有问题，目前靠 `ScDisplay.ScrollChanged → UpdatePosition` 每次重算。改造时保留这一逻辑，但把定位入口拆成 `UpdatePosition(animated)`：击键可动画，滚动/Reset/首次/结束必须瞬时。
8. **WPF 取消动画前先固定当前视觉值**：每次启动新动画前先读取当前值，清动画，再把当前视觉值写回 base value，最后从当前值动画到目标值，避免 `BeginAnimation(prop, null)` 后回跳到旧 base value。
9. **光标移动用 RenderTransform**：`Canvas.Left/Top` 只作为基准位置，视觉移动走 `TranslateTransform.X/Y`，避免每帧触发布局；滚动同步期间用 `TrackPosition` 更新基准坐标，不清掉正在进行的击键动画。

---

## Chunk 1: 配置与默认值

### Task 1: 加配置项

**Files:**
- Modify: `Config/Config.cs`
- Modify: `WinConfig/WinConfig.xaml.cs`
- Test: 新建 `Tests/SmoothCaretConfigTests.ps1`

- [x] 加失败测试：`跟打` 分类的 `字帖模式` 下应包含 `平滑光标` 配置项。
- [x] 加失败测试：默认值应为 `动态`。
- [x] 在 `Config.cs` 添加键：`平滑光标=是`、`平滑光标模式=动态`、`平滑光标固定时长=200`，并添加动态模式三锚点 `平滑光标快/中/慢=140/200/280` ms。
- [x] 在 `WinConfig` 注册到 `跟打` 分类的 `字帖模式` 子项。
- [x] 在 `WinConfig.CreateValueControl` 中让 `平滑光标` 走开关控件，为 `平滑光标模式` 创建 `动态 / 固定` 下拉框，保存中文值。
- [x] 加失败测试：应包含 `平滑换行` 配置项，默认 `是`（与光标分离，可独立开关；monkeytype 也分开 `smoothCaret` 和 `smoothLineScroll`，但 monkeytype 的 `smoothLineScroll` 默认是 false，TypeSunny 默认开启是本产品决策）。
- [x] 添加键 `平滑换行`，默认 `是`；旧配置键 `平滑滚动` 自动迁移到 `平滑换行`。
- [x] 运行测试通过。

---

## Chunk 2: 抽取统一的 SmoothCaret 类

### Task 2: 新增 `UI/SmoothCaret.cs`

**Files:**
- Create: `UI/SmoothCaret.cs`
- Test: `Tests/SmoothCaretTests.cs` + `Tests/SmoothCaretTests.ps1`（新建，STA/WPF 编译期冒烟测试）

类设计参考 `monkeytype/frontend/src/ts/elements/caret.ts:31` 的 `Caret` 类。

- [x] 加测试：速度映射 `关闭/慢/中/快` 对应用户配置的 `0/慢/中/快` ms，默认 `0/280/200/140` ms。
- [x] 加测试：动态时长能随输入间隔变化，并用平滑权重避免在快慢输入间突变。
- [x] 加 STA 冒烟测试：构造一个 `SmoothCaret`，调用 `AnimatePosition(100, 200)`，验证最终 `Canvas.Left == 100 && Canvas.Top == 200`。
- [x] 加 STA 冒烟测试：连续两次 `AnimatePosition`，第二次必须从当前视觉值继续到新目标，不回跳到旧 base value。
- [x] 加 STA 冒烟测试：`AnimatePosition` 使用 `TranslateTransform` 承载视觉移动，`Canvas.Left/Top` 固化到目标基准。
- [x] 加 STA 冒烟测试：`TrackPosition` 更新基准位置时保留正在进行的光标动画，滚动同步不打断击键移动。
- [x] 加单元测试：`SetPosition(x, y)` 瞬时生效。
- [x] 加单元测试：`StopBlinking()` 后 `Opacity == 1`。
- [x] 实现 `SmoothCaret` 类，字段：
  - `Border Element` — 光标外观
  - `SmoothMotionTiming` — 固定毫秒值与动态输入节奏估算
  - 从 `Config` 读取当前速度（每次动画前刷新，配置变更下次击键立即生效）
- [x] 实现 `AnimatePosition(double x, double y, double? height = null)`：
  - 速度 `off` → 走 `SetPosition`
  - 时长 = 速度档位映射（默认 slow=280, medium=200, fast=140，可在设置页调整）
  - 用 `CubicEase{EasingMode=EaseInOut}`
  - 用 `TranslateTransform.X/Y` 做视觉移动，`Canvas.Left/Top` 只保存目标基准位置
  - 调用前先读取当前视觉值，`BeginAnimation(..., null)` 清旧动画，再把当前视觉值写回 base value
  - 必要时同步动画 `HeightProperty`（换行时字号可能变）
- [x] 实现 `SetPosition(double x, double y)`：清动画 + 直接赋值。
- [x] 实现 `TrackPosition(double x, double y)`：更新基准位置但保留 transform 上的当前视觉偏移。
- [x] 实现 `StartBlinking() / StopBlinking() / UpdateBlinkingAnimation()`（迁移 CopybookMode.cs:129-132 的逻辑，集中到这里；`StopBlinking()` 必须先清 `OpacityProperty` 动画再设 `Opacity = 1`）。
- [x] 实现 `ApplyForeground(Brush)`、`Show()`、`Hide()`。
- [x] 编译期测试通过。

### Task 3: 字帖模式接入 SmoothCaret

**Files:**
- Modify: `UI/Modes/CopybookMode.cs`

- [x] 加冒烟测试：字帖模式打字时光标位置变化应触发动画（结构验证 `UpdatePosition(true)` 走 `AnimatePosition`，滚动/首次/结束走 `SetPosition`）。Tests/SmoothCaretImplementationTests.ps1。
- [x] 替换 `_cursor = new Border()` 与闪烁逻辑为 `_cursor = new SmoothCaret(...)`。
- [x] `UpdatePosition()`（CopybookMode.cs:1147）改为 `UpdatePosition(bool animated = false)`；animated=true 时 `_cursor.AnimatePosition(x - 2, y + padTop, lineHeight)`，否则 `_cursor.SetPosition(..., lineHeight)`。
- [x] `ScheduleFinalVisualsAndStop()`（CopybookMode.cs:1107）：结束时光标定位用 `SetPosition`（瞬时，不要滑过去）。
- [x] `Reset()`（CopybookMode.cs:251）和 `Enable()` 首次定位也用 `SetPosition`。
- [x] `OnDisplayScrollChanged`：滚动期间继续走 `SetPosition`（避免动画叠加滚动补偿造成抖动）。详见 Chunk 3 Task 6。
- [x] 冒烟测试通过。

### Task 4: 临摹模式接入 SmoothCaret

**Files:**
- Modify: `UI/Modes/TracingMode.cs`

- [x] 加冒烟测试：临摹模式光标动画。Tests/SmoothCaretImplementationTests.ps1。
- [x] 同 Task 3 的替换，但定位参考是 `_mirrorBlocks[_currentIndex]`（TracingMode.cs:1054）不变。
- [x] **额外注意**：临摹模式 `ScrollToCurrentChar()`（TracingMode.cs:1112）会用 `BeginInvoke(Render)` 二次 `UpdatePosition`——这是滚动后的坐标修正。改为 `UpdatePosition(false)` 调用（不要二次动画），避免和首次动画叠加。
- [x] 冒烟测试通过。

---

## Chunk 3: 平滑换行

### Task 5: ScrollViewer 平滑动画

**Files:**
- Create: `Utils/SmoothScrollHelper.cs`
- Modify: `UI/MainWindow.xaml.cs`（`SmoothScrollTo`，约第 926 行）
- Test: `Tests/SmoothScrollTests.cs` + `Tests/SmoothScrollTests.ps1`（新建，STA/WPF 编译期冒烟测试）

WPF 的 `ScrollViewer.VerticalOffset` 是只读依赖属性，不能直接 `BeginAnimation`。两种实现：

**方案 A（推荐）**：自定义 attached property，setter 内部调用 `ScrollToVerticalOffset`，让 `DoubleAnimation` 动画这个 attached property。

**方案 B**：`CompositionTarget.Rendering` 每帧插值。简单但耦合到全局事件，不推荐。

- [x] 加测试：`平滑换行=否` 时 helper 直接调用瞬时滚动路径。
- [x] 加 STA 冒烟测试：`SmoothScrollHelper.AnimateScrollTo(scrollViewer, 100, 125ms)`，等动画结束后 `VerticalOffset == 100`。
- [x] 加 STA 冒烟测试：连续两次调用，第二次从当前 offset 继续到新目标，不回跳旧 base value。
- [x] 实现 attached property `VerticalOffsetProperty`，setter 内 `((ScrollViewer)d).ScrollToVerticalOffset((double)e.NewValue)`。
- [x] 实现 `AnimateScrollTo(ScrollViewer sv, double target, int durationMs, EasingFunctionBase ease, Action started = null, Action completed = null)`：
  - 先读取当前 offset，`sv.BeginAnimation(VerticalOffsetProperty, null)` 清旧动画，把当前 offset 写回 attached base value
  - 起 `DoubleAnimation(from=sv.VerticalOffset, to=target, duration)`
  - `Config["平滑换行"] == "否"` → 直接 `ScrollToVerticalOffset`
- [x] 修改 `MainWindow.SmoothScrollTo`（MainWindow.xaml.cs:926）：把内部的 `ScrollToVerticalOffset` 换成 `SmoothScrollHelper.AnimateScrollTo`，时长 125ms，缓动 `CubicEase{InOut}`，并保留 `forceScroll` 与阈值判定。
- [x] `SmoothScrollTo` 返回 `bool`，表示本次是否实际发起/执行滚动；没有超过阈值时返回 false，便于模式层决定是否开启滚动同步。滚动时长调整为 150ms，缓动使用 `QuadraticEase{EaseOut}` 降低急停顿感。
- [x] 验证字帖（CopybookMode.cs:1216 `ScrollToCurrentChar`）、临摹（TracingMode.cs:1112）调用该方法后也变成平滑。
- [x] 单元测试通过。

### Task 6: 滚动动画期间同步光标坐标

**Files:**
- Modify: `UI/Modes/CopybookMode.cs`、`UI/Modes/TracingMode.cs`

Canvas overlay 不在 ScrollViewer 内，滚动时光标的 Canvas 坐标需要每帧重算。

- [x] 加冒烟测试：滚动动画期间，光标同步路径存在。Tests/SmoothCaretImplementationTests.ps1。
- [x] 字帖 / 临摹新增 `_isScrollAnimating` 标志和 `OnRenderingDuringScroll` 回调。
- [x] `ScrollToCurrentChar()` 调用 `_main.SmoothScrollTo(targetOffset, started: StartScrollSync, completed: StopScrollSync)`；只有实际滚动时滚动同步才会启动。
- [x] `StartScrollSync` 订阅 `CompositionTarget.Rendering` 并设标志；`StopScrollSync` 清标志、取消订阅并最终 `UpdatePosition(false)`。
- [x] 渲染回调内：如果标志为 true，调 `UpdatePosition(false)`；内部使用 `TrackPosition` 跟随每一帧滚动位置，不清掉正在进行的光标 transform 动画。
- [x] `Disable()` 中取消订阅，避免窗口隐藏/模式切换后继续跑。
- [x] 冒烟测试通过。

**或者更优雅**：把 `_overlay` Canvas 放进 ScrollViewer 内，作为显示区内容的兄弟节点。这样滚动天然同步。但要重排 XAML 结构，风险较大——**先用方案 A 做完，结构调整列为后续优化**。

---

## Chunk 4: 动态速度与背景色平滑

### Task 7: 输入节奏驱动光标时长

**Files:**
- Create: `UI/SmoothMotionTiming.cs`
- Modify: `UI/SmoothCaret.cs`
- Modify: `UI/Modes/CopybookMode.cs`
- Modify: `UI/Modes/TracingMode.cs`
- Test: `Tests/SmoothCaretTests.cs`

- [x] 加测试：快输入间隔映射到更短动画，普通节奏落在中档附近，慢输入间隔映射到更长动画，并限制在用户配置的快/慢区间。
- [x] 加测试：快慢输入切换时使用平滑权重，避免每个字符的动画时长突变。
- [x] `平滑光标=否` 时关闭；`平滑光标模式=动态` 或缺省时走动态三锚点；`平滑光标模式=固定` 时走 `平滑光标固定时长`。
- [x] 字帖 / 临摹在每次真实输入事件后调用 `_cursor?.RecordInput()`，不在每个 keydown 或滚动事件里记录。
- [x] 背景动画时长从同一个 `SmoothMotionTiming` 读取，与当前光标动画时长保持一致。
- [x] 测试通过。

### Task 8: 打对/打错背景色淡入

**Files:**
- Create: `UI/SmoothBackground.cs`
- Modify: `UI/MainWindow.xaml.cs`
- Test: `Tests/SmoothBackgroundTests.cs` + `Tests/SmoothBackgroundTests.ps1`

- [x] 加 STA 冒烟测试：背景色立即设置目标颜色，但元素 `Opacity` 保持 `1`，避免文字一起淡入。
- [x] 加 STA 冒烟测试：半透明背景刷子淡入后保留目标透明度，不被强制变成不透明。
- [x] 加 STA 冒烟测试：清空背景不会留下元素透明度或旧动画。
- [x] `SmoothBackground.Apply(...)` 对 `TextBlock` / `Border` / `Control` 统一处理背景。
- [x] 对 `SolidColorBrush` 创建新的可动画 brush，避免修改 `Brushes.Red` 这类 frozen/shared brush。
- [x] 非 `SolidColorBrush` 背景直接设置，不做文字 opacity 降级方案。
- [x] `MainWindow.SetDisplayBlockStateBackground(...)` 和代码显示 overlay 背景统一走 `SmoothBackground`。
- [x] 测试通过。

---

## Chunk 5: 普通跟打模式可见光标（可选/二期）

普通模式当前没有独立光标，靠背景色暗示位置。是否要加可见的"待打位置"光标参考 monkeytype，由用户决定。

### Task 9: 普通模式加 Canvas overlay 与光标

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

## Chunk 6: 收尾

### Task 10: 配置变更实时生效

**Files:**
- Modify: `UI/SmoothCaret.cs`
- Modify: `UI/MainWindow.xaml.cs`（配置变更回调）

- [x] 加冒烟测试：改 `平滑光标` 设置后，已存在的光标实例速度下次动画立即更新。
- [x] `SmoothCaret` 提供 `RefreshSpeedFromConfig()`，从 `Config` 重读。
- [x] `SmoothCaret` 每次 `AnimatePosition` 都从 `Config` 读取速度；主题/模式刷新时调用 `UpdateBlinkingAnimation()`。
- [x] 同样地，`平滑换行` 切换为"否"时立即生效（下次滚动用 `ScrollToVerticalOffset`）。
- [x] 冒烟测试通过。

### Task 11: 性能与边界

**Files:**
- Tests: `Tests/SmoothCaretPerformanceTests.ps1`（可选）

- [ ] 高频连打（≥10 字/秒）时不能积压动画——验证连续 50 次 `AnimatePosition` 后 `Element.GetCurrentValue` 与最终目标一致。
- [ ] 字号 Ctrl+滚轮改变时光标 `Height` 立即跟新（已在 CopybookMode.cs:1135 处理，验证不被动画破坏）。
- [ ] 隐藏窗口（最小化）期间不消耗动画 CPU——`CompositionTarget.Rendering` 仅在可见 + 滚动中订阅。
- [ ] IME 候选框定位仍然准确（`_inputCapture` 的 `Canvas.SetLeft/SetTop` 保持瞬时，**不要**走光标动画通道）。
- [ ] 冒烟通过。

### Task 12: 文档与 changelog

**Files:**
- Modify: `Version/version.txt`
- Modify: 主页 `README.md`（如有功能列表）

- [ ] changelog 加一条：`feat: 字帖/临摹平滑光标与通用平滑换行（默认开启，设置-跟打可调档/毫秒值/关闭）`。
- [ ] README 提到新功能。

---

## 验收清单

- [ ] 字帖模式：击键时光标视觉滑动按当前输入节奏动态调整，换行时光标和文字同步上滑。
- [ ] 临摹模式：同上，且光标停在镜像行而非原文行（不变）。
- [ ] 普通模式：滚动平滑，打对/打错背景色淡入但文字不闪；如开启位置光标，跟着击键平滑移动。
- [ ] `平滑光标=否` 时立刻恢复瞬时跳。
- [ ] `平滑换行=否` 时立刻恢复瞬时滚动。
- [ ] 连打 / IME / 换页 / Reset 无残影、无积压、无错位。
- [ ] WinTrainer 不受影响。

---

## 文件清单

新增：
- `UI/SmoothCaret.cs`
- `UI/SmoothMotionTiming.cs`
- `UI/SmoothBackground.cs`
- `Utils/SmoothScrollHelper.cs`
- `Tests/SmoothBackgroundTests.cs`
- `Tests/SmoothBackgroundTests.ps1`
- `Tests/SmoothCaretTests.cs`
- `Tests/SmoothScrollTests.cs`
- `Tests/SmoothCaretConfigTests.ps1`
- `Tests/SmoothCaretImplementationTests.ps1`
- `Tests/SmoothCaretTests.ps1`
- `Tests/SmoothScrollTests.ps1`
- `Tests/MainModeSmoothCaretTests.ps1`（Task 7 启用时）

修改：
- `Config/Config.cs`
- `WinConfig/WinConfig.xaml.cs`
- `UI/MainWindow.xaml`（仅 Task 7 启用时）
- `UI/MainWindow.xaml.cs`
- `UI/Modes/CopybookMode.cs`
- `UI/Modes/TracingMode.cs`
- `Version/version.txt`（Task 10 启用时）
