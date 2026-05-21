$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$sources = @(
    (Join-Path $root 'Tests\ThemeColorHelperTests.cs')
)

$helperPath = Join-Path $root 'Utils\ThemeColorHelper.cs'
if (Test-Path $helperPath) {
    $sources += $helperPath
}

Add-Type -Path $sources

$testType = [AppDomain]::CurrentDomain.GetAssemblies() |
    ForEach-Object { $_.GetType('TypeSunny.Tests.ThemeColorHelperTests', $false) } |
    Where-Object { $_ -ne $null } |
    Select-Object -First 1

if ($null -eq $testType) {
    throw 'Unable to load ThemeColorHelperTests.'
}

$main = $testType.GetMethod('Main', [System.Reflection.BindingFlags]'NonPublic, Static')
if ($null -eq $main) {
    throw 'Unable to find ThemeColorHelperTests.Main.'
}

$exitCode = [int]$main.Invoke($null, @())
if ($exitCode -ne 0) {
    throw "ThemeColorHelperTests failed with exit code $exitCode."
}
