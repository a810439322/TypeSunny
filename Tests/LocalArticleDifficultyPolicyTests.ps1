$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
Add-Type -Path @(
    (Join-Path $root 'Article\LocalArticleDifficultyPolicy.cs'),
    (Join-Path $root 'Tests\LocalArticleDifficultyPolicyTests.cs')
)

$testType = [AppDomain]::CurrentDomain.GetAssemblies() |
    ForEach-Object { $_.GetType('TypeSunny.Tests.LocalArticleDifficultyPolicyTests', $false) } |
    Where-Object { $_ -ne $null } |
    Select-Object -First 1

if ($null -eq $testType) {
    throw 'Unable to load LocalArticleDifficultyPolicyTests.'
}

$main = $testType.GetMethod('Main', [System.Reflection.BindingFlags]'NonPublic, Static')
if ($null -eq $main) {
    throw 'Unable to find LocalArticleDifficultyPolicyTests.Main.'
}

$exitCode = [int]$main.Invoke($null, @())
if ($exitCode -ne 0) {
    throw "LocalArticleDifficultyPolicyTests failed with exit code $exitCode."
}
