$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$trainerXaml = Get-Content -Raw -Encoding UTF8 (Join-Path $root 'WinTrainer\WinTrainer.xaml')
$trainerCode = Get-Content -Raw -Encoding UTF8 (Join-Path $root 'WinTrainer\WinTrainer.xaml.cs')

function Assert-Contains($name, $text, $needle) {
    if (-not $text.Contains($needle)) {
        throw "$name missing expected text: $needle"
    }
}

function Assert-NotContains($name, $text, $needle) {
    if ($text.Contains($needle)) {
        throw "$name should not contain text: $needle"
    }
}

function Assert-Ordered($name, $text, $first, $second) {
    $firstIndex = $text.IndexOf($first)
    $secondIndex = $text.IndexOf($second)
    if ($firstIndex -lt 0) {
        throw "$name missing first marker: $first"
    }
    if ($secondIndex -lt 0) {
        throw "$name missing second marker: $second"
    }
    if ($firstIndex -ge $secondIndex) {
        throw "$name expected [$first] before [$second]"
    }
}

function Get-Block($text, $startNeedle, $endNeedle) {
    $start = $text.IndexOf($startNeedle)
    if ($start -lt 0) {
        throw "Unable to find block start: $startNeedle"
    }

    $end = $text.IndexOf($endNeedle, $start + $startNeedle.Length)
    if ($end -lt 0) {
        throw "Unable to find block end: $endNeedle"
    }

    return $text.Substring($start, $end - $start)
}

Assert-Contains 'article stats stores target hit setting' $trainerCode 'public string TargetHitSetting { get; set; }'
Assert-Contains 'article stats stores hit decrease setting' $trainerCode 'public string HitDecreaseSetting { get; set; }'
Assert-Contains 'article stats stores group size setting' $trainerCode 'public string GroupSizeSetting { get; set; }'
Assert-Contains 'article stats stores target accuracy setting' $trainerCode 'public string TargetAccuracySetting { get; set; }'

$saveBlock = Get-Block $trainerCode 'private void SaveCurrentArticleStatistics()' 'private void LoadArticleStatistics'
Assert-Contains 'save captures target hit per text' $saveBlock 'TargetHitSetting = cfg["换段击键"]'
Assert-Contains 'save captures hit decrease per text' $saveBlock 'HitDecreaseSetting = cfg["每轮降击"]'
Assert-Contains 'save captures group size per text' $saveBlock 'GroupSizeSetting = cfg["每组字数"]'
Assert-Contains 'save captures target accuracy per text' $saveBlock 'TargetAccuracySetting = cfg["换段键准"]'

$loadBlock = Get-Block $trainerCode 'private void LoadArticleStatistics(string articleName)' 'private void SaveStatisticsToFile()'
Assert-Contains 'load applies per-text settings' $loadBlock 'ApplyArticleSettings(data);'
Assert-Ordered 'load applies settings before using cached display groups' $loadBlock 'ApplyArticleSettings(data);' '// 恢复文章内容（包括乱序状态）'

Assert-Contains 'stats file persists per-text settings' $trainerCode 'writer.WriteLine($"C\t{data.TargetHitSetting}\t{data.HitDecreaseSetting}\t{data.GroupSizeSetting}\t{data.TargetAccuracySetting}");'
Assert-Contains 'stats file loads per-text settings' $trainerCode 'line.StartsWith("C\t")'
Assert-Contains 'setting restore suppresses textbox change handlers' $trainerCode '_isApplyingArticleSettings'

Assert-Contains 'shuffle all command resource exists' $trainerXaml '<RoutedUICommand x:Key="ShuffleAll"'
Assert-Contains 'restore order command resource exists' $trainerXaml '<RoutedUICommand x:Key="RestoreOrder"'
Assert-Contains 'ctrl shift l shuffles all trainer text' $trainerXaml 'Gesture="Ctrl+Shift+L" Command="{StaticResource ShuffleAll}"'
Assert-Contains 'ctrl shift u restores trainer text order' $trainerXaml 'Gesture="Ctrl+Shift+U" Command="{StaticResource RestoreOrder}"'
Assert-NotContains 'restore order no longer uses ctrl shift s' $trainerXaml 'Ctrl+Shift+S'
Assert-Contains 'shuffle all command handler exists' $trainerCode 'private void InternalHotkeyCtrlShiftL'
Assert-Contains 'restore order command handler exists' $trainerCode 'private void InternalHotkeyCtrlShiftU'

Assert-Contains 'shuffle button shows shortcut tooltip' $trainerXaml 'ToolTip="全体乱序 / 余字乱序（Ctrl+Shift+L）"'
Assert-Contains 'restore button shows shortcut tooltip' $trainerXaml 'ToolTip="恢复顺序（Ctrl+Shift+U）"'
Assert-Contains 'send button shows shortcut tooltip' $trainerXaml 'ToolTip="发文（Enter）"'

$buttonLines = $trainerXaml -split "`n" | Where-Object { $_ -match '<Button\b' }
$missingTooltip = $buttonLines | Where-Object { $_ -notmatch 'ToolTip=' }
if ($missingTooltip.Count -gt 0) {
    throw "Every trainer button should have a ToolTip. Missing:`n$($missingTooltip -join "`n")"
}

Write-Host 'All trainer per-text settings shortcut tests passed.'
