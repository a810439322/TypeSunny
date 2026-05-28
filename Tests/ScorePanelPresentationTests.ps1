$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$mainXaml = Get-Content -Raw -Encoding UTF8 (Join-Path $root 'UI\MainWindow.xaml')
$mainCode = Get-Content -Raw -Encoding UTF8 (Join-Path $root 'UI\MainWindow.xaml.cs')
$configCode = Get-Content -Raw -Encoding UTF8 (Join-Path $root 'Config\Config.cs')
$configWindowCode = Get-Content -Raw -Encoding UTF8 (Join-Path $root 'WinConfig\WinConfig.xaml.cs')
$autoHideScoreLabelKey = [string]::Concat([char[]]@(0x5931, 0x7126, 0x540E, 0x81EA, 0x52A8, 0x9690, 0x85CF, 0x6210, 0x7EE9, 0x533A, 0x6587, 0x5B57))
$scoreCategoryTitle = [string]::Concat([char[]]@(0x6210, 0x7EE9))

function Get-MethodBlock([string]$source, [string]$methodName) {
    $match = [regex]::Match($source, 'private\s+void\s+' + [regex]::Escape($methodName) + '\([^)]*\)\s*\{')
    if (-not $match.Success) {
        throw "Unable to find $methodName handler."
    }

    $depth = 0
    for ($i = $match.Index; $i -lt $source.Length; $i++) {
        if ($source[$i] -eq '{') {
            $depth++
        } elseif ($source[$i] -eq '}') {
            $depth--
            if ($depth -eq 0) {
                return $source.Substring($match.Index, $i - $match.Index + 1)
            }
        }
    }

    throw "Unable to parse $methodName handler."
}

$match = [regex]::Match($mainXaml, '<TextBox\s+Name="TbxResults"[^>]*>')
if (-not $match.Success) {
    throw 'Unable to find TbxResults TextBox.'
}

if ($match.Value -match 'FontWeight\s*=\s*["'']Bold["'']') {
    throw 'TbxResults should not set FontWeight="Bold".'
}

if ($match.Value -notmatch 'HorizontalScrollBarVisibility\s*=\s*["'']Hidden["'']') {
    throw 'TbxResults should hide its horizontal scrollbar.'
}

if ($match.Value -notmatch 'VerticalScrollBarVisibility\s*=\s*["'']Hidden["'']') {
    throw 'TbxResults should hide its vertical scrollbar.'
}

if ($match.Value -notmatch 'MouseDoubleClick\s*=\s*["'']TbxResults_MouseDoubleClick["'']') {
    throw 'TbxResults should copy the hovered/clicked result row on double-click.'
}

if ($match.Value -notmatch 'MouseLeave\s*=\s*["'']TbxResults_MouseLeave["'']') {
    throw 'TbxResults should restore the hover prompt when the mouse leaves.'
}

foreach ($eventName in @('MouseEnter', 'GotKeyboardFocus', 'LostKeyboardFocus')) {
    if ($match.Value -notmatch ($eventName + '\s*=\s*["'']TbxResults_' + $eventName + '["'']')) {
        throw "TbxResults should update auto-hidden score labels on $eventName."
    }
}

if ($mainXaml -notmatch 'Name\s*=\s*["'']TbxResultsDisplay["'']') {
    throw 'Score panel should include a display-only overlay for auto-hidden score labels.'
}

if ($mainCode -notmatch 'DisableResultsDisplayWrapping') {
    throw 'Score panel display overlay should explicitly disable wrapping.'
}

if ($mainCode -notmatch 'RenderResultsDisplayOverlay') {
    throw 'Score panel should render score labels through a display-only overlay.'
}

if ($mainCode -notmatch 'ShouldShowResultLabels') {
    throw 'Score panel should decide label visibility from hover/focus state.'
}

if ($mainCode -notmatch ('Config\.GetBool\("' + [regex]::Escape($autoHideScoreLabelKey) + '"\)')) {
    throw 'Score panel should honor the auto-hide score label setting.'
}

if ($configCode -notmatch ('"' + [regex]::Escape($autoHideScoreLabelKey) + '"\s*,\s*"是"')) {
    throw 'Score label auto-hide should default to enabled.'
}

if (-not $configWindowCode.Contains($autoHideScoreLabelKey)) {
    throw 'Score settings should include the score label auto-hide switch.'
}

if ($mainCode -notmatch 'ResultHoverCopyHint') {
    throw 'Score panel should define a hover copy hint.'
}

$hintMatch = [regex]::Match($mainXaml, '<Border\s+x:Name="ResultHoverCopyHint"[^>]*>')
if (-not $hintMatch.Success) {
    throw 'Unable to find ResultHoverCopyHint border.'
}

if ($hintMatch.Value -match 'Background\s*=\s*["'']#26000000["'']') {
    throw 'Score panel hover copy hint should be visibly highlighted, not a nearly transparent dark overlay.'
}

if ($mainXaml -notmatch 'Foreground\s*=\s*["'']White["'']') {
    throw 'Score panel hover copy hint text should use a high-contrast foreground.'
}

if ($mainCode -notmatch 'ShowResultHoverHintAtPoint') {
    throw 'Score panel should update the hover prompt from mouse position.'
}

if ($mainCode -notmatch 'GetResultLineIndexAtPoint') {
    throw 'Score panel hover and double-click should resolve rows from the mouse Y position.'
}

if ($mainCode -notmatch 'TbxResults_MouseDoubleClick') {
    throw 'Score panel should handle double-click copy directly.'
}

if ($mainCode -match 'lineIndex\s*==\s*0') {
    throw 'Score panel copy logic should not special-case the first displayed row.'
}

if ($mainCode -notmatch 'DispatcherTimer\s+_resultsRelayoutTimer') {
    throw 'Score panel should use a debounce timer for delayed relayout.'
}

if ($mainCode -notmatch 'Interval\s*=\s*TimeSpan\.FromSeconds\(3\)') {
    throw 'Score panel delayed relayout should run after 3 seconds.'
}

if ($mainCode -notmatch 'targetControl == TbxResults[\s\S]{0,700}ScheduleResultsRelayout\(\);') {
    throw 'Score panel mouse wheel should schedule delayed relayout.'
}

if ($mainCode -notmatch 'Keyboard\.Modifiers != ModifierKeys\.Control[\s\S]{0,250}sender == TbxResults[\s\S]{0,120}ScheduleResultsRelayout\(\);') {
    throw 'Score panel normal mouse wheel should schedule delayed relayout without blocking scrolling.'
}

if ($mainCode -notmatch 'GetFirstVisibleLineIndex\(\)') {
    throw 'Score panel relayout should read the first visible text line after scrolling.'
}

if ($mainCode -notmatch 'CalculateFirstVisibleResultRowIndex') {
    throw 'Score panel relayout should align columns from the currently visible result row window.'
}

if ($mainCode -notmatch 'SetResultsTextPreservingScroll\(sb\.ToString\(\), preserveScroll: !commitCounterBuffer\)') {
    throw 'Score panel refresh relayout should preserve the current scroll offset.'
}

if ($mainCode -notmatch 'GetScrollViewer\(TbxResultsDisplay\)') {
    throw 'Score panel display overlay should use its own ScrollViewer.'
}

if ($mainCode -notmatch 'overlayScrollViewer\?\.ScrollToVerticalOffset\(scrollOffset\)') {
    throw 'Score panel display overlay should keep the same vertical offset as TbxResults.'
}

if ($mainCode -notmatch 'SyncResultsDisplayScrollLater') {
    throw 'Score panel normal scrolling should sync the display overlay scroll position.'
}

if ($mainCode -notmatch 'if \(inResultsArea\)[\s\S]{0,700}ScheduleResultsRelayout\(\);') {
    throw 'Window-level score panel mouse wheel should schedule delayed relayout.'
}

if ($mainCode -notmatch 'GridSplitterResults_DragCompleted[\s\S]{0,1200}ScheduleResultsRelayout\(\);') {
    throw 'Score panel height changes should schedule delayed relayout.'
}

if ($mainCode -notmatch 'GridSplitterResults_PreviewMouseUp[\s\S]{0,1200}ScheduleResultsRelayout\(\);') {
    throw 'Score panel custom height drag should schedule delayed relayout.'
}

$sizeChangedBlock = Get-MethodBlock $mainCode 'win_size_change'
if ($sizeChangedBlock -match 'RefreshTypingStatDisplay\(\);') {
    throw 'Window width resizing should not refresh score panel on every SizeChanged event.'
}

if ($sizeChangedBlock -notmatch '_isWindowResizeDragInProgress[\s\S]{0,200}return;[\s\S]{0,120}RunWindowResizeCompletedWork\(\);') {
    throw 'Window SizeChanged should defer heavy resize work while native border dragging is active.'
}

if ($mainCode -notmatch 'private bool _isWindowResizeDragInProgress') {
    throw 'Window resize should track native border dragging so heavy work can be deferred until drag end.'
}

if ($mainCode -notmatch 'ResizeBorder_MouseLeftButtonDown[\s\S]{0,1800}RunWindowResizeCompletedWork\(\);') {
    throw 'Native border resizing should run deferred resize work after SendMessage returns.'
}

if ($mainCode -notmatch 'private void RunWindowResizeCompletedWork\(\)[\s\S]{0,900}RefreshTypingStatDisplay\(\);') {
    throw 'Deferred resize work should refresh the score panel after resizing completes.'
}

Write-Host 'Score panel presentation tests passed.'
