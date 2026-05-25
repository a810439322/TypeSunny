$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
Add-Type -Path @(
    (Join-Path $root 'Core\StateManager.cs'),
    (Join-Path $root 'Core\SpeedFollowHintFormatter.cs'),
    (Join-Path $root 'Tests\SpeedFollowHintFormatterTests.cs')
)

$testType = [AppDomain]::CurrentDomain.GetAssemblies() |
    ForEach-Object { $_.GetType('TypeSunny.Tests.SpeedFollowHintFormatterTests', $false) } |
    Where-Object { $_ -ne $null } |
    Select-Object -First 1

if ($null -eq $testType) {
    throw 'Unable to load SpeedFollowHintFormatterTests.'
}

$main = $testType.GetMethod('Main', [System.Reflection.BindingFlags]'NonPublic, Static')
if ($null -eq $main) {
    throw 'Unable to find SpeedFollowHintFormatterTests.Main.'
}

$exitCode = [int]$main.Invoke($null, @())
if ($exitCode -ne 0) {
    throw "SpeedFollowHintFormatterTests failed with exit code $exitCode."
}
