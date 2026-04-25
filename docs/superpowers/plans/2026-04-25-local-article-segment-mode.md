# Local Article Segment Mode Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an independent automatic/manual segment mode for local articles, controlled from the local article manager, without routing local article page turns through Wenlai APIs.

**Architecture:** Store the local article mode in `ArticleConfig` through `ArticleManager`. `WinArticle` owns the UI for changing the mode. `MainWindow` reads the local mode only when the active source is `TxtSource.book`, while Wenlai continues to use `Config["文来换段模式"]`.

**Tech Stack:** C# 9, .NET Framework 4.8, WPF XAML, existing `ArticleConfig`, `ArticleManager`, and `StateManager`.

---

## Constraints

- Do not run Git staging, commit, reset, restore, checkout, or similar Git-mutating commands. The user will handle Git.
- This repository has no test project. Do not introduce a new test framework for this small WPF change.
- Verify by building the solution and by preserving a manual validation checklist in the final response.

## File Structure

- Modify `Article/ArticleManager.cs`: add a local article segment mode property backed by `ArticleConfig`, with default value `自动`.
- Modify `Article/WinArticle.xaml`: add a compact "换段模式" selector to the local article manager's bottom settings panel.
- Modify `Article/WinArticle.xaml.cs`: initialize the selector, save changes, and show the manual-mode user hint only on user-initiated switch.
- Modify `UI/MainWindow.xaml.cs`: respect the local article segment mode in completion handling; tighten Ctrl+P/Ctrl+O routing so local article state never falls through to Wenlai paging.
- Create `docs/superpowers/plans/2026-04-25-local-article-segment-mode.md`: this plan.

## Chunk 1: Local Article Config And UI

### Task 1: Add local segment mode config

**Files:**
- Modify: `Article/ArticleManager.cs`

- [x] **Step 1: Add constants and property**

Add constants near the existing static fields:

```csharp
private const string SegmentModeConfigKey = "本地文章换段模式";
public const string SegmentModeAuto = "自动";
public const string SegmentModeManual = "手动";
```

Add property near `SectionSize`:

```csharp
public static string SegmentMode
{
    get
    {
        string mode = ArticleConfig.GetString(SegmentModeConfigKey);
        return mode == SegmentModeManual ? SegmentModeManual : SegmentModeAuto;
    }
    set
    {
        string mode = value == SegmentModeManual ? SegmentModeManual : SegmentModeAuto;
        ArticleConfig.Set(SegmentModeConfigKey, mode);
        ArticleConfig.WriteConfig(500);
    }
}

public static bool IsManualSegmentMode
{
    get { return SegmentMode == SegmentModeManual; }
}
```

- [x] **Step 2: Add default config value**

Extend `ArticleConfig.SetDefault(...)` in the `ArticleManager` static constructor:

```csharp
"本地文章换段模式", SegmentModeAuto
```

Expected behavior: existing installs default to automatic mode.

### Task 2: Add mode selector in local article manager

**Files:**
- Modify: `Article/WinArticle.xaml`
- Modify: `Article/WinArticle.xaml.cs`

- [x] **Step 1: Add XAML selector**

In the bottom settings panel next to "每段字数", add:

```xml
<TextBlock Text="换段模式" .../>
<ComboBox x:Name="CbSegmentMode" ... SelectionChanged="CbSegmentMode_SelectionChanged">
    <ComboBoxItem Content="自动"/>
    <ComboBoxItem Content="手动"/>
</ComboBox>
```

Keep it compact and aligned with the existing panel controls.

- [x] **Step 2: Initialize selector**

In `InitControls()`, set `CbSegmentMode.SelectedIndex` based on `ArticleManager.SegmentMode`.

- [x] **Step 3: Save selector changes and show hint**

Add `CbSegmentMode_SelectionChanged`. If `AllLoaded` is false, return. Save selected mode through `ArticleManager.SegmentMode`. If selected mode is manual, show:

```text
本地文章手动换段模式：

打完当前段后不会自动发送下一段。
可按 Ctrl+P 发下一段，Ctrl+O 发上一段。
也可以点击底部 < / > 按钮翻页。
```

Expected behavior: loading a window with manual mode already selected does not show a message because `AllLoaded` is false during initialization.

## Chunk 2: Runtime Behavior And Source Isolation

### Task 3: Respect local manual mode on completion

**Files:**
- Modify: `UI/MainWindow.xaml.cs`

- [x] **Step 1: Add local score-only helper**

Near `NextAndSendArticle`, add a small helper that only sends or copies the result when `自动发送成绩` is enabled:

```csharp
private void SendLocalArticleResultOnly(string result, string qqGroupName, int delay = 0)
{
    if (!Config.GetBool("自动发送成绩") || string.IsNullOrEmpty(result))
        return;

    if (qqGroupName != "")
    {
        QQHelper.SendQQMessage(qqGroupName, result, delay, this);
    }
    else
    {
        Win32SetText(result);
        FocusInput();
    }
}
```

- [x] **Step 2: Gate automatic local paging by mode**

In the `StateManager.txtSource == TxtSource.book` branch inside `StopHelper`, read:

```csharp
bool localManualMode = ArticleManager.IsManualSegmentMode;
```

When existing code would call `NextAndSendArticle(result)` or `NextAndSendArticle()`, do this instead:

- If `localManualMode` is false, keep existing automatic behavior.
- If `localManualMode` is true, call `SendLocalArticleResultOnly(result, qqGroupName, 250 or 0)` for result-bearing paths and do not advance the article.
- For the no-result retype-complete path, do nothing in manual mode because there is no score to send and no next segment should be loaded.

Expected behavior: manual mode never calls `NextAndSendArticle`.

### Task 4: Tighten Ctrl+P/Ctrl+O routing

**Files:**
- Modify: `UI/MainWindow.xaml.cs`

- [x] **Step 1: Change Ctrl+P routing**

Use explicit source checks:

```csharp
if (StateManager.txtSource == TxtSource.articlesender)
{
    if (articleCache.HasArticle())
        LoadNextSegment();
}
else if (StateManager.txtSource == TxtSource.book)
{
    ArticleManager.NextSection();
    await SendArticle();
}
else if (ArticleManager.Title != "")
{
    ArticleManager.NextSection();
    await SendArticle();
}
```

- [x] **Step 2: Change Ctrl+O routing**

Mirror Ctrl+P with `LoadPreviousSegment()` and `ArticleManager.PrevSection()`.

Expected behavior: the only path to Wenlai paging starts with `StateManager.txtSource == TxtSource.articlesender`.

## Chunk 3: Verification

### Task 5: Build and inspect

**Files:**
- No source edits expected.

- [x] **Step 1: Build solution**

Run:

```powershell
msbuild TypeSunny.sln /p:Configuration=Debug /p:Platform="Any CPU"
```

If `msbuild` is unavailable, use the installed Visual Studio MSBuild path or `dotnet msbuild` if it supports this solution.

Expected: build exits with code 0.

- [x] **Step 2: Note build side effects**

The project's MSBuild target updates `Version/version.txt` and `Version/GeneratedVersion.cs`. If those files change during verification, report that the build generated them. Do not run Git commands to revert them.

- [x] **Step 3: Manual validation checklist**

Report these cases for the user to run in the app:

- Local article mode `自动`: finishing a segment still sends the next segment.
- Local article mode `手动`: finishing a segment sends/copies only the result.
- Load Wenlai, then load a local article, then press Ctrl+P/Ctrl+O: local pages change; Wenlai API should not be called.
- Wenlai Ctrl+P/Ctrl+O still pages through Wenlai when the current source is Wenlai.
