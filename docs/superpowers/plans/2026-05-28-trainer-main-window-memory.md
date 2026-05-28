# 晴练单主窗口单独记忆 Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a default-on晴练单 switch that gives `TxtSource.trainer` its own remembered main-window layout, mode, button visibility, size, and reset behavior.

**Architecture:** Add a small config-scope helper that maps selected main-window config keys to `练单场景_` keys only while trainer memory is enabled and the main window is in trainer scope. Update existing main-window save/apply paths to use the helper instead of duplicating layout logic.

**Tech Stack:** WPF, C#, .NET Framework project, existing `Config` text store, PowerShell static regression tests, existing C# test harness where practical.

---

## File Structure

- Modify `Config/Config.cs`
  - Add default `练单主窗口单独记忆 = 是`.
  - Add default `练单场景_...` keys or allow helper fallback to normal keys.
  - Add safe removal helper only if needed by reset.
- Create `UI/TrainerMainWindowConfigScope.cs`
  - Own key mapping, active-scope state, scoped get/set helpers, reset.
  - Keep pure key mapping testable.
- Modify `UI/MainWindow.xaml.cs`
  - Replace direct reads/writes for scoped keys with helper calls.
  - Enter/exit trainer scope from `LoadText`.
  - Reapply layout safely with suppression during scope switches.
  - Expose methods used by `WinTrainer` and `WinConfig`.
- Modify `WinTrainer/WinTrainer.xaml`
  - Add `主窗口单独记忆` checkbox and `重置主窗口记忆` button.
- Modify `WinTrainer/WinTrainer.xaml.cs`
  - Initialize and save the switch.
  - Notify main window on toggle.
  - Reset trainer main-window memory after confirmation.
- Modify `WinConfig/WinConfig.xaml.cs`
  - Route homepage button visibility/order config reads/writes through current main-window scope.
- Add tests:
  - `Tests/TrainerMainWindowConfigScopeTests.cs`
  - `Tests/TrainerMainWindowConfigScopeTests.ps1`
  - `Tests/TrainerMainWindowMemoryUiTests.ps1`

## Chunk 1: Scoped Config Helper

### Task 1: Write Failing Helper Tests

**Files:**
- Create: `Tests/TrainerMainWindowConfigScopeTests.cs`
- Create: `Tests/TrainerMainWindowConfigScopeTests.ps1`
- Modify: none

- [ ] **Step 1: Write the failing C# test**

Create a small executable test that references `Config.cs` and new helper API. Expected helper API:

```csharp
using System;
using TypeSunny;
using TypeSunny.UI;

public static class TrainerMainWindowConfigScopeTests
{
    public static void Main()
    {
        Config.Path = "";
        Config.dicts.Clear();
        Config.SetDefault(
            "练单主窗口单独记忆", "是",
            "窗口宽度", "966.4",
            "练单场景_窗口宽度", "620",
            "一键极简", "否",
            "练单场景_一键极简", "是");

        AssertEqual("normal key outside trainer scope", "窗口宽度",
            TrainerMainWindowConfigScope.ResolveKey("窗口宽度"));

        TrainerMainWindowConfigScope.EnterTrainerScope();
        AssertEqual("trainer key in trainer scope", "练单场景_窗口宽度",
            TrainerMainWindowConfigScope.ResolveKey("窗口宽度"));
        AssertEqual("trainer scoped value", "620",
            TrainerMainWindowConfigScope.GetString("窗口宽度"));
        AssertEqual("trainer scoped bool", true,
            TrainerMainWindowConfigScope.GetBool("一键极简"));

        Config.dicts["练单主窗口单独记忆"] = "否";
        AssertEqual("disabled maps normal key", "窗口宽度",
            TrainerMainWindowConfigScope.ResolveKey("窗口宽度"));
        AssertEqual("disabled reads normal value", "966.4",
            TrainerMainWindowConfigScope.GetString("窗口宽度"));

        Config.dicts["练单主窗口单独记忆"] = "是";
        TrainerMainWindowConfigScope.ResetTrainerScopedValues();
        AssertEqual("reset removes trainer value", false,
            Config.dicts.ContainsKey("练单场景_窗口宽度"));
        AssertEqual("reset keeps switch", "是",
            Config.GetString("练单主窗口单独记忆"));
        AssertEqual("missing trainer value falls back normal", "966.4",
            TrainerMainWindowConfigScope.GetString("窗口宽度"));
    }

    private static void AssertEqual<T>(string name, T expected, T actual)
    {
        if (!object.Equals(expected, actual))
            throw new Exception(name + ": expected [" + expected + "] got [" + actual + "]");
    }
}
```

- [ ] **Step 2: Write PowerShell compile/run wrapper**

Use existing test style: create temp dir, generate `.csproj`, link `Config/Config.cs`, `Utils/PasswordCrypto.cs` if needed, and `UI/TrainerMainWindowConfigScope.cs`.

Run command:

```powershell
powershell -ExecutionPolicy Bypass -File Tests\TrainerMainWindowConfigScopeTests.ps1
```

Expected: fails because `UI/TrainerMainWindowConfigScope.cs` does not exist.

### Task 2: Implement Helper

**Files:**
- Create: `UI/TrainerMainWindowConfigScope.cs`
- Modify: `TypeSunny.csproj`
- Modify: `Config/Config.cs`

- [ ] **Step 1: Add default switch to `ConfigList`**

Add near existing trainer/main-window settings:

```csharp
"练单主窗口单独记忆", "是",
```

- [ ] **Step 2: Implement helper**

Create static class:

```csharp
namespace TypeSunny.UI
{
    internal static class TrainerMainWindowConfigScope
    {
        public const string EnabledConfigKey = "练单主窗口单独记忆";
        public const string Prefix = "练单场景_";

        private static bool _isTrainerScopeActive;
        private static readonly HashSet<string> ScopedKeys = new HashSet<string>
        {
            "窗口高度", "窗口宽度", "窗口坐标X", "窗口坐标Y",
            "一键极简", "一键极简后窗口高度",
            "成绩面板展开", "展开窗口高度",
            "发文区跟打区比例", "成绩区高度比例",
            "首页功能按钮顺序",
            "显示首页文来", "显示首页练单", "显示首页晴双拼", "显示首页赛文",
            "显示首页设置", "显示首页本地文章", "显示首页重打",
            "显示首页剪贴板载文", "显示首页群载文", "显示首页选群"
        };

        public static bool IsTrainerScopeActive { get { return _isTrainerScopeActive; } }
        public static bool IsEnabled { get { return Config.GetBool(EnabledConfigKey); } }

        public static void EnterTrainerScope() { _isTrainerScopeActive = true; }
        public static void ExitTrainerScope() { _isTrainerScopeActive = false; }

        public static string ResolveKey(string key)
        {
            if (!_isTrainerScopeActive || !IsEnabled || !ScopedKeys.Contains(key))
                return key;
            return Prefix + key;
        }

        public static string GetString(string key)
        {
            string scopedKey = ResolveKey(key);
            if (scopedKey != key && Config.dicts.ContainsKey(scopedKey) && Config.dicts[scopedKey] != "")
                return Config.dicts[scopedKey];
            return Config.GetString(key);
        }

        // GetBool/GetDouble/Set overloads/SetRaw/ResetTrainerScopedValues/IsScopedKey
    }
}
```

Implement `Set(string key, bool/int/double/string)`, `SetRaw(string key, string value)`, `ResetTrainerScopedValues()`, `IsScopedKey(string key)`, and `GetAllTrainerScopedKeys()`.

- [ ] **Step 3: Add compile include**

Add to `TypeSunny.csproj`:

```xml
<Compile Include="UI\TrainerMainWindowConfigScope.cs" />
```

- [ ] **Step 4: Run helper test**

Run:

```powershell
powershell -ExecutionPolicy Bypass -File Tests\TrainerMainWindowConfigScopeTests.ps1
```

Expected: PASS.

## Chunk 2: Main Window Scope Switching

### Task 3: Write Failing Static MainWindow Tests

**Files:**
- Create: `Tests/TrainerMainWindowMemoryUiTests.ps1`

- [ ] **Step 1: Add assertions**

Assert:

- `MainWindow.xaml.cs` contains `SyncTrainerMainWindowConfigScope(source);` after `StateManager.txtSource = source;`.
- `RunWindowResizeCompletedWork()` calls `TrainerMainWindowConfigScope.Set("窗口宽度"` and `TrainerMainWindowConfigScope.Set("一键极简后窗口高度"`.
- `SaveDisplayInputRatio()` writes `TrainerMainWindowConfigScope.SetRaw("发文区跟打区比例"` and `TrainerMainWindowConfigScope.SetRaw("成绩区高度比例"`.
- `ApplyHomeToolbarSettings()` reads homepage visibility through scoped helper.
- one-key compact reads `TrainerMainWindowConfigScope.GetBool(SuperCompactModeConfigKey)`.
- reset entry method exists: `public void ResetTrainerMainWindowMemory()`.

- [ ] **Step 2: Run and verify failure**

Run:

```powershell
powershell -ExecutionPolicy Bypass -File Tests\TrainerMainWindowMemoryUiTests.ps1
```

Expected: FAIL because main window is not wired.

### Task 4: Wire MainWindow Reads/Writes

**Files:**
- Modify: `UI/MainWindow.xaml.cs`

- [ ] **Step 1: Add local scoped helpers**

Add private wrappers to reduce edit noise:

```csharp
private static string ScopedConfigString(string key) => TrainerMainWindowConfigScope.GetString(key);
private static bool ScopedConfigBool(string key) => TrainerMainWindowConfigScope.GetBool(key);
private static double ScopedConfigDouble(string key) => TrainerMainWindowConfigScope.GetDouble(key);
private static void SetScopedConfig(string key, bool value) => TrainerMainWindowConfigScope.Set(key, value);
private static void SetScopedConfig(string key, double value, int fraction = -1) => TrainerMainWindowConfigScope.Set(key, value, fraction);
private static void SetScopedConfigRaw(string key, string value) => TrainerMainWindowConfigScope.SetRaw(key, value);
```

- [ ] **Step 2: Add scope switch methods**

Implement:

```csharp
private void SyncTrainerMainWindowConfigScope(TxtSource source)
{
    if (source == TxtSource.unchange)
        return;

    bool shouldUseTrainerScope = source == TxtSource.trainer && TrainerMainWindowConfigScope.IsEnabled;
    if (shouldUseTrainerScope == TrainerMainWindowConfigScope.IsTrainerScopeActive)
        return;

    SaveCurrentMainWindowScopedState();
    BeginSuppressWindowSizeChangeUpdates();
    try
    {
        if (shouldUseTrainerScope)
            TrainerMainWindowConfigScope.EnterTrainerScope();
        else
            TrainerMainWindowConfigScope.ExitTrainerScope();

        ApplyScopedMainWindowState();
    }
    finally
    {
        EndSuppressWindowSizeChangeUpdatesLater();
    }
}
```

Also expose:

```csharp
public void RefreshTrainerMainWindowMemoryMode()
public void ResetTrainerMainWindowMemory()
```

- [ ] **Step 3: Call from `LoadText`**

After `StateManager.txtSource = source;`, call:

```csharp
SyncTrainerMainWindowConfigScope(source);
```

- [ ] **Step 4: Replace scoped reads**

Replace direct reads for scoped keys in main-window layout paths:

- constructor initial width/height/position can remain normal at startup.
- `InitDisplay()` window `Height`/`Width`.
- `ApplyDisplayInputRatio()`.
- `ApplyHomeToolbarSettings()`.
- `ApplySuperCompactModeLayout()`, `CaptureSuperCompactLayoutSnapshot()`, `UpdateMainContextMenuVisibility()`.
- results panel init paths around `成绩面板展开`, `展开窗口高度`, `成绩区高度比例`.

Use `ScopedConfig...` wrappers.

- [ ] **Step 5: Replace scoped writes**

Replace writes in:

- `RunWindowResizeCompletedWork()`.
- `SaveDisplayInputRatio()`.
- `expanded(...)` and `expd_Collapsed(...)`.
- `ApplySuperCompactModeLayout(false)` restore path.
- `CollapseResultsPanelLayout(...)` writing `展开窗口高度`.
- menu click for one-key compact.
- homepage toolbar order normalization.

- [ ] **Step 6: Save current state before switching/closing**

Implement `SaveCurrentMainWindowScopedState()`:

- width, height, left, top.
- if super compact, write `一键极简后窗口高度`; otherwise write `窗口高度`.
- write `窗口宽度`, `窗口坐标X`, `窗口坐标Y`.
- call `SaveDisplayInputRatio()` if loaded and not suppressed.

Call from `Window_Closing` before final config write.

- [ ] **Step 7: Re-run static test**

Run:

```powershell
powershell -ExecutionPolicy Bypass -File Tests\TrainerMainWindowMemoryUiTests.ps1
```

Expected: PASS.

## Chunk 3: 晴练单 UI

### Task 5: Add Failing UI Assertions

**Files:**
- Modify: `Tests/TrainerMainWindowMemoryUiTests.ps1`

- [ ] **Step 1: Add WinTrainer assertions**

Assert:

- `WinTrainer.xaml` contains `CbTrainerMainWindowMemory`.
- `WinTrainer.xaml` contains text `主窗口单独记忆`.
- `WinTrainer.xaml` contains `BtnResetTrainerMainWindowMemory`.
- `WinTrainer.xaml.cs` reads `练单主窗口单独记忆`.
- `WinTrainer.xaml.cs` calls `RefreshTrainerMainWindowMemoryMode()`.
- `WinTrainer.xaml.cs` calls `ResetTrainerMainWindowMemory()`.

- [ ] **Step 2: Run and verify failure**

Run static test. Expected: FAIL.

### Task 6: Implement 晴练单 Controls

**Files:**
- Modify: `WinTrainer/WinTrainer.xaml`
- Modify: `WinTrainer/WinTrainer.xaml.cs`

- [ ] **Step 1: Add controls**

In the toolbar, after `CbCloseAfterSend`, add compact controls:

```xml
<CheckBox x:Name="CbTrainerMainWindowMemory" Margin="10,0,0,0" VerticalAlignment="Center"
          Checked="CbTrainerMainWindowMemory_Checked"
          Unchecked="CbTrainerMainWindowMemory_Unchecked"
          Foreground="{DynamicResource TextForeground}">
    <TextBlock Text="主窗口单独记忆" Foreground="{DynamicResource TextForeground}" FontSize="12"/>
</CheckBox>
<Button x:Name="BtnResetTrainerMainWindowMemory" Content="重置主窗口记忆" Margin="8,0,0,0"
        Click="BtnResetTrainerMainWindowMemory_Click" Style="{StaticResource ModernButtonStyle}"/>
```

- [ ] **Step 2: Initialize switch**

In config initialization after `CbCloseAfterSend`:

```csharp
CbTrainerMainWindowMemory.IsChecked = Config.GetBool(TrainerMainWindowConfigScope.EnabledConfigKey);
```

- [ ] **Step 3: Add handlers**

```csharp
private void SaveTrainerMainWindowMemorySetting()
{
    if (!CfgInit) return;
    Config.Set(TrainerMainWindowConfigScope.EnabledConfigKey, CbTrainerMainWindowMemory.IsChecked == true);
    MainWindow.Current?.RefreshTrainerMainWindowMemoryMode();
}
```

Reset:

```csharp
private void BtnResetTrainerMainWindowMemory_Click(object sender, RoutedEventArgs e)
{
    var result = MessageBox.Show("确定要清空练单场景下的主窗口记忆吗？", "重置确认", MessageBoxButton.YesNo, MessageBoxImage.Question);
    if (result != MessageBoxResult.Yes) return;
    MainWindow.Current?.ResetTrainerMainWindowMemory();
}
```

- [ ] **Step 4: Re-run static test**

Expected: PASS.

## Chunk 4: Settings Page Scope

### Task 7: Write Failing Settings Assertions

**Files:**
- Modify: `Tests/TrainerMainWindowMemoryUiTests.ps1`

- [ ] **Step 1: Add assertions**

Assert `WinConfig.xaml.cs` contains:

- `TrainerMainWindowConfigScope.GetString`
- `TrainerMainWindowConfigScope.GetBool`
- `TrainerMainWindowConfigScope.Set`
- scoped usage in homepage settings save/read functions.

- [ ] **Step 2: Run and verify failure**

Expected: FAIL until settings page is wired.

### Task 8: Wire Homepage Settings in WinConfig

**Files:**
- Modify: `WinConfig/WinConfig.xaml.cs`

- [ ] **Step 1: Locate homepage settings helpers**

Find methods that build and save homepage button order/visibility.

- [ ] **Step 2: Use current main-window scope**

Where homepage values are read:

```csharp
TrainerMainWindowConfigScope.GetBool(key)
TrainerMainWindowConfigScope.GetString(key)
```

Where homepage values are saved:

```csharp
TrainerMainWindowConfigScope.Set(key, value)
TrainerMainWindowConfigScope.SetRaw(key, value)
```

This edits current scope only; no extra UI is added.

- [ ] **Step 3: Refresh main window toolbar**

Keep existing `RefreshMainWindowHomeToolbar()` call after writes.

- [ ] **Step 4: Re-run static test**

Expected: PASS.

## Chunk 5: Build and Regression

### Task 9: Full Verification

**Files:**
- None unless failures require fixes.

- [ ] **Step 1: Run focused tests**

```powershell
powershell -ExecutionPolicy Bypass -File Tests\TrainerMainWindowConfigScopeTests.ps1
powershell -ExecutionPolicy Bypass -File Tests\TrainerMainWindowMemoryUiTests.ps1
```

Expected: both PASS.

- [ ] **Step 2: Run related existing tests**

```powershell
powershell -ExecutionPolicy Bypass -File Tests\HomeToolbarSettingsTests.ps1
powershell -ExecutionPolicy Bypass -File Tests\SettingsAutoSaveTests.ps1
powershell -ExecutionPolicy Bypass -File Tests\TrainerAutoSendPolicyTests.ps1
```

Expected: PASS.

- [ ] **Step 3: Build**

Use the repo’s existing build command. If no wrapper exists:

```powershell
msbuild TypeSunny.sln /p:Configuration=Debug /m
```

Expected: build succeeds.

- [ ] **Step 4: Manual smoke checklist**

Run app and verify:

- Open 晴练单, send/load练单 text. Main window enters trainer scope.
- Toggle one-key compact in trainer; load non-trainer text; compact state restores to normal scope.
- Return to trainer; trainer compact state returns.
- Resize main window in trainer; load non-trainer; normal size returns.
- Hide homepage button in trainer via settings; load non-trainer; normal button visibility returns.
- Click reset in 晴练单; trainer-specific main-window layout returns to inherited normal settings.

- [ ] **Step 5: Review diff**

```powershell
git diff --stat
git diff -- Config/Config.cs UI/MainWindow.xaml.cs UI/TrainerMainWindowConfigScope.cs WinTrainer/WinTrainer.xaml WinTrainer/WinTrainer.xaml.cs WinConfig/WinConfig.xaml.cs Tests/TrainerMainWindowConfigScopeTests.cs Tests/TrainerMainWindowConfigScopeTests.ps1 Tests/TrainerMainWindowMemoryUiTests.ps1
```

Expected: changes are limited to scoped config behavior and tests.
