$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
Add-Type -Path @(
    (Join-Path $root 'Net\DailyWordsModels.cs'),
    (Join-Path $root 'Tests\DailyWordsModelsTests.cs')
)

$testType = [AppDomain]::CurrentDomain.GetAssemblies() |
    ForEach-Object { $_.GetType('TypeSunny.Tests.DailyWordsModelsTests', $false) } |
    Where-Object { $_ -ne $null } |
    Select-Object -First 1

if ($null -eq $testType) {
    throw 'Unable to load DailyWordsModelsTests.'
}

$main = $testType.GetMethod('Main', [System.Reflection.BindingFlags]'NonPublic, Static')
if ($null -eq $main) {
    throw 'Unable to find DailyWordsModelsTests.Main.'
}

$exitCode = [int]$main.Invoke($null, @())
if ($exitCode -ne 0) {
    throw "DailyWordsModelsTests failed with exit code $exitCode."
}
