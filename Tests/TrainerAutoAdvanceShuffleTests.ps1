$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$trainerCode = Get-Content -Raw (Join-Path $root 'WinTrainer\WinTrainer.xaml.cs')

function Assert-Contains {
    param(
        [string]$Name,
        [string]$Text,
        [string]$Expected
    )

    if (-not $Text.Contains($Expected)) {
        throw "$Name missing expected text: $Expected"
    }
}

function Assert-NotContains {
    param(
        [string]$Name,
        [string]$Text,
        [string]$Unexpected
    )

    if ($Text.Contains($Unexpected)) {
        throw "$Name should not contain unexpected text: $Unexpected"
    }
}

function Assert-Ordered {
    param(
        [string]$Name,
        [string]$Text,
        [string]$First,
        [string]$Second
    )

    $firstIndex = $Text.IndexOf($First)
    $secondIndex = $Text.IndexOf($Second)
    if ($firstIndex -lt 0) {
        throw "$Name missing first marker: $First"
    }
    if ($secondIndex -lt 0) {
        throw "$Name missing second marker: $Second"
    }
    if ($firstIndex -ge $secondIndex) {
        throw "$Name expected [$First] before [$Second]"
    }
}

function Get-Block {
    param(
        [string]$Text,
        [string]$Start,
        [string]$End
    )

    $startIndex = $Text.IndexOf($Start)
    if ($startIndex -lt 0) {
        throw "missing block start: $Start"
    }

    $endIndex = $Text.IndexOf($End, $startIndex)
    if ($endIndex -lt 0) {
        throw "missing block end after $Start`: $End"
    }

    return $Text.Substring($startIndex, $endIndex - $startIndex)
}

$autoNextBlock = Get-Block $trainerCode 'public bool AutoNextGroup(out string roundResultRecord)' 'private void ResetRoundStatistics'
$lastSuppressIndex = $autoNextBlock.LastIndexOf('SliderInit = false;')
$lastSldValueIndex = $autoNextBlock.LastIndexOf('sld.Value = Convert.ToInt32')
$lastRestoreIndex = $autoNextBlock.LastIndexOf('SliderInit = true;')
$lastInitGroupIndex = $autoNextBlock.LastIndexOf('InitGroup();')
if ($lastSuppressIndex -lt 0 -or $lastSldValueIndex -lt 0 -or $lastRestoreIndex -lt 0 -or $lastInitGroupIndex -lt 0) {
    throw 'auto next block missing slider suppression markers.'
}
if (-not ($lastSuppressIndex -lt $lastSldValueIndex -and $lastSldValueIndex -lt $lastRestoreIndex -and $lastRestoreIndex -lt $lastInitGroupIndex)) {
    throw 'auto next must suppress slider events, set slider, restore events, then initialize next group once.'
}

$fileSelectionBlock = Get-Block $trainerCode `
    'private void FileSelector_SelectionChanged' `
    'private void Slider_ValueChanged'
Assert-Contains 'programmatic file refresh skips extra current-group shuffle' $fileSelectionBlock 'ReadTxt(skipInGroupRand: _isRefreshingFileList);'

$refreshBlock = Get-Block $trainerCode `
    'public void RefreshFileList()' `
    'private class NaturalStringComparer'
Assert-Contains 'refresh marks file list updates as programmatic' $refreshBlock '_isRefreshingFileList = true;'
Assert-Contains 'refresh clears programmatic file list marker' $refreshBlock '_isRefreshingFileList = false;'

$visibleChangedBlock = Get-Block $trainerCode `
    'private void Window_IsVisibleChanged' `
    'private void TitleBar_MouseLeftButtonDown'
Assert-Contains 'visible refresh reloads trainer display from restored group state' $visibleChangedBlock 'ShowWords();'
Assert-Contains 'visible refresh keeps send text aligned with visible group state' $visibleChangedBlock 'PushTrainerSectionToMain();'

Write-Host 'Trainer auto-advance shuffle tests passed.'
