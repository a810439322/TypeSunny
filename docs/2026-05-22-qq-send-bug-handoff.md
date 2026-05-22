# QQ 自动发送不稳定 BUG 交接文档

日期：2026-05-22  
项目路径：`E:\nas同步\项目代码\SynologyDrive\TypeSunny`

## 结论先行

这个 BUG 不是单纯的“延迟不够”，也不是只找不到 QQ 窗口。日志显示：当鼠标悬停在 QQ 左侧会话列表里的其他对话上时，QQ 会触发悬停预览或弹出/激活独立聊天窗口；随后自动化里的物理鼠标点击会打到错误的 QQ UI 上，导致焦点跑到别的群，粘贴不稳定，甚至发错群或无法回到目标群。

当前源码已经做过多轮补丁，但没有经过用户环境的最终验证。最后一次用户日志仍然是旧运行版本产生的，日志里没有出现新补丁的 `clear hover before click`，所以不能把当前源码视为已修复。

## 用户可见现象

- 打完一段后，程序要往 QQ 群发“成绩”和“下一段文章”。
- 经常只发出去一条，或者两条都发不出去。
- 切到 QQ 后会闪一下，又跳回晴跟打，随后卡住。
- 粘贴前焦点可能已经回到晴跟打输入框。
- 鼠标悬停在 QQ 左侧其他对话上时，更容易出问题。
- 严重时会把鼠标悬停的那个群点出来一个独立窗口，之后自动化再也回不到目标群。

## 关键日志证据

日志文件位置：`bin\Debug\QQ发送调试.log`

用户要求：每次看完日志后删除该日志。最近一次日志已经按要求删除。

### 1. QQ 主窗口本身通常能找到

日志里多次能找到主 QQ 窗口：

```text
QQ window Find done: window=type=Pane, name="QQ", class="Chrome_WidgetWin_1",
rect=(545,73,1473,860)
```

这说明“完全找不到 QQ 窗口”不是主要根因。

### 2. 旧逻辑点击输入框时使用了容易被悬停预览影响的坐标

旧日志中出现：

```text
ActivateDocumentInput click: xFraction=0.58
ActivateDocumentInput click: xFraction=0.50
ActivateDocumentInput click: xFraction=0.64
```

这些点击是基于 QQ 的 `Document` 整体矩形算出来的，而日志里的 `Document` 范围是整个 QQ 窗口：

```text
Document rect=(545,73,1473,860)
```

也就是说，这不是一个稳定的“输入框矩形”。它依赖 QQ 当前 Chromium UI、鼠标悬停状态、弹层状态。

### 3. 点击后焦点跑到了左侧会话列表中的其他群

出问题时，点击输入区之后日志变成：

```text
after ActivateDocumentInput focused element:
type=Group,
name="092五笔正规闲聊群④ ...",
rect=(601,549,851,613)

target focus safe=False,
isDescendantOfQQ=True,
isInConversationList=True
```

这个矩形 `(601,549,851,613)` 正好在 QQ 左侧会话列表范围内：

```text
会话列表 rect=(601,165,851,856)
```

所以自动化实际点到了左侧会话项，而不是目标群 `打字` 的输入区。

### 4. 恢复目标群时，QQ 又把悬停群变成了独立窗口

后续日志：

```text
target group recovery matched child[0]: extracted="打字"
after target group recovery invoke focused element:
type=Pane,
name="092五笔正规闲聊群④",
class="Chrome_WidgetWin_1",
rect=(396,131,1116,771)
```

这个 `Chrome_WidgetWin_1` 不是主 QQ 窗口 `name="QQ"`，而是一个以群名命名的独立聊天窗口。用户观察到“如果鼠标悬停在别的对话上，就可能触发点击，把别的群点出来单独窗口”，日志与这个现象一致。

### 5. 保护逻辑曾经阻止过发错群，但没有解决根因

已有保护逻辑会在粘贴后检查焦点：

```text
after CtrlV target focus safe=False
targetNameSafe=False
focus is not in target conversation after CtrlV; skip send
```

这能降低发错群概率，但代价是消息发不出去；它只是保护栏，不是根本修复。

## 当前相关代码位置

主要文件：

- `Utils/QQHelper.cs`
- `Utils/Win32.cs`
- `UI/MainWindow.xaml.cs`
- `Tests/QQSendSerializationTests.ps1`

关键函数：

- `QQHelper.SendQQMessage(...)`
- `QQHelper.SendQQMessageHelper(...)`
- `QQHelper.SendQQMessageD(...)`
- `QQHelper.PasteAndSendMessage(...)`
- `QQHelper.ActivateDocumentInput(...)`
- `QQHelper.FindQQWindowWithRetry(...)`
- `QQHelper.FindGroupListWithRetry(...)`
- `QQHelper.IsFocusedElementSafeForTargetConversation(...)`
- `MainWindow.FocusInput()`
- `Win32.Click(...)`
- `Win32.MoveCursor(...)`
- `Win32.ClickCurrentPosition(...)`

## 已经做过的补丁

这些改动已经在源码中出现，但不等于已通过真实 QQ 场景验证：

- `SendQQMessage` 的延迟已关掉：`new Timer(..., 0, Timeout.Infinite)`。
- 单条发送内容放进 `MsgRequest.msgContent`，不再靠剪贴板当异步队列。
- 增加 `SendAutomationLock`，避免 QQ 窗口和剪贴板自动化并发。
- `FocusInput()` 增加 `QQHelper.TryDeferFocusInput("MainWindow.FocusInput")`，尝试避免晴跟打在 QQ 自动化期间抢回焦点。
- `SendMessage(...)` 改为返回 `bool`，并记录找发送按钮、按钮 enabled、invoke 的日志。
- `PasteAndSendMessage(...)` 增加重试，粘贴前后都检查目标会话焦点。
- `FindQQWindowWithRetry(...)` 和 `FindGroupListWithRetry(...)` 增加短时间重试。
- 增加 `IsFocusedElementSafeForTargetConversation(...)`：
  - 拒绝 QQ UIA 树外的焦点。
  - 拒绝落在 `会话列表` 里的焦点。
  - 拒绝右侧 pane/group 名称明显不是目标群的焦点。
- 移除了之前加过的 1200ms 延迟回焦逻辑。
- 最后一次源码补丁尝试在 `ActivateDocumentInput(...)` 里：
  - 点击前调用 `ClearQQHoverBeforeClick(...)`。
  - 先把鼠标移动到右侧安全点击点。
  - 等 `QQHoverClearDelayMs = 180`。
  - 再 `ClickCurrentPosition()`。
  - 避开旧的 `0.58` 和 `0.50` 点击点。

注意：用户最后提供的日志没有出现 `clear hover before click`，仍然出现旧 `xFraction=0.58/0.50/0.64`。这表示那次复现跑的不是最新源码产物，或者最新产物没有被用户实际调试到。

## 目前最可信的问题链路

1. 用户打完一段，触发 `MainWindow.xaml.cs` 中的 QQ 发送调用。
2. `SendQQMessage` 或 `SendQQMessageD` 开始 QQ 自动化。
3. 程序找到主 QQ 窗口 `name="QQ"`。
4. 程序找到 QQ 的 `会话列表`。
5. 如果当前就在目标群，代码尝试找底部 `Document` 作为输入区。
6. `ActivateDocumentInput` 使用物理鼠标点击激活输入区。
7. 如果鼠标原本悬停在左侧其他会话，QQ 可能已经显示悬停预览或准备打开该会话。
8. 自动化点击被 QQ 悬停 UI 劫持，焦点落到左侧其他会话或独立聊天窗口。
9. `Ctrl+V` 可能粘到错误位置，或者粘贴后发送按钮不启用。
10. 焦点保护逻辑发现不在目标群，于是跳过发送。
11. 用户看到“只发一条”“什么都没发”“闪来闪去”“粘贴不稳定”“发错群/弹出别的群窗口”。

## Claude 修复时建议重点看

### 方向 1：尽量移除物理鼠标点击依赖

当前最大不稳定点是 `ActivateDocumentInput(...)` 依赖鼠标坐标。建议优先研究能否用更确定的方式激活 QQ 输入框：

- UIA `SetFocus` 是否足够。
- 是否能通过键盘导航稳定进入输入区。
- 是否能利用 QQ 的发送按钮、输入区附近的 UIA 结构推导真实输入区，而不是用整个 `Document` 矩形。
- 是否能用 `NativeWindowHandle` + Win32 foreground/focus API 更稳定地操作主窗口。

如果必须点击，也不要基于整个 `Document` 宽高随便取比例。应从已知安全区域推导：

- 左边界必须大于 `会话列表.Right + margin`。
- y 坐标应在右侧输入区范围内，不要落到消息列表、会话列表或悬停弹层。
- 可参考日志中目标右侧区域常见矩形：`rect=(851,579,1289,800)`。
- 发送按钮常见矩形：`rect=(1191,814,1219,842)`，输入区通常在发送按钮上方或左侧。

### 方向 2：在自动化开始前主动处理 QQ 悬停/弹层状态

用户明确说“鼠标悬停位置会影响 BUG”。所以修复应把鼠标位置当成输入变量：

- 自动化开始后，立即把鼠标移出 QQ 左侧会话列表。
- 移动后用条件检查，而不是固定猜测：
  - 当前 top-level focus 是否仍为主 QQ。
  - 当前 focus 是否在 `会话列表`。
  - 是否出现非 `name="QQ"` 的 `Chrome_WidgetWin_1` 独立窗口。
- 可尝试发送 `Esc` 关闭 QQ 悬停预览/弹层，再重新定位主 QQ。
- 结束时不要无条件把鼠标恢复到 QQ 左侧会话列表内，否则下一次发送又会触发同一问题。至少要判断保存的鼠标位置是否在 `会话列表` 内。

### 方向 3：独立聊天窗口必须显式识别和隔离

日志已经出现：

```text
type=Pane, name="092五笔正规闲聊群④", class="Chrome_WidgetWin_1"
```

建议修复者增加明确策略：

- 枚举 top-level `Chrome_WidgetWin_1`。
- 主 QQ 窗口必须满足：
  - `name == "QQ"`，并且
  - 子树内存在 `name == "会话列表"`。
- 群名命名的独立窗口不能被当成主 QQ。
- 如果自动化期间出现独立窗口获得焦点：
  - 重新激活主 QQ。
  - 或关闭/最小化该独立窗口。
  - 或直接中止本次发送并记录日志，不要继续粘贴。

### 方向 4：继续保留“目标群校验”，但别把它当修复

`IsFocusedElementSafeForTargetConversation(...)` 很重要，应保留或加强。它的作用是防止发错群。

但当前用户要的是“稳定发出去”，不是“发现不安全就跳过”。所以真正的修复应发生在焦点获取和输入区激活之前。

## 建议新增/保留日志

保留这些日志点，方便用户复现后判断：

- 自动化开始时的鼠标位置。
- 鼠标是否位于 QQ 主窗口内。
- 鼠标是否位于 `会话列表` 内。
- 移动鼠标前后的坐标。
- 是否发送过 `Esc`。
- 当前 foreground/top-level 窗口名称和 class。
- 所有 top-level `Chrome_WidgetWin_1` 候选窗口。
- 选择主 QQ 窗口的理由。
- 点击输入区前的焦点元素。
- 点击输入区后的焦点元素。
- 粘贴前后的焦点安全检查。
- 发送按钮 enabled 状态。
- 若跳过发送，记录具体原因。

用户希望每次分析后删除 `bin\Debug\QQ发送调试.log`，调试流程里要遵守。

## 静态检查

已有静态测试：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tests\QQSendSerializationTests.ps1
```

它检查的是源码结构和关键保护逻辑，不是 QQ 真实 UI 端到端测试。

最近一次运行结果：

```text
All QQ send serialization tests passed.
```

不要把这个测试通过理解成 BUG 已修复。真实验证仍然需要用户在 QQ 上复现。

## 给下一个修复者的最小任务

1. 先确认用户运行的二进制确实包含最新源码。新日志里必须能看到 `clear hover before click`，否则是在调旧版本。
2. 让用户把鼠标悬停在 QQ 左侧其他会话上复现一次。
3. 看日志中：
   - 鼠标初始坐标是否在 `会话列表` 内。
   - 是否出现非主 QQ 的 `Chrome_WidgetWin_1`。
   - `ActivateDocumentInput` 后焦点是否仍落入 `会话列表`。
4. 如果仍然会弹出独立窗口，优先做“识别并隔离独立窗口”和“自动化开始前清理悬停状态”。
5. 如果不会弹窗但还是不粘贴，继续沿 `PasteAndSendMessage` 的焦点检查和发送按钮 enabled 日志排查。

## 明确不要再走的方向

- 不要再单纯加固定延迟。用户已经明确认为这不优雅，且日志证明根因不是普通延迟。
- 不要只靠“发送失败重试”。如果焦点已在错误群，重试可能继续打错对象。
- 不要把 `Document` 整个窗口矩形当成真实输入框。
- 不要删除目标群安全检查来换取“看起来能发出去”。这会提高发错群风险。
- 不要无日志地改 UIA/Win32 混合流程。这个 BUG 强依赖 QQ 状态和鼠标位置，必须靠日志确认。
