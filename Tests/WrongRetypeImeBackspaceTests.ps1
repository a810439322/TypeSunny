$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$mainCode = Get-Content -Raw (Join-Path $root 'UI\MainWindow.xaml.cs')

function Get-Block($name, $code, $start, $end) {
    $startIndex = $code.IndexOf($start)
    if ($startIndex -lt 0) {
        throw "Unable to find start marker for ${name}: $start"
    }

    $endIndex = $code.IndexOf($end, $startIndex)
    if ($endIndex -lt 0) {
        throw "Unable to find end marker for ${name}: $end"
    }

    return $code.Substring($startIndex, $endIndex - $startIndex)
}

function Get-RegexBlock($name, $code, $pattern) {
    $match = [regex]::Match($code, $pattern, [System.Text.RegularExpressions.RegexOptions]::Singleline)
    if (!$match.Success) {
        throw "Unable to find block for ${name}."
    }

    return $match.Value
}

function Assert-Contains($name, $haystack, $needle) {
    if (!$haystack.Contains($needle)) {
        throw "${name}: expected to contain '$needle'."
    }
}

function Assert-NotContains($name, $haystack, $needle) {
    if ($haystack.Contains($needle)) {
        throw "${name}: should not contain '$needle'."
    }
}

$textInputStats = Get-Block `
    'HandleTextInputStats' `
    $mainCode `
    'internal void HandleTextInputStats(TextCompositionEventArgs e)' `
    'private void InputBox_TextInput'

Assert-NotContains `
    'Backspace logging helper must not exist because it records the next pending word' `
    $mainCode `
    'LogBack'

$emptyTextInputBranch = Get-Block `
    'empty TextInput branch' `
    $textInputStats `
    'if (e.Text == "")' `
    '// 成功上屏，清除 composing 标记'

Assert-NotContains `
    'Canceled or empty IME TextInput must not record the next pending word as wrong retype history' `
    $emptyTextInputBranch `
    'LogBack();'

Assert-Contains `
    'Canceled IME composition with typed code must enter wrong retype history through the composition-start target' `
    $emptyTextInputBranch `
    'LogCompositionTargetWrong();'

Assert-Contains `
    'Canceled IME composition must clear the saved start target after recording' `
    $emptyTextInputBranch `
    'ClearCompositionState();'

$successfulTextInputBranch = Get-Block `
    'successful TextInput branch' `
    $mainCode `
    '// 成功上屏，清除 composing 标记' `
    'private void InputBox_TextInput'

Assert-Contains `
    'Successful commit after an IME composition backspace must record the composition-start target as wrong retype history' `
    $successfulTextInputBranch `
    'RecordCompositionBackspaceWrongIfNeeded();'

Assert-Contains `
    'Successful commit after an IME composition backspace must reuse the same composition-start target recorder' `
    $mainCode `
    'LogCompositionTargetWrong();'

$committedTextBranch = Get-RegexBlock `
    'non-empty committed TextInput branch' `
    $successfulTextInputBranch `
    'if \(e\.Text != "" && e\.Text != "\\r"\)\s*\{[\s\S]*?StateManager\.TextInput = true;[\s\S]*?\}'

Assert-Contains `
    'Composition backspace history must be recorded only when text is actually committed' `
    $committedTextBranch `
    'RecordCompositionBackspaceWrongIfNeeded();'

$keyDownStats = Get-Block `
    'HandleKeyDownStats' `
    $mainCode `
    'internal void HandleKeyDownStats(KeyEventArgs e)' `
    'private void InputBox_PreviewKeyDown'

$physicalBackspaceCase = Get-Block `
    'physical Backspace case' `
    $keyDownStats `
    'case Key.Back:' `
    '// bime hit'

Assert-Contains `
    'Physical Backspace correction should still record the previous typed word path' `
    $physicalBackspaceCase `
    'LogCorrection();'

$bimeBackspaceCase = Get-Block `
    'BIME Backspace case' `
    $keyDownStats `
    'case Key.F16:' `
    'case Key.ImeProcessed:'

Assert-NotContains `
    'BIME Backspace is a backspace count signal and must not record the next pending word' `
    $bimeBackspaceCase `
    'LogBack();'

Assert-Contains `
    'BIME Backspace inside composition must mark that this composition had backspace for later successful commit logging' `
    $bimeBackspaceCase `
    'Score.CompositionHadBackspace = true;'

$bimeHitCase = Get-Block `
    'BIME hit case' `
    $keyDownStats `
    'case Key.F14:' `
    'case Key.F15:'

Assert-Contains `
    'BIME hit must start composition tracking so a later BIME backspace has a stable target' `
    $bimeHitCase `
    'Score.CompositionStartTargetPosition = ResolveCurrentCommitTargetPosition();'

$imeProcessedBackspaceCase = Get-RegexBlock `
    'IME processed Backspace case' `
    $keyDownStats `
    'case Key\.ImeProcessed:[\s\S]*?switch \(e\.ImeProcessedKey\)[\s\S]*?case Key\.Back:[\s\S]*?break;'

Assert-NotContains `
    'IME processed Backspace edits IME composition and must not record the next pending word' `
    $imeProcessedBackspaceCase `
    'LogBack();'

Assert-Contains `
    'IME processed Backspace inside composition must mark that this composition had backspace for later successful commit logging' `
    $imeProcessedBackspaceCase `
    'Score.CompositionHadBackspace = true;'

Assert-NotContains `
    'IME processed Backspace must not directly record a canceled-composition wrong target' `
    $imeProcessedBackspaceCase `
    'LogCompositionTargetWrong();'

$queuedImeBackspaceBlock = Get-RegexBlock `
    'queued IME Backspace key-state block' `
    $keyDownStats `
    'if \(Win32\.GetKeyState\(Win32\.VK_BACK\) < 0\)\s*\{[\s\S]*?\}'

Assert-NotContains `
    'IME key processed while physical Backspace is down must not record the next pending word' `
    $queuedImeBackspaceBlock `
    'LogBack();'

Assert-Contains `
    'Queued IME Backspace inside composition must mark that this composition had backspace for later successful commit logging' `
    $queuedImeBackspaceBlock `
    'Score.CompositionHadBackspace = true;'

Assert-NotContains `
    'Queued IME Backspace must not directly record a canceled-composition wrong target' `
    $queuedImeBackspaceBlock `
    'LogCompositionTargetWrong();'

$imeCompositionStartBlock = Get-RegexBlock `
    'main IME composition start block' `
    $keyDownStats `
    'if \(!Score\.IsComposing\)\s*\{[\s\S]*?Score\.CompositionStartHit = Score\.Hit;[\s\S]*?\}'

Assert-Contains `
    'Main IME composition start must save the target index active before the composition is canceled or committed' `
    $imeCompositionStartBlock `
    'Score.CompositionStartTargetPosition = ResolveCurrentCommitTargetPosition();'

$normalScoreBlock = Get-Block `
    'normal score calculation' `
    $mainCode `
    'internal void CalScore()' `
    'private void SendLocalArticleResultOnly'

Assert-Contains `
    'Newly wrong normal text positions must enter historical wrong retype records immediately' `
    $normalScoreBlock `
    'LogWrong(i, TextInfo.Words[i]);'

$copybookCode = Get-Content -Raw (Join-Path $root 'UI\Modes\CopybookMode.cs')
$copybookRefreshBlock = Get-Block `
    'copybook state refresh' `
    $copybookCode `
    'private void RefreshTypedStateFromInputBuffer()' `
    'private static WordStates ToWordState'

Assert-Contains `
    'Newly wrong copybook positions must enter historical wrong retype records' `
    $copybookRefreshBlock `
    '_main.LogWrong(i, TextInfo.Words[i]);'

$copybookCompositionStartBlock = Get-Block `
    'copybook composition start' `
    $copybookCode `
    'private void OnCompositionStart(object sender, TextCompositionEventArgs e)' `
    'private void OnCompositionUpdate'

Assert-Contains `
    'Copybook composition start must save its current target index for canceled-code wrong history' `
    $copybookCompositionStartBlock `
    'Score.CompositionStartTargetPosition = CurrentIndex;'

$tracingCode = Get-Content -Raw (Join-Path $root 'UI\Modes\TracingMode.cs')
$tracingInputBlock = Get-Block `
    'tracing input processing' `
    $tracingCode `
    'private void ProcessInputText(string inputText, string committedComposition = null)' `
    'private void OnPreviewKeyDown'

Assert-Contains `
    'Newly wrong tracing positions must enter historical wrong retype records' `
    $tracingInputBlock `
    '_main.LogWrong(i, TextInfo.Words[i]);'

$tracingCompositionStartBlock = Get-Block `
    'tracing composition start' `
    $tracingCode `
    'private void OnCompositionStart(object sender, TextCompositionEventArgs e)' `
    'private void OnCompositionUpdate'

Assert-Contains `
    'Tracing composition start must save its current target index for canceled-code wrong history' `
    $tracingCompositionStartBlock `
    'Score.CompositionStartTargetPosition = CurrentIndex;'

Write-Host 'Wrong retype IME Backspace tests passed.'
