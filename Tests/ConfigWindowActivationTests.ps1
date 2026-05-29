$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$mainCode = Get-Content -Path (Join-Path $root 'UI\MainWindow.xaml.cs') -Raw -Encoding UTF8

function Assert-Contains($name, $content, $needle) {
    if (-not $content.Contains($needle)) {
        throw "$name expected to contain [$needle]"
    }
}

Assert-Contains 'context menu settings handler uses shared config opener' $mainCode 'OpenConfigWindow();'
Assert-Contains 'config opener restores minimized existing settings window' $mainCode 'existingConfig.WindowState == WindowState.Minimized'
Assert-Contains 'config opener sets minimized settings window back to normal' $mainCode 'existingConfig.WindowState = WindowState.Normal'
Assert-Contains 'config opener shows hidden existing settings window' $mainCode 'existingConfig.Show();'

Write-Host 'All ConfigWindowActivation tests passed.'
