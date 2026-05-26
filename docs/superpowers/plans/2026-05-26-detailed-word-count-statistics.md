# Detailed Word Count Statistics Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add hidden detailed word-count statistics opened from the results panel total word count, with input-time category accounting, six-level difficulty distribution, startup migration, and LiveCharts2 charts.

**Architecture:** Add a focused `Logs\DetailedWordCountLog.cs` service that owns persistence, migration, aggregation, chart view models, and input-time accounting. `MainWindow` will maintain a current `TypingWordCountContext` built during `LoadText` and call the service anywhere `CounterLog.Buffer[0]` is incremented. A new `UI\WinDetailedWordCountStatistics` window renders one-page charts and tables.

**Tech Stack:** WPF on .NET Framework 4.8, Newtonsoft.Json, LiveCharts2 (`LiveChartsCore.SkiaSharpView.WPF`), existing PowerShell/C# test style.

---

## Chunk 1: Core Statistics Service

### Task 1: Add failing logic tests

**Files:**
- Create: `Tests\DetailedWordCountLogTests.cs`
- Create: `Tests\DetailedWordCountLogTests.ps1`
- Modify later: `TypeSunny.csproj`

- [ ] Write tests for:
  - first migration writes all old total words to `历史数据`
  - migration only runs once
  - article sender increments category and difficulty
  - local article increments category and difficulty
  - trainer increments category only
  - new items preserve first `StartDate`
  - pie chart model merges items beyond top 8
  - difficulty model keeps `淼 / 水 / 易 / 普 / 难 / 虐` order
- [ ] Run `powershell -ExecutionPolicy Bypass -File Tests\DetailedWordCountLogTests.ps1`
- [ ] Verify failure is due to missing `DetailedWordCountLog` types.

### Task 2: Implement core log

**Files:**
- Create: `Logs\DetailedWordCountLog.cs`
- Modify: `TypeSunny.csproj`

- [ ] Add data classes:
  - `DetailedWordCountStore`
  - `DetailedWordCountItem`
  - `TypingWordCountContext`
  - `DetailedWordCountSnapshot`
  - `DetailedWordCountSummary`
  - chart/table item view models
- [ ] Add constants for category and difficulty dimensions.
- [ ] Add storage path support, including injectable path for tests.
- [ ] Add `EnsureMigrated(int legacyTotalWords, DateTime now)`.
- [ ] Add `AddTypedWords(int words, TypingWordCountContext context, DateTime now)`.
- [ ] Add `LoadSnapshot(int totalWords, DateTime now)` with undercount calibration to `历史数据`.
- [ ] Add chart model helpers:
  - category chart top 8 plus `其他`
  - difficulty fixed-order rows
- [ ] Add debounced async save plus `Flush()`.
- [ ] Run `Tests\DetailedWordCountLogTests.ps1` and make it pass.

## Chunk 2: Input-Time Accounting Integration

### Task 3: Add static regression tests

**Files:**
- Create: `Tests\DetailedWordCountIntegrationTests.ps1`

- [ ] Assert `MainWindow` has a current detailed word-count context.
- [ ] Assert `LoadText` updates that context.
- [ ] Assert normal `TbxInput_TextChanged` calls detailed word-count helper next to `CounterLog.Buffer[0]`.
- [ ] Assert `CopybookMode` and `TracingMode` call detailed word-count helper next to their `CounterLog.Buffer[0]`.
- [ ] Run the test and verify failure.

### Task 4: Wire context and accounting

**Files:**
- Modify: `UI\MainWindow.xaml.cs`
- Modify: `UI\Modes\CopybookMode.cs`
- Modify: `UI\Modes\TracingMode.cs`

- [ ] Add `TypingWordCountContext currentWordCountContext` to `MainWindow`.
- [ ] Add `BuildWordCountContext(TxtSource source, string loadedText)`.
- [ ] In `LoadText`, after text extraction and source assignment, build and store the context.
- [ ] Add `RecordDetailedTypedWords(int words)` method on `MainWindow`.
- [ ] In `TbxInput_TextChanged`, compute added length once, add to `CounterLog.Buffer[0]`, and call `RecordDetailedTypedWords(addedLength)`.
- [ ] In `CopybookMode` and `TracingMode`, call `_main.RecordDetailedTypedWords(si.LengthInTextElements)` next to existing counter updates.
- [ ] On startup after `CounterLog.LoadDailyResults()`, call detailed migration using `CounterLog.GetSum("字数")`.
- [ ] On shutdown, call `DetailedWordCountLog.Flush()`.
- [ ] Run integration test and core tests.

## Chunk 3: Statistics Window and LiveCharts2

### Task 5: Add package and UI files

**Files:**
- Modify: `packages.config`
- Modify: `TypeSunny.csproj`
- Create: `UI\WinDetailedWordCountStatistics.xaml`
- Create: `UI\WinDetailedWordCountStatistics.xaml.cs`
- Modify: `UI\MainWindow.xaml`
- Modify: `UI\MainWindow.xaml.cs`

- [ ] Install/add `LiveChartsCore.SkiaSharpView.WPF` package references using NuGet/packages.config style.
- [ ] Add `WinDetailedWordCountStatistics` window with one-page layout:
  - top summary
  - left category pie chart and table
  - right difficulty bar chart and list
  - bottom status
- [ ] Bind LiveCharts2 series from snapshot view models.
- [ ] Apply existing theme colors.
- [ ] If chart initialization throws, show a concise full-update message.
- [ ] Add right-click menu item to `TbxResults`: `详细字数统计`.
- [ ] Click handler opens the window.

### Task 6: Add UI static tests

**Files:**
- Modify/Create: `Tests\DetailedWordCountIntegrationTests.ps1`

- [ ] Assert `TbxResults` has context menu item or code-behind menu initialization.
- [ ] Assert window XAML references LiveCharts2 controls.
- [ ] Assert chart failure path contains “全量更新”.
- [ ] Run the UI static test.

## Chunk 4: Verification

### Task 7: Build and targeted tests

**Commands:**
- `powershell -ExecutionPolicy Bypass -File Tests\DetailedWordCountLogTests.ps1`
- `powershell -ExecutionPolicy Bypass -File Tests\DetailedWordCountIntegrationTests.ps1`
- `msbuild TypeSunny.sln /t:Restore,Build /p:Configuration=Debug /p:Platform="Any CPU"`

- [ ] Run tests fresh.
- [ ] Run build fresh.
- [ ] Inspect `git diff --stat` and key diffs.
- [ ] Report any verification failures with exact command output.
