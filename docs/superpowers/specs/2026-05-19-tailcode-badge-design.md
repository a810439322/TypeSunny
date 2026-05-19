---
date: 2026-05-19
topic: 词提字提尾码角标
status: draft
---

# 词提字提尾码角标设计

## 背景

当前词提/字提的编码显示只有一种主呈现：打开“编码下显”时，`MainWindow.CreateDisplayElement` 会把每个发文字 `TextBlock` 包成竖向 `StackPanel`，上面是字，下面是完整编码。关闭“编码下显”后，发文区只显示原字，候选尾码数字也随之消失。

用户希望关闭词提/字提编码下显后，仍能把选重尾码数字显示在字词左上角，并且这个数字要跟字绑定，随字号放大缩小、换页重绘、显隐切换一起工作。

## 目标

1. 词提和字提都支持“尾码角标”。
2. 编码下显开启时保持现有完整下显行为，不叠加角标。
3. 编码下显关闭时，如果尾码角标开启，则只显示选重尾码数字。
4. 角标默认放在字的左上角，和字作为一个整体参与 `WrapPanel` 排版。
5. 角标随发文区字号、换页、重绘、字帖/临摹/贪吃蛇等模式刷新。
6. 无选重尾码时不显示角标。

## 非目标

- 不改变词提分词算法、字提方案加载逻辑。
- 不改变现有完整编码下显的进度着色能力。
- 不新增 Canvas 浮动层来单独定位角标。
- 不为字提新增独立颜色设置；字提角标先使用发文区字体色。
- 不改变成绩统计里的“选重”计算。

## 用户确认的决策

| 决策点 | 选择 |
|---|---|
| 显示内容 | 选重尾码数字，不是选重次数 |
| 覆盖范围 | 词提和字提都做 |
| 默认位置 | 左上角 |
| 可视化评审 | 文字设计，不开浏览器伴随 |
| 编码下显开启时 | 保持完整下显优先，不显示角标 |
| 角标颜色 | 词提沿用词提选重色；字提用发文区字体色 |

## 配置

新增两个布尔配置项，默认开启：

```text
"词提尾码角标", "是"
"字提尾码角标", "是"
```

设置入口放在原有分类里：

- “词提”分类：放在 `词提编码下显` 后面，文案 `词提尾码角标`。
- “字提”分类：放在 `字提编码下显` 后面，文案 `字提尾码角标`。

交互规则：

1. `词提编码下显` / `字提编码下显` 仍按现有逻辑互斥。
2. 尾码角标开关不强制互斥。两者都开启时按现有来源优先级解析：词提优先，字提其次。
3. 如果同一来源的“编码下显”开启，完整下显优先，尾码角标暂时不显示。
4. 如果用户关闭尾码角标，则该来源在关闭编码下显后也不显示数字。

## 渲染方案

沿用 `UI/MainWindow.xaml.cs` 里的 `CreateDisplayElement(TextBlock textBlock, int globalIndex)` 作为唯一入口，但把它拆成三种返回：

```text
None            -> 直接返回原 TextBlock
FullInlineCode  -> 现有竖向 StackPanel：字 + 完整编码
TailBadge       -> 新 Grid：字 + 左上角尾码 TextBlock
```

### 1. 完整下显保持不变

完整下显继续使用现有结构：

```text
StackPanel(Vertical)
  ├── 原字 TextBlock
  └── 编码 TextBlock
```

`TextInfo.CodeLabels` 和 `UpdateCodeLabelProgress` 继续只服务完整下显。角标模式不做编码输入进度着色。

### 2. 角标绑定在字上

角标模式使用一个自包含包装控件，例如：

```text
Grid
  ├── 原字 TextBlock
  └── 尾码 TextBlock（HorizontalAlignment=Left, VerticalAlignment=Top）
```

这个 `Grid` 作为一个整体放进 `TbDispay` 的 `WrapPanel`，所以它会自然跟随：

- 自动换行
- 换页重建
- 词提不拆行的词组 `StackPanel`
- 字帖模式光标定位
- 临摹模式原文行重建
- 贪吃蛇模式显隐和透明度

角标字号使用 `DisplayFontSize` 的比例值，建议起点为 `0.30` 到 `0.35`。角标不新增独立字号配置，保证 Ctrl+滚轮调整发文区字号时同步缩放。

### 3. 不额外抬高行距

角标是叠在字的左上角，不再像完整下显那样占一整行。因此：

- `Core/Paginator.ArrangePage` 只在完整编码下显时执行 `lineH *= 1.5`。
- 仅开启尾码角标时，不放大分页行高。
- 字帖/临摹模式里 `codeDisplayExtra` 也只在完整下显时增加；角标模式不把 IME 候选框往下顶。

## 数据解析

新增一个尾码提取 helper，输入是现有编码文本，输出尾码数字：

```text
TryGetTailBadgeText(rawCode) -> string
```

规则：

1. `null`、空白返回空。
2. 如果包含 `·`，只取 `·` 前面的部分，和现有字提完整下显逻辑一致。
3. 去掉首尾空白。
4. 只识别结尾的选重数字 `2-9` 或 `0`。
5. 结尾是 `_` 或非数字时不显示。
6. 如果尾部出现多个数字，仅显示最后一个选重数字，和 `CiTiHelper.SelectKeys` 的尾码语义一致。

示例：

| 原始编码 | 角标 |
|---|---|
| `rm2` | `2` |
| `okvivi0` | `0` |
| `zg_` | 不显示 |
| `a_` | 不显示 |
| `abcd` | 不显示 |

词提数据来源：

- 继续使用 `CiTiHelper.GetCodeForChar(globalIndex)`。
- 多字词只在词首显示尾码，和现有 `GetCodeForChar` “只在首字返回编码”的行为一致。
- 角标颜色使用 `GetCodeDisplayColor(globalIndex)`，因此非首选候选沿用 `词提选重色`。

字提数据来源：

- 继续使用 `ZiTiHelper.GetZiTi(TextInfo.Words[globalIndex])`。
- 用同一个尾码提取 helper 解析尾部数字。
- 角标颜色使用 `Colors.DisplayForeground`，也就是发文区字体色。

## 显示来源解析

新增内部解析方法，避免继续用 `IsCodeDisplayEnabled()` 同时代表完整下显和角标：

```text
GetCodePresentation(globalIndex):
  1. 如果词提完整下显可用 -> FullInlineCode + 词提完整码
  2. 如果字提完整下显可用 -> FullInlineCode + 字提完整码
  3. 如果词提尾码角标可用且存在尾码 -> TailBadge + 词提尾码
  4. 如果字提尾码角标可用且存在尾码 -> TailBadge + 字提尾码
  5. 否则 None
```

完整下显“可用”的条件沿用现在逻辑：对应开关为“是”、对应功能启用、方案非空。

尾码角标“可用”的条件为：对应尾码角标开关为“是”、对应功能启用、方案非空、完整下显未命中、尾码提取结果非空。

## 影响面

### `UI/MainWindow.xaml.cs`

主要改动：

- 拆分 `CreateDisplayElement`。
- 新增 `CreateFullInlineCodeElement`。
- 新增 `CreateTailBadgeElement`。
- 新增尾码提取 helper。
- 把 `IsCodeDisplayEnabled()` 语义收窄为“完整下显是否启用”，或新增 `IsFullCodeDisplayEnabled()` 供分页和字帖定位使用。
- `UpdateCodeLabelProgress` 只在完整下显模式下工作。

### `Core/Paginator.cs`

当前分页只要 `词提编码下显` 或 `字提编码下显` 开启就放大行高。保留这个行为，但不要把新角标开关纳入行高放大。

### `UI/Modes/CopybookMode.cs` 和 `UI/Modes/TracingMode.cs`

这两个模式当前用 `_main.IsCodeDisplayEnabled()` 决定 `codeDisplayExtra`。改为使用完整下显判断，避免角标模式错误下移 IME 候选框和未上屏编码提示。

### `WinConfig/WinConfig.xaml.cs`

在词提、字提分类里新增两个设置项。保存逻辑不需要把尾码角标纳入现有编码下显互斥逻辑。

### `Core/TextInfo.cs`

`CodeLabels` 保持原用途。角标不需要新增全局列表，避免后续状态同步复杂化。

## 错误处理和边界

| 场景 | 行为 |
|---|---|
| 方案为空 | 不显示角标 |
| 功能未启用 | 不显示角标 |
| 编码为空 | 不显示角标 |
| 编码结尾不是 `2-9/0` | 不显示角标 |
| 完整编码下显开启 | 完整下显优先，不显示角标 |
| 词提和字提角标都开启 | 词提优先，字提其次 |
| 多字词 | 只在首字显示 |
| 标点 | 通常无数字尾码，不显示 |
| Ctrl+滚轮调整发文区字号 | 角标随 `DisplayFontSize` 比例同步缩放 |

## 测试

### 轻量代码级测试

新增或扩展 PowerShell 文本断言：

1. `Config\Config.cs` 包含 `词提尾码角标` 和 `字提尾码角标` 默认值。
2. `WinConfig\WinConfig.xaml.cs` 的词提/字提分类包含两个新配置项。
3. `MainWindow.xaml.cs` 中完整下显和尾码角标使用不同判断方法。
4. `Paginator.cs` 不引用尾码角标配置做行高放大。
5. `CopybookMode.cs` / `TracingMode.cs` 使用完整下显判断计算 `codeDisplayExtra`。

### helper 行为测试

如果尾码提取 helper 做成 `internal static`，增加简单测试覆盖：

| 输入 | 期望 |
|---|---|
| `rm2` | `2` |
| `okvivi0` | `0` |
| `zg_` | 空 |
| `a_` | 空 |
| `abcd` | 空 |
| `abc3·说明` | `3` |

### 手动验证

1. 词提编码下显开启：仍显示完整编码，角标不显示。
2. 词提编码下显关闭、词提尾码角标开启：有选重尾码的词首显示左上角数字。
3. 字提编码下显关闭、字提尾码角标开启：有选重尾码的字显示左上角数字。
4. 关闭对应尾码角标：数字消失。
5. Ctrl+滚轮调整发文区字号：角标跟随缩放。
6. 字帖模式和临摹模式：角标跟字走，IME 候选框不因角标额外下移。
7. 贪吃蛇模式：角标随对应字一起显隐/透明。

