$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot

function Read-Utf8($path) {
    return [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)
}

$articleXaml = Read-Utf8 (Join-Path $root 'Article\WinArticle.xaml')
$articleCode = Read-Utf8 (Join-Path $root 'Article\WinArticle.xaml.cs')
$trainerXaml = Read-Utf8 (Join-Path $root 'WinTrainer\WinTrainer.xaml')
$trainerCode = Read-Utf8 (Join-Path $root 'WinTrainer\WinTrainer.xaml.cs')
$configCode = Read-Utf8 (Join-Path $root 'Config\Config.cs')

$localCloseAfterSendKey = -join ([int[]](0x672C,0x5730,0x53D1,0x6587,0x540E,0x5173,0x95ED,0x7A97,0x53E3) | ForEach-Object { [char]$_ })
$trainerCloseAfterSendKey = -join ([int[]](0x7EC3,0x5355,0x53D1,0x6587,0x540E,0x5173,0x95ED,0x7A97,0x53E3) | ForEach-Object { [char]$_ })
$checkboxLabel = -join ([int[]](0x53D1,0x6587,0x540E,0x5173,0x95ED,0x7A97,0x53E3) | ForEach-Object { [char]$_ })
$noValue = -join ([int[]](0x5426) | ForEach-Object { [char]$_ })

function Assert-Contains($name, $content, $needle) {
    if (-not $content.Contains($needle)) {
        throw "$name expected to contain [$needle]"
    }
}

Assert-Contains 'local article has close-after-send checkbox' $articleXaml 'x:Name="CbCloseAfterSend"'
Assert-Contains 'local article checkbox label' $articleXaml $checkboxLabel
Assert-Contains 'local article config default' $configCode ('"' + $localCloseAfterSendKey + '", "' + $noValue + '"')
Assert-Contains 'local article initializes remembered close-after-send state' $articleCode ('CbCloseAfterSend.IsChecked = Config.GetBool("' + $localCloseAfterSendKey + '");')
Assert-Contains 'local article saves close-after-send state' $articleCode ('Config.Set("' + $localCloseAfterSendKey + '", CbCloseAfterSend.IsChecked == true);')
Assert-Contains 'local article closes after send when enabled' $articleCode 'CloseArticleWindowAfterSendIfNeeded();'

Assert-Contains 'trainer has close-after-send checkbox' $trainerXaml 'x:Name="CbCloseAfterSend"'
Assert-Contains 'trainer checkbox label' $trainerXaml $checkboxLabel
Assert-Contains 'trainer config default' $trainerCode ('{"' + $trainerCloseAfterSendKey + '", "' + $noValue + '" }')
Assert-Contains 'trainer initializes remembered close-after-send state' $trainerCode ('CbCloseAfterSend.IsChecked = cfg["' + $trainerCloseAfterSendKey + '"] == "' + $noValue + '" ? false : true;')
Assert-Contains 'trainer saves close-after-send state' $trainerCode ('cfg["' + $trainerCloseAfterSendKey + '"] = CbCloseAfterSend.IsChecked == true ? "')
Assert-Contains 'trainer closes after send when enabled' $trainerCode 'CloseTrainerWindowAfterSendIfNeeded();'

Write-Host 'All close-after-send window tests passed.'
