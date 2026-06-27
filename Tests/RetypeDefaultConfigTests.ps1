$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$configCode = Get-Content -Raw -Encoding UTF8 (Join-Path $root 'Config\Config.cs')

function Join-Chars {
    param([int[]]$Codes)
    return -join ($Codes | ForEach-Object { [char]$_ })
}

function Assert-Match {
    param(
        [string]$Name,
        [string]$Text,
        [string]$Pattern
    )

    if ($Text -notmatch $Pattern) {
        throw $Name
    }
}

function Quote-Regex {
    param([string]$Text)
    return [regex]::Escape($Text)
}

$wrongRetype = Join-Chars @(0x9519, 0x5b57, 0x91cd, 0x6253)
$slowRetype = Join-Chars @(0x6162, 0x5b57, 0x91cd, 0x6253)
$no = Join-Chars @(0x5426)

Assert-Match 'Wrong retype should default to disabled.' $configCode "`"$(Quote-Regex $wrongRetype)`"\s*,\s*`"$(Quote-Regex $no)`""
Assert-Match 'Slow retype should default to disabled.' $configCode "`"$(Quote-Regex $slowRetype)`"\s*,\s*`"$(Quote-Regex $no)`""

Write-Host 'Retype default config tests passed.'
