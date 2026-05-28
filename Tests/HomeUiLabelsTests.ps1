$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$mainXaml = Get-Content -Path (Join-Path $root 'UI\MainWindow.xaml') -Raw
$shuangReadme = Get-Content -Path (Join-Path $root 'Resources\Shuang\README.md') -Raw
Add-Type -Path (Join-Path $root 'UI\HomeToolbarSettings.cs')

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

Assert-Contains 'Ctrl+E hotkey' $mainXaml 'Gesture="Ctrl+E"'
Assert-NotContains 'Alt+E hotkey' $mainXaml 'Alt+E'
Assert-Contains 'clipboard button label' $mainXaml 'Content="剪贴板Ctrl+E"'
Assert-Contains 'clipboard menu label' $mainXaml 'Header="剪贴板Ctrl+E"'

Assert-Contains 'shuang button label' $mainXaml 'Content="晴双拼"'
Assert-Contains 'shuang menu label' $mainXaml 'Header="晴双拼"'
Assert-Contains 'trainer button label' $mainXaml 'Content="晴练单"'
Assert-Contains 'trainer menu label' $mainXaml 'Header="晴练单"'
Assert-NotContains 'old trainer menu label' $mainXaml 'Header="练单"'
Assert-Contains 'wenlai race-style button' $mainXaml '<Button x:Name="BtnRandomArticle" DockPanel.Dock="Left" Content="文来Ctrl+R"'
Assert-Contains 'trainer race-style corner' $mainXaml 'x:Name="BtnTrainer" DockPanel.Dock="Left" Content="晴练单"'
Assert-Contains 'shuang race-style corner' $mainXaml 'x:Name="BtnShuang" DockPanel.Dock="Left" Content="晴双拼"'
Assert-Contains 'top button corner helper' $mainXaml 'TopBarGroupedButtonBorder'
Assert-Contains 'clipboard buffer should not reserve layout space' $mainXaml 'Visibility="Collapsed" Focusable="False"'

Assert-Contains 'previous article context item' $mainXaml 'Header="上一段Ctrl+O"'
Assert-Contains 'next article context item' $mainXaml 'Header="下一段Ctrl+P"'
Assert-Contains 'results toggle context item' $mainXaml 'Header="收起成绩"'
Assert-Contains 'super compact context item' $mainXaml 'x:Name="MenuHomeSuperCompact" Header="一键极简" IsCheckable="True"'
$resultsToggleBlockMatch = [regex]::Match($mainXaml, '<Button x:Name="BtnToggleResults"[\s\S]*?</Button>')
if (-not $resultsToggleBlockMatch.Success) {
    throw 'results toggle button block was not found'
}
$resultsToggleBlock = $resultsToggleBlockMatch.Value
Assert-Contains 'results toggle keeps breathing room from right edge' $resultsToggleBlock 'Margin="0,0,10,0"'
Assert-Contains 'results toggle right padding participates in layout' $resultsToggleBlock 'Padding="{TemplateBinding Padding}"'

$configCode = Get-Content -Path (Join-Path $root 'WinConfig\WinConfig.xaml.cs') -Raw
Assert-Contains 'home settings section title' $configCode '首页按纽显示'
Assert-Contains 'home toggle label text binding' $configCode 'label = new TextBlock'
$categoryOrderMatch = [regex]::Match($configCode, 'Title = "主题"[\s\S]*?new ConfigCategory\s*\{\s*Title = "首页"')
if (-not $categoryOrderMatch.Success) {
    throw 'home settings category expected to be directly below theme'
}

$fixedModuleLabels = [TypeSunny.UI.HomeToolbarSettings]::FixedModuleEntries | ForEach-Object { $_.DisplayName }
if (-not ($fixedModuleLabels -contains '本地文章模块')) {
    throw 'fixed local article module label expected to contain [本地文章模块]'
}
if (-not ($fixedModuleLabels -contains '设置')) {
    throw 'fixed settings button label expected to contain [设置]'
}

$mainCode = Get-Content -Path (Join-Path $root 'UI\MainWindow.xaml.cs') -Raw
Assert-Contains 'top button corner applier' $mainCode 'ApplyTopBarButtonCornerRadius'
Assert-Contains 'top bar layout applier' $mainCode 'ApplyTopBarLayout'
Assert-Contains 'top button group collapses when empty' $mainCode 'stack1.Visibility = hasVisibleTopButtons ? Visibility.Visible : Visibility.Collapsed'
Assert-Contains 'top button group margin removed when empty' $mainCode 'stack1.Margin = hasVisibleTopButtons ? TopButtonGroupMargin : new Thickness(0)'
Assert-Contains 'top button row restores auto height' $mainCode 'buttonArea1.Height = GridLength.Auto'
Assert-Contains 'top row min height compacts when buttons hidden' $mainCode 'buttonArea1.MinHeight = hasVisibleTopButtons ? TopBarExpandedMinHeight : TopBarCompactMinHeight'
Assert-Contains 'super compact config key' $mainCode 'SuperCompactModeConfigKey'
Assert-Contains 'super compact trims bottom button row' $mainCode 'TrimSuperCompactBottomButtonRow()'
Assert-Contains 'super compact collapses bottom button row without reserving layout space' $mainCode 'resultsButtonPanel.Visibility = Visibility.Collapsed'
Assert-Contains 'super compact restores bottom button row visibility' $mainCode 'resultsButtonPanel.Visibility = Visibility.Visible'
Assert-Contains 'super compact saves stable layout snapshot' $mainCode 'private SuperCompactLayoutSnapshot _superCompactLayoutSnapshot'
Assert-Contains 'super compact restores before reordering buttons' $mainCode 'if (!isSuperCompact && _isSuperCompactLayoutApplied)'
Assert-Contains 'super compact uses dedicated collapsed layout' $mainCode 'ApplySuperCompactCollapsedLayout(snapshot, true)'
Assert-Contains 'super compact fixes bottom button row height' $mainCode 'typingAreaAndButtonsGrid.RowDefinitions[1].Height = new GridLength(0, GridUnitType.Pixel)'
Assert-Contains 'super compact restores bottom button row auto height' $mainCode 'bottomButtonRow.Height = GridLength.Auto'
Assert-Contains 'super compact skips ratio save' $mainCode 'if (_isSuperCompactLayoutApplied || _suppressWindowSizeChangeUpdatesDepth > 0)'
Assert-NotContains 'super compact should not reuse generic collapsed results layout' $mainCode 'CollapseResultsPanelLayout(shouldAdjustWindowHeight, false, SuperCompactCollapsedBottomBorderHeight)'
Assert-NotContains 'super compact restore must not force bottom row min height' $mainCode 'resultsButtonPanel.MinHeight = BottomButtonPanelExpandedMinHeight'
Assert-Contains 'super compact restores bottom button row' $mainCode 'RestoreSuperCompactBottomButtonRow()'
Assert-Contains 'super compact restore resets window height from snapshot' $mainCode 'this.Height = snapshot.WindowHeight'
Assert-Contains 'super compact suppresses size changed page rearrange' $mainCode 'BeginSuppressWindowSizeChangeUpdates()'
Assert-Contains 'super compact restores size changed suppression later' $mainCode 'EndSuppressWindowSizeChangeUpdatesLater()'
Assert-Contains 'size change handler ignores internal layout updates' $mainCode 'if (_suppressWindowSizeChangeUpdatesDepth > 0)'
Assert-Contains 'super compact restores expanded results layout' $mainCode 'ExpandResultsPanelLayout(true)'
Assert-Contains 'super compact preserves manually collapsed results state' $mainCode 'CollapseResultsPanelLayout(false, false, NormalCollapsedBottomBorderHeight)'
Assert-Contains 'super compact records expanded window height without changing results setting' $mainCode 'else if (adjustWindowHeight)'
Assert-Contains 'super compact closing saves expanded window height' $mainCode 'Config.GetBool(SuperCompactModeConfigKey) && _isResultsExpanded && _expandedWindowHeight > 0'
Assert-Contains 'toggle results uses expanded layout helper' $mainCode 'ExpandResultsPanelLayout(true)'
Assert-Contains 'toggle results uses collapsed layout helper' $mainCode 'CollapseResultsPanelLayout(true, true)'
Assert-Contains 'collapsed layout uses requested bottom border height' $mainCode 'collapsedGridHeight = gridContentHeight - resultsAreaHeight - 5 + collapsedBottomBorderHeight'
Assert-Contains 'results layout has version token' $mainCode 'private int _resultsLayoutVersion'
Assert-Contains 'results layout version helper' $mainCode 'private int BeginResultsLayoutChange()'
Assert-Contains 'results layout stale guard helper' $mainCode 'private bool IsStaleResultsLayoutChange(int layoutVersion)'
Assert-Contains 'super compact settings force reapplies compact layout' $mainCode 'ApplySuperCompactModeLayout(true, true)'
Assert-Contains 'super compact force refresh overload' $mainCode 'private void ApplySuperCompactModeLayout(bool isSuperCompact, bool forceRefresh = false)'
Assert-Contains 'super compact captures visual results state' $mainCode 'ResultsExpanded = IsResultsPanelVisuallyExpanded(mainGrid)'
Assert-Contains 'super compact visual results helper' $mainCode 'private bool IsResultsPanelVisuallyExpanded(Grid mainGrid)'
Assert-Contains 'super compact restores results state from snapshot' $mainCode 'bool shouldRestoreResultsExpanded = snapshot != null ? snapshot.ResultsExpanded : _isResultsExpanded'
Assert-Contains 'super compact syncs expanded state before restore' $mainCode '_isResultsExpanded = shouldRestoreResultsExpanded'
Assert-Contains 'super compact restores expanded snapshot results' $mainCode 'if (shouldRestoreResultsExpanded)'
$resultLayoutBeginCount = ([regex]::Matches($mainCode, 'BeginResultsLayoutChange\(\);')).Count
if ($resultLayoutBeginCount -lt 3) {
    throw 'results layout changes expected to invalidate stale delayed callbacks'
}
$resultLayoutGuardCount = ([regex]::Matches($mainCode, 'IsStaleResultsLayoutChange\(layoutVersion\)')).Count
if ($resultLayoutGuardCount -lt 2) {
    throw 'results layout delayed callbacks expected to ignore stale toggle states'
}
Assert-Contains 'finished prompt includes page shortcuts' $mainCode 'ContinuationShortcutHint = "（Ctrl+O上一段 / Ctrl+P下一段）"'
Assert-Contains 'manual no-error finish queues continuation' $mainCode 'QueuePendingArticleContinuationFor(savedTxtSource)'
Assert-Contains 'manual continuation supports wenlai next' $mainCode 'ArticleContinuationAction.WenlaiNext'
Assert-Contains 'manual continuation supports local next' $mainCode 'ArticleContinuationAction.LocalNext'
Assert-Contains 'trainer title uses trainer stat line' $mainCode 'return "[练单] " + trainerStatText'
Assert-Contains 'trainer title refreshes from trainer stat update' $mainCode 'ApplyTrainerTitleText()'
Assert-Contains 'trainer window title is not overwritten by progress title' $mainCode 'StateManager.txtSource == TxtSource.trainer && !string.IsNullOrEmpty(trainerTitleText)'

$homeConfigDragFeedbackCount = ([regex]::Matches($configCode, 'DragAdorner currentAdorner = null')).Count
if ($homeConfigDragFeedbackCount -lt 2) {
    throw 'home toolbar drag feedback expected to use DragAdorner'
}

$homeConfigInsertionLineCount = ([regex]::Matches($configCode, 'InsertionLineAdorner insertionAdorner = null')).Count
if ($homeConfigInsertionLineCount -lt 2) {
    throw 'home toolbar drag feedback expected to use InsertionLineAdorner'
}

Assert-NotContains 'shuang readme mini program heading' $shuangReadme '微信小程序版'
Assert-NotContains 'shuang readme mini program QR' $shuangReadme '小程序二维码'
Assert-NotContains 'shuang readme mini program image' $shuangReadme 'mini-program-qrcode'

Write-Host 'All home UI label tests passed.'
