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

function Get-Block($name, $content, $pattern) {
    $match = [regex]::Match($content, $pattern)
    if (-not $match.Success) {
        throw "Unable to find $name block."
    }
    return $match.Value
}

$refreshBlock = Get-Block `
    'RefreshCurrentDifficultyPredictionDisplay' `
    $mainCode `
    'private void RefreshCurrentDifficultyPredictionDisplay\(\)[\s\S]*?private int GetCurrentTitleTypedWords'

Assert-Contains 'title difficulty refresh calculates from current text' $refreshBlock 'string difficulty = difficultyDict.CalcText(currentText);'
Assert-NotContains 'title difficulty refresh must not seed from score text' $refreshBlock 'string difficulty = Score.DifficultyText;'
Assert-NotContains 'title difficulty refresh must not keep stale score difficulty' $refreshBlock 'if (string.IsNullOrWhiteSpace(difficulty))'

$updateTitleBlock = Get-Block `
    'UpdateWindowTitle' `
    $mainCode `
    'private void UpdateWindowTitle\(int typedWords, int totalWords\)[\s\S]*?public void LoadText'

Assert-Contains 'window title uses shared local difficulty formatter' $updateTitleBlock 'GetTitleDifficultyText()'
Assert-NotContains 'window title must not read article sender difficulty directly' $updateTitleBlock 'articleCache.GetCurrentDifficulty()'
Assert-NotContains 'window title must not read score difficulty directly' $updateTitleBlock 'Score.DifficultyText'

$difficultyFormatterBlock = Get-Block `
    'GetTitleDifficultyText' `
    $mainCode `
    'private string GetTitleDifficultyText\(\)[\s\S]*?private string GetTrainerTitleText'

Assert-Contains 'title difficulty formatter uses calculated display difficulty' $difficultyFormatterBlock 'currentDifficultyText'
Assert-NotContains 'title difficulty formatter must not read article sender difficulty directly' $difficultyFormatterBlock 'articleCache.GetCurrentDifficulty()'
Assert-NotContains 'title difficulty formatter must not read score difficulty directly' $difficultyFormatterBlock 'Score.DifficultyText'

$trainerTitleBlock = Get-Block `
    'GetTrainerTitleText' `
    $mainCode `
    'private string GetTrainerTitleText\(\)[\s\S]*?private bool ApplyTrainerTitleText'

Assert-Contains 'trainer title uses shared local difficulty formatter' $trainerTitleBlock 'GetTitleDifficultyText()'
Assert-NotContains 'trainer title must not read article sender difficulty directly' $trainerTitleBlock 'articleCache.GetCurrentDifficulty()'
Assert-NotContains 'trainer title must not read score difficulty directly' $trainerTitleBlock 'Score.DifficultyText'

$formatArticleBlock = Get-Block `
    'FormatArticleSenderContent' `
    $mainCode `
    'private string FormatArticleSenderContent\([\s\S]*?private void SendFormattedArticle'

Assert-Contains 'article sender formatted output calculates difficulty from sent content' $formatArticleBlock 'difficultyDict.CalcText(content)'
Assert-NotContains 'article sender formatted output must not prefer passed interface difficulty' $formatArticleBlock 'string diffText = string.IsNullOrEmpty(difficulty) ? currentDifficultyText : difficulty;'
Assert-NotContains 'article sender formatted output must not use title difficulty state' $formatArticleBlock 'currentDifficultyText'

$sendFormattedBlock = Get-Block `
    'SendFormattedArticle' `
    $mainCode `
    'private void SendFormattedArticle\(string content\)[\s\S]*?public void SendContentToClipboardOrQQ'

Assert-NotContains 'manual article sender send must not read interface difficulty' $sendFormattedBlock 'articleCache.GetCurrentDifficulty()'

Assert-Contains 'auto article sender send formats content without interface difficulty argument' $mainCode 'FormatArticleSenderContent(title, segment, mark);'
Assert-NotContains 'auto article sender send must not pass interface difficulty' $mainCode 'FormatArticleSenderContent(title, segment, mark, difficultyText);'
Assert-NotContains 'main window must not read article sender interface difficulty for display or sending' $mainCode 'articleCache.GetCurrentDifficulty()'

Assert-NotContains `
    'text loading must not copy article sender difficulty into score/title state' `
    $mainCode `
    'Score.DifficultyText = articleCache.GetCurrentDifficulty();'

Write-Host 'All title difficulty local calculation tests passed.'
