$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$articleXaml = Get-Content -Path (Join-Path $root 'Article\WinArticle.xaml') -Raw
$articleCode = Get-Content -Path (Join-Path $root 'Article\WinArticle.xaml.cs') -Raw

function Assert-Contains($name, $content, $needle) {
    if (-not $content.Contains($needle)) {
        throw "$name expected to contain [$needle]"
    }
}

Assert-Contains 'article window key handler binding' $articleXaml 'PreviewKeyDown="Window_PreviewKeyDown"'
Assert-Contains 'article title bar icon named for dynamic logo refresh' $articleXaml 'x:Name="TitleBarIcon"'
Assert-Contains 'article logo refresh method' $articleCode 'private void ApplyCurrentLogo()'
Assert-Contains 'article logo uses current logo config' $articleCode 'Config.GetString("当前Logo")'
Assert-Contains 'article window icon is updated' $articleCode 'this.Icon = new BitmapImage(iconUri)'
Assert-Contains 'article title icon is updated' $articleCode 'TitleBarIcon.Source = new BitmapImage(iconUri)'
Assert-Contains 'article shortcuts handler' $articleCode 'Window_PreviewKeyDown(object sender, KeyEventArgs e)'
Assert-Contains 'enter sends article from article manager' $articleCode 'SendCurrentArticle()'
Assert-Contains 'left shortcut goes previous' $articleCode 'case Key.Left:'
Assert-Contains 'right shortcut goes next' $articleCode 'case Key.Right:'
Assert-Contains 'tab cycles article page menu' $articleCode 'MoveFocus(new TraversalRequest(FocusNavigationDirection.Next))'

Write-Host 'All WinArticle shortcut and logo tests passed.'
