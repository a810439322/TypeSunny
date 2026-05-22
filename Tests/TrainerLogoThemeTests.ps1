$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$trainerXaml = Get-Content -Path (Join-Path $root 'WinTrainer\WinTrainer.xaml') -Raw -Encoding UTF8
$trainerCode = Get-Content -Path (Join-Path $root 'WinTrainer\WinTrainer.xaml.cs') -Raw -Encoding UTF8
$configCode = Get-Content -Path (Join-Path $root 'WinConfig\WinConfig.xaml.cs') -Raw -Encoding UTF8
$currentLogoKey = [string]::Concat([char]0x5F53, [char]0x524D, [char]0x004C, [char]0x006F, [char]0x0067, [char]0x006F)
$currentLogoConfigRead = 'Config.GetString("' + $currentLogoKey + '")'

function Assert-Contains($name, $content, $needle) {
    if (-not $content.Contains($needle)) {
        throw "$name expected to contain [$needle]"
    }
}

Assert-Contains 'trainer title bar icon is named for dynamic logo refresh' $trainerXaml 'x:Name="TitleBarIcon"'
Assert-Contains 'trainer logo refresh method exists' $trainerCode 'private void ApplyCurrentLogo()'
Assert-Contains 'trainer logo uses current logo config' $trainerCode $currentLogoConfigRead
Assert-Contains 'trainer window icon is updated' $trainerCode 'this.Icon = new BitmapImage(iconUri)'
Assert-Contains 'trainer title icon is updated' $trainerCode 'TitleBarIcon.Source = new BitmapImage(iconUri)'
Assert-Contains 'trainer constructor applies current logo' $trainerCode 'ApplyCurrentLogo();'
Assert-Contains 'config logo change refreshes trainer windows' $configCode 'if (window is WinTrainer trainerWindow)'
Assert-Contains 'config logo change invokes trainer refresh' $configCode 'trainerWindow.RefreshTheme();'

Write-Host 'Trainer logo theme tests passed.'
