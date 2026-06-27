# 首页底部按钮区高度问题调试记录

日期：2026-06-26

## 问题背景

首页底部“成绩栏上方按钮区”的高度在多轮修改后仍然不稳定。用户期望的行为是：

- 有底部按钮时，底部按钮区应恢复正常高度，按钮完整显示。
- 所有底部按钮隐藏时，底部按钮区应缩成一键极简类似的窄边框高度。
- 成绩展开/收起图标必须一直可见，位置应与上方发文区、跟打区右边缘对齐，而不是贴到窗口最右边。
- 打开/关闭成绩栏时，发文区和跟打区不应抖动，也不应出现窗口下沿两步缩小、残留高度、收起后点不开等问题。
- 一键极简开启/关闭、正常模式隐藏/恢复按钮、成绩展开/收起之间不能互相破坏布局。

结论先写清楚：这轮问题没有被真正修好。当前代码只是累积了多轮尝试，仍然存在用户反馈的“又展不开了”“隐藏按钮后回不去”“加按钮又撑不开”等回归。

## 每轮主要操作

以下是按问题推进顺序整理的操作记录。没有逐轮 commit 快照，因此这里以当前代码和对话中的反馈还原每一类尝试。

### 1. 从底部空按钮区高度入手

最初问题是“如果把按钮全部隐藏，成绩栏上面的按钮区高度还是很高”。处理方向是让底部按钮全隐藏时进入 compact 状态。

涉及点：

- 在 `ApplyHomeToolbarSettings()` 后追加底部工具栏布局刷新。
- 尝试根据底部按钮是否可见切换正常高度和紧凑高度。
- 调整 `resultsButtonPanel`、底部 `RowDefinition` 的高度和最小高度。

失败表现：

- 空按钮区有时能变窄，但有按钮时撑不开，或者再次隐藏后不回到窄高度。
- 说明“高度切换”没有和实际按钮可见状态稳定绑定。

### 2. 处理一键极简下沿高度和视觉边框

用户反馈一键极简下沿比左右边框大，后来又反馈两步缩小、卡顿、高度太小。

涉及点：

- 调整紧凑高度常量，当前相关值是：
  - `CompactCollapsedBottomToolbarHeight = 15`
  - `ResultsToggleCompactHeight = 15`
  - `DefaultNormalBottomToolbarHeight = 31`
  - `NormalCollapsedBottomBorderHeight = 10`
- 在一键极简时通过 `typingAreaAndButtonsGrid.Margin = new Thickness(0, 0, 0, CompactCollapsedBottomToolbarHeight)` 给底部留视觉空间。
- 在 `ApplySuperCompactCollapsedLayout()` 中计算要从窗口高度移除的区域。

失败表现：

- 视觉上仍然出现底部边框高度和左右不一致。
- 窗口下沿收缩出现“两步变化”，说明至少有两个路径在先后改窗口高度或行高。

### 3. 处理成绩展开/收起图标消失

用户反馈“成绩的开关图标都不见了”“展开成绩栏，那个按钮直接没了”“收起以后点不开了”。

涉及点：

- 新增 `CompactResultsToggleHost`，放在跟打区和底部按钮区的覆盖层：
  - `Grid.Row="0"`
  - `Grid.RowSpan="2"`
  - `HorizontalAlignment="Right"`
  - `VerticalAlignment="Bottom"`
  - `Margin="0,0,15,0"`
- 在 compact 状态把 `BtnToggleResults` 从 `resultsButtonPanel` 移到 `CompactResultsToggleHost`。
- 在 normal 状态再把 `BtnToggleResults` 移回 `resultsButtonPanel` 的第 3 列。
- 为成绩按钮换成 chevron 图标，并新增 `Ctrl+J` 快捷键复用 `BtnToggleResults_Click()`。

失败表现：

- 图标有时能显示，但在成绩展开、收起、一键极简恢复、按钮显示状态变化之间仍可能丢失或不可点击。
- 当前做法改变了按钮的视觉树父级，WPF 的 hit test、行高裁剪、父容器可见性都可能影响它。

### 4. 处理图标右对齐

用户反馈图标没有靠最右边，后来又要求不要太靠右，要和上方发文区、跟打区右边界对齐。

涉及点：

- `CompactResultsToggleHost` 设置右对齐，并用 `Margin="0,0,15,0"` 对齐内容区右内边距。
- compact 状态下 `BtnToggleResults.Width = 15`、`Height = 15`、`Padding = 0`。
- normal 状态下恢复 `Width = 36`，清除高度，恢复 24px 图标 viewport。

失败表现：

- 图标位置调整和高度裁剪互相影响。靠右问题能局部改善，但按钮父级移动后又引入“点不开/消失”的风险。

### 5. 引入底部工具栏布局策略类

为了把“有按钮就是 normal、没按钮就是 compact”的判断从窗口代码里拆出来，新增了：

- `UI/HomeBottomToolbarLayoutPolicy.cs`
- `Tests/HomeBottomToolbarLayoutPolicyTests.cs`
- `Tests/HomeBottomToolbarLayoutPolicyTests.ps1`

当前策略：

```csharp
visibleFeatureButtonCount > 0 || hasVisibleLocalArticleModule
    ? HomeBottomToolbarLayoutMode.Normal
    : HomeBottomToolbarLayoutMode.Compact;
```

保留高度策略：

```csharp
Compact + 成绩展开 => 0
Compact + 成绩收起 => compactCollapsedToolbarHeight
Normal => normalToolbarHeight
```

失败表现：

- 这个策略本身很简单，但它依赖的输入不稳定：到底应该看配置、实际控件 Visibility、实际子控件数量，还是 WPF 已布局后的 ActualHeight，多轮尝试中反复切换。

### 6. 从配置判断改到实际视觉树判断

为了解决“按钮设置出来高度没撑开/按钮隐藏高度不缩小”，曾尝试不再只看配置，而是看 `FeatureToolbarPanel.Children` 中实际可见子控件数量。

当前代码：

```csharp
private int GetVisibleBottomFeatureButtonCount()
{
    if (FeatureToolbarPanel == null)
        return 0;

    return FeatureToolbarPanel.Children.OfType<UIElement>()
        .Count(control => control.Visibility == Visibility.Visible);
}
```

同时右侧本地发文按钮组当前又回到了配置判断：

```csharp
private bool HasVisibleBottomCommandButtons()
{
    return TrainerMainWindowConfigScope.GetBool(HomeToolbarSettings.ShowLocalArticleConfigKey);
}
```

失败表现：

- 只看视觉树时，设置变化和布局刷新时机可能不同步。
- 只看配置时，配置值和实际 UI 状态可能不同步。
- 现在两者混用，仍然可能出现“设置按钮能撑开，但全去掉回不去”或反过来的问题。

### 7. 引入模式和高度缓存

为了让高度变化一次性算出差值，避免发文区/跟打区抖动，加入了这些状态：

- `_lastBottomToolbarLayoutMode`
- `_lastNormalBottomToolbarHeight`
- `_currentBottomToolbarReservedHeight`

当前流程大致是：

1. 记录 previous layout mode。
2. 读取当前底部 row 的实际保留高度。
3. 根据当前可见按钮状态算目标 layout mode。
4. 应用 compact 或 normal 布局。
5. 算目标保留高度。
6. 用 `this.Height += current - previous` 调整窗口高度。

失败表现：

- 缓存可能陈旧。
- WPF `ActualHeight`、`DesiredSize`、`RowDefinition.Height`、`MinHeight` 和手动缓存之间可能互相打架。
- 多个入口都会刷新布局，缓存不一定对应当前真实视觉状态。

### 8. 去掉基于模式相等的提前返回

曾经存在“previous layout mode 等于 current layout mode 就不调整高度”的思路。后来因为用户反馈“加按钮/删按钮后高度不变”，尝试去掉这种 guard，改成只要高度差不为 0.5 以内就调整。

当前相关逻辑：

```csharp
double heightDelta = currentToolbarHeight - previousToolbarHeight;
if (Math.Abs(heightDelta) > 0.5)
    this.Height += heightDelta;
```

失败表现：

- 可以修掉一部分“模式缓存一致但高度不一致”的问题。
- 但如果 previous/current 高度来源本身错了，就会导致窗口高度跳动或撑开/收缩方向错误。

### 9. 统一底部高度写入函数

为了避免到处分别设置 row 高度、panel 高度和裁剪，加入了：

```csharp
private void ApplyBottomToolbarReservedHeight(double reservedHeight, bool clipToBounds)
```

它同时设置：

- `typingAreaAndButtonsGrid.RowDefinitions[1].MinHeight`
- `typingAreaAndButtonsGrid.RowDefinitions[1].Height`
- `resultsButtonPanel.MinHeight`
- `resultsButtonPanel.Height`
- `resultsButtonPanel.ClipToBounds`

失败表现：

- 统一写入减少了分散修改，但也引入了新风险：normal 状态也被固定成 pixel 高度 `31`，可能阻止按钮区按内容 Auto 撑开。
- compact 状态 `clipToBounds = true` 如果和按钮父级/覆盖层位置配合不好，会造成“只显示一点”或“点不开”。

### 10. 在 normal 恢复时强制测量按钮区

为了解决“加按钮撑不开”，在 `RestoreNormalBottomToolbarLayout()` 中尝试：

```csharp
resultsButtonPanel.UpdateLayout();
resultsButtonPanel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
double measuredHeight = resultsButtonPanel.DesiredSize.Height;
if (measuredHeight > 0)
    _lastNormalBottomToolbarHeight = measuredHeight;
```

失败表现：

- 测量发生在刚强制设为固定高度之后，`DesiredSize` 可能已经受 `Height = 31` 影响。
- 如果按钮可见性、父容器、WPF 布局队列还没稳定，测出来的值可能不是最终值。

### 11. 调整成绩展开/收起窗口高度计算

用户反馈“开关的时候发文区高度抖动”“窗口下沿往上跳一截，上面发文跟打区也会挤小一点”。

涉及点：

- 展开/收起前先读取当前底部 footer 高度：
  - `GetCollapsedResultsBottomBorderHeightForCurrentBottomToolbar()`
  - `GetCollapsedResultsWindowFooterHeightForCurrentBottomToolbar()`
- compact 时认为 collapsed bottom border 是 `0`，footer 是 `CompactCollapsedBottomToolbarHeight`。
- normal 时使用 `NormalCollapsedBottomBorderHeight`。
- `BtnToggleResults_Click()` 后再次调用 `ApplyBottomToolbarLayoutFromCurrentVisibility()`。

失败表现：

- 这可能导致成绩展开/收起路径和底部工具栏布局路径连续调整高度。
- 用户看到的“两步缩小”和“发文区抖动”，很可能就是两个布局入口在同一交互中先后改了窗口高度。

### 12. 一键极简恢复路径反复调整

用户多次反馈一键极简开启/关闭时高度异常、退出有显示 bug。

涉及点：

- `ApplySuperCompactModeLayout()` 里保存 `SuperCompactLayoutSnapshot`。
- 进入一键极简时 `TrimSuperCompactBottomButtonRow()` 隐藏按钮行。
- 退出时 `RestoreSuperCompactBottomButtonRow()` 当前直接调用 `ApplyBottomToolbarLayoutFromCurrentVisibility()`。
- 退出时还会根据快照恢复成绩展开状态，并调用 `ExpandResultsPanelLayout(false)` 或 `CollapseResultsPanelLayout(false, false, ...)`。

失败表现：

- 一键极简路径、成绩展开路径、底部按钮行 normal/compact 路径都可能在退出时连续执行。
- 快照里的 `BottomButtonRowHeight`、当前 row pixel height、当前配置状态可能不是同一个时间点的值。

### 13. 增加静态/结构测试

新增或修改过的测试包括：

- `Tests/HomeToolbarResultsToggleUiTests.ps1`
- `Tests/HomeBottomToolbarLayoutPolicyTests.ps1`
- `Tests/HomeBottomToolbarLayoutPolicyTests.cs`
- `Tests/HomeUiLabelsTests.ps1`

这些测试检查了：

- `Ctrl+J` 是否存在。
- 成绩开关图标是否使用 chevron。
- `CompactResultsToggleHost` 是否存在、是否右对齐。
- compact/normal 布局方法是否包含指定代码片段。
- 策略类在简单输入下是否返回 normal/compact。

失败表现：

- 这些测试主要是字符串断言和纯策略测试，不是真实 WPF UI 自动化。
- 它们不能证明运行时 `ActualHeight`、hit test、Dispatcher 布局时序、窗口高度调整是对的。
- 因此测试通过并不能覆盖用户实际看到的 bug。

## 当前代码的主要风险

### 多个状态源互相打架

当前底部区域至少受这些状态影响：

- 配置项：`HomeToolbarSettings.*`
- 实际 UI 控件：`FeatureToolbarPanel.Children`
- 本地发文组配置：`ShowLocalArticleConfigKey`
- `_lastBottomToolbarLayoutMode`
- `_lastNormalBottomToolbarHeight`
- `_currentBottomToolbarReservedHeight`
- WPF `RowDefinition.Height`
- WPF `RowDefinition.MinHeight`
- WPF `ActualHeight`
- `resultsButtonPanel.Height`
- `resultsButtonPanel.MinHeight`
- `resultsButtonPanel.DesiredSize`
- `resultsButtonPanel.ClipToBounds`
- `CompactResultsToggleHost` 中是否包含 `BtnToggleResults`
- `_isResultsExpanded`
- `_isSuperCompactLayoutApplied`
- `SuperCompactLayoutSnapshot`
- `this.Height` / `this.ActualHeight`

这些状态不是单一来源，任何一个入口如果只更新其中一部分，就会出现下一次交互基于旧状态计算的情况。

### 高度计算入口太多

目前可能改高度的路径包括：

- `ApplyTopBarHeightAdjustmentIfNeeded()`
- `ApplyBottomToolbarHeightAdjustmentIfNeeded()`
- `ApplySuperCompactCollapsedLayout()`
- `ExpandResultsPanelLayout()`
- `CollapseResultsPanelLayout()`
- 一键极简 restore 里的 snapshot window height 恢复

用户看到的“发文区抖动”“下沿两步缩小”，大概率来自同一次操作中多个路径连续修改 `this.Height` 或 row height。

### normal 状态被固定高度可能是根因之一

当前 normal 恢复会调用：

```csharp
ApplyBottomToolbarReservedHeight(DefaultNormalBottomToolbarHeight, false);
```

这会把 bottom row 和 `resultsButtonPanel` 都固定为 31px。若按钮实际高度、Margin、Padding、字体渲染、DPI 或主题导致需要更高空间，按钮就会“只显示一点”或无法完全撑开。

### compact 状态下按钮父级移动增加复杂度

`BtnToggleResults` 在 compact 和 normal 之间切换父级：

- compact：`CompactResultsToggleHost`
- normal：`resultsButtonPanel`

这能解决“底部行被压缩后图标还要可见”的需求，但也让点击区域、裁剪、父级可见性和布局时序更难判断。

## 当前已知测试结果

最后一次记录中这些命令通过过：

- `.\Tests\HomeUiLabelsTests.ps1`
- `.\Tests\HomeToolbarResultsToggleUiTests.ps1`
- `.\Tests\HomeBottomToolbarLayoutPolicyTests.ps1`
- `.\Tests\HomeToolbarSettingsTests.ps1`
- `.\Tests\TrainerMainWindowMemoryUiTests.ps1`
- `.\Tests\ScorePanelLayoutPolicyTests.ps1`
- MSBuild Debug 构建，输出到 `bin\CodexBuild\晴跟打.exe`

但这些通过不代表问题已修复。原因是缺少真实 UI 自动化和运行时布局日志。

## 建议的下一步

不要继续按视觉反馈猜测微调常量。下一步应该先加运行时证据，再决定重构方式。

建议先做一版临时诊断日志，记录每次进入以下方法前后的状态：

- `ApplyHomeToolbarSettings()`
- `ApplyBottomToolbarLayout()`
- `ApplyCompactBottomToolbarLayout()`
- `RestoreNormalBottomToolbarLayout()`
- `BtnToggleResults_Click()`
- `ExpandResultsPanelLayout()`
- `CollapseResultsPanelLayout()`
- `ApplySuperCompactModeLayout()`

至少记录：

- 当前操作名和时间顺序编号。
- `visibleFeatureButtonCount`
- `hasVisibleLocalArticleModule`
- 目标 `layoutMode`
- `_lastBottomToolbarLayoutMode`
- `_lastNormalBottomToolbarHeight`
- `_currentBottomToolbarReservedHeight`
- `bottomButtonRow.Height`
- `bottomButtonRow.MinHeight`
- `bottomButtonRow.ActualHeight`
- `resultsButtonPanel.Height`
- `resultsButtonPanel.MinHeight`
- `resultsButtonPanel.DesiredSize.Height`
- `resultsButtonPanel.ActualHeight`
- `resultsButtonPanel.Visibility`
- `resultsButtonPanel.ClipToBounds`
- `FeatureToolbarPanel.Children.Count`
- `FeatureToolbarPanel` 中可见子控件数量和名字
- `BtnToggleResults.Parent`
- `CompactResultsToggleHost.Visibility`
- `CompactResultsToggleHost.ActualHeight`
- `_isResultsExpanded`
- `_isSuperCompactLayoutApplied`
- `this.Height`
- `this.ActualHeight`

然后按固定步骤复现：

1. 正常模式，所有底部按钮隐藏。
2. 增加一个底部按钮。
3. 再把底部按钮全部隐藏。
4. 展开成绩。
5. 收起成绩。
6. 开启一键极简。
7. 关闭一键极简。
8. 重复第 2 和第 3 步。

只有拿到这些日志后，才能判断根因是：

- 状态判断错了。
- row/panel 高度写入顺序错了。
- 窗口高度补偿重复了。
- WPF 布局还没完成就测量了。
- 一键极简快照恢复污染了 normal/compact 状态。
- 或者 compact toggle host 的父级/裁剪设计本身不适合。

## 更稳的重构方向

如果继续处理，建议不要再在现有多个入口上加补丁，而是把底部区域收敛成一个状态机：

- 唯一输入：
  - 是否一键极简。
  - 成绩是否展开。
  - 是否存在底部功能按钮。
  - 是否存在本地发文命令组。
- 唯一输出：
  - 底部按钮区 visual mode。
  - 成绩按钮所在父级。
  - bottom row height。
  - bottom border height。
  - 是否需要调整 window height。
- 所有入口只更新输入，然后调用一个 `ApplyHomeBottomLayout(reason)`。
- `ApplyHomeBottomLayout` 一次性计算最终布局，不允许展开/收起路径和按钮路径各自再调整一次窗口高度。

同时要补一类真实运行验证，而不是只靠字符串测试：

- 启动 Debug exe。
- 用 UI Automation 或最小 WPF harness 操作配置。
- 截取/读取关键元素 `ActualHeight`。
- 断言“添加按钮后高度变为 normal”“删除全部按钮后高度变为 compact”“成绩开关始终可点击”。

## 涉及文件

本轮问题相关文件主要是：

- `UI/MainWindow.xaml.cs`
- `UI/MainWindow.xaml`
- `UI/HomeBottomToolbarLayoutPolicy.cs`
- `Tests/HomeToolbarResultsToggleUiTests.ps1`
- `Tests/HomeBottomToolbarLayoutPolicyTests.ps1`
- `Tests/HomeBottomToolbarLayoutPolicyTests.cs`
- `Tests/HomeUiLabelsTests.ps1`

当前工作区还有大量其他改动，和这个底部高度问题不一定相关。后续接手时不要直接回滚整个工作区。
