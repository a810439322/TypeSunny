using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TypeSunny.Personalization
{
    internal sealed class PersonalScorePredictionService
    {
        private readonly IPersonalTypingProfileStore store;
        private readonly AllHistoryTypingHistoryStore historyStore;
        private readonly Func<string, double> baseDifficultyCalculator;
        private readonly Func<string, IEnumerable<string>> difficultySegmenter;
        private readonly object pendingWriteLock = new object();
        private Task pendingWrite;
        private bool historyBootstrapAttempted;

        public PersonalScorePredictionService(
            IPersonalTypingProfileStore store,
            Func<string, double> baseDifficultyCalculator,
            Func<string, IEnumerable<string>> difficultySegmenter = null)
            : this(store, baseDifficultyCalculator, difficultySegmenter, new AllHistoryTypingHistoryStore())
        {
        }

        public PersonalScorePredictionService(
            IPersonalTypingProfileStore store,
            Func<string, double> baseDifficultyCalculator,
            Func<string, IEnumerable<string>> difficultySegmenter,
            AllHistoryTypingHistoryStore historyStore)
        {
            this.store = store ?? new PersonalTypingProfileStore();
            this.historyStore = historyStore;
            this.baseDifficultyCalculator = baseDifficultyCalculator ?? (_ => 0);
            this.difficultySegmenter = difficultySegmenter ?? (_ => new string[0]);
        }

        /// <summary>
        /// 同步训练入口：构造 session、按需查 DB、应用增量更新。
        /// 该方法**返回前**数据已落盘，便于测试断言。MainWindow 等 UI 调用应该走
        /// <see cref="TrainAsync"/>，避免阻塞主线程。
        /// </summary>
        public void Train(
            string targetText,
            IEnumerable<string> commitTexts,
            IEnumerable<long> commitTimes,
            IEnumerable<long> keyTimes,
            PersonalTypingRoundStats stats)
        {
            try
            {
                string[] fallbackSegments = ToArray(difficultySegmenter(targetText ?? ""));

                PersonalTypingSession session = PersonalTypingSessionBuilder.Build(
                    targetText,
                    commitTexts,
                    commitTimes,
                    keyTimes,
                    fallbackSegments);

                if (session.EffectiveStatCharacters <= 0)
                    return;

                // 只把 session.Samples 里出现的 text 拉到内存（这些是真正会被修改的 unit）
                var sampleTexts = new HashSet<string>();
                foreach (var s in session.Samples)
                {
                    if (s != null && !string.IsNullOrEmpty(s.Text))
                        sampleTexts.Add(s.Text);
                }

                PersonalTypingProfile profile = store.LoadWithUnits(sampleTexts);
                profile.Update(session, stats);
                // profile.Units 此时包含本次相关 unit 的最新值；ApplyTraining 只 UPSERT 这些行 + 写 Baseline。
                store.ApplyTraining(profile);
            }
            catch
            {
            }
        }

        /// <summary>
        /// 异步训练入口：把 <see cref="Train"/> 扔到后台线程，立刻返回。
        /// 多次调用串行执行（后一次等前一次完成），避免乱序写入。
        /// 关窗时主窗口应 <see cref="FlushPendingWrites"/> 等待落盘。
        /// </summary>
        public Task TrainAsync(
            string targetText,
            IEnumerable<string> commitTexts,
            IEnumerable<long> commitTimes,
            IEnumerable<long> keyTimes,
            PersonalTypingRoundStats stats)
        {
            // 复制成数组避免后台线程读到正在被外部修改的集合
            string[] commitTextArr = ToArray(commitTexts);
            long[] commitTimeArr = ToArray(commitTimes);
            long[] keyTimeArr = ToArray(keyTimes);

            lock (pendingWriteLock)
            {
                Task previous = pendingWrite;
                Task next;
                if (previous == null || previous.IsCompleted)
                {
                    next = Task.Run(() => Train(targetText, commitTextArr, commitTimeArr, keyTimeArr, stats));
                }
                else
                {
                    next = previous.ContinueWith(_ =>
                    {
                        try { Train(targetText, commitTextArr, commitTimeArr, keyTimeArr, stats); }
                        catch { }
                    }, TaskContinuationOptions.ExecuteSynchronously);
                }
                pendingWrite = next;
                return next;
            }
        }

        /// <summary>
        /// 把一轮的 <see cref="Calibrate"/> 和 <see cref="TrainAsync"/> 串到同一条后台任务上，
        /// 严格顺序：先 Calibrate（更新 TimeFactor/KeyFactor）再 Train（更新 Baseline + Units）。
        ///
        /// 优势：
        /// - UI 线程在 StopHelper 里完全不阻塞（之前 Calibrate 是同步的，可能等上一轮 TrainAsync 落盘）；
        /// - 同一轮内 Calibrate 与 Train 串行，跨轮之间也按 FIFO 排队，不会乱序；
        /// - 关窗时调 <see cref="FlushPendingWrites"/> 一并等齐。
        /// </summary>
        public Task CalibrateAndTrainAsync(
            PersonalScorePredictionSnapshot snapshot,
            PersonalTypingRoundStats stats,
            string expectedTextHash,
            string targetText,
            IEnumerable<string> commitTexts,
            IEnumerable<long> commitTimes,
            IEnumerable<long> keyTimes)
        {
            // 复制集合避免后台线程读到外部正在变化的状态
            string[] commitTextArr = ToArray(commitTexts);
            long[] commitTimeArr = ToArray(commitTimes);
            long[] keyTimeArr = ToArray(keyTimes);

            lock (pendingWriteLock)
            {
                Task previous = pendingWrite;
                Action job = () =>
                {
                    try { Calibrate(snapshot, stats, expectedTextHash); } catch { }
                    try { Train(targetText, commitTextArr, commitTimeArr, keyTimeArr, stats); } catch { }
                };

                Task next = (previous == null || previous.IsCompleted)
                    ? Task.Run(job)
                    : previous.ContinueWith(_ => job(), TaskContinuationOptions.ExecuteSynchronously);

                pendingWrite = next;
                return next;
            }
        }

        public Task RecordHistoryCalibrateAndTrainAsync(
            AllHistoryRoundRecord historyRecord,
            PersonalScorePredictionSnapshot snapshot,
            PersonalTypingRoundStats stats,
            string expectedTextHash)
        {
            if (historyRecord == null)
                return CalibrateAndTrainAsync(snapshot, stats, expectedTextHash, "", new string[0], new long[0], new long[0]);

            string[] commitTextArr = ToArray(historyRecord.CommitTexts);
            long[] commitTimeArr = ToArray(historyRecord.CommitTimes);
            long[] keyTimeArr = ToArray(historyRecord.KeyTimes);
            string targetText = historyRecord.TargetText ?? "";

            lock (pendingWriteLock)
            {
                Task previous = pendingWrite;
                Action job = () =>
                {
                    bool shouldTrain = historyRecord.IsFirstAttempt;
                    try
                    {
                        if (historyStore != null)
                        {
                            long roundId = historyStore.AppendRound(historyRecord);
                            AllHistoryRoundSummary summary = historyStore.LoadRoundSummary(roundId);
                            shouldTrain = summary != null && summary.IsFirstAttempt;
                        }
                    }
                    catch
                    {
                        // 历史库失败不能影响首打训练；显式重打仍不能训练画像。
                    }

                    if (!shouldTrain)
                        return;

                    try { Calibrate(snapshot, stats, expectedTextHash); } catch { }
                    try { TrainFromCommitUnitsOnly(historyRecord, stats); } catch { }
                };

                Task next = (previous == null || previous.IsCompleted)
                    ? Task.Run(job)
                    : previous.ContinueWith(_ => job(), TaskContinuationOptions.ExecuteSynchronously);

                pendingWrite = next;
                return next;
            }
        }

        public int RebuildProfileFromHistory(bool firstAttemptsOnly)
        {
            if (historyStore == null)
                return 0;

            int count = 0;
            var profile = new PersonalTypingProfile();
            foreach (AllHistoryReplayRound round in historyStore.LoadReplayRounds(firstAttemptsOnly))
            {
                if (round == null || round.Samples == null || round.Samples.Count == 0)
                    continue;

                var session = new PersonalTypingSession
                {
                    EffectiveStatCharacters = Math.Max(0, round.Stats.TotalWords - 3),
                    EffectiveMilliseconds = round.Stats.TotalSeconds * 1000.0
                };

                foreach (AllHistoryUnitSample sample in round.Samples)
                {
                    if (sample == null
                        || string.IsNullOrEmpty(sample.UnitText)
                        || sample.UnitLength <= 0
                        || sample.UnitLength > 4
                        || sample.StartCharIndex < 3
                        || !PersonalUnitExtractor.IsPureChineseUnit(sample.UnitText))
                    {
                        continue;
                    }

                    session.Samples.Add(new PersonalTypingUnitSample(
                        sample.UnitText,
                        sample.ElapsedMilliseconds,
                        sample.KeyCount));
                }

                if (session.EffectiveStatCharacters <= 0 || session.Samples.Count == 0)
                    continue;

                profile.Update(session, round.Stats);
                count++;
            }

            store.Save(profile);
            return count;
        }

        private void TrainFromCommitUnitsOnly(AllHistoryRoundRecord record, PersonalTypingRoundStats stats)
        {
            if (record == null || stats == null)
                return;

            var session = BuildCommitUnitSession(record);
            if (session.EffectiveStatCharacters <= 0 || session.Samples.Count == 0)
                return;

            var sampleTexts = new HashSet<string>();
            foreach (PersonalTypingUnitSample sample in session.Samples)
            {
                if (sample != null && !string.IsNullOrEmpty(sample.Text))
                    sampleTexts.Add(sample.Text);
            }

            PersonalTypingProfile profile = store.LoadWithUnits(sampleTexts);
            profile.Update(session, stats);
            store.ApplyTraining(profile);
        }

        private static PersonalTypingSession BuildCommitUnitSession(AllHistoryRoundRecord record)
        {
            var session = new PersonalTypingSession
            {
                EffectiveStatCharacters = Math.Max(0, record.TotalWords - 3),
                EffectiveMilliseconds = Math.Max(0, record.TotalSeconds * 1000.0)
            };

            string[] commits = ToArray(record.CommitTexts);
            long[] commitTimes = ToArray(record.CommitTimes);
            long[] keyTimes = ToArray(record.KeyTimes);
            int count = Math.Min(commits.Length, commitTimes.Length);
            int charIndex = 0;
            long previousCommitTime = 0;

            for (int i = 0; i < count; i++)
            {
                string unit = commits[i] ?? "";
                int unitLength = new System.Globalization.StringInfo(unit).LengthInTextElements;
                long commitTime = commitTimes[i];
                int start = charIndex;
                int end = charIndex + unitLength;

                if (start >= 3
                    && unitLength > 0
                    && unitLength <= 4
                    && PersonalUnitExtractor.IsPureChineseUnit(unit))
                {
                    session.Samples.Add(new PersonalTypingUnitSample(
                        unit,
                        EffectiveMillisecondsBetween(previousCommitTime, commitTime, keyTimes),
                        CountKeysBetween(previousCommitTime, commitTime, keyTimes)));
                }

                charIndex = end;
                previousCommitTime = commitTime;
            }

            return session;
        }

        private static int CountKeysBetween(long startExclusive, long endInclusive, long[] keyTimes)
        {
            int count = 0;
            if (keyTimes == null)
                return count;

            for (int i = 0; i < keyTimes.Length; i++)
            {
                if (keyTimes[i] > startExclusive && keyTimes[i] <= endInclusive)
                    count++;
            }
            return count;
        }

        private static double EffectiveMillisecondsBetween(long start, long end, long[] keyTimes)
        {
            if (end <= start)
                return 0;

            var events = new List<long>();
            events.Add(start);
            if (keyTimes != null)
            {
                for (int i = 0; i < keyTimes.Length; i++)
                {
                    if (keyTimes[i] > start && keyTimes[i] < end)
                        events.Add(keyTimes[i]);
                }
            }
            events.Add(end);

            double effective = end - start;
            for (int i = 1; i < events.Count; i++)
            {
                long gap = events[i] - events[i - 1];
                if (gap > 10000)
                    effective -= gap;
            }

            return Math.Max(0, effective);
        }

        /// <summary>
        /// 等待最近一次 <see cref="TrainAsync"/> 完成。MainWindow 关窗时调用，
        /// 避免后台 Train 还没落盘进程就退出。
        /// </summary>
        public void FlushPendingWrites()
        {
            Task task;
            lock (pendingWriteLock)
            {
                task = pendingWrite;
            }
            if (task == null)
                return;

            try { task.Wait(); }
            catch { /* 后台异常已被 Train 内部吞掉，这里只是等待 */ }
        }

        public PersonalScorePredictionSnapshot CreateSnapshot(string text, string baseDifficultyText)
        {
            try
            {
                // 物化一次，给 CollectAllKeys 和 Predict 共用，避免多次 enumerate 引起的潜在副作用
                string[] fallbackSegments = ToArray(difficultySegmenter(text ?? ""));
                HashSet<string> candidateKeys = PersonalUnitExtractor.CollectAllKeys(text, fallbackSegments);

                PersonalTypingProfile profile = store.LoadWithUnits(candidateKeys);
                if (profile.EffectiveStatCharacters <= 0)
                {
                    TryBootstrapProfileFromHistory();
                    profile = store.LoadWithUnits(candidateKeys);
                }
                if (profile.EffectiveStatCharacters <= 0)
                    return new PersonalScorePredictionSnapshot();

                double baseScore = GetBaseDifficultyScore(text, baseDifficultyText);
                PersonalScorePrediction prediction = PersonalScorePredictor.Predict(
                    text,
                    profile,
                    baseScore,
                    fallbackSegments);
                return PersonalScorePredictionSnapshot.FromPrediction(text, baseScore, prediction);
            }
            catch
            {
                return new PersonalScorePredictionSnapshot();
            }
        }

        /// <summary>
        /// 冷启动门控：累积有效字符不足该阈值时跳过校准更新——此时 DP 输出基本都是 fallback
        /// 速度（基线 120），ratio 反映的是"用户速度与默认 120 的差"而非"模型与现实的差"，
        /// 学进去只会污染 TimeFactor。仍允许使用已有 factor 显示。
        /// </summary>
        private const int CalibrationColdStartThreshold = 200;

        /// <summary>
        /// 校准本轮预测与实际成绩。
        ///
        /// 如果 <paramref name="expectedTextHash"/> 非空，必须与 <paramref name="snapshot"/> 的
        /// <see cref="PersonalScorePredictionSnapshot.TargetTextHash"/> 一致才会执行校准——避免
        /// snapshot 被中途文本/难度刷新覆盖后，A 文 snapshot 与 B 文 actual 配错。传 null 跳过校验。
        /// </summary>
        public void Calibrate(PersonalScorePredictionSnapshot snapshot, PersonalTypingRoundStats actual, string expectedTextHash = null)
        {
            try
            {
                if (snapshot == null || actual == null || !snapshot.HasPrediction)
                    return;

                if (!string.IsNullOrEmpty(expectedTextHash)
                    && !string.Equals(expectedTextHash, snapshot.TargetTextHash, StringComparison.Ordinal))
                {
                    // snapshot 不匹配本轮真实文本 —— 跳过，避免污染因子
                    return;
                }

                // Calibration 不需要任何 unit，按 baseline + 旧 calibration 计算即可
                PersonalTypingProfile profile = store.LoadWithUnits(new string[0]);
                if (profile.EffectiveStatCharacters < CalibrationColdStartThreshold)
                    return;
                profile.UpdateCalibration(snapshot, actual);
                store.ApplyCalibration(profile.Calibration);
            }
            catch
            {
            }
        }

        public string AppendPrediction(string text, string baseDifficultyText)
        {
            return AppendPrediction(text, baseDifficultyText, true, null, null);
        }

        public string AppendPrediction(
            string text,
            string baseDifficultyText,
            bool enabled,
            IEnumerable<string> order,
            Func<string, bool> isVisible)
        {
            try
            {
                if (!enabled)
                    return baseDifficultyText;

                PersonalScorePrediction prediction = Predict(text, baseDifficultyText);
                if (prediction.PredictedSpeed <= 0)
                    return baseDifficultyText;

                string prefix = string.IsNullOrWhiteSpace(baseDifficultyText)
                    ? ""
                    : baseDifficultyText.Trim() + " ";
                return prefix + PersonalScorePredictionFormatter.Format(prediction, order, isVisible);
            }
            catch
            {
                return baseDifficultyText;
            }
        }

        public string AppendPredictionSnapshot(
            string baseDifficultyText,
            PersonalScorePredictionSnapshot snapshot,
            bool enabled,
            IEnumerable<string> order,
            Func<string, bool> isVisible)
        {
            try
            {
                if (!enabled || snapshot == null || !snapshot.HasPrediction)
                    return baseDifficultyText;

                string predictionText = PersonalScorePredictionFormatter.Format(
                    snapshot.ToPrediction(),
                    order,
                    isVisible);
                if (string.IsNullOrWhiteSpace(predictionText))
                    return baseDifficultyText;

                string prefix = string.IsNullOrWhiteSpace(baseDifficultyText)
                    ? ""
                    : baseDifficultyText.Trim() + " ";
                return prefix + predictionText;
            }
            catch
            {
                return baseDifficultyText;
            }
        }

        public PersonalScorePrediction Predict(string text, string baseDifficultyText)
        {
            string[] fallbackSegments = ToArray(difficultySegmenter(text ?? ""));
            HashSet<string> candidateKeys = PersonalUnitExtractor.CollectAllKeys(text, fallbackSegments);

            PersonalTypingProfile profile = store.LoadWithUnits(candidateKeys);
            if (profile.EffectiveStatCharacters <= 0)
            {
                TryBootstrapProfileFromHistory();
                profile = store.LoadWithUnits(candidateKeys);
            }
            if (profile.EffectiveStatCharacters <= 0)
                return new PersonalScorePrediction();

            double baseScore = GetBaseDifficultyScore(text, baseDifficultyText);
            return PersonalScorePredictor.Predict(
                text,
                profile,
                baseScore,
                fallbackSegments);
        }

        private double GetBaseDifficultyScore(string text, string baseDifficultyText)
        {
            double baseScore = ExtractDifficultyScore(baseDifficultyText);
            if (baseScore <= 0)
                baseScore = Math.Max(0.01, baseDifficultyCalculator(text ?? ""));
            return baseScore;
        }

        private void TryBootstrapProfileFromHistory()
        {
            if (historyBootstrapAttempted || historyStore == null)
                return;

            historyBootstrapAttempted = true;
            try { RebuildProfileFromHistory(firstAttemptsOnly: true); }
            catch { }
        }

        internal static double ExtractDifficultyScore(string difficultyText)
        {
            if (string.IsNullOrWhiteSpace(difficultyText))
                return 0;

            int open = difficultyText.LastIndexOf('(');
            int close = difficultyText.LastIndexOf(')');
            if (open < 0 || close <= open)
                return 0;

            string raw = difficultyText.Substring(open + 1, close - open - 1);
            double value;
            if (double.TryParse(raw, out value))
                return value;
            return 0;
        }

        private static T[] ToArray<T>(IEnumerable<T> source)
        {
            if (source == null)
                return new T[0];
            var arr = source as T[];
            if (arr != null)
                return arr;
            var list = new List<T>(source);
            return list.ToArray();
        }
    }
}
