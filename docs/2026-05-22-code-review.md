# Code Review — 2026-05-22

审查范围：自 `0b03030` 后的工作区改动（尚未提交）。

```
 Core/Score.cs                         | +12
 Tests/CiTiConfigPlanTests.ps1         | +7 -1
 Tests/TrainerLogActualWordsTests.ps1  | +60
 Tests/TypingCursorVisibilityTests.ps1 | +10
 TypeSunny.csproj                      | +1
 UI/MainWindow.xaml                    | +2 -2
 UI/MainWindow.xaml.cs                 | +70 -6
 Version/GeneratedVersion.cs           | +1 -1
 Version/version.txt                   | +1 -1
 WinTrainer/TrainerLog.cs              | +23 -41
 WinTrainer/WinTrainer.xaml            | +4 -1
 WinTrainer/WinTrainer.xaml.cs         | +13
 + Tests/ScoreFormatRowsTests.{cs,ps1}
 + Tests/ScorePanelPresentationTests.ps1
 + Tests/TrainerHistoryUiTests.ps1
 + Tests/ZiTiCornerPromptTests.ps1
 + WinTrainer/WinTrainerHistoryWindow.cs
```

主要改动：晴练单成绩对齐豁免、字提右上角提示在编码下显时也保留、跟打中临时显鼠标、晴练单历史窗口、`recent.json` 不再清理。

---

## 🔴 严重

### 1. `BtnHistory` 永远被 Slider 遮住，用户点不到

**位置：** `WinTrainer/WinTrainer.xaml:255-259`

```xml
<Slider x:Name="sld" Grid.Column="2" HorizontalAlignment="Stretch" .../>
<StackPanel Grid.Column="2" HorizontalAlignment="Center" ... Orientation="Horizontal">
    <Button x:Name="BtnReset" ... Visibility="Collapsed"/>
    <Button x:Name="BtnHistory" Content="历史" .../>
</StackPanel>
```

`Slider` 和 `StackPanel` 共享 `Grid.Column="2"`。原本是通过 `UpdateUIState()`（`WinTrainer.xaml.cs:2531`）做互斥切换：

| 状态 | sld | BtnReset |
|---|---|---|
| 未开始练习 | Visible | Collapsed |
| 练习进行中 | Collapsed | Visible |

`BtnReset` 自己 `Collapsed` 时不占位，所以从不和 Slider 重叠。

现在 `BtnHistory` 永远 `Visible`，但未开始练习时 `sld` 也是 `Visible` 且 `HorizontalAlignment=Stretch` 横向铺满整列，居中的 `StackPanel` 落在 Slider 上方但响应鼠标的是 Slider，"历史"按钮点不到。

更糟的是 `Tests/TrainerHistoryUiTests.ps1:40` 显式断言 `BtnHistory.Visibility` 不被 `UpdateUIState` 控制，反而把这个 bug 锁在了测试里。

**修复方向：**
- 给进度条区加第 4 列，"历史"按钮放进新列，和 Slider 分开；或
- 把 Slider 的 `Grid.Column` 改成单独占一列，StackPanel 占另一列。

---

## 🟡 中等

### 2. `recent.json` 现在无限增长

**位置：** `WinTrainer/TrainerLog.cs:344-347`

```csharp
private static void CleanOldRecords(...)
{
    // recent.json is the trainer history source. Keep every completed round.
}
```

旧逻辑："最近 24h、最多 30 条"。改后："永不清理"。这是配合历史窗口的有意改动，但：

- 每次 `WriteRecord` 都重写整个 `recent.json`（紧凑 JSON，单行）。重度用户一年累计上万条，每次结束一段都要序列化并写整个文件，I/O 量随时间线性增长。
- 文件 corruption 后单次解析失败就丢全部历史：`LoadRecentData` 在 `JsonConvert.DeserializeObject` 抛异常时返回空 `RecentRecords`，下次保存就把空数据写回，老记录覆盖。

**建议：** 保留例如 ≤5000 条 + 不限时间，或按月归档拆文件。至少给 `LoadRecentData` 加损坏数据备份。

### 3. `ReadRecentRecords` 默认值反转

**位置：** `WinTrainer/TrainerLog.cs:511`

```csharp
public static List<...> ReadRecentRecords(int count = 0)   // 之前是 30
```

兼容性 OK（搜过没有外部代码传 `count`），但语义反转了：

| 调用 | 之前 | 现在 |
|---|---|---|
| `ReadRecentRecords()` | 最多 30 条 | 全部 |
| `ReadRecentRecords(0)` | 空列表 | 全部 |

以后有人按"0 表示什么都不要"的直觉写 `ReadRecentRecords(0)`，会拿到全部历史。

**建议：** 改成 `int? count = null`，或提供 `ReadAllRecords()` 把"全部"和"上限 N"显式区分。

### 4. `KeepsAllTrainerHistoryRecordsAndWritesCompactJson` 测试存在竞态

**位置：** `Tests/TrainerLogActualWordsTests.ps1:136-186`

```csharp
for (int i = 0; i < 35; i++)
    TrainerLog.WriteRecord(...);
WaitForRecentCount(title, 35);
```

`WriteRecord` 内部是 `Task.Run` 派发的，35 次"读 → 改 → 写"并发抢同一个 `recent.json`。没有锁保护时后写覆盖先写，最终条数可能 <35，触发 5 秒超时。

**建议：** 把 `WriteRecord` 改成串行化（`lock` 或单线程 channel），或测试改成同步调用底层写入路径。

---

## 🟢 轻微

### 5. `MainWin_PreviewMouseMove` 容易误触发显鼠标

**位置：** `UI/MainWindow.xaml.cs:5397`

WPF 中 `PreviewMouseMove` 即使用户没真动鼠标也可能触发（焦点变化、布局调整、`Mouse.OverrideCursor` 被设置等）。每次进来都重启 3 秒计时器并 `SetMouseCursor(Cursors.Arrow, null)`，导致"明明没动鼠标但光标自己冒出来"。

**建议：** 比较 `e.GetPosition(this)` 与上次位置，> N 像素才触发。

### 6. `ShouldIgnoreForScoreAlignment` 硬编码字面量

**位置：** `Core/Score.cs:590` 和 `WinTrainer/WinTrainer.xaml.cs:1501`

`[晴练单]` 在两处独立的字面量里出现，一处写一处读，未来改动易脱钩。

**建议：** 抽 `Score.TrainerSummaryPrefix` 常量供两边共用。

### 7. `TbxResults` 移除 `FontFamily=Consolas`

**位置：** `UI/MainWindow.xaml:715`

移除 `FontSize/FontFamily/FontWeight` 让主题统一是合理的。但 `FormatRows` 按显示宽度对齐时依赖等宽字体，需要确认主题里 `TextBox` 的默认样式确实指定了等宽字体，否则中文/数字混排时列错位。

---

## 改动要点

- `Score.FormatRows`：以 `[晴练单]` 开头的行不参与对齐宽度计算，避免被很长的练单文件名拉宽其他成绩行。
- `UpdateZiTi`：移除 `字提编码下显` 开关，右上角字提提示在编码下显时也保留（仅 `启用字提=false` 才清空）。
- 鼠标光标：跟打中默认隐藏；用户移动/点击后显示 3 秒再隐藏（`MouseCursorTemporaryRevealMilliseconds = 3000`）。
- 晴练单历史：新增 `BtnHistory` + `WinTrainerHistoryWindow`，使用 DataGrid 展示，可按标题过滤。
- `TrainerLog`：`recent.json` 改成紧凑 JSON + 不清理；`GetRecordsByExercise` 改成从 `recent.json` 读，不再扫日期文件。
