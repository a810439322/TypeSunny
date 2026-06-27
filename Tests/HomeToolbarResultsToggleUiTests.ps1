$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$mainXaml = Get-Content -Path (Join-Path $root 'UI\MainWindow.xaml') -Raw -Encoding UTF8
$mainCode = Get-Content -Path (Join-Path $root 'UI\MainWindow.xaml.cs') -Raw -Encoding UTF8

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

Assert-Contains 'Ctrl+J command resource exists' $mainXaml '<RoutedUICommand x:Key="CtrlJ" Text="CtrlJ"/>'
Assert-Contains 'Ctrl+J input binding exists' $mainXaml '<KeyBinding Gesture="Ctrl+J"  Command="{StaticResource CtrlJ}" />'
Assert-Contains 'Ctrl+J command binding exists' $mainXaml '<CommandBinding Command="{StaticResource CtrlJ}" Executed="InternalHotkeyCtrlJ"/>'
Assert-Contains 'Ctrl+J handler exists' $mainCode 'private void InternalHotkeyCtrlJ(object sender, ExecutedRoutedEventArgs e)'
Assert-Contains 'Ctrl+J reuses results toggle handler' $mainCode 'BtnToggleResults_Click(sender, e);'

Assert-Contains 'bottom toolbar grid exists' $mainXaml 'x:Name="resultsButtonPanel"'
Assert-Contains 'left feature toolbar host exists' $mainXaml 'x:Name="FeatureToolbarPanel"'
Assert-Contains 'right command toolbar host exists' $mainXaml 'x:Name="RightCommandToolbarPanel"'
Assert-Contains 'results toggle is placed in right command column' $mainXaml 'Grid.Column="3"'
Assert-Contains 'results toggle chevron path exists' $mainXaml 'x:Name="ResultsToggleChevronIcon"'
Assert-Contains 'expanded chevron geometry exists' $mainCode 'ResultsToggleChevronUpGeometry = "M18 15 L12 9 L6 15"'
Assert-Contains 'collapsed chevron geometry exists' $mainCode 'ResultsToggleChevronDownGeometry = "M6 9 L12 15 L18 9"'
Assert-Contains 'compact expanded chevron geometry exists' $mainCode 'ResultsToggleCompactChevronUpGeometry = "M11.25 9.375 L7.5 5.625 L3.75 9.375"'
Assert-Contains 'compact collapsed chevron geometry exists' $mainCode 'ResultsToggleCompactChevronDownGeometry = "M3.75 5.625 L7.5 9.375 L11.25 5.625"'
Assert-Contains 'results toggle uses icon updater' $mainCode 'SetResultsToggleChevron(isResultsExpanded:'
Assert-NotContains 'results toggle no longer uses text down arrow' $mainCode 'BtnToggleResults.Content = "▼";'
Assert-NotContains 'results toggle no longer uses text up arrow' $mainCode 'BtnToggleResults.Content = "▲";'
Assert-Contains 'results toggle restored to right grid column' $mainCode 'Grid.SetColumn(BtnToggleResults, 3);'
Assert-Contains 'results toggle compact host also keeps right column' $mainCode 'Grid.SetColumn(BtnToggleResults, 0);'
Assert-NotContains 'results toggle no longer relies on DockPanel right docking' $mainCode 'DockPanel.SetDock(BtnToggleResults, Dock.Right);'

$compactHostDeclarationIndex = $mainXaml.IndexOf('x:Name="CompactResultsToggleHost"')
$resultsPanelDeclarationIndex = $mainXaml.IndexOf('x:Name="resultsButtonPanel"')
if ($compactHostDeclarationIndex -lt 0) {
    throw 'compact results toggle host was not found'
}
if ($resultsPanelDeclarationIndex -lt 0) {
    throw 'results button panel was not found'
}
if ($compactHostDeclarationIndex -gt $resultsPanelDeclarationIndex) {
    throw 'compact results toggle host expected to be declared before the bottom toolbar panel'
}

$compactHostLineMatch = [regex]::Match($mainXaml, '<Grid x:Name="CompactResultsToggleHost"[^>]*/>')
if (-not $compactHostLineMatch.Success) {
    throw 'compact results toggle host declaration was not found'
}
$compactHostLine = $compactHostLineMatch.Value
Assert-Contains 'compact results toggle host overlays the whole typing/buttons container' $compactHostLine 'Grid.RowSpan="2"'
Assert-Contains 'compact results toggle host is aligned to outer right edge' $compactHostLine 'HorizontalAlignment="Right"'
Assert-Contains 'compact results toggle host aligns with input/result content right edge' $compactHostLine 'Margin="0,0,15,0"'
Assert-NotContains 'compact results toggle host should not live in input text column' $compactHostLine 'Grid.Column="0"'

$superCompactTrimBlockMatch = [regex]::Match($mainCode, 'private void TrimSuperCompactBottomButtonRow\(bool adjustWindowHeight\)[\s\S]*?private void RestoreSuperCompactBottomButtonRow\(\)')
if (-not $superCompactTrimBlockMatch.Success) {
    throw 'super compact trim block was not found'
}
$superCompactTrimBlock = $superCompactTrimBlockMatch.Value
Assert-Contains 'super compact keeps results toggle in compact overlay before hiding toolbar row' $superCompactTrimBlock 'MoveResultsToggleToCompactHost();'
Assert-Contains 'super compact trim routes through unified bottom layout' $superCompactTrimBlock '"super compact enter"'
Assert-Contains 'super compact trim allows unified internal height compensation' $superCompactTrimBlock 'allowHeightAdjustmentDuringInternalLayout: true'
Assert-NotContains 'super compact trim must not mutate bottom row height directly' $superCompactTrimBlock 'typingAreaAndButtonsGrid.RowDefinitions[1].Height = new GridLength(0, GridUnitType.Pixel)'
Assert-NotContains 'super compact trim must not hide results panel directly' $superCompactTrimBlock 'resultsButtonPanel.Visibility = Visibility.Collapsed'

$superCompactRestoreBlockMatch = [regex]::Match($mainCode, 'private void RestoreSuperCompactBottomButtonRow\(\)[\s\S]*?private void ApplySuperCompactCollapsedLayout')
if (-not $superCompactRestoreBlockMatch.Success) {
    throw 'super compact restore block was not found'
}
$superCompactRestoreBlock = $superCompactRestoreBlockMatch.Value
Assert-Contains 'super compact restore reapplies current bottom toolbar mode directly' $superCompactRestoreBlock 'ApplyHomeBottomLayout("super compact restore", adjustWindowHeight: false);'
Assert-NotContains 'super compact restore should not force normal toolbar margin before compact decision' $superCompactRestoreBlock 'BottomToolbarNormalMargin'
Assert-NotContains 'super compact restore should not force auto bottom row before compact decision' $superCompactRestoreBlock 'bottomButtonRow.Height = GridLength.Auto'

$superCompactApplyBlockMatch = [regex]::Match($mainCode, 'private void ApplySuperCompactModeLayout\(bool isSuperCompact, bool forceRefresh = false, double normalWindowHeightOverride = 0\)[\s\S]*?private bool IsResultsPanelVisuallyExpanded')
if (-not $superCompactApplyBlockMatch.Success) {
    throw 'super compact apply block was not found'
}
$superCompactApplyBlock = $superCompactApplyBlockMatch.Value
$restoreCallIndex = $superCompactApplyBlock.IndexOf('RestoreSuperCompactBottomButtonRow();')
$clearAppliedIndex = $superCompactApplyBlock.IndexOf('_isSuperCompactLayoutApplied = false;')
$restoreResultsIndex = $superCompactApplyBlock.IndexOf('_isResultsExpanded = shouldRestoreResultsExpanded;')
if ($restoreCallIndex -lt 0 -or $clearAppliedIndex -lt 0 -or $restoreResultsIndex -lt 0) {
    throw 'super compact exit ordering markers were not found'
}
if ($restoreCallIndex -lt $clearAppliedIndex -or $restoreCallIndex -lt $restoreResultsIndex) {
    throw 'super compact exit should reapply bottom toolbar only after clearing compact state and restoring results state'
}

$compactLayoutBlockMatch = [regex]::Match($mainCode, 'private void ApplyCompactBottomToolbarLayout\(HomeBottomToolbarLayoutPlan plan\)[\s\S]*?private void RestoreNormalBottomToolbarLayout\(HomeBottomToolbarLayoutPlan plan\)')
if (-not $compactLayoutBlockMatch.Success) {
    throw 'compact bottom toolbar layout block was not found'
}
$compactLayoutBlock = $compactLayoutBlockMatch.Value
Assert-Contains 'compact results toggle button uses compact height' $compactLayoutBlock 'BtnToggleResults.Height = ResultsToggleCompactHeight;'
Assert-Contains 'compact results toggle icon viewport uses compact height' $compactLayoutBlock 'ResultsToggleChevronViewport.Height = ResultsToggleCompactIconViewportSize;'
Assert-Contains 'compact results toggle icon viewport uses compact width' $compactLayoutBlock 'ResultsToggleChevronViewport.Width = ResultsToggleCompactIconViewportSize;'
Assert-Contains 'compact results toggle height matches content side inset' $mainCode 'private const double ResultsToggleCompactHeight = 15;'
Assert-Contains 'compact results toggle icon viewport matches content side inset' $mainCode 'private const double ResultsToggleCompactIconViewportSize = 15;'
Assert-Contains 'compact collapsed clickable row height matches content side inset' $mainCode 'private const double CompactCollapsedBottomToolbarHeight = 15;'
Assert-Contains 'compact bottom toolbar clears extra bottom border row through policy plan' $compactLayoutBlock 'ApplyCollapsedResultsBottomBorderHeight(plan.CollapsedBottomBorderHeight);'
Assert-Contains 'compact bottom toolbar keeps a planned reserved hit-test row' $compactLayoutBlock 'ApplyBottomToolbarReservedHeight(plan.ToolbarReservedHeight, true);'
Assert-Contains 'compact bottom toolbar hides full-width empty toolbar panel' $compactLayoutBlock 'resultsButtonPanel.Visibility = Visibility.Collapsed;'
Assert-Contains 'compact bottom toolbar keeps host inside parent hit-test bounds' $compactLayoutBlock 'CompactResultsToggleHost.Margin = CompactResultsToggleMargin;'
Assert-Contains 'compact bottom toolbar keeps toggle host visible when results are expanded' $compactLayoutBlock 'CompactResultsToggleHost.Visibility = Visibility.Visible;'
Assert-Contains 'compact bottom toolbar follows policy toggle host placement' $compactLayoutBlock 'if (plan.UseCompactToggleHost)'
Assert-NotContains 'compact bottom toolbar must not hide expanded results toggle host' $compactLayoutBlock 'CompactResultsToggleHost.Visibility = _isResultsExpanded ? Visibility.Collapsed : Visibility.Visible;'
Assert-NotContains 'compact bottom toolbar must not move toggle outside parent hit-test bounds' $mainCode '-CompactCollapsedBottomBorderHeight'
Assert-NotContains 'compact bottom toolbar must not use negative collapsed toggle margin' $mainCode 'CompactResultsToggleCollapsedMargin'
Assert-Contains 'compact bottom toolbar reapplies compact chevron geometry' $compactLayoutBlock 'SetResultsToggleChevron(_isResultsExpanded);'

$bottomToolbarLayoutBlockMatch = [regex]::Match($mainCode, 'private void ApplyHomeBottomLayout\([\s\S]*?private static HomeBottomToolbarLayoutMode GetCurrentHomeBottomLayoutMode\(')
if (-not $bottomToolbarLayoutBlockMatch.Success) {
    throw 'bottom toolbar layout applier block was not found'
}
$bottomToolbarLayoutBlock = $bottomToolbarLayoutBlockMatch.Value
Assert-Contains 'bottom toolbar layout captures previous actual footer height before applying target plan' $bottomToolbarLayoutBlock 'double previousToolbarHeight = GetCurrentHomeBottomLayoutActualFooterHeight();'
Assert-Contains 'bottom toolbar layout captures super compact state once before measurement' $bottomToolbarLayoutBlock 'bool isSuperCompact = IsSuperCompactBottomLayoutActive();'
Assert-Contains 'bottom toolbar layout captures feature button count once before measurement' $bottomToolbarLayoutBlock 'int visibleFeatureButtonCount = GetVisibleBottomFeatureButtonCount();'
Assert-Contains 'bottom toolbar layout captures local article visibility once before measurement' $bottomToolbarLayoutBlock 'bool hasVisibleLocalArticleModule = HasVisibleBottomCommandButtons();'
Assert-Contains 'bottom toolbar layout decides final mode before mutating live UI for measurement' $bottomToolbarLayoutBlock 'var layoutMode = GetCurrentHomeBottomLayoutMode('
Assert-Contains 'bottom toolbar layout measures normal height only for normal target mode' $bottomToolbarLayoutBlock 'layoutMode == HomeBottomToolbarLayoutMode.Normal'
Assert-Contains 'bottom toolbar layout reuses cached normal height for compact target mode' $bottomToolbarLayoutBlock ': _lastNormalBottomToolbarHeight;'
Assert-NotContains 'bottom toolbar compact target must not restore normal layout just to measure' $bottomToolbarLayoutBlock '? _lastNormalBottomToolbarHeight'
Assert-Contains 'bottom toolbar layout creates final plan from captured state' $bottomToolbarLayoutBlock 'visibleFeatureButtonCount,'
Assert-Contains 'bottom toolbar layout creates final plan from captured local article state' $bottomToolbarLayoutBlock 'hasVisibleLocalArticleModule);'
Assert-Contains 'bottom toolbar layout applies window height change only from unified entrypoint' $bottomToolbarLayoutBlock 'ApplyBottomToolbarHeightAdjustmentIfNeeded('
Assert-Contains 'bottom toolbar layout compensates with planned footer total' $bottomToolbarLayoutBlock 'GetPlannedHomeBottomLayoutFooterHeight(plan)'
Assert-Contains 'home toolbar applies unified bottom layout after inserting buttons' $mainCode 'ApplyHomeBottomLayout("toolbar settings", adjustWindowHeight: true);'
Assert-Contains 'home toolbar avoids transient normal measurement before one-key compact enter' $mainCode 'if (!isSuperCompact || !applySuperCompactMode)'
Assert-Contains 'bottom toolbar actual feature visibility helper exists' $mainCode 'private int GetVisibleBottomFeatureButtonCount()'
Assert-Contains 'bottom toolbar actual command visibility helper exists' $mainCode 'private bool HasVisibleBottomCommandButtons()'
Assert-Contains 'bottom toolbar counts feature buttons from actual panel children' $mainCode 'FeatureToolbarPanel.Children.OfType<UIElement>()'
Assert-Contains 'bottom toolbar command visibility follows local article module config' $mainCode 'TrainerMainWindowConfigScope.GetBool(HomeToolbarSettings.ShowLocalArticleConfigKey)'
Assert-NotContains 'bottom toolbar must not let stale right-panel visibility keep empty toolbar tall' $mainCode 'RightCommandToolbarPanel.Children.OfType<UIElement>()'
Assert-Contains 'bottom toolbar has one unified home bottom layout entrypoint' $mainCode 'private void ApplyHomeBottomLayout('
Assert-Contains 'bottom toolbar creates pure policy plans from captured state' $mainCode 'private HomeBottomToolbarLayoutPlan CreateCurrentHomeBottomLayoutPlan('
Assert-Contains 'bottom toolbar measures normal content before fixing row height' $mainCode 'private double MeasureNormalBottomToolbarHeight()'
Assert-Contains 'bottom toolbar integration uses pure policy plan' $mainCode 'HomeBottomToolbarLayoutPolicy.CreatePlan('
Assert-NotContains 'bottom toolbar no longer keeps stale mode cache' $mainCode '_lastBottomToolbarLayoutMode'
Assert-NotContains 'bottom toolbar no longer keeps stale reserved height cache' $mainCode '_currentBottomToolbarReservedHeight'
Assert-NotContains 'bottom toolbar no longer resolves previous mode from mixed visual state' $mainCode 'ResolvePreviousBottomToolbarLayoutMode'
Assert-NotContains 'bottom toolbar no longer uses old reserved height helper' $mainCode 'private double GetBottomToolbarReservedHeight(HomeBottomToolbarLayoutMode? layoutMode)'
Assert-Contains 'bottom toolbar has one authoritative row height applier' $mainCode 'private void ApplyBottomToolbarReservedHeight(double reservedHeight, bool clipToBounds)'
Assert-Contains 'bottom toolbar compact uses planned reserved height' $compactLayoutBlock 'ApplyBottomToolbarReservedHeight(plan.ToolbarReservedHeight, true);'
Assert-NotContains 'bottom toolbar compact must not keep full-width panel visible when every bottom button is hidden' $compactLayoutBlock 'resultsButtonPanel.Visibility = Visibility.Visible'
Assert-Contains 'bottom toolbar current actual height helper exists' $mainCode 'private double GetCurrentBottomToolbarActualReservedHeight()'
Assert-Contains 'bottom toolbar height adjustment helper exists' $mainCode 'private void ApplyBottomToolbarHeightAdjustmentIfNeeded('
Assert-Contains 'bottom toolbar allows controlled one-key compact entry compensation' $mainCode 'bool allowDuringInternalLayout = false'
Assert-Contains 'bottom toolbar uses policy toggle host placement' $mainCode 'plan.UseCompactToggleHost'
Assert-NotContains 'bottom toolbar height adjustment must not gate on layout mode because cached modes can be stale while reserved height changes' $mainCode 'previousLayoutMode == layoutMode'
Assert-Contains 'bottom toolbar height adjustment is suppressed while one-key compact config is active' $mainCode 'TrainerMainWindowConfigScope.GetBool(SuperCompactModeConfigKey)'
Assert-Contains 'bottom toolbar actual height reads current bottom row pixel height' $mainCode 'bottomButtonRow.Height.GridUnitType == GridUnitType.Pixel'
Assert-Contains 'bottom toolbar actual height reads current bottom row actual height' $mainCode 'bottomButtonRow.ActualHeight > 0.5'
Assert-Contains 'bottom toolbar footer height includes collapsed bottom border row' $mainCode 'GetCurrentBottomToolbarActualReservedHeight() + GetCurrentCollapsedBottomBorderActualHeight()'
Assert-Contains 'bottom toolbar planned footer height includes toolbar and collapsed border' $mainCode 'return plan.ToolbarReservedHeight + plan.CollapsedBottomBorderHeight;'
Assert-Contains 'bottom toolbar height adjustment receives current target height from caller' $mainCode 'double currentToolbarHeight,'
Assert-Contains 'bottom toolbar height adjustment grows or shrinks window by button row delta' $mainCode 'this.Height += heightDelta;'
Assert-Contains 'bottom toolbar height adjustment accepts shrink deltas' $mainCode 'Math.Abs(heightDelta) > 0.5'
Assert-NotContains 'bottom toolbar no longer uses old policy reserved helper directly' $mainCode 'HomeBottomToolbarLayoutPolicy.GetReservedHeight('
Assert-Contains 'bottom toolbar caches measured normal button row height for later compact shrink' $mainCode '_lastNormalBottomToolbarHeight = measuredHeight;'
Assert-NotContains 'bottom toolbar no longer waits a frame before shrinking after buttons are hidden' $bottomToolbarLayoutBlock 'Dispatcher.BeginInvoke(new Action(() =>'

$normalLayoutBlockMatch = [regex]::Match($mainCode, 'private void RestoreNormalBottomToolbarLayout\(HomeBottomToolbarLayoutPlan plan\)[\s\S]*?private void ApplySuperCompactBottomToolbarLayout\(HomeBottomToolbarLayoutPlan plan\)')
if (-not $normalLayoutBlockMatch.Success) {
    throw 'normal bottom toolbar layout block was not found'
}
$normalLayoutBlock = $normalLayoutBlockMatch.Value
Assert-Contains 'normal results toggle clears compact height' $normalLayoutBlock 'BtnToggleResults.ClearValue(FrameworkElement.HeightProperty);'
Assert-Contains 'normal bottom toolbar follows policy toggle host placement' $normalLayoutBlock 'if (plan.UseCompactToggleHost)'
Assert-Contains 'normal results toggle icon viewport restores normal height' $normalLayoutBlock 'ResultsToggleChevronViewport.Height = ResultsToggleNormalIconViewportSize;'
Assert-Contains 'normal results toggle icon viewport restores normal width' $normalLayoutBlock 'ResultsToggleChevronViewport.Width = ResultsToggleNormalIconViewportSize;'
Assert-Contains 'normal bottom toolbar clears compact container bottom margin' $normalLayoutBlock 'typingAreaAndButtonsGrid.Margin = new Thickness(0);'
Assert-Contains 'normal bottom toolbar uses planned measured height' $normalLayoutBlock 'ApplyBottomToolbarReservedHeight(plan.ToolbarReservedHeight, false);'
Assert-NotContains 'normal bottom toolbar must not fix default height before measuring' $normalLayoutBlock 'ApplyBottomToolbarReservedHeight(DefaultNormalBottomToolbarHeight, false);'
Assert-NotContains 'normal bottom toolbar should not set row min height outside authoritative applier' $normalLayoutBlock 'bottomButtonRow.MinHeight = DefaultNormalBottomToolbarHeight;'
Assert-NotContains 'normal bottom toolbar should not set row height outside authoritative applier' $normalLayoutBlock 'bottomButtonRow.Height = GridLength.Auto;'
Assert-Contains 'bottom toolbar authoritative applier controls clipping' $mainCode 'resultsButtonPanel.ClipToBounds = clipToBounds;'
Assert-NotContains 'normal bottom toolbar no longer measures after fixed height is applied' $normalLayoutBlock 'resultsButtonPanel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));'
Assert-Contains 'normal bottom toolbar uses result-aware planned bottom border height' $normalLayoutBlock 'ApplyCollapsedResultsBottomBorderHeight(plan.CollapsedBottomBorderHeight);'
Assert-Contains 'normal bottom toolbar reapplies normal chevron geometry' $normalLayoutBlock 'SetResultsToggleChevron(_isResultsExpanded);'

$toggleResultsClickBlockMatch = [regex]::Match($mainCode, 'private void BtnToggleResults_Click\(object sender, RoutedEventArgs e\)[\s\S]*?private void GridSplitterArticleTyping_DragStarted')
if (-not $toggleResultsClickBlockMatch.Success) {
    throw 'results toggle click block was not found'
}
$toggleResultsClickBlock = $toggleResultsClickBlockMatch.Value
Assert-Contains 'results toggle expand uses captured current bottom toolbar heights' $toggleResultsClickBlock 'ExpandResultsPanelLayout(true, collapsedBottomBorderHeight, collapsedWindowFooterHeight);'
Assert-Contains 'results toggle collapse uses captured current bottom toolbar heights' $toggleResultsClickBlock 'CollapseResultsPanelLayout(true, true, collapsedBottomBorderHeight, collapsedWindowFooterHeight);'
Assert-Contains 'results toggle click reapplies current bottom toolbar layout without second window compensation' $toggleResultsClickBlock 'ApplyHomeBottomLayout("results toggled", adjustWindowHeight: false);'
Assert-NotContains 'results toggle click must not use old layout applier' $toggleResultsClickBlock 'ApplyBottomToolbarLayoutFromCurrentVisibility();'
Assert-Contains 'collapsed results border helper exists' $mainCode 'private double GetCollapsedResultsBottomBorderHeightForCurrentBottomToolbar()'
Assert-Contains 'collapsed results border helper uses policy plan' $mainCode 'normalToolbarHeight: _lastNormalBottomToolbarHeight).CollapsedBottomBorderHeight;'
Assert-Contains 'collapsed results window footer helper exists' $mainCode 'private double GetCollapsedResultsWindowFooterHeightForCurrentBottomToolbar()'
Assert-Contains 'collapsed results window footer helper uses policy plan' $mainCode 'normalToolbarHeight: _lastNormalBottomToolbarHeight).CollapsedWindowFooterHeight;'
Assert-Contains 'collapsed startup layout uses current bottom toolbar border height' $mainCode 'double collapsedBottomBorderHeight = GetCollapsedResultsBottomBorderHeightForCurrentBottomToolbar();'
Assert-NotContains 'collapsed startup layout no longer forces fixed normal bottom border height' $mainCode 'grid_a.RowDefinitions[7].Height = new GridLength(10, GridUnitType.Pixel);'
Assert-Contains 'results toggle captures current bottom border height before changing layout' $toggleResultsClickBlock 'double collapsedBottomBorderHeight = GetCollapsedResultsBottomBorderHeightForCurrentBottomToolbar();'
Assert-Contains 'results toggle captures current footer height before changing layout' $toggleResultsClickBlock 'double collapsedWindowFooterHeight = GetCollapsedResultsWindowFooterHeightForCurrentBottomToolbar();'
Assert-Contains 'results toggle is blocked while one-key compact is configured' $toggleResultsClickBlock 'TrainerMainWindowConfigScope.GetBool(SuperCompactModeConfigKey)'
Assert-Contains 'results expand layout accepts current collapsed footer height parameter' $mainCode 'double collapsedWindowFooterHeight)'
Assert-Contains 'results expand window height removes current collapsed footer height only once' $mainCode 'this.Height = this.ActualHeight + resultsH + 5 - collapsedWindowFooterHeight;'
Assert-NotContains 'results expand no longer subtracts fixed normal border height' $mainCode 'this.Height = this.ActualHeight + resultsH + 5 - 10;'
Assert-Contains 'results collapse window height restores current collapsed footer height only once' $mainCode 'collapsedGridHeight = gridContentHeight - resultsAreaHeight - 5 + collapsedWindowFooterHeight'
Assert-Contains 'results expand assigns final star rows synchronously after calculating window height' $mainCode 'mainGrid.RowDefinitions[6].Height = new GridLength(resultsH, GridUnitType.Star);'
Assert-NotContains 'results expand no longer changes row heights in delayed callback' $mainCode 'if (r6u == GridUnitType.Pixel && rh > 0)'
Assert-Contains 'results collapse assigns final article star row synchronously after calculating window height' $mainCode 'mainGrid.RowDefinitions[2].Height = new GridLength(_collapsedArticleHeight, GridUnitType.Star);'
Assert-NotContains 'super compact collapsed layout no longer subtracts bottom row after unified bottom layout handled it' $mainCode 'removedHeight += Math.Max(0, snapshot.BottomButtonRowHeight);'
Assert-NotContains 'super compact collapsed layout no longer subtracts bottom border after unified bottom layout handled it' $mainCode 'removedHeight += Math.Max(0, snapshot.BottomBorderHeight);'
Assert-Contains 'super compact collapsed layout preserves prior unified bottom height adjustment' $mainCode 'double baseHeight = this.Height > 0 ? this.Height : snapshot.WindowHeight;'
Assert-Contains 'super compact collapsed layout subtracts remaining removed height from current base height' $mainCode 'double targetHeight = baseHeight - removedHeight;'

Assert-Contains 'title minimize uses shared icon style' $mainXaml 'Style="{StaticResource SunnyTitleBarMinimizeButtonStyle}"'
Assert-Contains 'title maximize uses shared icon style' $mainXaml 'Style="{StaticResource SunnyTitleBarMaximizeButtonStyle}"'
Assert-Contains 'title close uses shared icon style' $mainXaml 'Style="{StaticResource SunnyTitleBarCloseIconButtonStyle}"'
Assert-Contains 'maximize icon uses shared state helper' $mainCode 'TitleBarButtonIcons.SetMaximizeButtonState(BtnMaximize, _isCustomMaximized);'
Assert-NotContains 'main window no longer creates title icon paths inline' $mainCode 'CreateTitleIconPath('

Write-Host 'All home toolbar results toggle UI tests passed.'
