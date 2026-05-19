$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$sources = @(
    (Join-Path $root 'Tests\CodeDisplayHelperTests.cs')
)

$helperPath = Join-Path $root 'Utils\CodeDisplayHelper.cs'
if (Test-Path $helperPath) {
    $sources += $helperPath
}

Add-Type -Path $sources

$testType = [AppDomain]::CurrentDomain.GetAssemblies() |
    ForEach-Object { $_.GetType('TypeSunny.Tests.CodeDisplayHelperTests', $false) } |
    Where-Object { $_ -ne $null } |
    Select-Object -First 1

if ($null -eq $testType) {
    throw 'Unable to load CodeDisplayHelperTests.'
}

$main = $testType.GetMethod('Main', [System.Reflection.BindingFlags]'NonPublic, Static')
if ($null -eq $main) {
    throw 'Unable to find CodeDisplayHelperTests.Main.'
}

$exitCode = [int]$main.Invoke($null, @())
if ($exitCode -ne 0) {
    throw "CodeDisplayHelperTests failed with exit code $exitCode."
}
