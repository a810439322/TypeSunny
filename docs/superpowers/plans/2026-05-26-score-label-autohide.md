# Score Label Autohide Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Hide score item labels by default while keeping score values and copy behavior unchanged.

**Architecture:** Keep the existing score text as the source of truth for copy, parsing, logs, and sending. Add a display-only layer over the existing results textbox that renders the same lines with score labels transparent while the panel is inactive, and visible while hovered or focused.

**Tech Stack:** WPF, C#, existing PowerShell smoke tests.

---

## Chunk 1: Settings And Defaults

### Task 1: Add Config Switch

**Files:**
- Modify: `Config/Config.cs`
- Modify: `WinConfig/WinConfig.xaml.cs`
- Test: `Tests/ScorePanelPresentationTests.ps1`

- [ ] Add a failing presentation test that expects the `成绩` category to include `失焦后自动隐藏成绩区文字`.
- [ ] Add a failing presentation/default test that expects the config key to default to enabled.
- [ ] Add the config key with default `是`.
- [ ] Add the key to the `成绩` category so it renders as a generated checkbox.
- [ ] Run `powershell -ExecutionPolicy Bypass -File Tests/ScorePanelPresentationTests.ps1`.

## Chunk 2: Display-Only Label Hiding

### Task 2: Add Results Display Overlay

**Files:**
- Modify: `UI/MainWindow.xaml`
- Modify: `UI/MainWindow.xaml.cs`
- Test: `Tests/ScorePanelPresentationTests.ps1`

- [ ] Add a failing presentation test that expects a display-only `RichTextBox` overlay and hover/focus event hooks.
- [ ] Add a failing test that expects copy logic to read from `TbxResults.Text`.
- [ ] Add the display-only overlay above `TbxResults`, with the original textbox still present for source text, caret, and copy logic.
- [ ] Render score lines into the overlay, making all known score item prefixes transparent when the setting is enabled and the results panel is inactive.
- [ ] Show labels on mouse enter or keyboard focus, hide again on mouse leave plus lost focus.
- [ ] Keep load-more and copy-tip text visible.
- [ ] Run targeted tests.

## Chunk 3: Verification

### Task 3: Verify Behavior

**Files:**
- Test: `Tests/ScorePanelPresentationTests.ps1`
- Test: existing score layout tests if affected

- [ ] Run `powershell -ExecutionPolicy Bypass -File Tests/ScorePanelPresentationTests.ps1`.
- [ ] Run `powershell -ExecutionPolicy Bypass -File Tests/ScorePanelLayoutPolicyTests.ps1`.
- [ ] Build the project if the local toolchain is available.
