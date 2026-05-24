# 赛文模式下强制关闭词提与字提编码下显，统一字提 5 秒延迟规则

> 备注：按用户偏好（memory: feedback_plan_location），执行前会把此 plan 移到项目 `docs/` 下。当前 plan mode 仅允许编辑此路径文件，故先写在这里。

## Context

当前赛文相关辅助显示存在两个问题：

1. **5 秒延迟规则覆盖范围不全**——`UpdateZiTi`（`UI/MainWindow.xaml.cs:1659`）的 5 秒延迟判定只对 `TxtSource.raceApi` 生效。锦标赛（`jbs`）完全没限制，极速杯（`jisucup`）则在第 1644 行被"完全清空"硬切。
2. **多条字提/词提显示路径绕过了上述规则**——编码下显（`GetZiTiCodeText`、`IsFullZiTiCodeDisplayEnabled`）、字提选重数字角标（`MainWindow.xaml.cs:1161-1167`）、所有词提渲染入口（着色 / 加粗 / 编码 / 角标 / 不拆行）都**没有任何赛文模式检查**，开关一开就立即生效。

用户在 jbs 模式下打开字提开关时字提立即显示，根因就是上述路径都不受 5 秒规则约束。

**目标**：三种赛文模式（`raceApi`/`jbs`/`jisucup`）统一应用以下策略——
- **词提**：所有渲染入口强制关闭
- **字提编码下显 + 字提选重数字角标**：强制关闭
- **上方字提行 `TbkZiTi`**：保留 5 秒延迟显示（5 秒内无输入才显示）

## 设计思路

引入一个单一事实源 `StateManager.IsRaceMode()`，集中赛文模式判断，后续若赛文模式发生变化（如新增 `TxtSource` 值）只在此处改一处。

所有"是否启用词提"的判读统一改走新增门面 `IsCiTiEffective()`（= `启用词提` AND `!IsRaceMode()`），避免每个调用点 inline 复制条件。

## 关键改动

### 1. `Core/StateManager.cs`：新增 `IsRaceMode()`

```csharp
internal static bool IsRaceMode()
{
    return txtSource == TxtSource.raceApi
        || txtSource == TxtSource.jbs
        || txtSource == TxtSource.jisucup;
}
```

### 2. `UI/MainWindow.xaml.cs`：新增 `IsCiTiEffective()`

```csharp
internal bool IsCiTiEffective()
{
    return Config.GetBool("启用词提") && !StateManager.IsRaceMode();
}
```

### 3. `UI/MainWindow.xaml.cs`：批量替换"启用词提"读取点

以下行的 `Config.GetBool("启用词提")` 全部替换为 `IsCiTiEffective()`：

| 行号 | 函数 | 含义 |
|------|------|------|
| `940` | `GetCiTiForeground` | 词提着色 |
| `967` | `IsCiTiBold` | 词提加粗 |
| `1149` | `GetDisplayBadgeTextAndColor` | 词提选重角标条件 |
| `1270` | `IsCiTiNoSplitLineEnabled` | 词提不拆行 |
| `1471` | `GetTypingCodeText` | 跟打编码（词提分支） |
| `1623` | `IsFullCiTiCodeDisplayEnabled` | 词提编码下显守门员 |

**不动** `2668` 行 `ShouldLoadCiTiSegments`——保留段预计算，避免切换模式后冷启动延迟；显示层关闭即可。

### 4. `UI/MainWindow.xaml.cs`：字提编码下显 + 角标加赛文拦截

**`IsFullZiTiCodeDisplayEnabled`（1628）**：末尾追加 `&& !StateManager.IsRaceMode()`。  
**字提选重数字角标判断（1161）**：在现有条件链末尾追加 `&& !StateManager.IsRaceMode()`。

### 5. `UI/MainWindow.xaml.cs`：上方字提行 5 秒规则覆盖三种赛文模式

**`UpdateZiTi`（1635）**：
- 删除 1644-1649 的"jisucup 直接清空"分支（jisucup 改走统一的 5 秒延迟逻辑）
- 1659 行 `if (StateManager.txtSource == TxtSource.raceApi)` → `if (StateManager.IsRaceMode())`

**`StartZiTiTimer`（5520）**：5529 行启动条件 → `StateManager.IsRaceMode() && Config.GetBool("启用字提")`

**`TbxInput_TextChanged`（6738）**：`if (StateManager.txtSource == TxtSource.raceApi)` → `if (StateManager.IsRaceMode())`

### 6. `LoadText` 中 `LastInputTime` 重置（无需改动）

第 6416 行 `StateManager.LastInputTime = DateTime.Now;` 已在 `if (TextInfo.Words.Count > 0)` 内无条件执行，三种赛文模式载文都会触发，符合"载文起算 5 秒"的语义。

## 不在本次范围内

- 词提段预计算逻辑（`ShouldLoadCiTiSegments` / `CiTiHelper.SplitText`）不动
- 词提的"启用词提"开关本身不变，关闭赛文模式后正常恢复
- 不改动设置窗口（`WinConfig`）的联动逻辑，配置项默认值保持不变
- 不修改临摹模式（`TracingMode`）—— 它通过 `_main.GetDisplayForeground/IsCiTiBold` 间接调用，主入口改动后自动生效

## 验证步骤

1. **编译**：在 Rider 中 build，确认无编译错误
2. **手动验证三种赛文模式**：进入 jbs / jisucup / raceApi 各一次，在每种模式下：
   - 打开"启用字提" + "字提方案" + "字提编码下显" + "字提选重数字角标" + "启用词提" + "词提方案" + "词提编码下显"
   - 预期：每个字下方编码不显示、字旁边角标不显示、词提着色/加粗不生效
   - 上方字提行：载文后 5 秒内不显示；停手 5 秒后出现下一字字提；继续打字立即消失
3. **回归验证非赛文模式**：trainer / articlesender / unchange / changeSheng 模式下，字提词提的所有开关行为应与改动前完全一致（立即显示）
4. **极速杯回归**：原 jisucup 上的"字提完全不显示"硬切已删除，确认极速杯打字过程中字提行不会闪现（因为打字时 < 5 秒）

## 关键文件

- `Core/StateManager.cs`
- `UI/MainWindow.xaml.cs`
