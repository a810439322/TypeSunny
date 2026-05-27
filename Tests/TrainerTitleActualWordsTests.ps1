$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$trainerCode = Get-Content -Raw (Join-Path $root 'WinTrainer\WinTrainer.xaml.cs')
$historyCode = Get-Content -Raw (Join-Path $root 'WinTrainer\WinTrainerHistoryWindow.cs')
$titleStatsCode = Get-Content -Raw (Join-Path $root 'WinTrainer\TrainerTitleWordStats.cs')
$trainerLogCode = Get-Content -Raw (Join-Path $root 'WinTrainer\TrainerLog.cs')
$mainCode = Get-Content -Raw (Join-Path $root 'UI\MainWindow.xaml.cs')
$copybookCode = Get-Content -Raw (Join-Path $root 'UI\Modes\CopybookMode.cs')
$tracingCode = Get-Content -Raw (Join-Path $root 'UI\Modes\TracingMode.cs')

function Assert-Contains {
    param(
        [string]$Name,
        [string]$Text,
        [string]$Expected
    )

    if (-not $Text.Contains($Expected)) {
        throw "$Name missing expected text: $Expected"
    }
}

function Assert-NotContains {
    param(
        [string]$Name,
        [string]$Text,
        [string]$Unexpected
    )

    if ($Text.Contains($Unexpected)) {
        throw "$Name should not contain unexpected text: $Unexpected"
    }
}

function Assert-Regex {
    param(
        [string]$Name,
        [string]$Text,
        [string]$Pattern
    )

    if ($Text -notmatch $Pattern) {
        throw "$Name expected to match pattern: $Pattern"
    }
}

function Get-Block {
    param(
        [string]$Text,
        [string]$Start,
        [string]$End
    )

    $startIndex = $Text.IndexOf($Start)
    if ($startIndex -lt 0) {
        throw "missing block start: $Start"
    }

    $endIndex = $Text.IndexOf($End, $startIndex)
    if ($endIndex -lt 0) {
        throw "missing block end after $Start`: $End"
    }

    return $Text.Substring($startIndex, $endIndex - $startIndex)
}

$trainerTitleBlock = Get-Block $trainerCode 'private void InitializeTitleStats()' 'private void UpdateTitleBarStats()'
Assert-Contains 'trainer title reads dedicated persistent stats' $trainerTitleBlock 'TrainerTitleWordStats.Read()'
Assert-Contains 'trainer title today uses dedicated actual words' $trainerTitleBlock '_displayedTodayWords = snapshot.TodayWords;'
Assert-Contains 'trainer title total uses dedicated actual words' $trainerTitleBlock '_displayedTotalWords = snapshot.TotalWords;'
Assert-NotContains 'trainer title must not sum grouped words' $trainerTitleBlock 'Sum(s => s.TotalWords)'

$recordLogBlock = Get-Block $trainerCode 'private void RecordRoundLog()' 'string GetMatchText()'
Assert-Contains 'trainer title log write keeps trainer log eligibility words' $recordLogBlock 'TrainerLog.WriteRecord(record);'
Assert-NotContains 'trainer title completion must not add grouped words after per-group updates' $recordLogBlock 'OnRecordWritten(record.TotalWords, record.InputWords);'
Assert-NotContains 'trainer title increment must not use grouped words only' $recordLogBlock 'OnRecordWritten(record.TotalWords);'

$getNextRoundBlock = Get-Block $trainerCode 'public void GetNextRound' 'public void RecordPartialProgress'
Assert-Contains 'trainer settled group actual words use trainer target words' $getNextRoundBlock 'int actualWordsDelta = GetCurrentGroupWordCount();'
Assert-NotContains 'trainer title must not double count settled groups' $getNextRoundBlock 'AddDisplayedActualWords(actualWordsDelta);'
Assert-NotContains 'trainer settled group actual words must not count raw input letters' $getNextRoundBlock 'Score.InputWordCount'
Assert-NotContains 'trainer settled group actual words must not count raw input plus backs' $getNextRoundBlock 'scoreInputWords + scoreBacks'

$partialProgressBlock = Get-Block $trainerCode 'public void RecordPartialProgress' 'internal void RefreshTitleWordStats'
Assert-Contains 'trainer partial progress derives actual target words' $partialProgressBlock 'int actualWords = GetCurrentPartialActualWordCount(inputWordCount);'
Assert-NotContains 'trainer partial progress must not count raw input letters directly' $partialProgressBlock 'Math.Min(inputWordCount, GetCurrentGroupWordCount())'
Assert-NotContains 'trainer title must not depend on ctrl+l partial progress' $partialProgressBlock 'TrainerTitleWordStats.AddWords'

$partialCounterBlock = Get-Block $trainerCode 'private int GetCurrentPartialActualWordCount' 'private int GetCurrentGroupWordCount'
Assert-Contains 'trainer partial progress uses commit-aware counter' $partialCounterBlock 'TrainerActualWordCounter.CountPartialWords'

$refreshTitleBlock = Get-Block $trainerCode 'internal void RefreshTitleWordStats' 'public void F3()'
Assert-Contains 'trainer title refresh ignores null snapshots' $refreshTitleBlock 'if (snapshot == null)'
Assert-Contains 'trainer title refresh marshals from score thread' $refreshTitleBlock 'if (!Dispatcher.CheckAccess())'
Assert-Contains 'trainer title refresh invokes on trainer dispatcher' $refreshTitleBlock 'Dispatcher.Invoke(new Action(() => RefreshTitleWordStats(snapshot)))'
Assert-Contains 'trainer title refresh reads persisted today words' $refreshTitleBlock '_displayedTodayWords = snapshot.TodayWords;'
Assert-Contains 'trainer title refresh reads persisted total words' $refreshTitleBlock '_displayedTotalWords = snapshot.TotalWords;'
Assert-NotContains 'trainer window must not persist title words itself' $refreshTitleBlock 'TrainerTitleWordStats.AddWords'
Assert-NotContains 'trainer window must not own title word persistence' $trainerCode 'TrainerTitleWordStats.AddWords'

$historyTitleBlock = Get-Block $historyCode 'private void UpdateTitleBarStats()' 'private void RestoreWindowState()'
Assert-Contains 'history title reads dedicated persistent stats' $historyTitleBlock 'TrainerTitleWordStats.Read()'
Assert-Contains 'history title today uses dedicated actual words' $historyTitleBlock 'snapshot.TodayWords'
Assert-Contains 'history title total uses dedicated actual words' $historyTitleBlock 'snapshot.TotalWords'
Assert-NotContains 'history title must not sum grouped words' $historyTitleBlock 'Sum(s => s.TotalWords)'

Assert-Contains 'title stats migrates existing trainer totals once' $titleStatsCode 'MigrateFromTrainerLogIfNeededLocked'
Assert-Contains 'title stats stores migration flag' $titleStatsCode 'MigratedFromTrainerLog'
Assert-Contains 'title stats migrates daily input totals' $titleStatsCode 'TrainerLog.ReadDailyInputWordTotals()'
Assert-Contains 'title stats can sync total floor from detailed trainer words' $titleStatsCode 'EnsureTotalAtLeast'
Assert-Contains 'title stats total floor does not mutate daily words' $titleStatsCode 'TotalWordsFloor'
Assert-Contains 'main syncs title total after flushing pending trainer title words' $mainCode 'SyncTrainerTitleTotalFromDetailedStats();'
Assert-Contains 'main loads detailed snapshot for trainer title sync' $mainCode 'DetailedWordCountLog.LoadSnapshot'
Assert-Contains 'main applies detailed trainer words as title total floor' $mainCode 'TrainerTitleWordStats.EnsureTotalAtLeast(snapshot.TrainerWords)'
Assert-Contains 'trainer log exposes daily actual totals for migration' $trainerLogCode 'public static Dictionary<string, int> ReadDailyInputWordTotals()'
Assert-Contains 'trainer log daily migration falls back to grouped words for legacy summaries' $trainerLogCode 'summary.TotalInputWords > 0 ? summary.TotalInputWords : summary.TotalWords'

$textChangedBlock = Get-Block $mainCode 'private void TbxInput_TextChanged' '//     AutomationElement aeInput;'
Assert-Contains 'main trainer input uses target-word delta counter' $textChangedBlock 'ResolveTypedWordCountDelta(addedLength)'
Assert-Contains 'main normal input records through shared typed word recorder' $textChangedBlock 'RecordTypedWords(wordsToRecord);'
Assert-Contains 'main resolves trainer delta from target words' $textChangedBlock 'trainerTypedWordCounter.AddFrom'
Assert-Contains 'main exposes direct-mode typed word resolver' $mainCode 'public int ResolveTypedWordCountDelta(string inputText, int fallbackInputTextElements, int targetStartIndex)'
Assert-Contains 'main direct-mode trainer delta uses shared incremental counter' $mainCode 'trainerTypedWordCounter.AddFrom('
Assert-NotContains 'main direct-mode trainer delta must not count each text event independently' $mainCode 'TrainerActualWordCounter.CountCommittedWords'
Assert-NotContains 'main normal input must not persist title stats outside shared recorder' $textChangedBlock 'TrainerTitleWordStats.AddWords'
Assert-NotContains 'main trainer path must not add raw input letters to global counter directly' $textChangedBlock 'CounterLog.Buffer[0] += addedLength;'

$recordWordsBlock = Get-Block $mainCode 'public void RecordTypedWords(int words)' 'private void QueueTrainerTitleTypedWords'
Assert-Contains 'main shared typed word recorder ignores zero delta' $recordWordsBlock 'if (words <= 0)'
Assert-Contains 'main shared typed word recorder updates global counter' $recordWordsBlock 'CounterLog.Buffer[0] += words;'
Assert-Contains 'main shared typed word recorder updates detailed stats' $recordWordsBlock 'RecordDetailedTypedWords(words);'
Assert-Contains 'main shared typed word recorder queues trainer title stats' $recordWordsBlock 'QueueTrainerTitleTypedWords(words);'
Assert-NotContains 'main input recorder must not refresh trainer title immediately' $recordWordsBlock 'trainer.RefreshTitleWordStats'
Assert-NotContains 'main input recorder must not persist trainer title immediately' $recordWordsBlock 'TrainerTitleWordStats.AddWords'

$typingStatBlock = Get-Block $mainCode 'private void UpdateTypingStatCore(List<string> newReportItems, bool commitCounterBuffer)' 'private int CountResultHeaderLines'
Assert-Contains 'typing stat refresh flushes trainer title with same cadence' $typingStatBlock 'FlushTrainerTitleTypedWords();'
Assert-Regex 'typing stat flush happens before optional counter commit' $typingStatBlock 'FlushTrainerTitleTypedWords\(\);[\s\S]{0,120}if\s*\(commitCounterBuffer\)'

$queueTitleBlock = Get-Block $mainCode 'private void QueueTrainerTitleTypedWords(int words)' 'public void RecordDetailedTypedWords'
Assert-Contains 'main exposes queued trainer title recorder' $queueTitleBlock 'private void QueueTrainerTitleTypedWords(int words)'
Assert-Contains 'queued trainer title recorder only applies in trainer context' $queueTitleBlock 'StateManager.txtSource != TxtSource.trainer'
Assert-Contains 'queued trainer title recorder accumulates pending words' $queueTitleBlock 'pendingTrainerTitleWords += words;'
Assert-Contains 'trainer title flush persists pending words' $queueTitleBlock 'TrainerTitleWordStats.AddWords(wordsToFlush)'
Assert-Contains 'trainer title flush refreshes visible trainer title' $queueTitleBlock 'trainer.RefreshTitleWordStats(trainerTitleSnapshot);'

$copybookInputBlock = Get-Block $copybookCode 'private void ProcessInputText(string inputText, string committedComposition = null)' 'private void ScheduleInputCaptureTrim()'
Assert-Contains 'copybook trainer input resolves target-word delta' $copybookInputBlock 'int wordsToRecord = _main.ResolveTypedWordCountDelta(inputText, si.LengthInTextElements, _currentIndex);'
Assert-Contains 'copybook records through shared typed word recorder' $copybookInputBlock '_main.RecordTypedWords(wordsToRecord);'
Assert-NotContains 'copybook must not add raw input letters to global counter' $copybookInputBlock 'CounterLog.Buffer[0] += si.LengthInTextElements;'
Assert-NotContains 'copybook must not record raw input letters to detailed stats' $copybookInputBlock '_main.RecordDetailedTypedWords(si.LengthInTextElements);'

$tracingInputBlock = Get-Block $tracingCode 'private void ProcessInputText(string inputText, string committedComposition = null)' 'private void OnPreviewKeyDown'
Assert-Contains 'tracing trainer input resolves target-word delta' $tracingInputBlock 'int wordsToRecord = _main.ResolveTypedWordCountDelta(inputText, si.LengthInTextElements, _currentIndex);'
Assert-Contains 'tracing records through shared typed word recorder' $tracingInputBlock '_main.RecordTypedWords(wordsToRecord);'
Assert-NotContains 'tracing must not add raw input letters to global counter' $tracingInputBlock 'CounterLog.Buffer[0] += si.LengthInTextElements;'
Assert-NotContains 'tracing must not record raw input letters to detailed stats' $tracingInputBlock '_main.RecordDetailedTypedWords(si.LengthInTextElements);'

$trainerResolverBlock = Get-Block $mainCode 'private WinTrainer GetCurrentTrainerWindow()' 'private void ShowWinTrainer()'
Assert-Contains 'main trainer resolver falls back to live trainer window' $trainerResolverBlock 'WinTrainer.Current'
Assert-Contains 'main trainer resolver refreshes cached trainer reference' $trainerResolverBlock 'winTrainer = current;'

$showTrainerBlock = Get-Block $mainCode 'private void ShowWinTrainer()' 'public string QQGroupName'
Assert-Contains 'show trainer keeps cached reference when current exists' $showTrainerBlock 'winTrainer = WinTrainer.Current;'

$ctrlLBlock = Get-Block $mainCode 'private void InternalHotkeyCtrlL' 'private void InternalHotkeyCtrlR'
Assert-Contains 'trainer ctrl+l uses shared partial progress helper' $ctrlLBlock 'RecordTrainerPartialProgressIfNeeded();'
Assert-Contains 'trainer ctrl+l resolves current trainer window' $ctrlLBlock 'var trainer = GetCurrentTrainerWindow();'
Assert-NotContains 'trainer ctrl+l must not require running stopwatch' $ctrlLBlock 'StateManager.typingState == TypingState.typing && sw.IsRunning'
Assert-Contains 'trainer partial progress predicate accepts paused input' $mainCode 'StateManager.typingState != TypingState.typing && StateManager.typingState != TypingState.pause'

Write-Host 'Trainer title actual word count tests passed.'
