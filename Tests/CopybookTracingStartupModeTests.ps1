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
Assert-Contains 'startup visibility guard helper includes deferred layout modes' $mainCode 'private static bool ShouldHideStartupUntilDeferredLayoutApplied()'
Assert-Contains 'startup visibility guard reads copybook config' $mainCode 'Config.GetBool("字帖模式")'
Assert-Contains 'startup visibility guard reads tracing config' $mainCode 'Config.GetBool("临摹模式")'
Assert-Contains 'constructor hides first frame until deferred layout is applied' $mainCode 'if (ShouldHideStartupUntilDeferredLayoutApplied())'
Assert-Contains 'startup reveal helper exists' $mainCode 'private void RevealStartupAfterDeferredLayout()'

$modeApplyCount = ([regex]::Matches($mainCode, 'ApplyConfiguredCopybookOrTracingMode\(\);')).Count
if ($modeApplyCount -lt 2) {
    throw 'copybook/tracing mode should be applied both during display updates and startup initialization'
}

$initDisplayMatch = [regex]::Match($mainCode, 'private void InitDisplay\(\)[\s\S]*?private void ReadBlindType\(\)')
if (-not $initDisplayMatch.Success) {
    throw 'InitDisplay block was not found'
}
$initDisplayBlock = $initDisplayMatch.Value
$modeApplyIndex = $initDisplayBlock.IndexOf('ApplyConfiguredCopybookOrTracingMode();')
$configLoadedIndex = $initDisplayBlock.IndexOf('StateManager.ConfigLoaded = true;')
if ($modeApplyIndex -lt 0 -or $configLoadedIndex -lt 0 -or $modeApplyIndex -gt $configLoadedIndex) {
    throw 'startup should apply copybook/tracing layout before marking ConfigLoaded true'
}

$constructorLoadedMatch = [regex]::Match($mainCode, 'this\.Loaded \+= \(s, e\) =>[\s\S]*?            };')
if (-not $constructorLoadedMatch.Success) {
    throw 'constructor Loaded startup block was not found'
}
$constructorLoadedBlock = $constructorLoadedMatch.Value
Assert-Contains 'startup reveal is protected by finally' $constructorLoadedBlock 'finally'
Assert-Contains 'startup reveal always runs after deferred layout attempt' $constructorLoadedBlock 'RevealStartupAfterDeferredLayout();'

Write-Host 'All copybook/tracing startup mode tests passed.'
