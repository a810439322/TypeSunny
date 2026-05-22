$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$mainCode = Get-Content -Path (Join-Path $root 'UI\MainWindow.xaml.cs') -Raw

function New-TextFromCodePoints([int[]]$codePoints) {
    return -join ($codePoints | ForEach-Object { [char]$_ })
}

function Assert-NotContains($name, $content, $needle) {
    if ($content.Contains($needle)) {
        throw "$name expected not to contain [$needle]"
    }
}

$updateZiTiMatch = [regex]::Match(
    $mainCode,
    'internal void UpdateZiTi\(\)\s*\{(?<body>.*?)\n        \}',
    [System.Text.RegularExpressions.RegexOptions]::Singleline)
if (-not $updateZiTiMatch.Success) {
    throw 'Unable to find MainWindow.UpdateZiTi.'
}

$ziTiCodeDisplayKey = New-TextFromCodePoints @(0x5B57, 0x63D0, 0x7F16, 0x7801, 0x4E0B, 0x663E)
$needle = 'Config.GetBool("' + $ziTiCodeDisplayKey + '")'
Assert-NotContains 'top-right ZiTi should not be hidden by ZiTi lower display' $updateZiTiMatch.Groups['body'].Value $needle

Write-Host 'All ZiTi corner prompt tests passed.'
