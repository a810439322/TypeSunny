$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$configCode = Get-Content -Path (Join-Path $root 'Config\Config.cs') -Raw
$mainCode = Get-Content -Path (Join-Path $root 'UI\MainWindow.xaml.cs') -Raw
$winConfigCode = Get-Content -Path (Join-Path $root 'WinConfig\WinConfig.xaml.cs') -Raw
$paginatorCode = Get-Content -Path (Join-Path $root 'Core\Paginator.cs') -Raw

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

Assert-Contains 'config has CiTi code display default enabled' $configCode '"词提编码下显", "是"'
Assert-Contains 'config has CiTi no-split-line default disabled' $configCode '"词提不拆行", "否"'
Assert-Contains 'config has CiTi Jian1 color' $configCode '"词提1简色", "#FF0000"'
Assert-Contains 'config has CiTi Jian2 color' $configCode '"词提2简色", "#FF8C00"'
Assert-Contains 'config has CiTi Jian3 color' $configCode '"词提3简色", "#0000FF"'
Assert-Contains 'config has CiTi normal color' $configCode '"词提4码色", "#808080"'
Assert-Contains 'config has CiTi non-preferred color' $configCode '"词提选重色", "#008000"'
Assert-Contains 'config has ZiTi code display default disabled' $configCode '"字提编码下显", "否"'
Assert-NotContains 'old aggregate code display key removed from config' $configCode '"启用编码下显"'
Assert-NotContains 'old code display source key removed from config' $configCode '"编码下显来源"'

Assert-Contains 'main exposes code display enabled helper' $mainCode 'internal bool IsCodeDisplayEnabled()'
Assert-Contains 'main exposes typing code helper' $mainCode 'internal string GetTypingCodeText(int globalIndex)'
Assert-Contains 'main computes CiTi through a shared segment loading helper' $mainCode 'private bool ShouldLoadCiTiSegments()'
Assert-Contains 'main computes CiTi when CiTi prompt is enabled' $mainCode 'Config.GetBool("启用词提")'
Assert-Contains 'main computes CiTi when CiTi display is requested' $mainCode 'Config.GetBool("词提编码下显")'
Assert-Contains 'main still references ZiTi lower display settings' $mainCode 'Config.GetBool("字提编码下显")'
Assert-Contains 'main applies CiTi alternate bolding' $mainCode 'CiTiHelper.ShouldBold(segIdx)'
Assert-Contains 'main applies no-split line grouping' $mainCode 'Config.GetBool("词提不拆行")'
Assert-Contains 'main loads CiTi segment data for no-split line grouping' $mainCode 'ShouldLoadCiTiSegments()'
Assert-NotContains 'main should not load CiTi segments for standalone no-split when CiTi is disabled' $mainCode '|| Config.GetBool("词提不拆行")'
Assert-Contains 'main reloads CiTi segments through a current-text helper' $mainCode 'ReloadCiTiSegmentsForCurrentText();'
Assert-Contains 'main uses displayed words as CiTi reload source' $mainCode 'private string GetCurrentCiTiSegmentSourceText()'
Assert-Contains 'main falls back to displayed Words for CiTi config reload' $mainCode 'return string.Concat(TextInfo.Words);'
Assert-NotContains 'main should not gate config-applied CiTi reload on MatchText' $mainCode 'if (CiTiHelper.IsLoaded && !string.IsNullOrEmpty(TextInfo.MatchText))'
Assert-NotContains 'main should not split stale MatchText during config-applied CiTi reload' $mainCode 'CiTiHelper.SplitText(TextInfo.MatchText)'
Assert-Contains 'main adds CiTi word panels for no-split line grouping' $mainCode 'AddCiTiWordGroup'
Assert-Contains 'main forces current display rebuild after config changes' $mainCode 'ForceDisplayRebuildAfterConfigChange();'
Assert-Contains 'config-applied rebuild clears display children before recalculation' $mainCode 'TbDispay.Children.Clear();'
Assert-Contains 'config-applied rebuild clears display blocks before recalculation' $mainCode 'TextInfo.Blocks.Clear();'
Assert-Contains 'config-applied rebuild clears lower code labels before recalculation' $mainCode 'TextInfo.CodeLabels.Clear();'
Assert-Contains 'config-applied rebuild clears state background overlays before recalculation' $mainCode 'TextInfo.StateBackgrounds.Clear();'
Assert-Contains 'page arrange should bypass looking-mode finished diff shortcut' $mainCode 'if (updateLevel < UpdateLevel.PageArrange && IsLookingType && StateManager.LastType)'
Assert-Contains 'main exposes current-page display rebuild for tracing measurement' $mainCode 'internal void RebuildCurrentPageDisplayElementsForTracingMeasurement()'
Assert-Contains 'tracing measurement rebuild preserves CiTi no-split grouping' $mainCode 'AddCiTiNoSplitLineDisplayElements();'
Assert-NotContains 'main no longer reads old code display source' $mainCode 'Config.GetString("编码下显来源")'
Assert-NotContains 'main no longer reads old aggregate code display switch' $mainCode 'Config.GetBool("启用编码下显")'

$noSplitMatch = [regex]::Match($mainCode, 'private bool IsCiTiNoSplitLineEnabled\(\)\s*\{(?<body>.*?)\n        \}', [System.Text.RegularExpressions.RegexOptions]::Singleline)
if (-not $noSplitMatch.Success) {
    throw 'Unable to find MainWindow.IsCiTiNoSplitLineEnabled.'
}
Assert-Contains 'CiTi no-split should be inactive when CiTi prompt is disabled' $noSplitMatch.Groups['body'].Value 'Config.GetBool("启用词提")'

$updateZiTiMatch = [regex]::Match($mainCode, 'internal void UpdateZiTi\(\)\s*\{(?<body>.*?)\n        \}', [System.Text.RegularExpressions.RegexOptions]::Singleline)
if (-not $updateZiTiMatch.Success) {
    throw 'Unable to find MainWindow.UpdateZiTi.'
}
Assert-NotContains 'top-right ZiTi should not be hidden by ZiTi lower display' $updateZiTiMatch.Groups['body'].Value 'Config.GetBool("字提编码下显")'

Assert-NotContains 'paginator no longer expands line height for code display' $paginatorCode 'lineH *= 1.5'
Assert-NotContains 'paginator does not implement CiTi visual no-split-line wrapping' $paginatorCode '"词提不拆行"'

Assert-Contains 'WinConfig adds ZiTi lower display item' $winConfigCode '"字提编码下显"'
Assert-Contains 'WinConfig adds CiTi lower display item' $winConfigCode '"词提编码下显"'
Assert-Contains 'WinConfig adds CiTi no-split-line item' $winConfigCode '"词提不拆行"'
Assert-Contains 'WinConfig adds CiTi Jian1 color item' $winConfigCode '"词提1简色"'
Assert-Contains 'WinConfig adds CiTi Jian2 color item' $winConfigCode '"词提2简色"'
Assert-Contains 'WinConfig adds CiTi Jian3 color item' $winConfigCode '"词提3简色"'
Assert-Contains 'WinConfig adds CiTi normal color item' $winConfigCode '"词提4码色"'
Assert-Contains 'WinConfig adds CiTi non-preferred color item' $winConfigCode '"词提选重色"'
Assert-Contains 'WinConfig shows CiTi color legend' $winConfigCode '颜色说明'
Assert-Contains 'WinConfig explains CiTi no-split-line behavior' $winConfigCode '行尾放不下时整词换行'
Assert-Contains 'WinConfig turns off ZiTi display when CiTi display is enabled' $winConfigCode 'Config.Set("字提编码下显", "否")'
Assert-Contains 'WinConfig turns off CiTi display when ZiTi display is enabled' $winConfigCode 'Config.Set("词提编码下显", "否")'
Assert-Contains 'WinConfig turns on CiTi when CiTi display is enabled' $winConfigCode 'Config.Set("启用词提", "是")'
Assert-Contains 'WinConfig turns on ZiTi when ZiTi display is enabled' $winConfigCode 'Config.Set("启用字提", "是")'
Assert-Contains 'WinConfig turns off CiTi no-split when CiTi is disabled' $winConfigCode 'Config.Set("词提不拆行", "否")'
Assert-Contains 'WinConfig checks CiTi main switch when CiTi display is enabled' $winConfigCode 'FindCheckBoxByLabel("启用词提")'
Assert-Contains 'WinConfig unchecks CiTi no-split when CiTi is disabled' $winConfigCode 'FindCheckBoxByLabel("词提不拆行")'
Assert-Contains 'WinConfig checks ZiTi main switch when ZiTi display is enabled' $winConfigCode 'FindCheckBoxByLabel("启用字提")'
Assert-Contains 'WinConfig creates tooltip label indicator' $winConfigCode 'CreateLabelControl'
Assert-Contains 'WinConfig marks tooltip indicator for discoverability' $winConfigCode 'ConfigTooltipIndicator'
Assert-Contains 'WinConfig copies label tooltip text into popup' $winConfigCode 'var tooltipText = labelBlock.ToolTip?.ToString() ?? ""'
Assert-Contains 'WinConfig finds label text inside label control containers' $winConfigCode 'GetLabelText'
Assert-NotContains 'WinConfig no longer shows old source dropdown' $winConfigCode '"编码下显来源"'
Assert-Contains 'TracingMode restores grouped display elements before mirror measurement' (Get-Content -Path (Join-Path $root 'UI\Modes\TracingMode.cs') -Raw) '_main.RebuildCurrentPageDisplayElementsForTracingMeasurement();'

Write-Host 'All CiTi config plan tests passed.'
