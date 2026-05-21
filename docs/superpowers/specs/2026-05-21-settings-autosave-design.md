# 设置页自动保存设计

## 背景

当前设置页混用两套保存路径：

- 普通控件只在点击底部“应用”或关闭窗口确认保存时，由 `Save_Click` / `Window_Closing` 扫描当前分类并写入。
- 部分自定义区域在控件事件中直接调用 `Config.Set(...)`，例如主题模式、Logo、首页按钮、过滤规则、成绩显示项。

这会导致用户切换设置分类时，普通控件的未应用修改被 `ShowCategory(...)` 重建页面丢弃。目标是把设置页改为统一自动保存，不再依赖底部“应用”和“关闭”按钮。

## 目标行为

1. 设置页所有可编辑项都自动保存。
2. 文本框采用“失焦或回车保存”：
   - 单行文本框：`LostFocus` 保存；按 `Enter` 保存并移走焦点。
   - 多行文本框：`LostFocus` 保存；预览类交互仍可在输入时实时刷新，但配置写入不依赖每次输入。
3. 非文本控件按自然交互立即保存：
   - `CheckBox` 勾选变化立即保存。
   - `ComboBox` 选择变化立即保存。
   - 颜色按钮确认选色后立即保存。
   - 拖拽排序在 `Drop` 完成后保存。
4. 切换分类和关闭设置窗口时做兜底保存：
   - 切换分类前扫描当前页控件并保存一次。
   - 关闭窗口前扫描当前页控件并 `Config.WriteConfig(0)` 立即落盘。
   - 不再弹“是否保存”。
5. 移除底部“应用”和“关闭”按钮。保留标题栏右上角关闭按钮。

## 非目标

- 不重构整个设置页架构。
- 不改变 `Config` 文件格式。
- 不改变设置项默认值或业务含义。
- 不调整其他窗口中的非设置页保存逻辑。

## 设计

在 `WinConfig.xaml.cs` 中新增一组小型 helper，集中处理保存和刷新：

- `SaveConfigValue(string key, string value, RefreshKind refreshKind = RefreshKind.Auto)`
  - 值没有变化时直接返回。
  - 值变化时调用 `Config.Set(...)`。
  - 复用现有词提/字提编码下显互斥规则。
  - 根据设置类型触发必要刷新。
- `SaveCurrentCategoryControls()`
  - 复用现有 `ExtractControlValue(...)` 扫描当前页值列控件。
  - 应用互斥规则后逐项调用 `SaveConfigValue(...)`。
  - 用于分类切换前和窗口关闭前的兜底。
- `AttachTextBoxAutoSave(TextBox tb, string key, Func<string, string> normalize = null)`
  - `LostFocus` 保存。
  - `KeyDown Enter` 保存单行文本框。
  - 保存后可按 key 触发刷新。
- `ScheduleConfigSavedRefresh()`
  - 对普通设置保存后防抖调用 `ConfigSaved()`，避免每次控件事件都重建主窗口。

现有已实时保存的区域改为同一 helper 保存，避免多套规则分叉。

## 刷新策略

设置写入和界面刷新分开处理：

- 主题模式、颜色、字体、Logo：保存后刷新设置窗口和主窗口相关外观。
- 首页按钮和固定模块：保存后调用 `RefreshMainWindowHomeToolbar()`。
- 成绩显示时间和成绩显示项：保存后刷新成绩显示。
- 跟打、字提、词提、文来、赛文等普通设置：保存后防抖触发 `ConfigSaved()`，由主窗口 `ReloadCfg()` 统一应用。
- 文来/赛文服务器地址：保存后走普通刷新；现有依赖方会在后续操作中读取配置，必要时由 `ReloadCfg()` 同步赛文服务器管理器。

## 控件覆盖

需要补自动保存事件的控件：

- 普通 `TextBox`：字体大小、字数、重复次数、服务器地址、签名等。
- 普通 `CheckBox`：跟打、字提、词提等分类里的布尔设置。
- 普通 `ComboBox`：字体、字提方案、词提方案、盲打模式、文来换段模式、重打跳转模式、字数模式、赛文输入法等。
- 颜色 `Button`：主题色和词提颜色。
- 动态文来控件：文来难度、文来分类。
- 自定义区域：过滤、首页、成绩显示项，统一到保存 helper。

## 错误处理

- 自动保存不向用户弹出成功提示。
- 保存失败沿用 `Config.WriteConfig` 现有容错。
- 文本框非法输入保持原有行为：能保存的按文本保存；已有范围校验的项保留校验，例如过滤最大重试次数。

## 测试

优先用静态回归测试覆盖高风险点：

- 设置页不再包含底部 `应用` / `关闭` 按钮。
- `NavButton_Click` 切换分类前调用 `SaveCurrentCategoryControls()`。
- `Window_Closing` 调用 `SaveCurrentCategoryControls()` 和 `Config.WriteConfig(0)`，且不再弹未保存确认。
- `CreateValueControl` 创建的普通 `TextBox`、`CheckBox`、`ComboBox` 有自动保存事件。
- `ColorButton_Click` 选色后直接写配置。
- 文来难度和分类动态下拉选择变化直接写配置。

再运行现有相关 PowerShell 测试，确保已有首页设置回归不被破坏。
