# Claude 交接：超级简洁模式 / 成绩栏布局问题

## 当前用户复现

用户反馈的最新问题：

1. 开启“超级简洁模式”。
2. 关闭“超级简洁模式”。
3. 下面的成绩栏没有展开。
4. 再次开启“超级简洁模式”后，样式已经乱了。

前一个相关症状：

- 普通成绩栏反复“收起 / 打开 / 收起 / 打开”后，布局样式会乱。

注意：这些问题集中在 `UI/MainWindow.xaml.cs` 的主窗口布局状态管理，尤其是 `grid_a.RowDefinitions[2/4/5/6/7]`、`resultsTextBoxGrid`、`TbxResults`、`resultsButtonPanel`、`buttonArea1`。

## 我已经做过的操作

### 1. 给普通成绩栏收起/展开加了布局版本号

文件：`UI/MainWindow.xaml.cs`

新增字段和 helper：

- `_resultsLayoutVersion`
- `BeginResultsLayoutChange()`
- `IsStaleResultsLayoutChange(int layoutVersion)`

目的：

- `ExpandResultsPanelLayout()` 和 `CollapseResultsPanelLayout()` 都会先同步改行高，再通过 `Dispatcher.BeginInvoke` 延迟把行高转成 `Star`。
- 用户快速切换时，旧的 `BeginInvoke` 可能在新状态之后执行，把旧状态写回去。
- 我加了版本号，让旧回调在执行前发现自己已经过期并直接 `return`。

已改位置：

- `ExpandResultsPanelLayout(bool adjustWindowHeight)`：进入时 `BeginResultsLayoutChange()`，延迟回调里检查 `IsStaleResultsLayoutChange(layoutVersion)`。
- `CollapseResultsPanelLayout(bool adjustWindowHeight, bool saveExpandedHeight, ...)`：同上。

### 2. 给超级简洁模式拆了专门的布局路径

文件：`UI/MainWindow.xaml.cs`

相关函数：

- `ApplySuperCompactModeLayout(bool isSuperCompact, bool forceRefresh = false)`
- `CaptureSuperCompactLayoutSnapshot()`
- `TrimSuperCompactBottomButtonRow()`
- `RestoreSuperCompactBottomButtonRow()`
- `ApplySuperCompactCollapsedLayout(SuperCompactLayoutSnapshot snapshot, bool adjustWindowHeight)`

目的：

- 超级简洁模式不是普通“收起成绩栏”，它还要隐藏首页顶部按钮、隐藏底部功能按钮行、去掉底部预留高度。
- 之前如果复用普通 `CollapseResultsPanelLayout()`，容易把普通成绩栏状态、超级简洁状态、底部按钮行状态混在一起。

已经做的行为：

- 开启超级简洁时：
  - 捕获布局快照。
  - 隐藏 `resultsButtonPanel`。
  - 把 `typingAreaAndButtonsGrid.RowDefinitions[1]` 设为 `0px`。
  - 隐藏成绩区、分隔条、底部边框。
  - 压缩顶部按钮行。
- 关闭超级简洁时：
  - 恢复底部按钮行 `Height = Auto`。
  - 恢复 `resultsButtonPanel.Visibility = Visible`。
  - 恢复窗口高度。
  - 再按快照/状态决定成绩区展开或收起。

### 3. 防止超级简洁期间保存错误比例

文件：`UI/MainWindow.xaml.cs`

已改：

- `SaveDisplayInputRatio()` 开头增加：
  - `_isSuperCompactLayoutApplied` 时直接返回。
  - `_suppressWindowSizeChangeUpdatesDepth > 0` 时直接返回。

目的：

- 超级简洁模式会把成绩区和按钮区压成 0。
- 如果这个时候保存 `成绩区高度比例` / `发文区跟打区比例`，退出后会用错误比例恢复，造成样式异常。

### 4. 处理设置变更时超级简洁状态被破坏

文件：`UI/MainWindow.xaml.cs`

已改：

- `ApplyHomeToolbarSettings()` 里，如果当前 `isSuperCompact == true`，调用：
  - `ApplySuperCompactModeLayout(true, true)`

目的：

- 设置页改变首页按钮显示/顺序后，会调用 `ApplyTopBarLayout()`。
- 如果超级简洁已经开启，`ApplyTopBarLayout()` 可能把顶部按钮行恢复出来。
- 所以需要强制重套超级简洁的隐藏布局。

风险点：

- 这个强制重套不能覆盖第一次进入超级简洁时的正常布局快照，否则退出时会恢复到已经压扁的快照。
- 当前 `CaptureSuperCompactLayoutSnapshot()` 在 `_isSuperCompactLayoutApplied && _superCompactLayoutSnapshot != null` 时会复用已有快照，不会重新抓压扁后的布局。

### 5. 尝试修复“关闭超级简洁后成绩栏没有展开”

文件：`UI/MainWindow.xaml.cs`

新增 helper：

- `IsResultsPanelVisuallyExpanded(Grid mainGrid)`

已改：

- `CaptureSuperCompactLayoutSnapshot()` 里：
  - `ResultsExpanded = IsResultsPanelVisuallyExpanded(mainGrid)`
- 关闭超级简洁时：
  - `bool shouldRestoreResultsExpanded = snapshot != null ? snapshot.ResultsExpanded : _isResultsExpanded`
  - `_isResultsExpanded = shouldRestoreResultsExpanded`
  - `Config.dicts["成绩面板展开"] = shouldRestoreResultsExpanded ? "是" : "否"`
  - 如果 `shouldRestoreResultsExpanded` 为 true，调用 `ExpandResultsPanelLayout(false)`。
  - 否则调用 `CollapseResultsPanelLayout(false, false, NormalCollapsedBottomBorderHeight)`。

我的判断：

- 关闭超级简洁时不能只看运行中的 `_isResultsExpanded`。
- 因为超级简洁会把实际成绩栏隐藏/压扁，运行状态和视觉布局可能分叉。
- 应按进入超级简洁前的快照恢复。

但是：

- 我没有做人工 UI 点击验证。
- 用户最新反馈发生在我上一轮修复之前；当前这次修改是否彻底解决，需要实际运行确认。

## 测试改动

文件：`Tests/HomeUiLabelsTests.ps1`

我加的是字符串约束型回归测试，主要锁定布局修复的关键结构：

- 必须有 `_resultsLayoutVersion`。
- 必须有 `BeginResultsLayoutChange()`。
- 必须有 `IsStaleResultsLayoutChange(int layoutVersion)`。
- 延迟回调必须检查 `IsStaleResultsLayoutChange(layoutVersion)`。
- 超级简洁必须用专门布局函数，不应复用旧的普通收起路径。
- 超级简洁必须隐藏底部按钮行且恢复为 `Auto`。
- 超级简洁必须通过 `IsResultsPanelVisuallyExpanded(mainGrid)` 捕获成绩区视觉展开状态。
- 关闭超级简洁时必须从快照恢复成绩区展开状态。

文件：`Tests/HomeToolbarSettingsTests.ps1`

- 这是之前为首页按钮显示/排序相关功能加的测试。
- 本轮只是继续运行确认没有破坏。

## 我运行过的验证命令

```powershell
& .\Tests\HomeUiLabelsTests.ps1
```

结果：通过。

```powershell
& .\Tests\HomeToolbarSettingsTests.ps1
```

结果：通过。

```powershell
& 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe' TypeSunny.csproj /p:Configuration=Debug /p:Platform=AnyCPU /p:OutDir=bin\CodexBuild\ /v:m
```

结果：编译通过，只有项目已有 warning。

注意：

- 没有跑正常 `bin\Debug` 输出，避免正在运行的 `晴跟打.exe` 锁文件。
- `dotnet build TypeSunny.csproj` 之前在这台机器上有 MSBuild `GenerateResource` x86 task host 问题，不适合作为当前验证命令。

## 当前工作区状态提醒

当前工作区不是干净状态，且很多改动不是本问题这一轮单独产生的。

`git status --short` 显示过这些路径有改动/新增：

- `Config/Config.cs`
- `TypeSunny.csproj`
- `UI/MainWindow.xaml`
- `UI/MainWindow.xaml.cs`
- `Version/GeneratedVersion.cs`
- `Version/version.txt`
- `WinConfig/WinConfig.xaml.cs`
- `Resources/Shuang/`
- `Tests/HomeToolbarSettingsTests.ps1`
- `Tests/HomeUiLabelsTests.ps1`
- `Tests/ShuangNextWordEngineTests.js`
- `UI/HomeToolbarSettings.cs`
- `Utils/ShuangToolLauncher.cs`

用户明确说过：

- 不要每次改回版本号，编译会自动改。

所以不要为了清理 diff 去回退：

- `Version/GeneratedVersion.cs`
- `Version/version.txt`

也不要随便 revert 其他未确认属于用户/前序任务的改动。

## 给 Claude 的建议排查方向

如果用户继续说“还是乱”，建议不要继续微调边框或 margin，优先做以下检查：

1. 在 `ApplySuperCompactModeLayout()` 入口、开启分支、关闭分支打印或断点查看：
   - `_isSuperCompactLayoutApplied`
   - `_superCompactLayoutSnapshot != null`
   - `snapshot.ResultsExpanded`
   - `_isResultsExpanded`
   - `Config.GetBool("成绩面板展开")`
   - `grid_a.RowDefinitions[5/6/7].Height`
   - `typingAreaAndButtonsGrid.RowDefinitions[1].Height`
   - `resultsTextBoxGrid.Visibility`
   - `resultsButtonPanel.Visibility`

2. 重点确认“关闭超级简洁”时是否真的走到了：
   - `_isResultsExpanded = shouldRestoreResultsExpanded`
   - `ExpandResultsPanelLayout(false)`

3. 如果 `ExpandResultsPanelLayout(false)` 之后成绩栏仍不显示，检查它内部：
   - `ApplyDisplayInputRatio()` 是否把 Row 6 比例设成 0 或异常小。
   - `resultsTextBoxGrid.Visibility` 是否被后续别的 `Dispatcher.BeginInvoke` 改回 `Collapsed`。
   - `gridSplitterResults.Visibility` 是否被改回 `Collapsed`。

4. 如果“再次开启超级简洁”时样式乱，检查当前快照是否被错误覆盖：
   - 正常情况下，超级简洁已开启时再次 `ApplySuperCompactModeLayout(true, true)` 应复用 `_superCompactLayoutSnapshot`。
   - 不应该在压扁布局上重新创建快照。

5. 可能需要把超级简洁布局状态做成更明确的状态机：
   - NormalExpanded
   - NormalCollapsed
   - SuperCompactFromExpanded
   - SuperCompactFromCollapsed
   避免通过多个 bool 和视觉状态互相推断。

## 关键代码位置

当前关键位置大致如下，行号可能随后续修改变动：

- `UI/MainWindow.xaml.cs`
  - `_resultsLayoutVersion`：约第 81 行
  - `ApplyHomeToolbarSettings()`：约第 2058 行
  - `ApplySuperCompactModeLayout()`：约第 2201 行
  - `IsResultsPanelVisuallyExpanded()`：约第 2264 行
  - `CaptureSuperCompactLayoutSnapshot()`：约第 2280 行
  - `ApplySuperCompactCollapsedLayout()`：约第 2353 行
  - `ExpandResultsPanelLayout()`：约第 2410 行
  - `CollapseResultsPanelLayout()`：约第 2500 行
  - `SaveDisplayInputRatio()`：约第 9530 行

## 我的结论

我做过两轮修复：

1. 第一轮解决普通成绩栏快速收起/展开的旧异步回调覆盖问题。
2. 第二轮解决超级简洁关闭时没有按进入前快照恢复成绩栏状态的问题。

两轮都有脚本测试和编译验证，但都没有真实 UI 手动点击验证。Claude 接手时，应优先实际复现用户路径，再根据运行时状态决定是否需要进一步改成显式状态机。
