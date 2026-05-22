$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$newtonsoft = Join-Path $root 'packages\Newtonsoft.Json.13.0.3\lib\net45\Newtonsoft.Json.dll'
[System.Reflection.Assembly]::LoadFrom($newtonsoft) | Out-Null
Add-Type -ReferencedAssemblies @($newtonsoft) -Path @(
    (Join-Path $root 'Personalization\QingDifficultyScale.cs'),
    (Join-Path $root 'Difficulty\QingDifficultyScorer.cs'),
    (Join-Path $root 'Difficulty\DifficultyDict.cs'),
    (Join-Path $root 'Tests\QingDifficultyScorerTests.cs')
)

$testType = [AppDomain]::CurrentDomain.GetAssemblies() |
    ForEach-Object { $_.GetType('TypeSunny.Tests.QingDifficultyScorerTests', $false) } |
    Where-Object { $_ -ne $null } |
    Select-Object -First 1

if ($null -eq $testType) {
    throw 'Unable to load QingDifficultyScorerTests.'
}

$main = $testType.GetMethod('Main', [System.Reflection.BindingFlags]'NonPublic, Static')
if ($null -eq $main) {
    throw 'Unable to find QingDifficultyScorerTests.Main.'
}

$exitCode = [int]$main.Invoke($null, @())
if ($exitCode -ne 0) {
    throw "QingDifficultyScorerTests failed with exit code $exitCode."
}
