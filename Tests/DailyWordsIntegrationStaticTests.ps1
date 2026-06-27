$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$mainXaml = Get-Content (Join-Path $root 'UI\MainWindow.xaml') -Raw -Encoding UTF8
$mainCode = Get-Content (Join-Path $root 'UI\MainWindow.xaml.cs') -Raw -Encoding UTF8
$trainerCode = Get-Content (Join-Path $root 'WinTrainer\WinTrainer.xaml.cs') -Raw -Encoding UTF8
$configCode = Get-Content (Join-Path $root 'Config\Config.cs') -Raw -Encoding UTF8
$projectCode = Get-Content (Join-Path $root 'TypeSunny.csproj') -Raw -Encoding UTF8
$serviceCode = Get-Content (Join-Path $root 'net\WenlaiDailyWordsService.cs') -Raw -Encoding UTF8

function Assert-Contains($name, $text, $pattern) {
    if ($text -notmatch $pattern) {
        throw "$name missing pattern: $pattern"
    }
}

function Assert-NotContains($name, $text, $pattern) {
    if ($text -match $pattern) {
        throw "$name should not match pattern: $pattern"
    }
}

function Get-Block($text, $start, $end) {
    $startIndex = $text.IndexOf($start)
    if ($startIndex -lt 0) {
        throw "Unable to find block start: $start"
    }

    $endIndex = $text.IndexOf($end, $startIndex)
    if ($endIndex -lt 0) {
        throw "Unable to find block end: $end"
    }

    return $text.Substring($startIndex, $endIndex - $startIndex)
}

Assert-Contains 'rank badge button' $mainXaml 'DailyWordsRankBadgeButton'
Assert-Contains 'rank badge trophy icon' $mainXaml 'DailyWordsRankTrophyIcon'
Assert-Contains 'rank badge text' $mainXaml 'DailyWordsRankBadgeText'
Assert-Contains 'rank badge click handler' $mainXaml 'DailyWordsRankBadgeButton_Click'
Assert-Contains 'rank badge button has no border' $mainXaml 'x:Name="DailyWordsRankBadgeButton"[\s\S]*?BorderThickness="0"'

Assert-Contains 'record typed words accumulates daily words' $mainCode 'AddPendingDailyWordsTypedStatistics\(words\)'
Assert-Contains 'trainer settled group records source weighted daily keystroke' $trainerCode 'RecordDailyWordsTrainerGroupStatisticsSafely\(actualWordsDelta,\s*hitrate\)'
Assert-Contains 'main exposes trainer daily keystroke recorder' $mainCode 'RecordDailyWordsTrainerGroupStatistics\(int sourceWords,\s*double hitrate\)'
Assert-Contains 'daily words flush queues upload' $mainCode 'QueueDailyWordsCompletionReport\(report\)'
Assert-Contains 'login flushes daily words' $mainCode 'OnWenlaiDailyWordsLoginStateChanged\(\)'
Assert-Contains 'logout refreshes daily words badge' $mainCode 'RefreshDailyWordsRankBadgeLoginState\(\)'
Assert-Contains 'leaderboard window opens' $mainCode 'WinDailyWordsLeaderboard'
Assert-Contains 'daily rank badge login gate' $mainCode 'private void OpenDailyWordsLeaderboardAfterLogin\(\)[\s\S]*?!IsWenlaiDailyWordsLoggedIn\(\)'
Assert-Contains 'daily rank badge opens login' $mainCode 'private void OpenDailyWordsLeaderboardAfterLogin\(\)[\s\S]*?ShowLoginDialog\(this\)'
Assert-Contains 'daily rank badge opens leaderboard after login' $mainCode 'private void OpenDailyWordsLeaderboardAfterLogin\(\)[\s\S]*?OpenDailyWordsLeaderboard\(\);'

$updateTypingStatBlock = Get-Block $mainCode 'public void UpdateTypingStat(List<string> newReportItems = null)' 'private void RefreshTypingStatDisplay()'
Assert-Contains 'trainer stat refresh must not flush before trainer group metric' $updateTypingStatBlock 'if \(StateManager\.txtSource != TxtSource\.trainer\)[\s\S]*?FlushPendingDailyWordsTypedStatistics\(\);'

$trainerPassedBeforeDispatcherBlock = Get-Block $trainerCode 'roundCompletedGroups++;' 'this.Dispatcher.Invoke(new Action(() =>'
Assert-NotContains 'trainer daily keystroke must not touch MainWindow from StopHelper background thread' $trainerPassedBeforeDispatcherBlock 'MainWindow\.Current\?\.RecordDailyWordsTrainerGroupStatistics'
Assert-Contains 'trainer daily keystroke records on UI dispatcher before next trainer LoadText' $trainerCode 'this\.Dispatcher\.Invoke\(new Action\(\(\) =>\s*\{[\s\S]*?RecordDailyWordsTrainerGroupStatisticsSafely\(actualWordsDelta,\s*hitrate\);[\s\S]*?AutoNextGroup\(out roundRecord\);[\s\S]*?MainWindow\.Current\.LoadText\(matchText'
Assert-Contains 'trainer daily keystroke recorder is guarded' $trainerCode 'private void RecordDailyWordsTrainerGroupStatisticsSafely\(int sourceWords,\s*double hitrate\)[\s\S]*?try[\s\S]*?MainWindow\.Current\?\.RecordDailyWordsTrainerGroupStatistics\(sourceWords,\s*hitrate\);[\s\S]*?catch'

function Text-FromCodePoints([int[]]$points) {
    return [string]::Concat(($points | ForEach-Object { [char]$_ }))
}

$prefix = Text-FromCodePoints -points @(0x5b57, 0x6570, 0x699c, 0x5f85, 0x4e0a, 0x4f20)
$pendingKeys = New-Object 'System.Collections.Generic.List[string]'
$pendingKeys.Add($prefix + (Text-FromCodePoints -points @(0x65e5, 0x671f)))
$pendingKeys.Add($prefix + (Text-FromCodePoints -points @(0x5b57, 0x6570)))
$pendingKeys.Add($prefix + (Text-FromCodePoints -points @(0x5355, 0x5b57, 0x5b57, 0x6570)))
$pendingKeys.Add($prefix + (Text-FromCodePoints -points @(0x6253, 0x6587, 0x5b57, 0x6570)))
$pendingKeys.Add($prefix + (Text-FromCodePoints -points @(0x6253, 0x6587, 0x5747, 0x901f)))
$pendingKeys.Add($prefix + (Text-FromCodePoints -points @(0x6253, 0x5355, 0x5747, 0x51fb)))

foreach ($key in $pendingKeys) {
    Assert-Contains "config key $key" $configCode ([regex]::Escape($key))
}

Assert-Contains 'daily service report endpoint' $projectCode 'Net\\WenlaiDailyWordsService\.cs'
Assert-Contains 'daily leaderboard window xaml' $projectCode 'UI\\WinDailyWordsLeaderboard\.xaml'
Assert-Contains 'daily leaderboard window code-behind' $projectCode 'UI\\WinDailyWordsLeaderboard\.xaml\.cs'

Assert-Contains 'report endpoint path' $serviceCode '/api/dailyWords/report'
Assert-Contains 'rank endpoint path' $serviceCode '/api/dailyWords/rank'
Assert-Contains 'leaderboard endpoint path' $serviceCode '/api/dailyWords/leaderboard'
Assert-Contains 'jwt auth fallback' $serviceCode 'JwtAuthProvider'
Assert-Contains 'cookie auth fallback' $serviceCode 'CookieAuthProvider'
Assert-Contains 'cookie account field' $serviceCode 'account\.Cookies'
