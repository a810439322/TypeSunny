# Settings Autosave Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Convert the settings window from mixed manual/apply saving to automatic saving with lost-focus/Enter text commits and category/close fallback saves.

**Architecture:** Keep the existing `WinConfig` structure and add small helper methods for autosave, fallback scanning, and debounced main-window refresh. Reuse existing value extraction and mutual-exclusion logic so immediate saves and fallback saves follow the same rules.

**Tech Stack:** WPF/C#, existing `Config` text store, PowerShell static regression tests.

---

## File Structure

- Modify: `WinConfig/WinConfig.xaml`
  - Remove the bottom `关闭` and `应用` button area.
  - Keep the title-bar close button.
- Modify: `WinConfig/WinConfig.xaml.cs`
  - Add autosave helpers.
  - Wire generic controls and custom dynamic controls into autosave.
  - Replace close confirmation with fallback save and immediate config flush.
- Create: `Tests/SettingsAutoSaveTests.ps1`
  - Static assertions for the new wiring and removed buttons.

## Chunk 1: Regression Tests

### Task 1: Add Settings Autosave Static Tests

**Files:**
- Create: `Tests/SettingsAutoSaveTests.ps1`

- [ ] **Step 1: Write the failing test**

Create `Tests/SettingsAutoSaveTests.ps1` with assertions that inspect `WinConfig.xaml` and `WinConfig.xaml.cs`:

```powershell
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$xaml = Get-Content -Path (Join-Path $root 'WinConfig\WinConfig.xaml') -Raw
$code = Get-Content -Path (Join-Path $root 'WinConfig\WinConfig.xaml.cs') -Raw

function Assert-Contains($name, $content, $needle) {
    if (-not $content.Contains($needle)) {
        throw "$name expected to contain [$needle]"
    }
}

function Assert-NotContains($name, $content, $needle) {
    if ($content.Contains($needle)) {
        throw "$name expected not to contain [$needle]"
    }
}

Assert-NotContains 'settings xaml removes bottom apply button' $xaml 'Content="应用"'
Assert-NotContains 'settings xaml removes bottom close button' $xaml 'x:Name="Cancel"'
Assert-Contains 'category switch saves current controls first' $code 'SaveCurrentCategoryControls();'
Assert-Contains 'closing flushes autosaved config' $code 'Config.WriteConfig(0);'
Assert-NotContains 'closing no longer asks whether to save' $code '设置已修改，是否保存？'
Assert-Contains 'textboxes attach lost-focus autosave' $code 'AttachTextBoxAutoSave'
Assert-Contains 'autosave supports enter commits' $code 'Key.Enter'
Assert-Contains 'checkboxes attach autosave' $code 'AttachCheckBoxAutoSave'
Assert-Contains 'comboboxes attach autosave' $code 'AttachComboBoxAutoSave'
Assert-Contains 'color picker writes selected color immediately' $code 'SaveConfigValue(colorKey, colorHex'
Assert-Contains 'dynamic difficulty saves on selection change' $code 'SaveWenlaiDifficultySelection'
Assert-Contains 'dynamic category saves on selection change' $code 'SaveWenlaiCategorySelection'

Write-Host 'All SettingsAutoSave tests passed.'
```

- [ ] **Step 2: Run test to verify it fails**

Run: `powershell -ExecutionPolicy Bypass -File .\Tests\SettingsAutoSaveTests.ps1`

Expected: FAIL because the XAML still contains the bottom buttons and autosave helpers do not exist yet.

## Chunk 2: Autosave Wiring

### Task 2: Add Autosave Helpers and Fallback Save

**Files:**
- Modify: `WinConfig/WinConfig.xaml.cs`

- [ ] **Step 1: Add helper fields and methods**

Add near the existing fields in `WinConfig`:

```csharp
private System.Windows.Threading.DispatcherTimer _configSavedRefreshTimer;
```

Add helper methods near the current save methods:

```csharp
private void SaveControlValue(FrameworkElement control, string labelText)
{
    var key = new List<string>();
    var value = new List<string>();
    ExtractControlValue(control, labelText, key, value);
    ApplyCodeDisplayMutualExclusion(key, value);
    SaveChangedConfigValues(key, value);
}

private void SaveCurrentCategoryControls()
{
    var key = new List<string>();
    var value = new List<string>();
    foreach (var item in ContentPanel.Children)
    {
        if (!(item is FrameworkElement fe)) continue;
        if ((int)fe.GetValue(Grid.ColumnProperty) != 1) continue;
        string labelText = FindLabelInContentPanel((int)fe.GetValue(Grid.RowProperty), 0);
        if (!string.IsNullOrEmpty(labelText))
            ExtractControlValue(item, labelText, key, value);
    }
    ApplyCodeDisplayMutualExclusion(key, value);
    SaveChangedConfigValues(key, value);
}
```

Add focused control attach helpers:

```csharp
private void AttachTextBoxAutoSave(TextBox tb, string itemKey, Func<string, string> normalize = null, Func<string, bool> canSave = null)
{
    if (tb == null || tb.IsReadOnly) return;
    Action save = () =>
    {
        string value = normalize != null ? normalize(tb.Text) : tb.Text;
        if (canSave == null || canSave(tb.Text))
            SaveConfigValue(itemKey, value);
    };
    tb.LostFocus += (s, e) => save();
    tb.KeyDown += (s, e) =>
    {
        if (!tb.AcceptsReturn && e.Key == Key.Enter)
        {
            save();
            e.Handled = true;
            Keyboard.ClearFocus();
        }
    };
}
```

Add similar `AttachCheckBoxAutoSave`, `AttachComboBoxAutoSave`, `AttachAutoSave`, `ScheduleConfigSavedRefresh`, `FlushConfigSavedRefresh`, and `SaveConfigValue` helpers. `SaveConfigValue` must skip unchanged values, call `Config.Set`, apply `SaveCodeDisplayMutualExclusion`, and schedule `ConfigSaved()` through the debounce timer.

- [ ] **Step 2: Wire category-switch fallback**

Update `NavButton_Click` so it calls `SaveCurrentCategoryControls();` before `ShowCategory(categoryIndex);`.

- [ ] **Step 3: Replace close confirmation**

Replace `Window_Closing` with a fallback save:

```csharp
private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
{
    SaveCurrentCategoryControls();
    FlushConfigSavedRefresh();
    Config.WriteConfig(0);
}
```

- [ ] **Step 4: Run the failing test**

Run: `powershell -ExecutionPolicy Bypass -File .\Tests\SettingsAutoSaveTests.ps1`

Expected: still FAIL until XAML and custom controls are fully wired.

## Chunk 3: Control Coverage

### Task 3: Wire Built-in and Custom Controls

**Files:**
- Modify: `WinConfig/WinConfig.xaml.cs`

- [ ] **Step 1: Attach generic autosave in `CreateValueControl`**

Before `return valueControl;`, call:

```csharp
AttachAutoSave(itemKey, valueControl);
```

Ensure `AttachAutoSave` skips controls already handled by special events: `主题模式`, `当前Logo`, `成绩显示时间`, `当前版本`, `最新版本`, `修复安装`, `软件更新Q群`, `作者邮箱QQ`.

- [ ] **Step 2: Save colors immediately**

In `ColorButton_Click`, after `btn.Content = colorHex;`, save the selected color key:

```csharp
string colorKey = btn.Tag?.ToString();
if (!string.IsNullOrEmpty(colorKey))
    SaveConfigValue(colorKey, colorHex, scheduleRefresh: false);
```

Keep the existing custom-theme switch and theme refresh calls.

- [ ] **Step 3: Wire dynamic 文来 controls**

In `LoadDifficultyDataAsync`, after creating and initializing the difficulty combo box, add:

```csharp
cb.SelectionChanged += (s, e) => SaveWenlaiDifficultySelection(cb);
```

In `LoadCategoryDataAsync`, after creating and initializing the category combo box, add:

```csharp
cb.SelectionChanged += (s, e) => SaveWenlaiCategorySelection(cb);
```

Add helper methods that read the existing `Tag` mapping and save `文来难度` / `文来分类`.

- [ ] **Step 4: Change filter text config writes to lost-focus saves**

For filter blacklist boxes, use `AttachTextBoxAutoSave(..., RegexFilter.EncodeMultiline)`.

For dual replacement boxes, save with `SaveDualBoxes(...)` on `LostFocus`; keep `TextChanged` for preview refresh only. Update preview to read current dual boxes rather than stale config values.

- [ ] **Step 5: Run static tests**

Run: `powershell -ExecutionPolicy Bypass -File .\Tests\SettingsAutoSaveTests.ps1`

Expected: PASS.

## Chunk 4: Remove Bottom Buttons

### Task 4: Remove Manual Buttons From XAML

**Files:**
- Modify: `WinConfig/WinConfig.xaml`

- [ ] **Step 1: Remove bottom button row**

Remove the `Grid Grid.Row="2"` block containing `Cancel` and `Save`.

Change row definitions so there is no dedicated button row:

```xml
<RowDefinition Height="30"/>
<RowDefinition Height="*"/>
<RowDefinition Height="5"/>
```

Move the bottom border from `Grid.Row="3"` to `Grid.Row="2"`.

- [ ] **Step 2: Remove unused button handlers if no references remain**

Remove `Cancel_Click` and leave `Save_Click` only if tests or compatibility still need it. Prefer removing both if the compiler confirms there are no references.

- [ ] **Step 3: Run static tests**

Run: `powershell -ExecutionPolicy Bypass -File .\Tests\SettingsAutoSaveTests.ps1`

Expected: PASS.

## Chunk 5: Verification

### Task 5: Run Focused Verification

**Files:**
- Test: `Tests/SettingsAutoSaveTests.ps1`
- Test: `Tests/HomeToolbarSettingsTests.ps1`

- [ ] **Step 1: Run new settings autosave test**

Run: `powershell -ExecutionPolicy Bypass -File .\Tests\SettingsAutoSaveTests.ps1`

Expected: `All SettingsAutoSave tests passed.`

- [ ] **Step 2: Run existing homepage settings test**

Run: `powershell -ExecutionPolicy Bypass -File .\Tests\HomeToolbarSettingsTests.ps1`

Expected: `All HomeToolbarSettings tests passed.`

- [ ] **Step 3: Build if available**

Run: `msbuild TypeSunny.sln /t:Build /p:Configuration=Debug`

Expected: build succeeds. If `msbuild` is unavailable, report that and include the passing focused tests.
