$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$fixtureDir = Join-Path $root 'Resources\词提'
$fixturePath = Join-Path $fixtureDir '测试词提.txt'
$hadFixture = Test-Path $fixturePath
$oldFixture = if ($hadFixture) { Get-Content -Path $fixturePath -Raw } else { $null }

try {
    New-Item -ItemType Directory -Path $fixtureDir -Force | Out-Null
    @(
        "中`tz_",
        "国`tg_",
        "中国`tzg_",
        "人`tr_",
        "民`tm_",
        "人民`trm2",
        "天地`ttd_"
    ) | Set-Content -Path $fixturePath -Encoding UTF8

    Add-Type -Path @(
        (Join-Path $root 'Utils\CiTiHelper.cs'),
        (Join-Path $root 'Tests\CiTiHelperTests.cs')
    )

    $testType = [AppDomain]::CurrentDomain.GetAssemblies() |
        ForEach-Object { $_.GetType('TypeSunny.Tests.CiTiHelperTests', $false) } |
        Where-Object { $_ -ne $null } |
        Select-Object -First 1

    if ($null -eq $testType) {
        throw 'Unable to load CiTiHelperTests.'
    }

    $main = $testType.GetMethod('Main', [System.Reflection.BindingFlags]'NonPublic, Static')
    if ($null -eq $main) {
        throw 'Unable to find CiTiHelperTests.Main.'
    }

    $exitCode = [int]$main.Invoke($null, @())
    if ($exitCode -ne 0) {
        throw "CiTiHelperTests failed with exit code $exitCode."
    }
}
finally {
    if ($hadFixture) {
        Set-Content -Path $fixturePath -Value $oldFixture -Encoding UTF8
    }
    elseif (Test-Path $fixturePath) {
        Remove-Item -LiteralPath $fixturePath
    }
}
