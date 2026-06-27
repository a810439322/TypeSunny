$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
Add-Type -Path @(
    (Join-Path $root 'Diff\DiffRes.cs'),
    (Join-Path $root 'Core\RetypeTextBuilder.cs'),
    (Join-Path $root 'Tests\RetypeTextBuilderTests.cs')
)

$testType = [AppDomain]::CurrentDomain.GetAssemblies() |
    ForEach-Object { $_.GetType('TypeSunny.Tests.RetypeTextBuilderTests', $false) } |
    Where-Object { $_ -ne $null } |
    Select-Object -First 1

if ($null -eq $testType) {
    throw 'Unable to load RetypeTextBuilderTests.'
}

$main = $testType.GetMethod('Main', [System.Reflection.BindingFlags]'NonPublic, Static')
if ($null -eq $main) {
    throw 'Unable to find RetypeTextBuilderTests.Main.'
}

$exitCode = [int]$main.Invoke($null, @())
if ($exitCode -ne 0) {
    throw "RetypeTextBuilderTests failed with exit code $exitCode."
}
