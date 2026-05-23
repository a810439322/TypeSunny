$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$newtonsoft = Join-Path $root 'packages\Newtonsoft.Json.13.0.3\lib\net45\Newtonsoft.Json.dll'
$sqlite = Join-Path $root 'lib\SQLite\System.Data.SQLite.dll'
[System.Reflection.Assembly]::LoadFrom($newtonsoft) | Out-Null
[System.Reflection.Assembly]::LoadFrom($sqlite) | Out-Null
Add-Type -ReferencedAssemblies @($newtonsoft, $sqlite, 'System.Data.dll') -Path @(
    (Join-Path $root 'Logs\DebugLog.cs'),
    (Join-Path $root 'Personalization\QingDifficultyScale.cs'),
    (Join-Path $root 'Personalization\PersonalPredictionCalibration.cs'),
    (Join-Path $root 'Personalization\PersonalTypingProfile.cs'),
    (Join-Path $root 'Personalization\IPersonalTypingProfileStore.cs'),
    (Join-Path $root 'Personalization\LegacyJsonProfile.cs'),
    (Join-Path $root 'Personalization\SqlitePersonalTypingProfileStore.cs'),
    (Join-Path $root 'Personalization\PersonalTypingProfileStore.cs'),
    (Join-Path $root 'Personalization\PersonalUnitExtractor.cs'),
    (Join-Path $root 'Personalization\PersonalTypingSessionBuilder.cs'),
    (Join-Path $root 'Personalization\PersonalScorePredictionFormatter.cs'),
    (Join-Path $root 'Personalization\PersonalScorePredictor.cs'),
    (Join-Path $root 'Personalization\PersonalScorePredictionService.cs'),
    (Join-Path $root 'Tests\PersonalScorePredictionTests.cs')
)

$testType = [AppDomain]::CurrentDomain.GetAssemblies() |
    ForEach-Object { $_.GetType('TypeSunny.Tests.PersonalScorePredictionTests', $false) } |
    Where-Object { $_ -ne $null } |
    Select-Object -First 1

if ($null -eq $testType) {
    throw 'Unable to load PersonalScorePredictionTests.'
}

$main = $testType.GetMethod('Main', [System.Reflection.BindingFlags]'NonPublic, Static')
if ($null -eq $main) {
    throw 'Unable to find PersonalScorePredictionTests.Main.'
}

$exitCode = [int]$main.Invoke($null, @())
if ($exitCode -ne 0) {
    throw "PersonalScorePredictionTests failed with exit code $exitCode."
}
