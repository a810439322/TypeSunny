# 词提字提尾码角标 Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a left-top tail-code badge for word-level and character-level hints when inline code display is disabled, while preserving the current full inline code display path.

**Architecture:** Keep the existing `MainWindow.CreateDisplayElement` entry point, but split it into full-code and tail-badge branches. Extract the tail-code parsing into a pure helper so the behavior can be unit-tested without WPF. Gate paginator spacing and input-capture offsets on full inline code display only, not on the new badge mode.

**Tech Stack:** C# 9, .NET Framework 4.8, WPF, existing `Config`, `MainWindow`, `CiTiHelper`, and `ZiTiHelper`

---

## File map

- Modify: `Config/Config.cs`
  - Add `词提尾码角标` and `字提尾码角标` defaults.
- Modify: `WinConfig/WinConfig.xaml.cs`
  - Add the two new settings items to the 词提 and 字提 sections.
- Modify: `UI/MainWindow.xaml.cs`
  - Split code presentation into full-code and tail-badge branches.
  - Add the new tail-code helper usage and separate full-display gating.
- Modify: `UI/Modes/CopybookMode.cs`
  - Use full-display gating only when deciding IME / composition offsets.
- Modify: `UI/Modes/TracingMode.cs`
  - Same offset gating as copybook mode.
- Modify: `Core/Paginator.cs`
  - Keep line-height inflation bound to full inline code display only.
- Create: `Utils/CodeDisplayHelper.cs`
  - Pure helper to extract the tail badge text from raw code strings.
- Create: `Tests/CodeDisplayHelperTests.cs`
  - Minimal helper tests for tail badge parsing.
- Create: `Tests/CodeDisplayHelperTests.ps1`
  - Loads the helper and test class, runs the test harness.

## Chunk 1: Tail badge helper

### Task 1: Write the failing helper test

**Files:**
- Create: `Tests/CodeDisplayHelperTests.cs`
- Create: `Tests/CodeDisplayHelperTests.ps1`

- [ ] **Step 1: Write the failing test**

Add a tiny harness that asserts:

```csharp
AssertEqual("2", CodeDisplayHelper.TryGetTailBadgeText("rm2"));
AssertEqual("0", CodeDisplayHelper.TryGetTailBadgeText("okvivi0"));
AssertEqual("", CodeDisplayHelper.TryGetTailBadgeText("zg_"));
AssertEqual("", CodeDisplayHelper.TryGetTailBadgeText("abcd"));
AssertEqual("3", CodeDisplayHelper.TryGetTailBadgeText("abc3·说明"));
```

- [ ] **Step 2: Run the test and watch it fail**

Run:

```powershell
& .\Tests\CodeDisplayHelperTests.ps1
```

Expected: fail because `CodeDisplayHelper` does not exist yet.

- [ ] **Step 3: Implement the minimal helper**

Create `Utils/CodeDisplayHelper.cs` with a single pure method that strips the `·` suffix, trims, and returns the trailing selection digit when present.

- [ ] **Step 4: Run the test and watch it pass**

Run:

```powershell
& .\Tests\CodeDisplayHelperTests.ps1
```

Expected: all helper assertions pass.

## Chunk 2: Presentation and settings

### Task 2: Add the new config and settings controls

**Files:**
- Modify: `Config/Config.cs`
- Modify: `WinConfig/WinConfig.xaml.cs`

- [ ] **Step 1: Add the failing config expectations**

Add text-based regression checks that the new config keys and settings labels exist.

- [ ] **Step 2: Run them and watch them fail**

Run the relevant PS assertions.

- [ ] **Step 3: Add the minimal config and UI entries**

Add the default values in `Config.cs` and the new items in the 词提 / 字提 sections of `WinConfig`.

- [ ] **Step 4: Run the checks again**

Expected: the new settings strings are present.

### Task 3: Split the display rendering

**Files:**
- Modify: `UI/MainWindow.xaml.cs`

- [ ] **Step 1: Write the failing integration expectation**

Add a text-level assertion that `MainWindow` now contains a separate full-display branch and a tail-badge branch.

- [ ] **Step 2: Run the check and watch it fail**

Expected: fail until the new helper methods exist.

- [ ] **Step 3: Implement the split**

Add a full-code branch that preserves the current vertical stack behavior and a tail-badge branch that returns a `Grid` with the base `TextBlock` plus a small top-left tail-code `TextBlock`.

- [ ] **Step 4: Verify the existing code-label progress path remains attached to full display only**

Keep `TextInfo.CodeLabels` and `UpdateCodeLabelProgress` for full inline display only.

## Chunk 3: Layout consumers

### Task 4: Gate pagination and offsets on full display only

**Files:**
- Modify: `Core/Paginator.cs`
- Modify: `UI/Modes/CopybookMode.cs`
- Modify: `UI/Modes/TracingMode.cs`

- [ ] **Step 1: Add regression checks**

Assert that the new tail-badge config does not appear in paginator line-height logic or input-capture offset logic.

- [ ] **Step 2: Implement the gating change**

Use the full inline display helper for spacing / offset decisions, so badge mode does not alter line height or IME placement.

- [ ] **Step 3: Re-run the text checks**

Expected: only full inline display affects pagination and offsets.

## Chunk 4: Final verification

### Task 5: Run the focused tests and inspect git state

**Files:**
- Check: all modified files above

- [ ] **Step 1: Run helper tests**

Run:

```powershell
& .\Tests\CodeDisplayHelperTests.ps1
```

- [ ] **Step 2: Run the existing focused regression scripts**

Run the existing `CiTiHelperTests.ps1` and any updated config-plan text assertions.

- [ ] **Step 3: Inspect git status**

Confirm only the intended feature files changed and the user-owned `Version/*` edits remain untouched.

