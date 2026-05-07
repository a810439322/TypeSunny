---
date: 2026-05-07
topic: 晴双拼资源缺失时自动下载全量包
status: draft
---

# 晴双拼资源缺失自动下载设计

## 背景

老用户通过程序内"检查更新"升级时，下载的是 `TypeSunny-{ver}-update.zip`（仅含 `.exe/.dll/.xml/.pdb/.config`）。新增的 `Resources\Shuang\` 资源目录不在该增量包内（见 `.github/workflows/release.yml:57`），因此已升级到带"晴双拼"按钮版本的老用户，点击按钮时会触发 `ShuangToolLauncher.Open()` 抛 `FileNotFoundException`。

目标：点击晴双拼按钮时若本地缺资源，提示用户并在确认后下载 `TypeSunny-{ver}-full.zip`，走现有 `Updater.exe` 全量替换 + 重启流程。

## 范围

**做：**
- 点击晴双拼按钮时先判定本地是否可用；不可用则弹确认窗。
- 确认后从 release assets 解析 full 包直链并下载。
- 下载完成后复用 `Updater.exe` 全量替换 + 重启。
- 抽取共享下载逻辑，避免两份 `DownloadFileAsync` / `ApplyThemeColors`。

**不做：**
- 不单独打 "Shuang.zip" 小补丁包。
- 不做后台静默下载。
- 不做"只解压 Resources\Shuang"的特殊路径。
- 不触碰 release.yml（当前已产出 full.zip，够用）。

## 用户确认的决策

| 决策点 | 选择 |
|---|---|
| 下载策略 | 走现有全量升级流程 |
| 全量包用法 | 完整替换 + 重启（等同一次升级） |
| 触发判定 | 仅检查 `Resources\Shuang\index.html` 存在 |
| 交互 | 弹窗告知 + 用户确认后才下载重启 |
| URL 来源 | 用 release API 解析 full.zip 直链 |
| 目标版本 | 总是 latest |
| Updater.exe 缺失 | 不处理（"有晴双拼按钮就不会没有 Updater.exe"） |
| 代码复用 | 抽共享工具，避免重复 |

## 架构

```
BtnShuang_Click (MainWindow.xaml.cs:9307)
    │
    ├── ShuangToolLauncher.IsAvailable?
    │       ├── true  → ShuangToolLauncher.Open()   [不变]
    │       └── false → new ShuangMissingDialog().ShowDialog()
    │
    └── ShuangMissingDialog
            ├── 用户点"取消"  → 关闭，不动
            └── 用户点"下载并重启"
                    ├── 若 VersionManager.FullPackageUrl 为空
                    │       → await VersionManager.CheckUpdateAsync(forceRefresh: true)
                    │       → 若仍为空：MessageBox + 引导 ReleasePage，返回
                    └── await UpdatePackageDownloader.DownloadAndApplyAsync(
                            VersionManager.FullPackageUrl,
                            progress: progressBar + txtProgress 更新,
                            ct: dialog CancellationToken)
                        → 内部：下载到 temp → 启动 Updater.exe → Application.Shutdown()
```

## 组件

### 1. `Version/VersionManager.cs` — 扩展 asset 解析

当前 `CheckUpdateAsync` 在 `assets` 循环里只找 `name.Contains("update")`，把 `FullPackageUrl` 硬编为网页 URL（`releases/tag/v{version}`）。

**改动：** `FullPackageUrl` 语义从"网页 URL"改为"直链 URL"——在同一 `foreach (var asset in assets)` 循环里同时找 full 和 update：

```csharp
string updateUrl = "", fullUrl = "";
foreach (var asset in assets)
{
    string name = asset["name"]?.ToString() ?? "";
    string url = asset["browser_download_url"]?.ToString() ?? "";
    if (string.IsNullOrEmpty(fullUrl) && name.Contains("full"))
        fullUrl = url;
    else if (string.IsNullOrEmpty(updateUrl) && name.Contains("update"))
        updateUrl = url;
}
UpdatePackageUrl = updateUrl;
FullPackageUrl   = fullUrl;
```

保留 `ReleasePage` 属性（仍是网页 URL，用于"手动下载"引导）。

**影响面：** `UpdateDialog.BtnUpdate_Click` 当前用的是 `UpdatePackageUrl`，不受影响。项目中未见其他地方读取 `FullPackageUrl`，语义变更安全。

### 2. `Utils/UpdatePackageDownloader.cs` — 新增，共享下载逻辑

抽取 `UpdateDialog` 中"下载 zip → 启动 Updater.exe → Shutdown"的流程：

```csharp
internal static class UpdatePackageDownloader
{
    internal static async Task DownloadAndApplyAsync(
        string packageUrl,
        IProgress<(long downloaded, long? total)> progress,
        CancellationToken ct);
    // 抛异常由调用方 catch 恢复 UI。
    // 成功时内部调用 Process.Start(Updater.exe) 并 Application.Current.Shutdown()。

    private static async Task DownloadFileAsync(
        string url, string filePath,
        IProgress<(long, long?)> progress,
        CancellationToken ct);
}
```

**来源：** 直接从 `UI/UpdateDialog.xaml.cs:172` 的 `DownloadFileAsync` 和 `UpdateDialog.xaml.cs:111-169` 的 `BtnUpdate_Click` 主体迁移。

**改造 `UpdateDialog.xaml.cs:BtnUpdate_Click`：** 改为调用 `UpdatePackageDownloader.DownloadAndApplyAsync(VersionManager.UpdatePackageUrl, ...)`，删除原下载实现，行为保持一致。

### 3. `Utils/DialogTheming.cs` — 新增，主题共享

把 `UpdateDialog.ApplyThemeColors` 中依赖具体控件名的逻辑改为参数化入口：

```csharp
internal static class DialogTheming
{
    internal static void Apply(
        Border mainBorder,
        TextBlock[] foregroundTexts,
        Button[] normalButtons,
        Button accentButton,
        ProgressBar progressBar);
}
```

`UpdateDialog.ApplyThemeColors` 和 `ShuangMissingDialog.ApplyThemeColors` 都转发到该静态方法。

### 4. `UI/ShuangMissingDialog.xaml` + `.xaml.cs` — 新增

结构模仿 `UpdateDialog.xaml`，内容差异：
- 标题："晴双拼资源缺失"
- 正文："晴双拼资源不在你当前安装版本中。需要下载完整包（约 XX MB）并重启程序替换。是否继续？"
- 按钮：`btnCancel`（取消）、`btnConfirm`（下载并重启）。**不** 需要"忽略版本/今日不再提醒"。
- 进度条区域：与 UpdateDialog 同构（`gridProgress` / `progressBar` / `txtProgress`），初始隐藏。

行为：
- `btnConfirm` 点击：
  - 若 `VersionManager.FullPackageUrl` 为空 → `await VersionManager.CheckUpdateAsync(forceRefresh: true)`。
  - 若仍为空 → `MessageBox` 提示"无法获取更新源"，Yes 打开 `ReleasePage`。窗口保持。
  - 否则切到进度条 UI → `UpdatePackageDownloader.DownloadAndApplyAsync(url, progress, ct)`。
  - catch 异常 → 恢复按钮区、`MessageBox` 报错。
- `btnCancel` / 窗口关闭 → `_cts.Cancel()` 关窗。
- 主题：`DialogTheming.Apply(...)`。

### 5. `UI/MainWindow.xaml.cs:BtnShuang_Click` — 改判定

**改动前（`MainWindow.xaml.cs:9307`）：**
```csharp
private void BtnShuang_Click(object sender, RoutedEventArgs e)
{
    try { ShuangToolLauncher.Open(AppDomain.CurrentDomain.BaseDirectory); }
    catch (Exception ex) { ... }
}
```

**改动后：**
```csharp
private void BtnShuang_Click(object sender, RoutedEventArgs e)
{
    string baseDir = AppDomain.CurrentDomain.BaseDirectory;
    if (ShuangToolLauncher.IsAvailable(baseDir))
    {
        try { ShuangToolLauncher.Open(baseDir); }
        catch (Exception ex) { /* 现有错误处理 */ }
        return;
    }
    new UI.ShuangMissingDialog(this).ShowDialog();
}
```

## 数据流

1. 用户点 BtnShuang
2. MainWindow 检 `IsAvailable` → false
3. MainWindow.show ShuangMissingDialog
4. 用户点"下载并重启"
5. Dialog ensure FullPackageUrl → 可能触发 `CheckUpdateAsync(true)` 拉 release API
6. Dialog 调 `UpdatePackageDownloader.DownloadAndApplyAsync(url, progress, ct)`
7. Downloader HTTP GET → 写到 `%TEMP%\TypeSunnyUpdate\update.zip`，每 8KB 回调进度
8. Downloader 启 `Updater.exe "zip" "appDir" pid "mainExe"`
9. `Application.Current.Shutdown()`
10. Updater.exe 解压 full.zip 覆盖 `appDir`（含 `Resources\Shuang\`）→ 重启 TypeSunny

## 错误处理

| 场景 | 处理 |
|---|---|
| `CheckUpdateAsync` 失败 / `FullPackageUrl` 空 | MessageBox "无法获取更新源"，Yes 打开 ReleasePage |
| 下载中网络中断 / 超时 | Downloader 抛异常 → Dialog catch → 恢复按钮、MessageBox 报错，**不重启** |
| 用户下载中点关闭 | CancellationToken → Downloader 响应取消 → 关窗 |
| temp 目录脏数据 | 下载前 `Directory.Delete(tempDir, true)`（沿用 UpdateDialog 行为） |
| Updater.exe 缺失 | 不处理（决策外），仅 `Debug.WriteLine` + 简短 MessageBox 兜底 |

## 测试

项目 `Tests/` 现状：PowerShell + JS 轻量脚本，无 .NET 单测框架。

### 新增 `Tests/ShuangMissingFlowTests.ps1`

用 PS 做文件/文本级断言：
1. `ShuangToolLauncher.cs` 的 `IsAvailable` 行为未被修改（保留 `File.Exists(index.html)` 判定）。
2. `VersionManager.cs` 中 assets 解析包含 full 分支（grep `name.Contains("full")`）。
3. `VersionManager.FullPackageUrl` 的赋值来源改自 asset URL（grep 旧 `GiteeReleasePage/tag/v` 行应消失）。
4. `UI/ShuangMissingDialog.xaml` 存在且含 `btnConfirm` / `btnCancel` / `progressBar` / `txtProgress`。
5. `MainWindow.xaml.cs` 的 `BtnShuang_Click` 引用 `IsAvailable` 和 `ShuangMissingDialog`。
6. 复用断言：`DownloadFileAsync` 的函数定义全仓库仅 1 处（在 `UpdatePackageDownloader.cs`）；`UpdateDialog.xaml.cs` 不再含该函数。
7. 同上，`ApplyThemeColors` 的实现应全部转发到 `DialogTheming.Apply`。

### 手动验证清单（执行阶段运行）

- A. 本地有完整资源 → 点按钮直接打开 index.html
- B. 手动删除 `Resources\Shuang\index.html` → 点按钮弹 ShuangMissingDialog
- C. B 场景下点"下载并重启" → 进度条推进、Updater.exe 启动、程序退出
- D. B 场景下点"取消" → 窗口关闭，主程序正常
- E. 断网 → 提示"无法获取更新源"
- F. 常规"检查更新"走 UpdateDialog → 行为与改造前一致（update.zip 下载 + 重启）

## 风险与权衡

- **语义变更 `FullPackageUrl`**：从网页 URL 改直链。项目内未见其他引用，但发布 workflow 之外若有文档提及需同步。
- **Gitee full.zip 可能缺失**（release.yml:187 标 required=false）：按决策，直接引导用户手动下载。
- **"完整替换 + 重启"强度大**：用户本意只是"开个双拼练习"，却被重启。权衡后接受——这正是你选的"等同一次升级"。
- **抽 `DialogTheming`**：`UpdateDialog.ApplyThemeColors` 当前直接访问私有字段，参数化后需按 Button/TextBlock 数组传入；如果有控件遗漏会导致主题不全。实施时需逐一核对现有 `ApplyThemeColors` 所触控件集。

## 文件变更清单

| 文件 | 动作 |
|---|---|
| `Version/VersionManager.cs` | 修改 asset 解析 |
| `Utils/UpdatePackageDownloader.cs` | 新增 |
| `Utils/DialogTheming.cs` | 新增 |
| `UI/UpdateDialog.xaml.cs` | 简化：下载流程 → `UpdatePackageDownloader`；主题 → `DialogTheming` |
| `UI/ShuangMissingDialog.xaml` | 新增 |
| `UI/ShuangMissingDialog.xaml.cs` | 新增 |
| `UI/MainWindow.xaml.cs:BtnShuang_Click` | 改判定 |
| `TypeSunny.csproj` | 登记新文件 |
| `Tests/ShuangMissingFlowTests.ps1` | 新增 |
