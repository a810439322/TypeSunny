$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$mainXaml = Get-Content -Raw (Join-Path $root 'UI\MainWindow.xaml')

$match = [regex]::Match($mainXaml, '<TextBox\s+Name="TbxResults"[^>]*>')
if (-not $match.Success) {
    throw 'Unable to find TbxResults TextBox.'
}

if ($match.Value -match 'FontWeight\s*=\s*["'']Bold["'']') {
    throw 'TbxResults should not set FontWeight="Bold".'
}

Write-Host 'Score panel presentation tests passed.'
