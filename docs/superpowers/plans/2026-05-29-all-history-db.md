# All History DB Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Record every completed typing round into `all_history.db` as observable per-round metadata plus real commit-derived unit samples, and allow current prediction profiles to be rebuilt from first-attempt history.

**Architecture:** Add a focused history store next to the existing personalization store. `all_history.db` is append-only source data; existing `profile.db` remains the fast prediction cache. Store only real commit units as samples, never split multi-character commits like `中国` into characters.

**Tech Stack:** C# .NET Framework 4.8, System.Data.SQLite, PowerShell Add-Type tests, existing personalization classes.

---

## Chunk 1: Core History Store

### Task 1: Add observable all-history persistence

**Files:**
- Create: `Personalization/AllHistoryTypingHistoryStore.cs`
- Create: `Tests/AllHistoryTypingHistoryTests.cs`
- Create: `Tests/AllHistoryTypingHistoryTests.ps1`
- Modify: `TypeSunny.csproj`

- [ ] Write failing tests proving a round inserts `texts`, `rounds`, and `unit_samples`.
- [ ] Run `powershell -ExecutionPolicy Bypass -File Tests\AllHistoryTypingHistoryTests.ps1` and confirm missing type failure.
- [ ] Implement store schema, append API, and load-first-attempt API.
- [ ] Run the test and confirm pass.

## Chunk 2: Prediction Integration

### Task 2: Feed history records from completed rounds

**Files:**
- Modify: `Personalization/PersonalScorePredictionService.cs`
- Modify: `UI/MainWindow.xaml.cs`
- Modify: `Tests/AllHistoryTypingHistoryTests.cs`

- [ ] Add test proving service writes real commit units and can replay only first attempts.
- [ ] Add service method to append history and train existing profile.
- [ ] Call it from round completion before/with `CalibrateAndTrainAsync`.
- [ ] Preserve existing async non-blocking behavior.

## Chunk 3: Display Gate

### Task 3: Remove 30% display confidence hiding

**Files:**
- Modify: `Personalization/PersonalScorePredictionFormatter.cs`
- Modify: `Tests/PersonalScorePredictionTests.cs`

- [ ] Update formatter test to expect low-confidence predictions still show.
- [ ] Remove formatter-level `MinDisplayConfidence` suppression.
- [ ] Keep score attachment threshold at `> 0.80`.

## Chunk 4: Verification

- [ ] Run `Tests\AllHistoryTypingHistoryTests.ps1`.
- [ ] Run `Tests\PersonalScorePredictionTests.ps1`.
- [ ] Run relevant build command or explain if unavailable.
