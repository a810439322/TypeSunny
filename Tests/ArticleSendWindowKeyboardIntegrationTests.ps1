$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot

function Read-Utf8($path) {
    return [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)
}

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

function Assert-Matches($name, $content, $pattern) {
    if (-not [regex]::IsMatch($content, $pattern)) {
        throw "$name expected to match [$pattern]"
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

$articleCode = Read-Utf8 (Join-Path $root 'Article\WinArticle.xaml.cs')
$trainerXaml = Read-Utf8 (Join-Path $root 'WinTrainer\WinTrainer.xaml')
$trainerCode = Read-Utf8 (Join-Path $root 'WinTrainer\WinTrainer.xaml.cs')
$articleEnterBlock = Get-Between 'local article enter case' $articleCode 'case Key.Enter:' 'case Key.Space:'
$trainerEnterBlock = Get-Between 'trainer enter case' $trainerCode 'case Key.Enter:' 'case Key.Space:'

Assert-Contains 'local article up key uses keyboard policy' $articleCode 'keyboardPolicy.HandleKey(ArticleSendKeyboardKey.Up)'
Assert-Contains 'local article down key uses keyboard policy' $articleCode 'keyboardPolicy.HandleKey(ArticleSendKeyboardKey.Down)'
Assert-Contains 'local article keyboard selection opens dropdown' $articleCode 'CbFiles.IsDropDownOpen = true;'
Assert-Contains 'local article enter confirm focuses preview' $articleEnterBlock 'ArticleSendKeyboardAction.ConfirmArticleSelection'
Assert-Contains 'local article enter confirm focus call' $articleEnterBlock 'FocusArticlePreview();'
Assert-Contains 'local article enter sends only after policy says send' $articleEnterBlock 'ArticleSendKeyboardAction.SendArticle'
Assert-Contains 'local article left keeps previous section' $articleCode 'case Key.Left:'
Assert-Contains 'local article right keeps next section' $articleCode 'case Key.Right:'

Assert-Contains 'trainer preview keydown handler exists' $trainerCode 'private void Window_PreviewKeyDown(object sender, KeyEventArgs e)'
Assert-NotContains 'trainer should not keep direct return keybinding' $trainerXaml 'KeyBinding Key="Return"'
Assert-Contains 'trainer up key uses keyboard policy' $trainerCode 'keyboardPolicy.HandleKey(ArticleSendKeyboardKey.Up)'
Assert-Contains 'trainer down key uses keyboard policy' $trainerCode 'keyboardPolicy.HandleKey(ArticleSendKeyboardKey.Down)'
Assert-Contains 'trainer keyboard selection opens dropdown' $trainerCode 'FileSelector.IsDropDownOpen = true;'
Assert-Contains 'trainer enter confirm focuses preview' $trainerEnterBlock 'ArticleSendKeyboardAction.ConfirmArticleSelection'
Assert-Contains 'trainer enter confirm focus call' $trainerEnterBlock 'FocusTrainerPreview();'
Assert-Contains 'trainer enter sends only after policy says send' $trainerEnterBlock 'ArticleSendKeyboardAction.SendArticle'

Write-Host 'All send-window keyboard integration tests passed.'
