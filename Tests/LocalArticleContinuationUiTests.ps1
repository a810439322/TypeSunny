$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
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

function Get-Between($name, $content, $start, $end) {
    $pattern = [regex]::Escape($start) + '([\s\S]*?)' + [regex]::Escape($end)
    $match = [regex]::Match($content, $pattern)
    if (-not $match.Success) {
        throw "$name expected to find block between [$start] and [$end]"
    }
    return $match.Groups[1].Value
}

Assert-Contains 'main window has continuation reentrancy flag' $mainCode '_isLocalArticleContinuationInProgress'

$continueBlock = Get-Between `
    'local article continuation method' `
    $mainCode `
    'private async Task ContinueLocalArticleAsync(bool next' `
    'internal void RecordLocalArticleContinuation'

Assert-Contains 'continuation returns while already running' $continueBlock 'if (_isLocalArticleContinuationInProgress)'
Assert-Contains 'continuation suppresses fast repeated local movement' $continueBlock 'ShouldSuppressRepeatedLocalArticleContinuation(next)'
Assert-Contains 'continuation sets running flag' $continueBlock '_isLocalArticleContinuationInProgress = true;'
Assert-Contains 'continuation clears running flag in finally' $continueBlock 'finally'
Assert-Contains 'continuation clears running flag' $continueBlock '_isLocalArticleContinuationInProgress = false;'
Assert-Contains 'continuation uses shared local article movement' $continueBlock 'MoveLocalArticleContinuation(next);'
Assert-Contains 'continuation records successful movement target' $continueBlock 'RecordLocalArticleContinuationSuccess(next);'
Assert-NotContains 'continuation does not advance from global preview progress' $continueBlock 'ArticleManager.NextSection();'
Assert-NotContains 'continuation does not rewind from global preview progress' $continueBlock 'ArticleManager.PrevSection();'

$nextArticleBlock = Get-Between `
    'automatic local next helper' `
    $mainCode `
    'private void NextArticle()' `
    'async void DelayStop'
Assert-Contains 'automatic local next uses shared paragraph-based movement' $nextArticleBlock 'MoveLocalArticleContinuation(next: true);'
Assert-NotContains 'automatic local next does not advance from global preview progress' $nextArticleBlock 'ArticleManager.NextSection();'

$prevButtonBlock = Get-Between `
    'home previous button handler' `
    $mainCode `
    'private async void BtnPrev_Click' `
    'private async void BtnNext_Click'
Assert-Contains 'previous button uses guarded local continuation' $prevButtonBlock 'await ContinueLocalArticleAsync(next: false, showFilterBlockedHint: true);'
Assert-NotContains 'previous button does not rewind from global preview progress' $prevButtonBlock 'ArticleManager.PrevSection();'

$nextButtonBlock = Get-Between `
    'home next button handler' `
    $mainCode `
    'private async void BtnNext_Click' `
    'private void ShowFilterBlockedHint'
Assert-Contains 'next button uses guarded local continuation' $nextButtonBlock 'await ContinueLocalArticleAsync(next: true, showFilterBlockedHint: true);'
Assert-NotContains 'next button does not advance from global preview progress' $nextButtonBlock 'ArticleManager.NextSection();'

$delayStopBlock = Get-Between 'stop helper retype ordering' $mainCode 'RetypeTextBuilder.MergeFinalWrongRecords(' 'bool hasSlow = Config.GetBool'
$slowDetectIndex = $delayStopBlock.IndexOf('SlowRetypeDetector.BuildSlowRecords(')
$localArticleIndex = $delayStopBlock.IndexOf('if (StateManager.txtSource == TxtSource.book)')
if ($slowDetectIndex -lt 0 -or $localArticleIndex -lt 0 -or $slowDetectIndex -gt $localArticleIndex) {
    throw 'slow retype records must be calculated before local article completion/continuation'
}
Assert-Contains 'local article completion checks pending slow retype' $delayStopBlock 'LocalArticleContinuationPolicy.ShouldDeferCompletionForPendingSlowRetype('
Assert-Contains 'local article slow retype path defers continuation' $delayStopBlock 'hasPendingSlowRetype'

Write-Host 'All local article continuation UI tests passed.'
