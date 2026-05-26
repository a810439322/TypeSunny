$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$mainPath = Join-Path $root 'UI\MainWindow.xaml.cs'
$mainXamlPath = Join-Path $root 'UI\MainWindow.xaml'
$copybookPath = Join-Path $root 'UI\Modes\CopybookMode.cs'
$tracingPath = Join-Path $root 'UI\Modes\TracingMode.cs'
$contextBuilderPath = Join-Path $root 'Logs\DetailedWordCountContextBuilder.cs'
$statsWindowXamlPath = Join-Path $root 'UI\WinDetailedWordCountStatistics.xaml'
$statsWindowCodePath = Join-Path $root 'UI\WinDetailedWordCountStatistics.xaml.cs'
$projectPath = Join-Path $root 'TypeSunny.csproj'
$packagesConfigPath = Join-Path $root 'packages.config'

$mainCode = Get-Content -Raw $mainPath
$mainXaml = Get-Content -Raw $mainXamlPath
$copybookCode = Get-Content -Raw $copybookPath
$tracingCode = Get-Content -Raw $tracingPath
$contextBuilderCode = Get-Content -Raw $contextBuilderPath
$statsWindowXaml = if (Test-Path $statsWindowXamlPath) { Get-Content -Raw $statsWindowXamlPath } else { '' }
$statsWindowCode = if (Test-Path $statsWindowCodePath) { Get-Content -Raw $statsWindowCodePath } else { '' }
$projectCode = Get-Content -Raw $projectPath
$packagesConfig = Get-Content -Raw $packagesConfigPath

function Assert-Contains($name, $text, $needle) {
    if ($text -notlike "*$needle*") {
        throw "$name expected to contain: $needle"
    }
}

function Assert-Regex($name, $text, $pattern) {
    if ($text -notmatch $pattern) {
        throw "$name expected to match pattern: $pattern"
    }
}

Assert-Contains 'main has detailed context field' $mainCode 'TypingWordCountContext currentWordCountContext'
Assert-Contains 'main owns detailed context builder' $mainCode 'DetailedWordCountContextBuilder _wordCountContextBuilder'
Assert-Contains 'load text stores detailed context' $mainCode 'currentWordCountContext = _wordCountContextBuilder.Build'
Assert-Contains 'main exposes detailed word recorder' $mainCode 'RecordDetailedTypedWords'
Assert-Contains 'main migrates detailed totals on startup' $mainCode 'DetailedWordCountLog.EnsureMigrated'
Assert-Contains 'main flushes detailed totals on shutdown' $mainCode 'DetailedWordCountLog.Flush'
Assert-Contains 'context builder contains detailed category mapping' $contextBuilderCode 'public TypingWordCountContext Build'
Assert-Contains 'context builder handles wenlai category' $contextBuilderCode 'category:wenlai:'
if ($mainCode -like '*category:wenlai:*' -or $mainCode -like '*category:book:*' -or $mainCode -like '*category:trainer:*') {
    throw 'MainWindow should not own detailed word count category mapping'
}

Assert-Regex `
    'normal input records detailed words near counter buffer' `
    $mainCode `
    'CounterLog\.Buffer\[0\]\s*\+=\s*addedLength;[\s\S]{0,240}RecordDetailedTypedWords\(addedLength\);'

Assert-Regex `
    'copybook records detailed words near counter buffer' `
    $copybookCode `
    'CounterLog\.Buffer\[0\]\s*\+=\s*si\.LengthInTextElements;[\s\S]{0,240}_main\.RecordDetailedTypedWords\(si\.LengthInTextElements\);'

Assert-Regex `
    'tracing records detailed words near counter buffer' `
    $tracingCode `
    'CounterLog\.Buffer\[0\]\s*\+=\s*si\.LengthInTextElements;[\s\S]{0,240}_main\.RecordDetailedTypedWords\(si\.LengthInTextElements\);'

Assert-Regex `
    'results textbox runtime context menu has detailed statistics item' `
    $mainCode `
    'TbxResults_Loaded[\s\S]{0,900}detailedWordCountItem[\s\S]{0,400}MenuItemDetailedWordCountStatistics_Click'
Assert-Contains 'main opens detailed statistics window' $mainCode 'WinDetailedWordCountStatistics'
Assert-Contains 'results textbox detailed context menu applies theme' $mainCode 'ThemeColorHelper.ApplyContextMenuTheme'
Assert-Regex `
    'statistics window has six summary columns' `
    $statsWindowXaml `
    'x:Name="summaryGrid"[\s\S]*?(<ColumnDefinition Width="\*"/>\s*){6}[\s\S]*?x:Name="summaryTypingDaysBorder"'
Assert-Contains 'statistics window summary shows article words' $statsWindowXaml 'Text="文章字数"'
Assert-Contains 'statistics window summary shows trainer words' $statsWindowXaml 'Text="练单字数"'
Assert-Contains 'statistics window summary shows typing days' $statsWindowXaml 'Text="打字天数"'
if ($statsWindowXaml -like '*分类合计*' -or $statsWindowXaml -like '*难度统计字数*' -or $statsWindowXaml -like '*Text="状态"*') {
    throw 'statistics window summary should not show category total, difficulty total, or alignment status'
}
if ($statsWindowXaml -like '*WindowChrome.WindowChrome*') {
    throw 'statistics window should use the same custom borderless chrome as the main window, not WindowChrome'
}
Assert-Contains 'statistics window uses transparent custom chrome' $statsWindowXaml 'AllowsTransparency="True"'
Assert-Contains 'statistics window is borderless' $statsWindowXaml 'WindowStyle="None"'
Assert-Contains 'statistics window has minimize button' $statsWindowXaml 'x:Name="BtnMinimize"'
Assert-Contains 'statistics window has maximize button' $statsWindowXaml 'x:Name="BtnMaximize"'
Assert-Contains 'statistics window has close button' $statsWindowXaml 'x:Name="BtnClose"'
Assert-Contains 'statistics window has close button red hover style' $statsWindowXaml 'x:Key="TitleBarCloseButtonStyle"'
Assert-Contains 'statistics window close button hover matches main window red' $statsWindowXaml '#E81123'
Assert-Contains 'statistics window close button pressed matches main window red' $statsWindowXaml '#C50B1D'
Assert-Contains 'statistics window close button uses close style' $statsWindowXaml 'Style="{StaticResource TitleBarCloseButtonStyle}"'
Assert-Contains 'statistics window has top resize border' $statsWindowXaml 'Name="ResizeTop"'
Assert-Contains 'statistics window has right resize border' $statsWindowXaml 'Name="ResizeRight"'
Assert-Contains 'statistics window uses custom WPF pie host' $statsWindowXaml 'x:Name="categoryPieHost"'
Assert-Contains 'statistics window uses custom WPF pie canvas' $statsWindowXaml 'x:Name="categoryPieCanvas"'
Assert-Contains 'statistics window has pie hover info surface' $statsWindowXaml 'x:Name="txtCategoryPieHoverInfo"'
Assert-Contains 'statistics custom pie redraws when resized' $statsWindowXaml 'SizeChanged="CategoryPieHost_SizeChanged"'
if ($statsWindowXaml -like '*lvc:PieChart*' -or $statsWindowXaml -like '*ChartPointPointerDown=*' -or $statsWindowXaml -like '*DataPointerDown=*') {
    throw 'statistics category pie should be custom WPF drawing because LiveCharts labels/clicks are unreliable here'
}
Assert-Contains 'statistics chart row can share height with detail row' $statsWindowXaml '<RowDefinition Height="2*"'
Assert-Contains 'statistics detail row can grow when resized' $statsWindowXaml '<RowDefinition Height="*"'
Assert-Contains 'statistics detail height can be resized by splitter' $statsWindowXaml 'x:Name="categoryDetailSplitter"'
Assert-Contains 'statistics detail splitter resizes rows' $statsWindowXaml 'ResizeDirection="Rows"'
Assert-Contains 'statistics pie minimizes chart padding' $statsWindowXaml 'x:Name="categoryChartBorder" BorderThickness="1" Padding="0"'
if ($statsWindowXaml -like '*x:Name="categorySelector"*') {
    throw 'statistics window should not use a separate category checkbox selector'
}
if ($statsWindowXaml -like '*x:Name="chkMergeCategoryProjects"*') {
    throw 'statistics window merge control should be a large toggle button, not a small checkbox'
}
if ($statsWindowXaml -like '*同项目归并*' -or $statsWindowXaml -like '*项目详情*' -or $statsWindowXaml -like '*Header="项目"*') {
    throw 'statistics window should use category wording instead of project wording'
}
if ($statsWindowCode -like '*当前显示全部项目*') {
    throw 'statistics focus text should use category wording instead of project wording'
}
Assert-Contains 'statistics window has category merge toggle' $statsWindowXaml 'x:Name="tglMergeCategoryProjects"'
Assert-Contains 'statistics window merge toggle uses category wording' $statsWindowXaml 'Content="同分类合并"'
Assert-Contains 'statistics window has optional history toggle' $statsWindowXaml 'x:Name="tglShowHistoryCategory"'
Assert-Contains 'statistics window can reset chart focus' $statsWindowXaml 'x:Name="btnResetCategoryFocus"'
Assert-Contains 'statistics reset focus button has clear state label' $statsWindowCode 'UpdateResetCategoryFocusButton'
Assert-Contains 'statistics reset focus button hides until chart focus is active' $statsWindowCode 'btnResetCategoryFocus.Visibility = string.IsNullOrWhiteSpace(focusedCategoryKey)'
Assert-Contains 'statistics reset focus button only offers actionable reset text' $statsWindowCode 'btnResetCategoryFocus.Content = "显示全部"'
Assert-Contains 'statistics refresh button updates visible refresh timestamp' $statsWindowCode '最后刷新：'
Assert-Contains 'statistics refresh timestamp uses current refresh time' $statsWindowCode 'LoadStatistics(DateTime refreshTime)'
if ($statsWindowCode -match 'btnResetCategoryFocus\.Content[\s\S]{0,80}"全部项目"') {
    throw 'statistics reset focus button should not show a non-actionable all-category state'
}
Assert-Contains 'statistics window uses stable difficulty list' $statsWindowXaml 'x:Name="difficultyList"'
Assert-Contains 'statistics window renames difficulty section' $statsWindowXaml 'Text="打文难度分布"'
Assert-Contains 'statistics difficulty rows receive hover enter animation' $statsWindowXaml 'MouseEnter="DifficultyRow_MouseEnter"'
Assert-Contains 'statistics difficulty rows receive hover leave animation' $statsWindowXaml 'MouseLeave="DifficultyRow_MouseLeave"'
Assert-Contains 'statistics difficulty rows have comfortable hit height' $statsWindowXaml 'MinHeight="34"'
Assert-Regex `
    'statistics difficulty rows use transparent background as full hit area' `
    $statsWindowXaml `
    '<Grid x:Name="difficultyRowRoot"[\s\S]{0,220}Background="Transparent"'
if ($statsWindowXaml -match '<Grid x:Name="difficultyRowRoot"[\s\S]{0,120}Margin="0,0,0,\d+"') {
    throw 'statistics difficulty rows should not use bottom margin because it creates a dead hover gap between adjacent rows'
}
Assert-Contains 'statistics difficulty rows expose hover background' $statsWindowXaml 'x:Name="difficultyRowHoverBg"'
Assert-Contains 'statistics difficulty rows expose animated bar fill' $statsWindowXaml 'x:Name="difficultyBarFill"'
Assert-Contains 'statistics detail table extends across both columns' $statsWindowXaml 'x:Name="categoryDetailGrid"'
Assert-Contains 'statistics detail table uses category wording' $statsWindowXaml 'Text="分类详情"'
Assert-Contains 'statistics detail first column uses category wording' $statsWindowXaml 'Header="分类"'
Assert-Contains 'statistics detail table spans both columns' $statsWindowXaml 'Grid.ColumnSpan="2"'
if ($statsWindowXaml -like '*lvc:CartesianChart*') {
    throw 'statistics window should not use cartesian chart for difficulty distribution'
}
Assert-Contains 'statistics window chart failure mentions full update' $statsWindowCode '全量更新'
Assert-Contains 'statistics window themes datagrid headers' $statsWindowCode 'DataGridColumnHeader'
Assert-Contains 'statistics window themes custom chart text' $statsWindowCode 'chartTextBrush'
Assert-Contains 'statistics window themes custom chart background' $statsWindowCode 'categoryPieHost.Background'
Assert-Contains 'statistics window themes custom chart connector' $statsWindowCode 'chartConnectorBrush'
Assert-Contains 'statistics window themes difficulty track' $statsWindowCode 'TrackBrush'
Assert-Contains 'statistics window themes difficulty hover background' $statsWindowCode 'difficultyHoverBrush'
Assert-Contains 'statistics window themes category toggles' $statsWindowCode 'ApplyToggleTheme'
Assert-Contains 'statistics window manually centers over owner without owner z-order' $statsWindowCode 'CenterOverOwner'
if ($statsWindowCode -match '(?m)^\s*Owner\s*=\s*owner\s*;') {
    throw 'statistics window must not set Owner because it keeps the window above the main window'
}
Assert-Contains 'statistics window exposes theme refresh' $statsWindowCode 'public void RefreshTheme()'
Assert-Contains 'config refreshes detailed statistics window theme' (Get-Content -Raw (Join-Path $root 'WinConfig\WinConfig.xaml.cs')) 'WinDetailedWordCountStatistics'
Assert-Contains 'statistics window supports title drag' $statsWindowCode 'DragMove()'
Assert-Contains 'statistics window supports custom resize' $statsWindowCode 'ResizeBorder_MouseLeftButtonDown'
Assert-Contains 'statistics pie draws solid WPF slices' $statsWindowCode 'CreatePieSliceGeometry'
Assert-Contains 'statistics pie draws a real circle when only one category exists' $statsWindowCode 'DrawFullPieCircle'
Assert-Contains 'statistics pie draws label connector lines' $statsWindowCode 'DrawPieLabel'
Assert-Contains 'statistics pie calculates labels before drawing to avoid overlap' $statsWindowCode 'BuildPieLabelLayouts'
Assert-Contains 'statistics pie separates overlapping label rows' $statsWindowCode 'ArrangePieLabelRows'
Assert-Contains 'statistics pie labels include category name' $statsWindowCode 'FormatPieDataLabel'
Assert-Contains 'statistics pie labels stay on one line' $statsWindowCode 'FormatPieDataLabel(layout.Item)'
Assert-Contains 'statistics pie labels keep enough Chinese category name text' $statsWindowCode 'ShortenLabel(item.DisplayName, 18)'
if ($statsWindowCode -like '*ShortenLabel(item.DisplayName, 6)*' -or $statsWindowCode -like '*ShortenLabel(item.DisplayName, 12)*') {
    throw 'statistics pie labels should not truncate category names to only a few Chinese characters'
}
Assert-Contains 'statistics pie percent stays in tooltip' $statsWindowCode 'FormatPieTooltip(item, chartTotal)'
if ($statsWindowCode -like '*ShortenLabel(item.DisplayName, 8) + "\n"*' -or $statsWindowCode -like '*+ "\n" + item.Words*') {
    throw 'statistics pie labels should not contain newline characters because LiveCharts can render them as square glyphs'
}
if ($statsWindowCode -like '*DataLabelsFormatter*' -or $statsWindowCode -like '*LiveChartsCore*') {
    throw 'statistics pie should not depend on LiveCharts data labels'
}
if ($statsWindowCode -like '*SkiaSharp*' -or $statsWindowCode -like '*SKColor*') {
    throw 'statistics pie should use WPF drawing primitives only, not SkiaSharp'
}
if ($projectCode -like '*LiveChartsCore*' -or $projectCode -like '*SkiaSharp*' -or $projectCode -like '*HarfBuzzSharp*') {
    throw 'project should not reference chart/Skia packages after replacing the chart with custom WPF drawing'
}
if ($packagesConfig -like '*LiveChartsCore*' -or $packagesConfig -like '*SkiaSharp*' -or $packagesConfig -like '*HarfBuzzSharp*') {
    throw 'packages.config should not keep chart/Skia packages after replacing the chart with custom WPF drawing'
}
Assert-Contains 'statistics pie click updates category focus' $statsWindowCode 'CategoryPieSlice_MouseLeftButtonDown'
Assert-Contains 'statistics pie click reads category key from WPF element tag' $statsWindowCode 'element.Tag as string'
Assert-Contains 'statistics pie has hover enter animation' $statsWindowCode 'CategoryPieSlice_MouseEnter'
Assert-Contains 'statistics pie has hover leave animation' $statsWindowCode 'CategoryPieSlice_MouseLeave'
Assert-Contains 'statistics pie animates hovered slices subtly' $statsWindowCode 'AnimatePieSlice'
Assert-Contains 'statistics pie uses real WPF hover animation' $statsWindowCode 'BeginAnimation'
Assert-Contains 'statistics pie hover shows current slice details' $statsWindowCode 'ShowPieHoverInfo'
Assert-Contains 'statistics pie groups slice connector and label hover by key' $statsWindowCode 'RegisterPieElement'
Assert-Contains 'statistics pie stores grouped hover elements by key' $statsWindowCode 'pieElementsByKey'
Assert-Contains 'statistics pie connector lines participate in hover' $statsWindowCode 'RegisterPieElement(layout.Item.Key, layout.Item, connector)'
Assert-Contains 'statistics pie connector and label clicks use same grouped hit logic' $statsWindowCode 'element.MouseLeftButtonDown += CategoryPieSlice_MouseLeftButtonDown'
Assert-Contains 'statistics difficulty list has hover enter handler' $statsWindowCode 'DifficultyRow_MouseEnter'
Assert-Contains 'statistics difficulty list has hover leave handler' $statsWindowCode 'DifficultyRow_MouseLeave'
Assert-Contains 'statistics difficulty list animates rows' $statsWindowCode 'AnimateDifficultyRow'
Assert-Contains 'statistics difficulty list animates the row scale' $statsWindowCode 'ScaleTransform.ScaleXProperty'
Assert-Contains 'statistics difficulty animation replaces frozen transforms before animation' $statsWindowCode 'scale.IsFrozen'
Assert-Contains 'statistics window can refresh detail rows without rebuilding pie' $statsWindowCode 'RefreshCategoryDetailRows'
Assert-Regex `
    'statistics pie click keeps existing series labels alive' `
    $statsWindowCode `
    'CategoryPieSlice_MouseLeftButtonDown[\s\S]*RefreshCategoryDetailRows\(\);'
if ($statsWindowCode -match 'CategoryPieChart_DataPointerDown' -or $statsWindowCode -match 'CategoryPieChart_ChartPointPointerDown') {
    throw 'statistics pie should not keep LiveCharts pointer handlers'
}
if ($statsWindowCode -like '*CategorySelectorItem*') {
    throw 'statistics window should not keep the old category checkbox selector view model'
}
Assert-Contains 'statistics window supports merging same categories' $statsWindowCode 'tglMergeCategoryProjects.IsChecked == true'
Assert-Contains 'statistics window keeps history available for optional chart display' $statsWindowCode 'tglShowHistoryCategory.IsChecked == true'
Assert-Contains 'statistics window preserves trainer grouping when merging' $statsWindowCode 'category:trainer'
Assert-Regex 'statistics window summary binds article words' $statsWindowCode 'txtArticleWords\.Text\s*=\s*\w+\.ArticleWords\.ToString\(\)'
Assert-Regex 'statistics window summary binds trainer words' $statsWindowCode 'txtTrainerWords\.Text\s*=\s*\w+\.TrainerWords\.ToString\(\)'
Assert-Regex 'statistics window summary binds typing days' $statsWindowCode 'txtTypingDays\.Text\s*=\s*\w+\.TypingDays\.ToString\(\)'
Assert-Contains 'detailed log exposes visible category items' (Get-Content -Raw (Join-Path $root 'Logs\DetailedWordCountLog.cs')) 'VisibleCategoryItems'
Assert-Contains 'detailed log keeps history in visible category items for optional selection' (Get-Content -Raw (Join-Path $root 'Logs\DetailedWordCountLog.cs')) 'BuildCategoryDisplayItems(categoryItems'
Assert-Contains 'detailed log excludes history from article words' (Get-Content -Raw (Join-Path $root 'Logs\DetailedWordCountLog.cs')) 'nonHistoryWords'
Assert-Contains 'detailed log can merge trainer categories' (Get-Content -Raw (Join-Path $root 'Logs\DetailedWordCountLog.cs')) 'category:trainer'
Assert-Contains 'detailed log uses trainer display label' (Get-Content -Raw (Join-Path $root 'Logs\DetailedWordCountLog.cs')) '练单'
Assert-Contains 'detailed context builder uses trainer display helper' $contextBuilderCode 'FormatTrainerCategoryDisplayName'
if ($contextBuilderCode -like '*"晴练单 / " + title*') {
    throw 'detailed context builder should not store raw qing trainer labels'
}
Assert-Contains 'detailed log persists typing dates' (Get-Content -Raw (Join-Path $root 'Logs\DetailedWordCountLog.cs')) 'TypingDates'
Assert-Contains 'results textbox detailed context menu uses common menu theme helper' $mainCode 'ApplyResultsContextMenuTheme'
Assert-Contains 'results textbox menu themes on open' $mainCode 'ResultsContextMenu_Opened'

Write-Host 'Detailed word count integration tests passed.'
