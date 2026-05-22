$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$trainerCode = Get-Content -Path (Join-Path $root 'WinTrainer\WinTrainer.xaml.cs') -Raw -Encoding UTF8
$mainCode = Get-Content -Path (Join-Path $root 'UI\MainWindow.xaml.cs') -Raw -Encoding UTF8
$articleForegroundKey = [string]::Concat([char]0x53D1, [char]0x6587, [char]0x533A, [char]0x5B57, [char]0x4F53, [char]0x8272)
$articleForegroundConfigRead = 'string displayFgColor = Config.GetString("' + $articleForegroundKey + '");'

function Assert-Contains($name, $content, $needle) {
    if (-not $content.Contains($needle)) {
        throw "$name expected to contain [$needle]"
    }
}

function Assert-NotContains($name, $content, $needle) {
    if ($content.Contains($needle)) {
        throw "$name should not contain [$needle]"
    }
}

Assert-Contains 'trainer display grid uses themed display brush' $trainerCode 'DisplayGrid.Background = displayBgBrush;'
Assert-Contains 'trainer text area uses themed display brush' $trainerCode 'fld.Background = displayBgBrush;'
Assert-Contains 'trainer display text reads article foreground theme color' $trainerCode $articleForegroundConfigRead
Assert-Contains 'trainer display text creates article foreground brush' $trainerCode 'var displayFgBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#" + displayFgColor));'
Assert-Contains 'trainer display text updates shared display foreground brush' $trainerCode 'Colors.DisplayForeground = displayFgBrush;'
Assert-Contains 'trainer text foreground uses display foreground brush' $trainerCode 'fld.Foreground = displayFgBrush;'
Assert-NotContains 'trainer text foreground should not use window foreground brush' $trainerCode 'fld.Foreground = fgBrush;'
Assert-NotContains 'trainer text area should not copy stale main display background' $trainerCode 'fld.Background = MainWindow.Current.BdDisplay.Background;'
Assert-Contains 'main theme refresh refreshes trainer display area' $mainCode 'WinTrainer.Current.RefreshTheme();'

Write-Host 'Trainer display theme tests passed.'
