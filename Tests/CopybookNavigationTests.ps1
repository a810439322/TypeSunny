$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$sources = @()
$navigationSource = Join-Path $root 'UI\Modes\CopybookNavigation.cs'
if (Test-Path $navigationSource) {
    $sources += $navigationSource
}
$sources += Join-Path $root 'Tests\CopybookNavigationTests.cs'

Add-Type -Path $sources

$testType = [AppDomain]::CurrentDomain.GetAssemblies() |
    ForEach-Object { $_.GetType('TypeSunny.Tests.CopybookNavigationTests', $false) } |
    Where-Object { $_ -ne $null } |
    Select-Object -First 1

if ($null -eq $testType) {
    throw 'Unable to load CopybookNavigationTests.'
}

$main = $testType.GetMethod('Main', [System.Reflection.BindingFlags]'NonPublic, Static')
if ($null -eq $main) {
    throw 'Unable to find CopybookNavigationTests.Main.'
}

$exitCode = [int]$main.Invoke($null, @())
if ($exitCode -ne 0) {
    throw "CopybookNavigationTests failed with exit code $exitCode."
}
