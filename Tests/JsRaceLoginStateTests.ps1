$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
Add-Type -Path @(
    (Join-Path $root 'Utils\PasswordCrypto.cs'),
    (Join-Path $root 'Net\JsRaceLoginState.cs'),
    (Join-Path $root 'Tests\JsRaceLoginStateTests.cs')
)

$testType = [AppDomain]::CurrentDomain.GetAssemblies() |
    ForEach-Object { $_.GetType('TypeSunny.Tests.JsRaceLoginStateTests', $false) } |
    Where-Object { $_ -ne $null } |
    Select-Object -First 1

if ($null -eq $testType) {
    throw 'Unable to load JsRaceLoginStateTests.'
}

$main = $testType.GetMethod('Main', [System.Reflection.BindingFlags]'NonPublic, Static')
if ($null -eq $main) {
    throw 'Unable to find JsRaceLoginStateTests.Main.'
}

$exitCode = [int]$main.Invoke($null, @())
if ($exitCode -ne 0) {
    throw "JsRaceLoginStateTests failed with exit code $exitCode."
}
