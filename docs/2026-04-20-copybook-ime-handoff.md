# 字帖模式 / 临摹模式输入链路排查交接

日期：2026-04-20

目的：记录这一轮围绕字帖模式、临摹模式、练单器、五码顶字失效、退格、候选框位置、滚动与错字提示的所有已知信息，避免重复排查。

## 一、用户侧约束与偏好

- 用户使用 Rider，不使用 VS。
- 用户可以自己编译和验证。
- 不接受“猜测式”结论，每个判断都要尽量能落到代码或日志证据上。
- 不接受依赖任意延时的核心输入逻辑修复，尤其是 IME/TSF 相关问题。
- 输入方案不能只针对四码定长，后续也要兼容拼音等其他输入法方案。
- 当前终端环境里不适合宣称本地构建通过，WPF 构建和 Rider 环境之间有差异。

## 二、日志位置

字帖诊断日志路径：

- `bin/Debug/copybook-diag.log`

对应代码：

- `UI/MainWindow.xaml.cs` 中的 `WriteCopybookDiagnostic(...)`

## 三、这轮已经确认并解决的问题

### 1. 练单器余字乱序后“每打一个字母都卡”

结论：

- 根因不是单个字母本身，而是 `trainer` 载文后没有稳定把焦点恢复到字帖真实输入宿主 `_inputCapture`。
- 顺序发时路径较稳定，乱序发时更容易暴露焦点没回到正确输入控件的问题。

已做改动：

- `WinTrainer/WinTrainer.xaml.cs`
- `UI/MainWindow.xaml.cs`

结果：

- 用户已确认“练单不卡了”。

### 2. 候选框位置明显不对、每输入一个字母位置乱跳

结论：

- 字帖模式和普通发文并没有完全复用输入宿主和定位链路。
- 后续通过统一 `FocusInput()`、`PrepareLoadedTextForInput()`、重算定位，候选框位置基本恢复正常。

现状：

- 用户反馈“位置差不多了”，但这个问题不是本轮未解决重点。

### 3. 退格语义错误

用户反馈过的错误表现：

- 正常删除没反应。
- 只有 1 个未上屏编码时，退格会删掉“未上屏编码 + 前一个字”。
- 快速操作时会出现重复删除、字母没打出来。

处理结果：

- 当前“正常删除”用户已确认可以。
- 快速并发场景下曾做过数轮修正，现阶段用户反馈“很完美”。

相关代码主要在：

- `UI/Modes/CopybookMode.cs`

### 4. 空格上屏会多出一个空格

原问题：

- 输入一个字，按空格时，应该只上屏这个字，但实际是“字 + 空格”。

处理结果：

- 用户已确认“现在可以了”。

### 5. 错字提示不随滚动、滚出视口还悬浮

处理结果：

- 已实现错字提示跟随滚动刷新。
- 对应字只要部分离开可视区，就隐藏错字提示。
- 用户还要求发文区支持滚轮查看，已处理。

相关代码：

- `UI/Modes/CopybookMode.cs`
- `UI/Modes/TracingMode.cs`

### 6. 临摹模式滚轮查看

处理结果：

- 已同步支持手动滚轮查看。
- 打字进度变化时继续自动回到当前字附近。

## 四、当前未解决的核心问题

### 五码顶字失效 / 下一字首码丢失

用户给出的典型复现：

- `lxpdllxpd+空格`

预期：

- 第一个 `lxpd` 对应的字上屏。
- 第 5 个码 `l` 同时作为下一字的首码进入新的 composition。

实际：

- 第一个字能正确上屏。
- 下一字的首码 `l` 丢失。
- 后面从 `xpd` 开始组码，导致出来的是别的字，例如 `剌`。

## 五、当前最关键的日志证据

### 最新一组日志（22:40）

日志片段来自：

- `bin/Debug/copybook-diag.log`

关键顺序：

1. 第一个字提交成功：

- `22:40:54.920 OnCompositionUpdate comp='莉'`
- `22:40:54.921 OnTextInput.Before text='莉' inputText='莉' currentIndex=0`
- `22:40:54.921 OnTextInput.After text='莉' inputText='莉' currentIndex=1`

2. 下一字首码 `L` 已经到程序：

- `22:40:54.926 OnPreviewKeyDown ... imeKey=L inputText='莉' currentIndex=1 scoreComposing=False`

3. 我们自己的 UI 更新在这之后插入：

- `22:40:54.932 UpdatePosition`
- `22:40:54.933 AdvanceVisuals`

4. 下一轮 composition 并没有从 `l` 开始，而是直接从 `x` 开始：

- `22:40:55.261 OnCompositionStart ... inputText='莉' currentIndex=1`
- `22:40:55.262 OnCompositionUpdate comp='x' active='x' imeComposing=True inputText='莉' currentIndex=1`

5. 后续继续变成：

- `xp`
- `xpd`
- 最终提交 `剌`

### 能从日志确定的事实

- 首码 `L` 不是“没到程序”，它明确到达了 `OnPreviewKeyDown`。
- 首码 `L` 也不是在 `OnPreviewKeyDown` 中被我们显式 `Handled` 掉的，因为当前代码里对字母键没有拦截。
- 首码丢失发生在：
  - `OnPreviewKeyDown(imeKey=L)` 之后
  - 下一轮 `OnCompositionStart/OnCompositionUpdate` 之前
- 当前日志里，下一轮 composition 仍然从 `x` 开始，而不是 `l`。

### 不能从日志直接证明的事

- 还不能只凭日志确定 TSF/WPF 内部到底是哪一个机制吞掉了 `L`。
- 所以不能下结论说“一定是某个 Win32/TSF 内部 API 问题”，这一步没有证据。

## 六、目前代码里的关键差异点

### 普通输入框链路

普通跟打区 `TbxInput`：

- XAML：`PreviewTextInput="InputBox_TextInput"`
- 代码：`InputBox_TextInput(...)` 只调用 `HandleTextInputStats(e);`
- 这里不主动 `Handled` 这个提交事件。

文件：

- `UI/MainWindow.xaml`
- `UI/MainWindow.xaml.cs`

### 字帖模式链路

字帖模式 `_inputCapture`：

- 事件挂在 `PreviewTextInput += OnTextInput`
- `OnTextInput(...)` 内部自己处理提交
- 最后 `e.Handled = true`

文件：

- `UI/Modes/CopybookMode.cs`

当前最值得怀疑、但还没有正式验证的点：

- 字帖模式在 `PreviewTextInput` 中接管并 `Handled` 当前提交，可能打断了 IME 那个“当前字提交 + 第五码继续作为下一字首码”的连续链路。

## 七、已经试过并被证伪的方向

### 方向 1：提交后宿主残留文本导致首码进不去 composition

当时的依据：

- 日志里在首码 `L` 到来时，`_inputCapture.Text` 还是 `'莉'`。

做过的尝试：

- 在下一次真实文本键到来前，先把 `_inputCapture.Text` 清空。
- 对应代码曾引入：
  - `MarkInputCaptureForNextTextEntryReset`
  - `PrepareInputCaptureForNextTextEntry`

结果：

- 从后续日志可以证明：
  - 首码 `L` 到来前，宿主已经被清空；
  - 但下一轮 composition 仍然直接从 `x` 开始。

结论：

- “只是因为 `_inputCapture.Text` 残留上一个已上屏字”这个解释不成立。
- 这套逻辑已撤回。

### 方向 2：`AdvanceVisuals` 的调度优先级太晚，打断了连续输入链路

当时的依据：

- 在首码 `L` 和下一轮 composition 之间，日志里能看到 `AdvanceVisuals`。

做过的尝试：

- `ScheduleAdvanceVisuals()` 从 `DispatcherPriority.ApplicationIdle` 改成 `DispatcherPriority.Input`。

结果：

- 最新 `22:40` 日志仍然复现同样问题。

结论：

- 单独改 `AdvanceVisuals` 优先级不能解决首码丢失。
- 但这部分改动目前仍留在代码里，后续可按需要决定是否回退。

## 八、当前代码里仍保留的有效改动

### `UI/MainWindow.xaml.cs`

保留内容：

- `WriteCopybookDiagnostic(...)`
- 统一发送入口 `SendContentToClipboardOrQQ(...)`
- `PrepareLoadedTextForInput(...)`
- `FocusInput()` 根据当前模式把焦点打到：
  - 普通 `TbxInput`
  - 字帖 `_copybookMode.FocusInputCapture()`
  - 临摹 `_tracingMode.FocusInputCapture()`

### `UI/Modes/CopybookMode.cs`

保留内容：

- 大量诊断日志
- 组合状态跟踪：
  - `_isImeComposing`
  - `_activeCompositionText`
  - `_lastImeCancelTicks`
- 退格保护逻辑
- 错字提示滚动刷新和可视区裁剪
- 鼠标滚轮查看
- `FocusInputCapture()`
- `ScheduleAdvanceVisuals()` 当前优先级为 `DispatcherPriority.Input`

### `UI/Modes/TracingMode.cs`

保留内容：

- `FocusInputCapture()`
- 鼠标滚轮查看
- 手动滚动后、继续输入时自动回位

### `WinTrainer/WinTrainer.xaml.cs`

保留内容：

- 练单器载文后显式调用 `MainWindow.Current.FocusInput()`
- 发送内容改走主窗口统一发送方法
- 资源目录路径修正为 `Resources/练单器/`

## 九、当前工作区里观察到的非本问题变更

当前 `git status` 里还有以下改动，但未必都属于这次输入链路排查：

- `Core/TextInfo.cs`
- `TypeSunny.csproj`
- `Version/GeneratedVersion.cs`
- `Version/version.txt`

这些文件不要在后续处理五码问题时误当成“必须一起回滚/一起提交”的内容。

## 十、方向 A 已实施：移除 `e.Handled = true`

日期：2026-04-20

### 改动内容

在 `CopybookMode.OnTextInput(...)` 中，移除了末尾的 `e.Handled = true`。

原因：字帖模式在 `PreviewTextInput` 中设置 `e.Handled = true` 后，WPF/TSF 内部的 TextBox 默认处理器不再执行，导致 IME 的”提交当前字 + 第5码启动新 composition”连续链路被截断。普通输入框 `TbxInput` 的 `InputBox_TextInput` 从不设 `e.Handled`，所以普通模式下五码顶字正常。

### 配套处理

移除 `e.Handled = true` 后，`_inputCapture.Text` 会因 TextBox 默认行为累积已上屏文字。新增 `ScheduleInputCaptureTrim()` 方法，在 `DispatcherPriority.ApplicationIdle` 优先级下清理累积文字，且仅在没有活跃 composition 时执行，避免干扰正在进行的输入。

### 仍然成立的设计原则

- `_inputCapture` 只做 IME 宿主/候选框锚点，不承担已上屏内容容器职责。
- 业务状态由 `ProcessInputText()` / `_currentIndex` / `TextInfo.wordStates` 维护。
- 空码/ESC 取消路径仍保留 `e.Handled = true`（这些场景不涉及连续 composition）。

### 待用户验证

- `lxpdllxpdl` 五码顶字场景，首码是否还会丢失。
- 退格、空格上屏、错字提示、滚动定位是否有回退。

## 十一、不要再重复走的坑

- 不要再靠任意延时去”赌”首码会不会进 composition。
- 不要再把”宿主残留文本”当成已证实根因。
- 不要只改多个变量后一起验证，否则日志价值会迅速下降。
- 不要把用户工作区其他脏文件误判为本问题改动。

## 十二、当前对外最准确的一句话总结

五码顶字失效的根因是字帖模式在 `PreviewTextInput` 中 `e.Handled = true` 打断了 TSF 连续提交链路。已移除该 `Handled`，并用 `ScheduleInputCaptureTrim()` 在空闲时清理 `_inputCapture.Text` 累积。待用户验证。

