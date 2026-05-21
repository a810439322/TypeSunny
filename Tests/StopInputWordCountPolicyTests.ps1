$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
Add-Type -Path @(
    (Join-Path $root 'Core\StateManager.cs'),
    (Join-Path $root 'UI\StopInputWordCountPolicy.cs'),
    (Join-Path $root 'Tests\StopInputWordCountPolicyTests.cs')
)

$testType = [AppDomain]::CurrentDomain.GetAssemblies() |
    ForEach-Object { $_.GetType('TypeSunny.Tests.StopInputWordCountPolicyTests', $false) } |
    Where-Object { $_ -ne $null } |
    Select-Object -First 1

if ($null -eq $testType) {
    throw 'Unable to load StopInputWordCountPolicyTests.'
}

$main = $testType.GetMethod('Main', [System.Reflection.BindingFlags]'NonPublic, Static')
if ($null -eq $main) {
    throw 'Unable to find StopInputWordCountPolicyTests.Main.'
}

$exitCode = [int]$main.Invoke($null, @())
if ($exitCode -ne 0) {
    throw "StopInputWordCountPolicyTests failed with exit code $exitCode."
}
