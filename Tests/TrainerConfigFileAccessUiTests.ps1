$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$trainerCode = Get-Content -Raw -Encoding UTF8 (Join-Path $root 'WinTrainer\WinTrainer.xaml.cs')

function Assert-Contains($name, $text, $needle) {
    if (-not $text.Contains($needle)) {
        throw "$name missing expected text: $needle"
    }
}

function Assert-NotContains($name, $text, $needle) {
    if ($text.Contains($needle)) {
        throw "$name should not contain text: $needle"
    }
}

function Get-Block($text, $startNeedle, $endNeedle) {
    $start = $text.IndexOf($startNeedle)
    if ($start -lt 0) {
        throw "Unable to find block start: $startNeedle"
    }

    $end = $text.IndexOf($endNeedle, $start + $startNeedle.Length)
    if ($end -lt 0) {
        throw "Unable to find block end: $endNeedle"
    }

    return $text.Substring($start, $end - $start)
}

$initBlock = Get-Block $trainerCode 'private void InitCfg()' 'private void WriteCfg()'
$writeBlock = Get-Block $trainerCode 'private void WriteCfg()' 'private int GetCharCount'

Assert-Contains 'trainer config initialization uses centralized reader' $initBlock 'TrainerConfig.ReadInto(cfg);'
Assert-NotContains 'trainer config initialization must not hold raw reader' $initBlock 'new StreamReader(TrainerConfig.Path)'

Assert-Contains 'trainer config save uses centralized writer' $writeBlock 'TrainerConfig.WriteValues(cfg);'
Assert-NotContains 'trainer config save must not hold raw writer' $writeBlock 'new StreamWriter(configPath)'
Assert-NotContains 'trainer config save no longer shows file-lock modal directly' $writeBlock 'MessageBox.Show'

Write-Host 'All trainer config file access UI tests passed.'
