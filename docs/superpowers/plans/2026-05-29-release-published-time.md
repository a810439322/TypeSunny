# Release Published Time Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Allow multiple releases with the same `yyyyMMdd` version to be detected by comparing release publication time, while showing publication time to users in Beijing time.

**Architecture:** Add a small pure `ReleaseIdentity` helper for version/time comparison, identity generation, ignore matching, and Beijing-time formatting. Keep network/API parsing and config persistence in `VersionManager`, and pass release metadata to `Updater.exe` so successful updates persist the installed release identity.

**Tech Stack:** C#/.NET Framework 4.8 WPF app, Newtonsoft.Json release API parsing, existing PowerShell test harness.

---

## Chunk 1: Release Identity Logic

### Task 1: Pure Comparison And Formatting

**Files:**
- Create: `Version/ReleaseIdentity.cs`
- Create: `Tests/VersionReleaseIdentityTests.cs`
- Create: `Tests/VersionReleaseIdentityTests.ps1`
- Modify: `TypeSunny.csproj`

- [ ] **Step 1: Write failing tests** covering newer version, same-version newer publication time, missing installed metadata, ignore identity, and Beijing-time display.
- [ ] **Step 2: Run `powershell -ExecutionPolicy Bypass -File Tests\VersionReleaseIdentityTests.ps1` and verify it fails because `ReleaseIdentity` does not exist.**
- [ ] **Step 3: Implement `ReleaseIdentity` with UTC storage/comparison and UTC+8 display formatting.**
- [ ] **Step 4: Add the new file to `TypeSunny.csproj`.**
- [ ] **Step 5: Run the identity tests and verify they pass.**

## Chunk 2: VersionManager Integration

### Task 2: Release API Metadata And Reminder Behavior

**Files:**
- Modify: `Version/VersionManager.cs`
- Modify: `Config/Config.cs`
- Modify: `UI/UpdateDialog.xaml.cs`

- [ ] **Step 1: Store `最新发布UTC时间`, `已安装版本`, and `已安装发布UTC时间` in config.**
- [ ] **Step 2: Parse release time from `published_at`, `created_at`, then `updated_at`, store as UTC ticks.**
- [ ] **Step 3: Change `HasUpdate` to use `ReleaseIdentity.HasUpdate`.**
- [ ] **Step 4: Change ignore behavior to store and compare `版本|发布时间ticks` where available.**
- [ ] **Step 5: Show latest release publication time in `UpdateDialog` as Beijing time.**

## Chunk 3: Updater Persistence

### Task 3: Persist Installed Release Identity After Successful Update

**Files:**
- Modify: `Utils/UpdatePackageDownloader.cs`
- Modify: `Updater/Program.cs`

- [ ] **Step 1: Pass latest version and release UTC ticks to `Updater.exe` as optional arguments.**
- [ ] **Step 2: After extraction succeeds, update `config.txt` with installed version and installed release UTC time.**
- [ ] **Step 3: Preserve existing config lines and append missing keys.**

## Chunk 4: Review, Verification, Merge

- [ ] **Step 1: Run focused tests.**
- [ ] **Step 2: Run relevant build verification for app and updater.**
- [ ] **Step 3: Request independent code review.**
- [ ] **Step 4: Fix review findings if any.**
- [ ] **Step 5: Merge `feature/release-published-time` into `master` after confirming the master worktree has no conflicting local edits.**
