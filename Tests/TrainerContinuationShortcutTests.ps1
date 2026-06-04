$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$mainCode = Get-Content -Path (Join-Path $root 'UI\MainWindow.xaml.cs') -Raw -Encoding UTF8
$trainerCode = Get-Content -Path (Join-Path $root 'WinTrainer\WinTrainer.xaml.cs') -Raw -Encoding UTF8

function Assert-Contains($name, $content, $needle) {
    if (-not $content.Contains($needle)) {
        throw "$name expected to contain [$needle]"
    }
}

function Get-Between($name, $content, $start, $end) {
    $pattern = [regex]::Escape($start) + '([\s\S]*?)' + [regex]::Escape($end)
    $match = [regex]::Match($content, $pattern)
    if (-not $match.Success) {
        throw "$name expected to find block between [$start] and [$end]"
    }
    return $match.Groups[1].Value
}

$ctrlPBlock = Get-Between 'main Ctrl+P handler' $mainCode 'private async void InternalHotkeyCtrlP(object sender, ExecutedRoutedEventArgs e)' 'private async void InternalHotkeyCtrlO'
Assert-Contains 'Ctrl+P routes trainer next when typing trainer text' $ctrlPBlock 'StateManager.txtSource == TxtSource.trainer'
Assert-Contains 'Ctrl+P records trainer partial progress' $ctrlPBlock 'RecordTrainerPartialProgressIfNeeded();'
Assert-Contains 'Ctrl+P calls trainer next segment' $ctrlPBlock 'trainer.LoadNextSegmentFromShortcut();'

$ctrlOBlock = Get-Between 'main Ctrl+O handler' $mainCode 'private async void InternalHotkeyCtrlO(object sender, ExecutedRoutedEventArgs e)' '/*'
Assert-Contains 'Ctrl+O routes trainer previous when typing trainer text' $ctrlOBlock 'StateManager.txtSource == TxtSource.trainer'
Assert-Contains 'Ctrl+O records trainer partial progress' $ctrlOBlock 'RecordTrainerPartialProgressIfNeeded();'
Assert-Contains 'Ctrl+O calls trainer previous segment' $ctrlOBlock 'trainer.LoadPreviousSegmentFromShortcut();'

Assert-Contains 'trainer exposes next shortcut method' $trainerCode 'public void LoadNextSegmentFromShortcut()'
Assert-Contains 'trainer exposes previous shortcut method' $trainerCode 'public void LoadPreviousSegmentFromShortcut()'

$nextBlock = Get-Between 'trainer next shortcut method' $trainerCode 'public void LoadNextSegmentFromShortcut()' 'public void LoadPreviousSegmentFromShortcut()'
Assert-Contains 'trainer next shortcut advances section' $nextBlock 'MoveSegmentFromShortcut(1)'
Assert-Contains 'trainer next shortcut builds current segment text' $nextBlock 'string matchText = GetMatchText();'
Assert-Contains 'trainer next shortcut loads current segment' $nextBlock 'MainWindow.Current.LoadText(matchText'

$previousBlock = Get-Between 'trainer previous shortcut method' $trainerCode 'public void LoadPreviousSegmentFromShortcut()' 'private void InternalHotkeyCtrlL'
Assert-Contains 'trainer previous shortcut rewinds section' $previousBlock 'MoveSegmentFromShortcut(-1)'
Assert-Contains 'trainer previous shortcut builds current segment text' $previousBlock 'string matchText = GetMatchText();'
Assert-Contains 'trainer previous shortcut loads current segment' $previousBlock 'MainWindow.Current.LoadText(matchText'

Assert-Contains 'trainer shortcut shared move helper exists' $trainerCode 'private bool MoveSegmentFromShortcut(int delta)'
Assert-Contains 'trainer shortcut clamps boundaries' $trainerCode 'Math.Max(0, Math.Min(TotalGroup - 1'
Assert-Contains 'trainer shortcut updates slider without triggering value changed' $trainerCode 'SliderInit = false;'
Assert-Contains 'trainer shortcut reinitializes selected group' $trainerCode 'InitGroup();'

Write-Host 'All trainer continuation shortcut tests passed.'
