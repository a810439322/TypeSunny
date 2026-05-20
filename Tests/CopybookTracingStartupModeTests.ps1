$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$mainCode = Get-Content -Path (Join-Path $root 'UI\MainWindow.xaml.cs') -Raw

function Assert-Contains($name, $content, $needle) {
    if (-not $content.Contains($needle)) {
        throw "$name expected to contain [$needle]"
    }
}

Assert-Contains 'copybook/tracing startup mode helper' $mainCode 'private void ApplyConfiguredCopybookOrTracingMode()'
Assert-Contains 'copybook/tracing startup mode reads copybook config' $mainCode 'bool copybookEnabled = Config.GetBool("字帖模式");'
Assert-Contains 'copybook/tracing startup mode reads tracing config' $mainCode 'bool tracingEnabled = Config.GetBool("临摹模式");'
Assert-Contains 'copybook/tracing startup mode enables tracing' $mainCode 'if (tracingEnabled && !_tracingMode.IsActive)'

$modeApplyCount = ([regex]::Matches($mainCode, 'ApplyConfiguredCopybookOrTracingMode\(\);')).Count
if ($modeApplyCount -lt 2) {
    throw 'copybook/tracing mode should be applied both during display updates and startup initialization'
}

Write-Host 'All copybook/tracing startup mode tests passed.'
