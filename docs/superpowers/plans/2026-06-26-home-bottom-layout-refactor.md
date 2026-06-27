# Home Bottom Layout Refactor Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the home bottom toolbar, results toggle, results panel, and one-key compact mode use one bounded bottom-layout decision path so height changes do not fight each other.

**Architecture:** Extend `HomeBottomToolbarLayoutPolicy` from a simple mode helper into a pure layout planner. `MainWindow` will use one `ApplyHomeBottomLayout(reason, adjustWindowHeight)` entrypoint for bottom toolbar visual state and bottom-row window compensation. Results expand/collapse keeps ownership of results-panel height and calls the bottom-layout entrypoint without a second window compensation.

**Tech Stack:** WPF, C#, PowerShell static tests, existing MSBuild build.

---

## Chunk 1: Policy Model

### Task 1: Add a Pure Bottom Layout Plan

**Files:**
- Modify: `UI/HomeBottomToolbarLayoutPolicy.cs`
- Modify: `Tests/HomeBottomToolbarLayoutPolicyTests.cs`

- [ ] **Step 1: Write failing policy tests**

Add tests for:
- all bottom buttons hidden + results collapsed -> `Compact`, toolbar row `15`, border `0`, footer `15`
- all bottom buttons hidden + results expanded -> `Compact`, toolbar row `0`, border `0`, footer `15`
- visible feature button -> `Normal`, measured normal toolbar height, border `10`, footer `10`
- visible local article module -> `Normal`
- one-key compact -> `SuperCompact`, toolbar row `0`, panel hidden

- [ ] **Step 2: Run policy tests and verify RED**

Run: `.\Tests\HomeBottomToolbarLayoutPolicyTests.ps1`

Expected: FAIL because `CreatePlan` / `SuperCompact` plan members do not exist.

- [ ] **Step 3: Implement minimal policy model**

Add:
- `HomeBottomToolbarLayoutMode.SuperCompact`
- immutable `HomeBottomToolbarLayoutPlan`
- `CreatePlan(...)`

Keep existing `GetLayoutMode(...)` and `GetReservedHeight(...)` temporarily for compatibility if needed, but make new integration use `CreatePlan`.

- [ ] **Step 4: Run policy tests and verify GREEN**

Run: `.\Tests\HomeBottomToolbarLayoutPolicyTests.ps1`

Expected: PASS.

## Chunk 2: MainWindow Integration

### Task 2: Replace Scattered Bottom Toolbar State With One Entrypoint

**Files:**
- Modify: `UI/MainWindow.xaml.cs`
- Modify: `Tests/HomeToolbarResultsToggleUiTests.ps1`
- Modify: `Tests/HomeUiLabelsTests.ps1`

- [ ] **Step 1: Write failing static integration tests**

Assert:
- `ApplyHomeBottomLayout(string reason, bool adjustWindowHeight)` exists.
- `BtnToggleResults_Click` calls bottom layout with `adjustWindowHeight: false`.
- normal bottom layout no longer calls `ApplyBottomToolbarReservedHeight(DefaultNormalBottomToolbarHeight, false)` before measuring.
- `_lastBottomToolbarLayoutMode` and `_currentBottomToolbarReservedHeight` are removed.
- bottom toolbar window compensation is only in `ApplyHomeBottomLayout`.

- [ ] **Step 2: Run static integration tests and verify RED**

Run:
- `.\Tests\HomeToolbarResultsToggleUiTests.ps1`
- `.\Tests\HomeUiLabelsTests.ps1`

Expected: FAIL on the new assertions.

- [ ] **Step 3: Implement the integration**

Add:
- `ApplyHomeBottomLayout(string reason, bool adjustWindowHeight)`
- `CreateCurrentHomeBottomLayoutPlan(double normalToolbarHeight)`
- `MeasureNormalBottomToolbarHeight()`
- `ApplyNormalBottomToolbarLayout(HomeBottomToolbarLayoutPlan plan)`
- `ApplyCompactBottomToolbarLayout(HomeBottomToolbarLayoutPlan plan)`
- `ApplySuperCompactBottomToolbarLayout(HomeBottomToolbarLayoutPlan plan)`

Refactor callers:
- `ApplyHomeToolbarSettings()` calls `ApplyHomeBottomLayout("toolbar settings", adjustWindowHeight: true)`.
- `BtnToggleResults_Click()` calls `ApplyHomeBottomLayout("results toggled", adjustWindowHeight: false)` after expand/collapse.
- `RestoreSuperCompactBottomButtonRow()` calls `ApplyHomeBottomLayout("super compact restore", adjustWindowHeight: false)`.

Remove:
- `_lastBottomToolbarLayoutMode`
- `_currentBottomToolbarReservedHeight`
- `ResolvePreviousBottomToolbarLayoutMode`
- `GetCurrentBottomToolbarActualReservedHeight(HomeBottomToolbarLayoutMode? fallbackLayoutMode)` fallback based on stale cache

- [ ] **Step 4: Run static integration tests and verify GREEN**

Run:
- `.\Tests\HomeToolbarResultsToggleUiTests.ps1`
- `.\Tests\HomeUiLabelsTests.ps1`

Expected: PASS.

## Chunk 3: Verification and Reviews

### Task 3: Build, Review Twice, Then Final Verification

**Files:**
- No new production files beyond Chunk 1 and Chunk 2.

- [ ] **Step 1: Run focused tests**

Run:
- `.\Tests\HomeBottomToolbarLayoutPolicyTests.ps1`
- `.\Tests\HomeToolbarResultsToggleUiTests.ps1`
- `.\Tests\HomeUiLabelsTests.ps1`
- `.\Tests\HomeToolbarSettingsTests.ps1`
- `.\Tests\TrainerMainWindowMemoryUiTests.ps1`
- `.\Tests\ScorePanelLayoutPolicyTests.ps1`

- [ ] **Step 2: Run Debug build**

Run: `msbuild TypeSunny.csproj /p:Configuration=Debug /p:OutputPath=bin\CodexBuild\`

- [ ] **Step 3: Review pass 1**

Ask an independent reviewer to check spec compliance against this plan and the debug summary.

- [ ] **Step 4: Fix review pass 1 findings and re-run focused tests**

- [ ] **Step 5: Review pass 2**

Ask an independent reviewer to check code quality, edge cases, and regression risk.

- [ ] **Step 6: Fix review pass 2 findings and re-run final verification**

Final verification commands:
- `.\Tests\HomeBottomToolbarLayoutPolicyTests.ps1`
- `.\Tests\HomeToolbarResultsToggleUiTests.ps1`
- `.\Tests\HomeUiLabelsTests.ps1`
- `.\Tests\HomeToolbarSettingsTests.ps1`
- `.\Tests\TrainerMainWindowMemoryUiTests.ps1`
- `.\Tests\ScorePanelLayoutPolicyTests.ps1`
- `msbuild TypeSunny.csproj /p:Configuration=Debug /p:OutputPath=bin\CodexBuild\`
