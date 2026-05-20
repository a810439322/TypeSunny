$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$tracing = Get-Content -Path (Join-Path $root 'UI\Modes\TracingMode.cs') -Raw
$mainCode = Get-Content -Path (Join-Path $root 'UI\MainWindow.xaml.cs') -Raw

$insertStart = $tracing.IndexOf('private void InsertMirrorBlocks()')
if ($insertStart -lt 0) {
    throw 'Unable to find TracingMode.InsertMirrorBlocks.'
}

$removeCall = $tracing.IndexOf('RemoveMirrorBlocks();', $insertStart)
if ($removeCall -lt 0) {
    throw 'TracingMode.InsertMirrorBlocks should restore original blocks before rebuilding mirror rows.'
}

$lineGrouping = $tracing.IndexOf('// 按 Y 坐标分组', $insertStart)
if ($lineGrouping -lt 0) {
    throw 'Unable to find mirror line grouping step.'
}

$layoutAfterRestore = $tracing.IndexOf('_main.TbDispay.UpdateLayout();', $removeCall)
if ($layoutAfterRestore -lt 0 -or $layoutAfterRestore -gt $lineGrouping) {
    throw 'Tracing mirror rows must update layout after restoring original blocks and before grouping by Y coordinates.'
}

if (-not $tracing.Contains('public void RefreshMirrorBlocksNow()')) {
    throw 'TracingMode should expose a synchronous mirror refresh for resize/zoom display updates.'
}

$refreshStart = $tracing.IndexOf('public void RefreshMirrorBlocksNow()')
if ($refreshStart -lt 0) {
    throw 'Unable to find RefreshMirrorBlocksNow.'
}

$refreshEnd = $tracing.IndexOf('public void FocusInputCapture()', $refreshStart)
if ($refreshEnd -lt 0) {
    throw 'Unable to find end of RefreshMirrorBlocksNow.'
}

$refreshBody = $tracing.Substring($refreshStart, $refreshEnd - $refreshStart)
$refreshInsert = $refreshBody.IndexOf('InsertMirrorBlocks();')
$refreshLayoutAfterInsert = $refreshBody.IndexOf('_main.TbDispay.UpdateLayout();', $refreshInsert)
$refreshPosition = $refreshBody.IndexOf('UpdatePosition();')
if ($refreshInsert -lt 0 -or $refreshLayoutAfterInsert -lt 0 -or $refreshPosition -lt 0 -or $refreshLayoutAfterInsert -gt $refreshPosition) {
    throw 'RefreshMirrorBlocksNow must update layout after rebuilding mirror rows and before positioning the cursor.'
}

if (-not $mainCode.Contains('_tracingMode.RefreshMirrorBlocksNow();')) {
    throw 'Display updates should refresh tracing mirror rows synchronously to avoid a visible ordinary-layout frame.'
}

$prepareStart = $mainCode.IndexOf('public void PrepareLoadedTextForInput(bool focus = true)')
if ($prepareStart -lt 0) {
    throw 'Unable to find PrepareLoadedTextForInput.'
}

$prepareScrollReset = $mainCode.IndexOf('ScDisplay.ScrollToVerticalOffset(0);', $prepareStart)
if ($prepareScrollReset -lt 0) {
    throw 'Unable to find PrepareLoadedTextForInput scroll reset.'
}

$prepareInitialRender = $mainCode.Substring($prepareStart, $prepareScrollReset - $prepareStart)
if ($prepareInitialRender.Contains('_tracingMode.ScheduleInsertMirrorBlocks();')) {
    throw 'Loaded article initialization should rely on synchronous tracing mirror refresh and not queue an extra mirror rebuild.'
}

$zoomStart = $mainCode.IndexOf('private void Window_PreviewMouseWheel(')
if ($zoomStart -lt 0) {
    throw 'Unable to find Window_PreviewMouseWheel.'
}

$controlWheelStart = $mainCode.IndexOf('private void Control_PreviewMouseWheel(', $zoomStart)
if ($controlWheelStart -lt 0) {
    throw 'Unable to find Control_PreviewMouseWheel.'
}

$windowZoomBody = $mainCode.Substring($zoomStart, $controlWheelStart - $zoomStart)
if ($windowZoomBody.Contains('_tracingMode.ScheduleInsertMirrorBlocks();')) {
    throw 'Font zoom should not queue an extra tracing mirror rebuild after synchronous display refresh.'
}

if (-not $tracing.Contains('_main.RebuildCurrentPageDisplayElementsForTracingMeasurement();')) {
    throw 'Tracing mirror grouping should restore the current display element structure through MainWindow.'
}

if (-not $mainCode.Contains('internal void RebuildCurrentPageDisplayElementsForTracingMeasurement()')) {
    throw 'MainWindow should expose a current-page display rebuild for tracing mirror measurement.'
}

if (-not $mainCode.Contains('AddCiTiNoSplitLineDisplayElements();')) {
    throw 'Tracing mirror measurement rebuild should preserve CiTi no-split grouping.'
}

if (-not $mainCode.Contains('CreateDisplayElement(TextInfo.Blocks[i], globalIdx)')) {
    throw 'Tracing mirror measurement rebuild should measure lower-code display containers instead of raw text blocks.'
}

$restoreStart = $tracing.IndexOf('private void RemoveMirrorBlocks()')
if ($restoreStart -lt 0) {
    throw 'Unable to find RemoveMirrorBlocks.'
}

$restoreEnd = $tracing.IndexOf('/// <summary>', $restoreStart + 1)
if ($restoreEnd -lt 0) {
    throw 'Unable to find end of RemoveMirrorBlocks.'
}

$restoreBody = $tracing.Substring($restoreStart, $restoreEnd - $restoreStart)
if ($restoreBody.Contains('wrapPanel.Add(block)') -or $restoreBody.Contains('wrapPanel.Children.Add(block);')) {
    throw 'Tracing mirror refresh should not measure raw text blocks directly because lower-code display changes block width.'
}

Write-Host 'All tracing mirror scale refresh tests passed.'
