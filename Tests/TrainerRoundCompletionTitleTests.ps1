$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$trainerCode = Get-Content -Path (Join-Path $root 'WinTrainer\WinTrainer.xaml.cs') -Raw -Encoding UTF8

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

function Assert-Ordered($name, $content, $first, $second) {
    $firstIndex = $content.IndexOf($first)
    $secondIndex = $content.IndexOf($second)

    if ($firstIndex -lt 0) {
        throw "$name expected first marker [$first]"
    }
    if ($secondIndex -lt 0) {
        throw "$name expected second marker [$second]"
    }
    if ($firstIndex -ge $secondIndex) {
        throw "$name expected [$first] before [$second]"
    }
}

$completionMatch = [regex]::Match(
    $trainerCode,
    'public bool AutoNextGroup[\s\S]*?return true; //'
)
if (-not $completionMatch.Success) {
    throw 'expected to find AutoNextGroup round-completion branch.'
}
$completionBlock = $completionMatch.Value

Assert-Contains 'round status text builder exists' $trainerCode 'private string BuildRoundStatusText()'
Assert-Contains 'round status text applier exists' $trainerCode 'private void ApplyRoundStatusText(string statText)'
Assert-Contains 'completion captures finished status before reset' $completionBlock 'string completedRoundStatus = BuildRoundStatusText();'
Assert-Ordered 'completion captures finished status before reset call' $completionBlock 'string completedRoundStatus = BuildRoundStatusText();' 'ResetRoundStatistics(clearVisibleStatus: false);'
Assert-Contains 'completion reset preserves visible trainer title status' $completionBlock 'ResetRoundStatistics(clearVisibleStatus: false);'
Assert-NotContains 'completion branch must not recalculate blank round status after reset' $completionBlock 'UpdateRoundStatus();'
Assert-Contains 'outer GetNextRound skips completion status recalculation' $trainerCode 'if (!roundCompleted)'
Assert-Contains 'round-completion dialog uses deferred helper' $trainerCode 'ShowRoundStatisticsDialogLater(roundRecord);'
Assert-Contains 'round-completion dialog waits for render/idle before blocking' $trainerCode 'DispatcherPriority.ContextIdle'

Write-Host 'All trainer round-completion title tests passed.'
