$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$scrollCode = Get-Content -Raw -Encoding UTF8 (Join-Path $root 'Utils\SmoothScrollHelper.cs')
$smoothLineWrapKey = -join @([char]0x5e73, [char]0x6ed1, [char]0x6362, [char]0x884c)

if ($scrollCode -notmatch 'public\s+static\s+bool\s+AnimateScrollTo\s*\(') {
    throw 'SmoothScrollHelper should expose AnimateScrollTo returning bool.'
}

if ($scrollCode -notmatch 'Action\s+started\s*=\s*null') {
    throw 'SmoothScrollHelper should accept a started callback.'
}

if ($scrollCode -notmatch 'Action\s+completed\s*=\s*null') {
    throw 'SmoothScrollHelper should accept a completed callback.'
}

if ($scrollCode -match ('Config\.GetBool\("' + [regex]::Escape($smoothLineWrapKey) + '"\)')) {
    throw 'SmoothScrollHelper should not expose smooth line wrap as a user-facing off switch.'
}

if ($scrollCode -notmatch 'BeginAnimation\s*\(\s*VerticalOffsetProperty\s*,\s*null\s*\)') {
    throw 'SmoothScrollHelper should cancel an existing offset animation before starting a new one.'
}

if ($scrollCode -notmatch 'SetValue\s*\(\s*VerticalOffsetProperty') {
    throw 'SmoothScrollHelper should pin the current offset as the attached property base value.'
}

Write-Host 'Smooth scroll helper structure tests passed.'

$sources = @(
    (Join-Path $root 'Tests\SmoothScrollTests.cs'),
    (Join-Path $root 'Utils\SmoothScrollHelper.cs')
)

Add-Type -Path $sources -ReferencedAssemblies @(
    'WindowsBase',
    'PresentationCore',
    'PresentationFramework',
    'System.Xaml'
)

$testType = [AppDomain]::CurrentDomain.GetAssemblies() |
    ForEach-Object { $_.GetType('TypeSunny.Tests.SmoothScrollTests', $false) } |
    Where-Object { $_ -ne $null } |
    Select-Object -First 1

if ($null -eq $testType) {
    throw 'Unable to load SmoothScrollTests.'
}

$main = $testType.GetMethod('Main', [System.Reflection.BindingFlags]'NonPublic, Static')
if ($null -eq $main) {
    throw 'Unable to find SmoothScrollTests.Main.'
}

$exitCode = [int]$main.Invoke($null, @())
if ($exitCode -ne 0) {
    throw "SmoothScrollTests failed with exit code $exitCode."
}
