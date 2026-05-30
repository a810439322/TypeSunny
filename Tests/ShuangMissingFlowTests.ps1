$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot

function Read-RepoFile($relativePath) {
    Get-Content -Path (Join-Path $root $relativePath) -Raw
}

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

function Assert-FileExists($relativePath) {
    $path = Join-Path $root $relativePath
    if (-not (Test-Path -Path $path)) {
        throw "expected file to exist: $relativePath"
    }
}

$launcher = Read-RepoFile 'Utils\ShuangToolLauncher.cs'
$versionManager = Read-RepoFile 'Version\VersionManager.cs'
$mainWindow = Read-RepoFile 'UI\MainWindow.xaml.cs'
$project = Read-RepoFile 'TypeSunny.csproj'

Assert-Contains 'shuang availability still checks index.html existence' $launcher 'return File.Exists(GetIndexPath(baseDirectory));'

Assert-Contains 'version manager parses full release asset' $versionManager 'name.Contains("full")'
Assert-Contains 'version manager uses browser_download_url for full package' $versionManager 'FullPackageUrl = fullUrl;'
Assert-NotContains 'full package should not point to release page tag' $versionManager 'FullPackageUrl = PreferredSource == UpdateSource.Gitee'

Assert-FileExists 'Utils\UpdatePackageDownloader.cs'
Assert-FileExists 'Utils\DialogTheming.cs'
Assert-FileExists 'UI\ShuangMissingDialog.xaml'
Assert-FileExists 'UI\ShuangMissingDialog.xaml.cs'

$downloader = Read-RepoFile 'Utils\UpdatePackageDownloader.cs'
Assert-Contains 'shared downloader exposes download and apply API' $downloader 'DownloadAndApplyAsync'
Assert-Contains 'shared downloader stages updater from package' $downloader 'UpdatePackageStager.StageUpdater'
Assert-Contains 'shared downloader starts staged updater' $downloader 'Process.Start(stagedUpdaterPath'
Assert-Contains 'shared downloader shuts down app after updater starts' $downloader 'Application.Current.Shutdown();'

$dialogXaml = Read-RepoFile 'UI\ShuangMissingDialog.xaml'
Assert-Contains 'missing dialog confirm button' $dialogXaml 'x:Name="btnConfirm"'
Assert-Contains 'missing dialog cancel button' $dialogXaml 'x:Name="btnCancel"'
Assert-Contains 'missing dialog progress bar' $dialogXaml 'x:Name="progressBar"'
Assert-Contains 'missing dialog progress text' $dialogXaml 'x:Name="txtProgress"'

$dialogCode = Read-RepoFile 'UI\ShuangMissingDialog.xaml.cs'
Assert-Contains 'missing dialog refreshes release assets when full url is empty' $dialogCode 'CheckUpdateAsync(forceRefresh: true)'
Assert-Contains 'missing dialog downloads full package' $dialogCode 'VersionManager.FullPackageUrl'
Assert-Contains 'missing dialog uses shared downloader' $dialogCode 'UpdatePackageDownloader.DownloadAndApplyAsync'
Assert-Contains 'missing dialog applies shared theming' $dialogCode 'DialogTheming.Apply'

Assert-Contains 'main shuang click checks availability first' $mainWindow 'ShuangToolLauncher.IsAvailable(baseDir)'
Assert-Contains 'main shuang click opens missing dialog' $mainWindow 'new ShuangMissingDialog(this).ShowDialog();'

$downloadFileDefinitions = ([regex]::Matches((Read-RepoFile 'UI\UpdateDialog.xaml.cs') + "`n" + $downloader, 'Task\s+DownloadFileAsync\s*\(')).Count
if ($downloadFileDefinitions -ne 1) {
    throw "DownloadFileAsync definition expected exactly once across UpdateDialog and UpdatePackageDownloader, got $downloadFileDefinitions"
}

$updateDialogCode = Read-RepoFile 'UI\UpdateDialog.xaml.cs'
Assert-NotContains 'update dialog should not own DownloadFileAsync' $updateDialogCode 'Task DownloadFileAsync('
Assert-Contains 'update dialog uses shared downloader' $updateDialogCode 'UpdatePackageDownloader.DownloadAndApplyAsync'
Assert-Contains 'update dialog applies shared theming' $updateDialogCode 'DialogTheming.Apply'

$applyThemeDefinitions = ([regex]::Matches($updateDialogCode + "`n" + $dialogCode, 'private void ApplyThemeColors\(\)[\s\S]*?DialogTheming\.Apply')).Count
if ($applyThemeDefinitions -lt 2) {
    throw 'UpdateDialog and ShuangMissingDialog should both forward ApplyThemeColors to DialogTheming.Apply'
}

Assert-Contains 'project includes shared downloader' $project 'Utils\UpdatePackageDownloader.cs'
Assert-Contains 'project includes update package stager' $project 'Utils\UpdatePackageStager.cs'
Assert-Contains 'project includes shared dialog theming' $project 'Utils\DialogTheming.cs'
Assert-Contains 'project includes missing dialog code-behind' $project 'UI\ShuangMissingDialog.xaml.cs'
Assert-Contains 'project includes missing dialog xaml' $project 'UI\ShuangMissingDialog.xaml'

Write-Host 'All Shuang missing flow tests passed.'
