$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
Add-Type -Path @(
    (Join-Path $root 'UI\LocalArticleContinuationPolicy.cs'),
    (Join-Path $root 'Tests\LocalArticleContinuationPolicyTests.cs')
)

$testType = [AppDomain]::CurrentDomain.GetAssemblies() |
    ForEach-Object { $_.GetType('TypeSunny.Tests.LocalArticleContinuationPolicyTests', $false) } |
    Where-Object { $_ -ne $null } |
    Select-Object -First 1

if ($null -eq $testType) {
    throw 'Unable to load LocalArticleContinuationPolicyTests.'
}

$main = $testType.GetMethod('Main', [System.Reflection.BindingFlags]'NonPublic, Static')
if ($null -eq $main) {
    throw 'Unable to find LocalArticleContinuationPolicyTests.Main.'
}

$exitCode = [int]$main.Invoke($null, @())
if ($exitCode -ne 0) {
    throw "LocalArticleContinuationPolicyTests failed with exit code $exitCode."
}
