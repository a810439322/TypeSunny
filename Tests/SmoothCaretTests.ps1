$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$sources = @(
    (Join-Path $root 'Tests\SmoothCaretTests.cs'),
    (Join-Path $root 'UI\SmoothMotionTiming.cs'),
    (Join-Path $root 'UI\SmoothCaret.cs')
)

Add-Type -Path $sources -ReferencedAssemblies @(
    'WindowsBase',
    'PresentationCore',
    'PresentationFramework',
    'System.Xaml'
)

$testType = [AppDomain]::CurrentDomain.GetAssemblies() |
    ForEach-Object { $_.GetType('TypeSunny.Tests.SmoothCaretTests', $false) } |
    Where-Object { $_ -ne $null } |
    Select-Object -First 1

if ($null -eq $testType) {
    throw 'Unable to load SmoothCaretTests.'
}

$main = $testType.GetMethod('Main', [System.Reflection.BindingFlags]'NonPublic, Static')
if ($null -eq $main) {
    throw 'Unable to find SmoothCaretTests.Main.'
}

$exitCode = [int]$main.Invoke($null, @())
if ($exitCode -ne 0) {
    throw "SmoothCaretTests failed with exit code $exitCode."
}
