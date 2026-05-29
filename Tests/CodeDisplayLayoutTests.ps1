$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$paginator = Get-Content -Path (Join-Path $root 'Core\Paginator.cs') -Raw
$mainCode = Get-Content -Path (Join-Path $root 'UI\MainWindow.xaml.cs') -Raw
$copybook = Get-Content -Path (Join-Path $root 'UI\Modes\CopybookMode.cs') -Raw
$tracing = Get-Content -Path (Join-Path $root 'UI\Modes\TracingMode.cs') -Raw
$win32 = Get-Content -Path (Join-Path $root 'Utils\Win32.cs') -Raw

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

function Get-MethodBody($name, $content, $signature, $nextMarker) {
    $start = $content.IndexOf($signature, [System.StringComparison]::Ordinal)
    if ($start -lt 0) {
        throw "Unable to find ${name}."
    }
    $end = $content.IndexOf($nextMarker, $start + $signature.Length, [System.StringComparison]::Ordinal)
    if ($end -lt 0) {
        throw "Unable to find end of ${name}."
    }
    return $content.Substring($start, $end - $start)
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
Assert-Contains 'main should expose shared IME offset for full code display' $mainCode 'GetCodeDisplayImeOffset(double fontSize)'
Assert-Contains 'full code display IME offset should keep candidate window close to the source text' $mainCode 'return fontSize * 0.08;'
Assert-Contains 'copybook input offset should use shared full-code IME offset' $copybook '_main.GetCodeDisplayImeOffset(fs)'
Assert-Contains 'tracing input offset should use shared full-code IME offset' $tracing '_main.GetCodeDisplayImeOffset(fs)'
Assert-NotContains 'copybook input offset should not use the full code label height' $copybook 'fs * 0.55'
Assert-NotContains 'tracing input offset should not use the full code label height' $tracing 'fs * 0.55'
Assert-NotContains 'copybook should not synchronously trim the IME host from TextChanged' $copybook '_inputCapture.TextChanged += OnInputCaptureTextChanged'
Assert-NotContains 'tracing should not synchronously trim the IME host from TextChanged' $tracing '_inputCapture.TextChanged += OnInputCaptureTextChanged'
Assert-Contains 'copybook should reset stale IME host text after layout reposition' $copybook 'ResetInputCaptureHostIfIdle();'
Assert-Contains 'tracing should reset stale IME host text after layout reposition' $tracing 'ResetInputCaptureHostIfIdle();'
Assert-Contains 'copybook stale host reset should guard active composition' $copybook 'if (HasActiveComposition())'
Assert-Contains 'tracing stale host reset should guard active composition' $tracing 'if (HasActiveComposition())'
Assert-Contains 'win32 should declare IME candidate form' $win32 'struct CANDIDATEFORM'
Assert-Contains 'win32 should declare IME composition form' $win32 'struct COMPOSITIONFORM'
Assert-Contains 'win32 should get IME context before positioning' $win32 'ImmGetContext'
Assert-Contains 'win32 should release IME context after positioning' $win32 'ImmReleaseContext'
Assert-Contains 'win32 should set IME candidate window explicitly' $win32 'ImmSetCandidateWindow'
Assert-Contains 'win32 should set IME composition window explicitly' $win32 'ImmSetCompositionWindow'
Assert-Contains 'main should expose explicit IME candidate positioning' $mainCode 'UpdateImeCandidateWindowPosition'
Assert-Contains 'main should expose cached IME position refresh without layout work' $mainCode 'RefreshImeCandidateWindowPosition'
Assert-Contains 'main should convert screen point back to native client pixels' $mainCode 'ScreenToClient'
Assert-Contains 'main should skip duplicate IME positioning on unchanged anchors' $mainCode '_lastImeCandidateClientX == clientPoint.x'
Assert-Contains 'copybook should update IME window position when moving hidden input host' $copybook '_main.UpdateImeCandidateWindowPosition(grid, new Point(x, inputTop));'
Assert-Contains 'tracing should update IME window position when moving hidden input host' $tracing '_main.UpdateImeCandidateWindowPosition(grid, new Point(x, inputTop));'
Assert-Contains 'copybook composition start should only refresh cached IME position' $copybook '_main.RefreshImeCandidateWindowPosition();'
Assert-Contains 'tracing composition start should only refresh cached IME position' $tracing '_main.RefreshImeCandidateWindowPosition();'
$copybookCompositionUpdate = Get-MethodBody 'CopybookMode.OnCompositionUpdate' $copybook 'private void OnCompositionUpdate' 'private void OnTextInput'
$tracingCompositionUpdate = Get-MethodBody 'TracingMode.OnCompositionUpdate' $tracing 'private void OnCompositionUpdate' 'private void OnTextInput'
Assert-NotContains 'copybook composition update should not recalculate anchor position' $copybookCompositionUpdate 'UpdatePosition();'
Assert-NotContains 'tracing composition update should not recalculate anchor position' $tracingCompositionUpdate 'UpdatePosition();'
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
Assert-Contains 'copybook should finalize committed lower code feedback with correctness' $copybook 'CommitCodeLabelProgress(commitIndex, committedComposition, isCorrect)'
Assert-Contains 'copybook should clear shifted code feedback from the edit point' $copybook 'ClearCodeLabelProgressFrom(commitIndex)'
Assert-Contains 'tracing should finalize committed lower code feedback with correctness' $tracing 'CommitCodeLabelProgress(_currentIndex, committedComposition, isCorrect)'
Assert-NotContains 'copybook should not overwrite committed feedback directly from composition text' $copybook 'UpdateCodeLabelProgress(_currentIndex, committedComposition)'
Assert-NotContains 'tracing should not overwrite committed feedback directly from composition text' $tracing 'UpdateCodeLabelProgress(_currentIndex, committedComposition)'
Assert-NotContains 'copybook should not color the IME composition overlay' $copybook 'GetCompositionGlyphBrush'
Assert-NotContains 'tracing should not color the IME composition overlay' $tracing 'GetCompositionGlyphBrush'
Assert-Contains 'copybook should resync presentation when code display is toggled' $copybook 'public void SyncCompositionPresentation()'
Assert-Contains 'tracing should resync presentation when code display is toggled' $tracing 'public void SyncCompositionPresentation()'
Assert-Contains 'main should expose typing-code helper' $mainCode 'GetTypingCodeText'
Assert-Contains 'copybook page selection should follow edit caret' $mainCode 'nextToType = _copybookMode.CurrentIndex;'
Assert-Contains 'copybook title progress should follow typed length' $mainCode 'typedWords = _copybookMode.TypedLength;'
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
Assert-Contains 'copybook should queue typed state background by global index' $copybook 'QueueDisplayBlockStateBackground(i, background)'
Assert-Contains 'copybook should avoid reanimating unchanged typed backgrounds' $copybook 'GetQueuedBackgroundForState'
Assert-Contains 'copybook should compare previous state before queueing background animation' $copybook 'previousState != state'
$copybookRefreshTypedStateBody = Get-MethodBody 'CopybookMode.RefreshTypedStateFromInputBuffer' $copybook 'private void RefreshTypedStateFromInputBuffer()' 'private static WordStates ToWordState'
Assert-NotContains 'copybook refresh should preserve already queued typed background changes' $copybookRefreshTypedStateBody '_pendingBackgroundChanges.Clear();'
Assert-Contains 'tracing should set typed state background by global index' $tracing 'SetDisplayBlockStateBackgroundByGlobalIndex(_currentIndex'
$copybookResetBody = Get-MethodBody 'CopybookMode.Reset' $copybook 'public void Reset()' '/// <summary>'
Assert-Contains 'copybook reset should discard stale pending typed backgrounds before next loaded text' $copybookResetBody '_pendingBackgroundChanges.Clear();'
$copybookProcessInputBody = Get-MethodBody 'CopybookMode.ProcessInputText' $copybook 'private void ProcessInputText(string inputText, string committedComposition = null)' 'private void ScheduleInputCaptureTrim()'
if (-not ($copybookProcessInputBody.Contains('ScheduleAdvanceVisuals();') -and $copybookProcessInputBody.Contains('FlushPendingBackgroundChanges();'))) {
    throw 'copybook last-character wrong input should flush queued typed backgrounds even though the caret cannot advance'
}
Assert-NotContains 'copybook should not move the caret back to overwrite a wrong final character' $copybookProcessInputBody '_inputBuffer.MoveCaret(_currentIndex);'
$copybookEndGuardIndex = $copybookProcessInputBody.IndexOf('if (_currentIndex >= TextInfo.Words.Count', [System.StringComparison]::Ordinal)
$copybookWordRecordIndex = $copybookProcessInputBody.IndexOf('_main.ResolveTypedWordCountDelta', [System.StringComparison]::Ordinal)
if ($copybookEndGuardIndex -lt 0 -or $copybookEndGuardIndex -gt $copybookWordRecordIndex) {
    throw 'copybook should discard input past the final character before recording actual typed words'
}
$tracingProcessInputBody = Get-MethodBody 'TracingMode.ProcessInputText' $tracing 'private void ProcessInputText(string inputText, string committedComposition = null)' 'private void QueueDisplayBlockStateBackground'
Assert-NotContains 'tracing should not rewind to overwrite a wrong final character' $tracingProcessInputBody '_currentIndex = TextInfo.Words.Count - 1;'
$tracingEndGuardIndex = $tracingProcessInputBody.IndexOf('if (_currentIndex >= TextInfo.Words.Count', [System.StringComparison]::Ordinal)
$tracingWordRecordIndex = $tracingProcessInputBody.IndexOf('_main.ResolveTypedWordCountDelta', [System.StringComparison]::Ordinal)
if ($tracingEndGuardIndex -lt 0 -or $tracingEndGuardIndex -gt $tracingWordRecordIndex) {
    throw 'tracing should discard input past the final character before recording actual typed words'
}
$copybookUpdatePositionBody = Get-MethodBody 'CopybookMode.UpdatePosition' $copybook 'private void UpdatePosition(bool animated = false)' 'private void UpdateCompositionPosition()'
Assert-NotContains 'copybook end caret should not skip positioning when current index is at text end' $copybookUpdatePositionBody '_inputCapture == null || _currentIndex >= TextInfo.Blocks.Count || TextInfo.Blocks.Count == 0'
Assert-Contains 'copybook end caret should anchor to last visible block' $copybookUpdatePositionBody 'visualIndex = Math.Min(_currentIndex, TextInfo.Blocks.Count - 1)'
Assert-Contains 'copybook end caret should move to the right side of the final block' $copybookUpdatePositionBody 'x += block.ActualWidth;'
$tracingUpdatePositionBody = Get-MethodBody 'TracingMode.UpdatePosition' $tracing 'private void UpdatePosition(bool animated = false)' 'private void UpdateCompositionPosition()'
Assert-NotContains 'tracing end caret should not skip positioning when current index is at text end' $tracingUpdatePositionBody '_inputCapture == null || _currentIndex >= _mirrorBlocks.Count || _mirrorBlocks.Count == 0'
Assert-Contains 'tracing end caret should anchor to last mirror block' $tracingUpdatePositionBody 'visualIndex = Math.Min(_currentIndex, _mirrorBlocks.Count - 1)'
Assert-Contains 'tracing end caret should move to the right side of the final mirror block' $tracingUpdatePositionBody 'x += mirrorBlock.ActualWidth;'
Assert-Contains 'code display should repair stale full-height textblock state backgrounds after overlay rebuild' $mainCode 'EnsureCodeDisplayStateBackgrounds();'
$ensureCodeDisplayBackgroundsBody = Get-MethodBody 'MainWindow.EnsureCodeDisplayStateBackgrounds' $mainCode 'private void EnsureCodeDisplayStateBackgrounds()' 'private Brush GetDisplayBlockStateBackground'
Assert-Contains 'code display repair clears stale full-height block backgrounds' $ensureCodeDisplayBackgroundsBody 'SmoothBackground.Apply(block, null, 0);'
Assert-Contains 'code display repair restores missing overlay backgrounds for already typed states' $ensureCodeDisplayBackgroundsBody 'stateBackground.Background == null'
$tracingRebuildBody = Get-MethodBody 'MainWindow.RebuildCurrentPageDisplayElementsForTracingMeasurement' $mainCode 'internal void RebuildCurrentPageDisplayElementsForTracingMeasurement()' 'private void AddCiTiNoSplitLineDisplayElements()'
Assert-Contains 'tracing grouped measurement rebuild should repair code display backgrounds before returning' $tracingRebuildBody 'EnsureCodeDisplayStateBackgrounds();'
if (-not [regex]::IsMatch($tracingRebuildBody, 'if\s*\(\s*IsCiTiNoSplitLineEnabled\(\)\s*\)\s*\{(?<body>.*?)\n\s*\}', [System.Text.RegularExpressions.RegexOptions]::Singleline)) {
    throw 'Unable to find grouped tracing measurement rebuild branch.'
}
$tracingGroupedRebuildBody = [regex]::Match($tracingRebuildBody, 'if\s*\(\s*IsCiTiNoSplitLineEnabled\(\)\s*\)\s*\{(?<body>.*?)\n\s*\}', [System.Text.RegularExpressions.RegexOptions]::Singleline).Groups['body'].Value
if (-not ($tracingGroupedRebuildBody.Contains('AddCiTiNoSplitLineDisplayElements();') -and $tracingGroupedRebuildBody.Contains('EnsureCodeDisplayStateBackgrounds();'))) {
    throw 'tracing grouped measurement rebuild should repair code display backgrounds after grouped display elements are rebuilt'
}
if ($tracingGroupedRebuildBody.IndexOf('EnsureCodeDisplayStateBackgrounds();') -gt $tracingGroupedRebuildBody.IndexOf('return;')) {
    throw 'tracing grouped measurement rebuild should repair code display backgrounds before returning'
}
