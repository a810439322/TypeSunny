$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$mainXaml = Get-Content -Path (Join-Path $root 'UI\MainWindow.xaml') -Raw -Encoding UTF8
$mainCode = Get-Content -Path (Join-Path $root 'UI\MainWindow.xaml.cs') -Raw -Encoding UTF8

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

if ($mainXaml.Contains('<KeyBinding Gesture="Ctrl+C"')) {
    throw 'Ctrl+C should not be a Window KeyBinding because it would steal TextBox copy.'
}

Assert-Contains 'main window preview keydown hook exists' $mainXaml 'PreviewKeyDown="MainWin_PreviewKeyDown"'
Assert-Contains 'Ctrl+C preview shortcut handler exists' $mainCode 'private void MainWin_PreviewKeyDown(object sender, KeyEventArgs e)'
Assert-Contains 'Ctrl+C preview handler recognizes Ctrl+C' $mainCode 'e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control'
Assert-Contains 'Ctrl+C preview handler preserves copy in text controls' $mainCode 'IsCopyTargetFocused()'
Assert-Contains 'copy-target helper checks TextBox' $mainCode 'focused is TextBox'
Assert-Contains 'copy-target helper checks RichTextBox' $mainCode 'focused is RichTextBox'

$ctrlCPreviewBlock = Get-Between 'Ctrl+C preview handler' $mainCode 'private void MainWin_PreviewKeyDown(object sender, KeyEventArgs e)' 'private bool IsCopyTargetFocused()'
Assert-Contains 'Ctrl+C preview opens statistics window' $ctrlCPreviewBlock 'OpenWenlaiStatisticsWindow();'
Assert-Contains 'Ctrl+C preview marks event handled' $ctrlCPreviewBlock 'e.Handled = true;'

$menuStatsBlock = Get-Between 'statistics menu handler' $mainCode 'private void MenuItemWenlaiStatistics_Click(object sender, RoutedEventArgs e)' 'private void MenuItemDetailedWordCountStatistics_Click'
Assert-Contains 'statistics menu uses shared opener' $menuStatsBlock 'OpenWenlaiStatisticsWindow();'

Assert-Contains 'shared statistics opener exists' $mainCode 'private void OpenWenlaiStatisticsWindow()'
Assert-Contains 'shared statistics opener creates window' $mainCode 'new WinStatistics(this)'

Write-Host 'All main window shortcut stats tests passed.'
