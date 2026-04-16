# Copybook Speed Hint Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Allow 字帖模式 and 速度跟随提示 to be enabled together, while keeping the speed hint slightly left of the正文起点 to avoid overlapping the copybook input/composition area.

**Architecture:** Keep the existing UI update flow and avoid introducing any new timers, threads, or dispatcher hops. Remove the settings-level mutual exclusion, then reuse the existing speed-hint positioning logic with a copybook-specific horizontal offset and a small guard/helper so all UI updates remain on the current WPF UI thread path.

**Tech Stack:** C#, WPF, existing Config/MainWindow/CopybookMode architecture

---

## File map

- Modify: `WinConfig/WinConfig.xaml.cs`
  - Remove the checkbox mutual-exclusion behavior between `字帖模式` and `速度跟随提示`.
- Modify: `UI/MainWindow.xaml.cs`
  - Adjust copybook-mode speed hint positioning.
  - Prefer consolidating duplicated speed-hint update logic to reduce inconsistent refresh behavior.
  - Preserve existing UI-thread-only updates.
- Check only: `UI/Modes/CopybookMode.cs`
  - Confirm no extra refresh loop or thread interaction is needed.

## Chunk 1: Remove settings mutual exclusion

### Task 1: Let both options stay checked

**Files:**
- Modify: `WinConfig/WinConfig.xaml.cs:551-567`

- [ ] **Step 1: Read the existing checkbox wiring**

Confirm the current `Checked` handlers that force-uncheck the opposite option are still limited to `字帖模式` and `速度跟随提示`.

- [ ] **Step 2: Write the failing regression expectation**

Manual regression expectation:
- Open settings
- Enable `字帖模式`
- Enable `速度跟随提示`
- Expected before fix: one checkbox auto-unchecks
- Expected after fix: both remain checked

- [ ] **Step 3: Remove the mutual-exclusion handlers**

Delete the `if (itemKey == "字帖模式") ... else if (itemKey == "速度跟随提示") ...` block so checkbox persistence falls back to normal config saving.

- [ ] **Step 4: Re-read the surrounding save logic**

Verify checkbox values still save through the existing generic handler and no special-case code still disables one option elsewhere.

## Chunk 2: Adjust copybook speed-hint placement safely

### Task 2: Keep speed hint in the existing UI update path

**Files:**
- Modify: `UI/MainWindow.xaml.cs:694-719`
- Modify: `UI/MainWindow.xaml.cs:938-973`
- Check: `UI/Modes/CopybookMode.cs`

- [ ] **Step 1: Identify the active speed-hint update paths**

Confirm where `TbAcc` text/visibility/position are updated during normal typing and copybook-mode updates.

- [ ] **Step 2: Define the regression expectation**

Manual regression expectation:
- In normal mode, speed hint behavior remains unchanged.
- In copybook mode with speed hint enabled, the speed value stays on the current line, near the left text edge, but shifted slightly left so it no longer visually sits under the composition/input area.
- No flicker, no duplicate jumps, no cross-thread UI exceptions.

- [ ] **Step 3: Implement the minimal positioning change**

Use the existing copybook branch in `UpdateSpeedFollowHint` and replace `AccLeft = 0;` with a small left offset.

Recommended first implementation:
```csharp
AccLeft = -Math.Max(8, DisplayFontSize * 0.2);
```

This keeps the hint near the content edge, avoids over-shifting at small sizes, and scales mildly with font size.

- [ ] **Step 4: Reduce duplicated speed-hint behavior if needed**

If both update sites can affect copybook mode, make them share the same copybook-specific positioning rule or route the display update through `UpdateSpeedFollowHint(...)` so there is a single source of truth.

- [ ] **Step 5: Re-check thread/refresh safety**

Verify the final implementation:
- adds no new `Timer`
- adds no new background thread work
- adds no new `Dispatcher.BeginInvoke` solely for speed hint updates
- only updates `TbAcc` from the existing UI update flow

## Chunk 3: Verify the change

### Task 3: Run project-appropriate verification and inspect the diff

**Files:**
- Modify: `WinConfig/WinConfig.xaml.cs`
- Modify: `UI/MainWindow.xaml.cs`

- [ ] **Step 1: Build with an available local tool**

Try one of:
- `msbuild TypeSunny.sln /t:Build /p:Configuration=Debug`
- `dotnet build TypeSunny.sln`

Expected: successful compile with no errors.

- [ ] **Step 2: If no build tool is available, record the exact blocker**

Document the missing command and do not claim build success.

- [ ] **Step 3: Inspect the final diff**

Review that only the intended files changed and there is no accidental refactor.

- [ ] **Step 4: Manual UI verification**

Check:
- both settings can stay enabled
- normal mode speed hint still behaves the same
- copybook mode speed hint is slightly left of the current text edge
- no obvious flicker or refresh contention during typing

- [ ] **Step 5: Optional commit after user approval**

Only commit if the user asks.
