$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$paginator = Get-Content -Path (Join-Path $root 'Core\Paginator.cs') -Raw
$mainCode = Get-Content -Path (Join-Path $root 'UI\MainWindow.xaml.cs') -Raw
$copybook = Get-Content -Path (Join-Path $root 'UI\Modes\CopybookMode.cs') -Raw
$tracing = Get-Content -Path (Join-Path $root 'UI\Modes\TracingMode.cs') -Raw

function Assert-Contains($name, $content, $needle) {
    if (-not $content.Contains($needle)) {
        throw "${name}: expected to find [$needle]"
    }
}

function Assert-NotContains($name, $content, $needle) {
    if ($content.Contains($needle)) {
        throw "${name}: expected not to find [$needle]"
    }
}

Assert-NotContains 'paginator should not expand line height for code display' $paginator 'lineH *= 1.5'
Assert-NotContains 'main display element should not stack code vertically' $mainCode 'new StackPanel { Orientation = Orientation.Vertical }'
Assert-NotContains 'copybook/tracing should not disable original lower code display' $mainCode '|| IsCopybookOrTracingActive()'
Assert-NotContains 'lower code display should not extend too close to next line' $mainCode '-DisplayFontSize * 0.35'
Assert-Contains 'lower code display should leave a small gap to the next line' $mainCode '-DisplayFontSize * 0.25'
Assert-Contains 'main should keep typed code feedback state by global index' $mainCode 'CodeLabelInputs'
Assert-Contains 'main should update lower code labels from typed text by global index' $mainCode 'UpdateCodeLabelProgress(int globalIndex, string typedText)'
Assert-Contains 'main should finalize correct commits from the displayed code text' $mainCode 'CommitCodeLabelProgress(int globalIndex, string typedText, bool isCorrect)'
Assert-Contains 'main should clear lower code label feedback when input is undone or display is disabled' $mainCode 'ClearCodeLabelProgress'
Assert-Contains 'main should clear all lower code feedback when code display is turned off' $mainCode 'ClearAllCodeLabelProgress();'
Assert-Contains 'copybook composition position should follow full code display offset' $copybook 'codeDisplayExtra'
Assert-Contains 'tracing composition position should follow full code display offset' $tracing 'codeDisplayExtra'
Assert-Contains 'copybook input capture trim should run before input-priority visual positioning' $copybook 'DispatcherPriority.Normal'
Assert-Contains 'tracing input capture trim should run before input-priority visual positioning' $tracing 'DispatcherPriority.Normal'
Assert-NotContains 'copybook input capture trim should not wait for application idle' $copybook 'DispatcherPriority.ApplicationIdle'
Assert-NotContains 'tracing input capture trim should not wait for application idle' $tracing 'DispatcherPriority.ApplicationIdle'
Assert-Contains 'copybook should trim committed TextBox residue as soon as TextChanged fires' $copybook '_inputCapture.TextChanged += OnInputCaptureTextChanged'
Assert-Contains 'tracing should trim committed TextBox residue as soon as TextChanged fires' $tracing '_inputCapture.TextChanged += OnInputCaptureTextChanged'
Assert-Contains 'copybook immediate trim should reuse composition-aware guard' $copybook 'TrimInputCaptureTextAfterCommit();'
Assert-Contains 'tracing immediate trim should reuse composition-aware guard' $tracing 'TrimInputCaptureTextAfterCommit();'
Assert-Contains 'main should focus loaded text only after layout settles' $mainCode 'FocusInputAfterLoadedTextLayout(focus);'
$prepareStart = $mainCode.IndexOf('public void PrepareLoadedTextForInput(bool focus = true)')
if ($prepareStart -lt 0) {
    throw 'Unable to find MainWindow.PrepareLoadedTextForInput.'
}
$prepareEnd = $mainCode.IndexOf('/// <summary>', $prepareStart)
if ($prepareEnd -lt 0) {
    throw 'Unable to find end of MainWindow.PrepareLoadedTextForInput.'
}
$prepareBody = $mainCode.Substring($prepareStart, $prepareEnd - $prepareStart)
$scrollIndex = $prepareBody.IndexOf('ScDisplay.ScrollToVerticalOffset(0);')
$updateIndex = $prepareBody.IndexOf('UpdateDisplay(UpdateLevel.PageArrange);')
if ($scrollIndex -lt 0 -or $updateIndex -lt 0 -or $scrollIndex -gt $updateIndex) {
    throw 'loaded text should reset display scroll before page arrange and mode input positioning'
}
if ([regex]::Matches($prepareBody, 'ScDisplay\.ScrollToVerticalOffset\(0\);').Count -ne 1) {
    throw 'loaded text should reset display scroll exactly once before layout focus scheduling'
}
Assert-NotContains 'loaded text should not schedule a second loaded-priority scroll reset' $prepareBody 'DispatcherPriority.Loaded'
if ([regex]::IsMatch($prepareBody, 'if\s*\(\s*focus\s*\)\s*\{?\s*FocusInput\(\);')) {
    throw 'loaded text should not focus immediately before mode positioning settles'
}
$loadTextStart = $mainCode.IndexOf('public void LoadText(string rawTxt')
if ($loadTextStart -lt 0) {
    throw 'Unable to find MainWindow.LoadText.'
}
$loadTextEnd = $mainCode.IndexOf('private void LoadTextFromClipBoard', $loadTextStart)
if ($loadTextEnd -lt 0) {
    throw 'Unable to find end of MainWindow.LoadText.'
}
$loadTextBody = $mainCode.Substring($loadTextStart, $loadTextEnd - $loadTextStart)
Assert-Contains 'loaded text should clear stale page number before display rebuild' $loadTextBody 'TextInfo.PageNum = -1;'
Assert-Contains 'loaded text should clear stale page start before display rebuild' $loadTextBody 'TextInfo.PageStartIndex = 0;'
Assert-NotContains 'tracing speed follow hint should not anchor to mirror typing row' $mainCode 'TryGetMirrorBlockPosition(nextToType'
Assert-NotContains 'copybook should not create extra persistent typed-code hints' $copybook 'ShowTypedCodeHint'
Assert-NotContains 'tracing should not create extra persistent typed-code hints' $tracing 'ShowTypedCodeHint'
Assert-Contains 'copybook should finalize committed lower code feedback with correctness' $copybook 'CommitCodeLabelProgress(_currentIndex, committedComposition, isCorrect)'
Assert-Contains 'tracing should finalize committed lower code feedback with correctness' $tracing 'CommitCodeLabelProgress(_currentIndex, committedComposition, isCorrect)'
Assert-NotContains 'copybook should not overwrite committed feedback directly from composition text' $copybook 'UpdateCodeLabelProgress(_currentIndex, committedComposition)'
Assert-NotContains 'tracing should not overwrite committed feedback directly from composition text' $tracing 'UpdateCodeLabelProgress(_currentIndex, committedComposition)'
Assert-NotContains 'copybook should not color the IME composition overlay' $copybook 'GetCompositionGlyphBrush'
Assert-NotContains 'tracing should not color the IME composition overlay' $tracing 'GetCompositionGlyphBrush'
Assert-Contains 'copybook should resync presentation when code display is toggled' $copybook 'public void SyncCompositionPresentation()'
Assert-Contains 'tracing should resync presentation when code display is toggled' $tracing 'public void SyncCompositionPresentation()'
Assert-Contains 'main should expose typing-code helper' $mainCode 'GetTypingCodeText'
Assert-Contains 'main should route typed-state backgrounds through a helper' $mainCode 'SetDisplayBlockStateBackground'
Assert-Contains 'main should translate global typing index before setting state background' $mainCode 'SetDisplayBlockStateBackground(globalIndex - TextInfo.PageStartIndex, background);'
Assert-Contains 'code display typed-state background should use independent background overlays' $mainCode 'StateBackgrounds'
Assert-Contains 'state background overlay should scale exactly with display font size' $mainCode 'Height = DisplayFontSize'
Assert-Contains 'state background overlay should sit slightly below text padding top' $mainCode 'StateBackgroundVerticalOffsetRatio'
Assert-Contains 'state background should stay below lower code label' $mainCode 'Panel.SetZIndex(stateBackground, 0)'
Assert-Contains 'lower code label should render above state background' $mainCode 'Panel.SetZIndex(codeTb, 2)'
Assert-Contains 'main should expose shared state background offset' $mainCode 'GetDisplayStateBackgroundTopOffset'
Assert-NotContains 'tracing source line default background should not fill the whole line border' $tracing 'border.Background = new SolidColorBrush(Color.FromArgb(20, 128, 128, 128));'
Assert-Contains 'tracing should build source line background as a separate overlay' $tracing 'CreateTracingSourceLineElement'
Assert-Contains 'tracing source line background should use display font height' $tracing 'sourceLineBackground.Height = MainWindow.DisplayFontSize'
Assert-Contains 'tracing source line background should reuse display state background offset' $tracing 'GetDisplayStateBackgroundTopOffset'
$removeMirrorStart = $tracing.IndexOf('private void RemoveMirrorBlocks()')
if ($removeMirrorStart -lt 0) {
    throw 'Unable to find TracingMode.RemoveMirrorBlocks.'
}
$removeMirrorEnd = $tracing.IndexOf('/// <summary>', $removeMirrorStart + 1)
if ($removeMirrorEnd -lt 0) {
    throw 'Unable to find end of TracingMode.RemoveMirrorBlocks.'
}
$removeMirrorBody = $tracing.Substring($removeMirrorStart, $removeMirrorEnd - $removeMirrorStart)
Assert-Contains 'tracing remove mirror should detach nested display children before clearing top-level children' $removeMirrorBody 'ClearNestedDisplayPanels(child);'
Assert-Contains 'tracing remove mirror should rebuild grouped current-page display elements for measurement' $removeMirrorBody '_main.RebuildCurrentPageDisplayElementsForTracingMeasurement();'
Assert-NotContains 'glyph feedback should not replace glyphs with inline UI containers' $mainCode 'new InlineUIContainer'
Assert-NotContains 'glyph feedback should not use compressed highlight height' $mainCode 'GlyphHighlightHeightRatio'
Assert-NotContains 'glyph feedback should not use full font inline Run background' $mainCode 'run.Background = background'
Assert-Contains 'lower code display should route its brush through contrast adaptation' $mainCode 'GetReadableCodeDisplayBrush'
Assert-Contains 'lower code display should compare against the display background' $mainCode 'ThemeColorHelper.CreateReadableForegroundBrush'
Assert-NotContains 'main progress should not paint full-height incorrect block backgrounds in code display mode' $mainCode 'TextInfo.Blocks[i].Background = IsBlindType ? null : Colors.IncorrectBackground'
Assert-NotContains 'main progress should not paint full-height correct block backgrounds in code display mode' $mainCode 'TextInfo.Blocks[i].Background = IsBlindType ? null : Colors.CorrectBackground'
Assert-NotContains 'copybook should not paint full-height correct block backgrounds' $copybook 'TextInfo.Blocks[_currentIndex].Background = Colors.CorrectBackground'
Assert-NotContains 'copybook should not paint full-height incorrect block backgrounds' $copybook 'TextInfo.Blocks[_currentIndex].Background = Colors.IncorrectBackground'
Assert-NotContains 'tracing should not paint full-height correct block backgrounds' $tracing 'TextInfo.Blocks[_currentIndex].Background = Colors.CorrectBackground'
Assert-NotContains 'tracing should not paint full-height incorrect block backgrounds' $tracing 'TextInfo.Blocks[_currentIndex].Background = Colors.IncorrectBackground'
Assert-NotContains 'copybook should not pass global current index as a local block index' $copybook 'SetDisplayBlockStateBackground(_currentIndex'
Assert-NotContains 'tracing should not pass global current index as a local block index' $tracing 'SetDisplayBlockStateBackground(_currentIndex'
Assert-NotContains 'copybook should not guard global current index with local block count before setting background' $copybook '!_main.IsBlindType && _currentIndex < TextInfo.Blocks.Count'
Assert-NotContains 'tracing should not guard global current index with local block count before setting background' $tracing '!_main.IsBlindType && _currentIndex < TextInfo.Blocks.Count'
Assert-Contains 'copybook should set typed state background by global index' $copybook 'SetDisplayBlockStateBackgroundByGlobalIndex(_currentIndex'
Assert-Contains 'tracing should set typed state background by global index' $tracing 'SetDisplayBlockStateBackgroundByGlobalIndex(_currentIndex'
