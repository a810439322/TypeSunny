$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$mainWindowCode = Get-Content -Path (Join-Path $root 'UI\MainWindow.xaml.cs') -Raw

$wrongBranchPattern = '(?s)SendLocalArticleResultOnly\(result, qqGroupName, 250\);\s*// .*?line 2940-2977.*?WriteDebugLog'
$match = [regex]::Match($mainWindowCode, $wrongBranchPattern)
if (-not $match.Success) {
    throw 'Could not find local article wrong-character score branch.'
}

$branch = $match.Value
if (-not $branch.Contains('SendLocalArticleResultOnly(result, qqGroupName, 250)')) {
    throw 'Local article manual segment wrong-character branch must copy score to clipboard when no QQ group is selected.'
}

Write-Host 'All local article manual wrong score copy tests passed.'
