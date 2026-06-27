$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot

function Read-Utf8($relativePath) {
    return Get-Content -Path (Join-Path $root $relativePath) -Raw -Encoding UTF8
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

function Assert-NotRegex($name, $content, $pattern) {
    if ($content -match $pattern) {
        throw "$name expected not to match [$pattern]"
    }
}

$appXaml = Read-Utf8 'App.xaml'
$project = Read-Utf8 'TypeSunny.csproj'

Assert-Contains 'shared title bar normal button style exists' $appXaml 'x:Key="SunnyTitleBarButtonStyle"'
Assert-Contains 'shared title bar close button style exists' $appXaml 'x:Key="SunnyTitleBarCloseButtonStyle"'
Assert-Contains 'shared minimize icon style exists' $appXaml 'x:Key="SunnyTitleBarMinimizeButtonStyle"'
Assert-Contains 'shared maximize icon style exists' $appXaml 'x:Key="SunnyTitleBarMaximizeButtonStyle"'
Assert-Contains 'shared restore icon style exists' $appXaml 'x:Key="SunnyTitleBarRestoreButtonStyle"'
Assert-Contains 'shared close icon style exists' $appXaml 'x:Key="SunnyTitleBarCloseIconButtonStyle"'
Assert-Contains 'shared title icon helper included in project' $project '<Compile Include="UI\TitleBarButtonIcons.cs" />'

$windowFiles = @(
    'UI\MainWindow.xaml',
    'WinConfig\WinConfig.xaml',
    'WinTrainer\WinTrainer.xaml',
    'UI\WinDetailedWordCountStatistics.xaml',
    'Article\WinArticle.xaml',
    'UI\WinStatistics.xaml',
    'UI\UpdateDialog.xaml',
    'UI\ShuangMissingDialog.xaml'
)

foreach ($file in $windowFiles) {
    $xaml = Read-Utf8 $file
    Assert-Contains "$file uses shared close icon style" $xaml 'SunnyTitleBarCloseIconButtonStyle'
    Assert-NotContains "$file does not use old close glyph" $xaml 'Content="×"'
    Assert-NotContains "$file does not use old heavy close glyph" $xaml 'Content="✕"'
    Assert-NotRegex "$file close button does not override shared width" $xaml '<Button[\s\S]{0,260}Width="(28|35)"[\s\S]{0,260}SunnyTitleBarCloseIconButtonStyle'
    Assert-NotRegex "$file close button does not override shared width after style" $xaml '<Button[\s\S]{0,260}SunnyTitleBarCloseIconButtonStyle[\s\S]{0,260}Width="(28|35)"'
}

$maximizableWindowFiles = @(
    'UI\MainWindow.xaml',
    'WinConfig\WinConfig.xaml',
    'WinTrainer\WinTrainer.xaml',
    'UI\WinDetailedWordCountStatistics.xaml'
)

foreach ($file in $maximizableWindowFiles) {
    $xaml = Read-Utf8 $file
    Assert-Contains "$file uses shared minimize style" $xaml 'SunnyTitleBarMinimizeButtonStyle'
    Assert-Contains "$file uses shared maximize style" $xaml 'SunnyTitleBarMaximizeButtonStyle'
    Assert-NotContains "$file does not use old minimize glyph" $xaml 'Content="━"'
    Assert-NotContains "$file does not use old maximize glyph" $xaml 'Content="◻"'
}

$articleXaml = Read-Utf8 'Article\WinArticle.xaml'
Assert-Contains 'article window uses shared minimize style' $articleXaml 'SunnyTitleBarMinimizeButtonStyle'
Assert-NotContains 'article window does not use old minimize glyph' $articleXaml 'Content="━"'

$codeFiles = @(
    'UI\MainWindow.xaml.cs',
    'WinConfig\WinConfig.xaml.cs',
    'WinTrainer\WinTrainer.xaml.cs',
    'UI\WinDetailedWordCountStatistics.xaml.cs'
)

foreach ($file in $codeFiles) {
    $code = Read-Utf8 $file
    Assert-Contains "$file updates maximize button through shared helper" $code 'TitleBarButtonIcons.SetMaximizeButtonState(BtnMaximize,'
    Assert-NotContains "$file does not assign old maximize glyph" $code 'BtnMaximize.Content = "◻";'
    Assert-NotContains "$file does not assign old restore glyph" $code 'BtnMaximize.Content = "◰";'
}

$historyCode = Read-Utf8 'WinTrainer\WinTrainerHistoryWindow.cs'
Assert-Contains 'trainer history window applies shared minimize style' $historyCode 'TitleBarButtonIcons.ApplyMinimizeButtonStyle(btnMinimize);'
Assert-Contains 'trainer history window applies shared close style' $historyCode 'TitleBarButtonIcons.ApplyCloseButtonStyle(btnClose);'
Assert-Contains 'trainer history window updates maximize button through shared helper' $historyCode 'TitleBarButtonIcons.SetMaximizeButtonState(btnMaximize,'
Assert-NotContains 'trainer history window does not build local chrome button templates' $historyCode 'BuildChromeButton('
Assert-NotContains 'trainer history window does not use old minimize glyph' $historyCode 'Content = "━"'
Assert-NotContains 'trainer history window does not use old close glyph' $historyCode 'Content = "×"'
Assert-NotContains 'trainer history window does not use old maximize glyph' $historyCode 'Content = "◻"'
Assert-NotContains 'trainer history window does not use old restore glyph' $historyCode 'Content = "◰"'

$dialogThemingCode = Read-Utf8 'Utils\DialogTheming.cs'
Assert-Contains 'chromeless dialogs apply shared close style' $dialogThemingCode 'TitleBarButtonIcons.ApplyCloseButtonStyle(closeButton);'
Assert-NotContains 'chromeless dialogs do not build local close button templates' $dialogThemingCode 'GetCloseButtonTemplate'
Assert-NotContains 'chromeless dialogs do not use old close glyph' $dialogThemingCode 'Content = "✕"'

Write-Host 'All shared title bar button style tests passed.'
