$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$sources = @(
    (Join-Path $root 'Tests\SmoothBackgroundTests.cs'),
    (Join-Path $root 'UI\SmoothBackground.cs')
)

Add-Type -Path $sources -ReferencedAssemblies @(
    'WindowsBase',
    'PresentationCore',
    'PresentationFramework',
    'System.Xaml'
)

$testType = [AppDomain]::CurrentDomain.GetAssemblies() |
    ForEach-Object { $_.GetType('TypeSunny.Tests.SmoothBackgroundTests', $false) } |
    Where-Object { $_ -ne $null } |
    Select-Object -First 1

if ($null -eq $testType) {
    throw 'Unable to load SmoothBackgroundTests.'
}

$main = $testType.GetMethod('Main', [System.Reflection.BindingFlags]'NonPublic, Static')
if ($null -eq $main) {
    throw 'Unable to find SmoothBackgroundTests.Main.'
}

$exitCode = [int]$main.Invoke($null, @())
if ($exitCode -ne 0) {
    throw "SmoothBackgroundTests failed with exit code $exitCode."
}
