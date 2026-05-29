using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TypeSunny.Core;
using TypeSunny.Personalization;

namespace TypeSunny.Tests
{
    internal static class AllHistoryTypingHistoryTests
    {
        private static int Main()
        {
            try
            {
                StorePersistsRealCommitUnitsWithoutSplittingWords();
                StoreLoadsOnlyFirstAttemptSamplesForReplay();
                StoreAssignsAttemptIndexFromHistory();
                ServiceTrainsProfileFromFirstAttemptHistoryOnly();
                ServiceSkipsWrongAndSlowRetypeHistory();
                ServiceRebuildsProfileFromFirstAttemptHistory();
                ExplicitRetypeDoesNotTrainWhenHistoryIsEmpty();
                StoreSubtractsLongPausesFromUnitElapsedMilliseconds();
                EmptyProfileBootstrapsFromHistoryOnFirstPrediction();
                Console.WriteLine("All AllHistoryTypingHistory tests passed.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("FAIL: " + ex.Message);
                return 1;
            }
        }

        private static void StorePersistsRealCommitUnitsWithoutSplittingWords()
        {
            string path = TempDbPath();
            try
            {
                using (var store = new AllHistoryTypingHistoryStore(path))
                {
                    var record = NewRoundRecord(
                        text: "我是中国人",
                        commits: new[] { "我", "是", "中国", "人" },
                        commitTimes: new long[] { 500, 900, 1600, 2200 },
                        keyTimes: new long[] { 100, 240, 410, 700, 950, 1200, 1450, 1700 });

                    long roundId = store.AppendRound(record);
                    List<AllHistoryUnitSample> samples = store.LoadUnitSamples(roundId).ToList();

                    AssertEqual("sample count", 4, samples.Count);
                    AssertEqual("third sample is real commit word", "中国", samples[2].UnitText);
                    AssertFalse("word commit is not split into 中", samples.Any(s => s.UnitText == "中"));
                    AssertFalse("word commit is not split into 国", samples.Any(s => s.UnitText == "国"));
                    AssertTrue("elapsed ms captured", samples[2].ElapsedMilliseconds > 0);
                    AssertTrue("key count captured", samples[2].KeyCount > 0);
                }
            }
            finally
            {
                DeleteDb(path);
            }
        }

        private static void StoreLoadsOnlyFirstAttemptSamplesForReplay()
        {
            string path = TempDbPath();
            try
            {
                using (var store = new AllHistoryTypingHistoryStore(path))
                {
                    store.AppendRound(NewRoundRecord("天地人", new[] { "天", "地", "人" },
                        new long[] { 300, 700, 1100 }, new long[] { 100, 250, 500, 800 },
                        retypeType: RetypeType.first, attemptIndex: 1));

                    store.AppendRound(NewRoundRecord("天地人", new[] { "天地", "人" },
                        new long[] { 300, 600 }, new long[] { 100, 250, 500, 800 },
                        retypeType: RetypeType.retype, attemptIndex: 2));

                    List<AllHistoryReplayRound> rounds = store.LoadReplayRounds(firstAttemptsOnly: true).ToList();

                    AssertEqual("first-attempt replay round count", 1, rounds.Count);
                    AssertEqual("first replay commit count", 3, rounds[0].Samples.Count);
                    AssertEqual("first replay first unit", "天", rounds[0].Samples[0].UnitText);
                }
            }
            finally
            {
                DeleteDb(path);
            }
        }

        private static void StoreAssignsAttemptIndexFromHistory()
        {
            string path = TempDbPath();
            try
            {
                using (var store = new AllHistoryTypingHistoryStore(path))
                {
                    string text = "山河";
                    string groupKey = PersonalScorePredictionSnapshot.ComputeTextHash(text);

                    var first = NewRoundRecord(text, new[] { "山", "河" },
                        new long[] { 300, 600 }, new long[] { 100, 250, 500 },
                        retypeType: RetypeType.first, attemptIndex: 0);
                    first.AttemptGroupKey = groupKey;
                    long firstId = store.AppendRound(first);

                    var second = NewRoundRecord(text, new[] { "山河" },
                        new long[] { 450 }, new long[] { 100, 250, 400 },
                        retypeType: RetypeType.retype, attemptIndex: 0);
                    second.AttemptGroupKey = groupKey;
                    long secondId = store.AppendRound(second);

                    AllHistoryRoundSummary firstSummary = store.LoadRoundSummary(firstId);
                    AllHistoryRoundSummary secondSummary = store.LoadRoundSummary(secondId);

                    AssertEqual("db assigns first attempt index", 1, firstSummary.AttemptIndex);
                    AssertTrue("db assigns first attempt flag", firstSummary.IsFirstAttempt);
                    AssertEqual("db assigns second attempt index", 2, secondSummary.AttemptIndex);
                    AssertFalse("db marks second as retype", secondSummary.IsFirstAttempt);
                    AssertEqual("db links previous round", firstId, secondSummary.PreviousRoundId.Value);
                }
            }
            finally
            {
                DeleteDb(path);
            }
        }

        private static void ServiceTrainsProfileFromFirstAttemptHistoryOnly()
        {
            string profilePath = TempDbPath();
            string historyPath = TempDbPath();
            PersonalTypingProfileStore profileStore = null;
            AllHistoryTypingHistoryStore historyStore = null;
            try
            {
                profileStore = new PersonalTypingProfileStore(profilePath);
                historyStore = new AllHistoryTypingHistoryStore(historyPath);
                var service = new PersonalScorePredictionService(
                    profileStore,
                    value => 2.0,
                    value => new string[0],
                    historyStore);

                string target = "一二三中国人";
                var first = NewRoundRecord(target, new[] { "一", "二", "三", "中国", "人" },
                    new long[] { 100, 200, 300, 800, 1100 },
                    new long[] { 50, 150, 250, 350, 450, 650, 850, 1000 },
                    retypeType: RetypeType.first,
                    attemptIndex: 0);
                first.AttemptGroupKey = PersonalScorePredictionSnapshot.ComputeTextHash(target);

                var second = NewRoundRecord(target, new[] { "一", "二", "三", "中国人" },
                    new long[] { 100, 200, 300, 700 },
                    new long[] { 50, 150, 250, 350, 450, 650 },
                    retypeType: RetypeType.retype,
                    attemptIndex: 0);
                second.AttemptGroupKey = first.AttemptGroupKey;

                service.RecordHistoryCalibrateAndTrainAsync(
                    first,
                    new PersonalScorePredictionSnapshot(),
                    first.ToRoundStats(),
                    first.TextHash);
                service.RecordHistoryCalibrateAndTrainAsync(
                    second,
                    new PersonalScorePredictionSnapshot(),
                    second.ToRoundStats(),
                    second.TextHash);
                service.FlushPendingWrites();

                PersonalTypingProfile profile = profileStore.Load();
                AssertTrue("first attempt unit trained", profile.Units.ContainsKey("中国"));
                AssertFalse("retype-only unit not trained", profile.Units.ContainsKey("中国人"));
            }
            finally
            {
                if (profileStore != null) profileStore.Dispose();
                if (historyStore != null) historyStore.Dispose();
                DeleteDb(profilePath);
                DeleteDb(PersonalTypingProfileStore.NormalizePathToDb(profilePath));
                DeleteDb(historyPath);
            }
        }

        private static void ServiceSkipsWrongAndSlowRetypeHistory()
        {
            string profilePath = TempDbPath();
            string historyPath = TempDbPath();
            PersonalTypingProfileStore profileStore = null;
            AllHistoryTypingHistoryStore historyStore = null;
            try
            {
                profileStore = new PersonalTypingProfileStore(profilePath);
                historyStore = new AllHistoryTypingHistoryStore(historyPath);
                var service = new PersonalScorePredictionService(
                    profileStore,
                    value => 2.0,
                    value => new string[0],
                    historyStore);

                string text = "一二三中国";
                var wrong = NewRoundRecord(text, new[] { "一", "二", "三", "中国" },
                    new long[] { 100, 200, 300, 800 },
                    new long[] { 50, 150, 250, 350, 450, 650 },
                    retypeType: RetypeType.wrongRetype,
                    attemptIndex: 0);
                wrong.RetypeReason = "wrong";

                var slow = NewRoundRecord(text, new[] { "一", "二", "三", "中国" },
                    new long[] { 120, 240, 360, 900 },
                    new long[] { 60, 180, 300, 420, 540, 720 },
                    retypeType: RetypeType.slowRetype,
                    attemptIndex: 0);
                slow.RetypeReason = "slow";

                service.RecordHistoryCalibrateAndTrainAsync(
                    wrong,
                    new PersonalScorePredictionSnapshot(),
                    wrong.ToRoundStats(),
                    wrong.TextHash);
                service.RecordHistoryCalibrateAndTrainAsync(
                    slow,
                    new PersonalScorePredictionSnapshot(),
                    slow.ToRoundStats(),
                    slow.TextHash);
                service.FlushPendingWrites();

                AssertEqual("wrong and slow retypes are not stored in all_history", 0, historyStore.LoadReplayRoundSummaries().Count());
                AssertFalse("wrong and slow retypes did not train", profileStore.Load().Units.ContainsKey("中国"));
            }
            finally
            {
                if (profileStore != null) profileStore.Dispose();
                if (historyStore != null) historyStore.Dispose();
                DeleteDb(profilePath);
                DeleteDb(PersonalTypingProfileStore.NormalizePathToDb(profilePath));
                DeleteDb(historyPath);
            }
        }

        private static void ServiceRebuildsProfileFromFirstAttemptHistory()
        {
            string profilePath = TempDbPath();
            string historyPath = TempDbPath();
            PersonalTypingProfileStore profileStore = null;
            AllHistoryTypingHistoryStore historyStore = null;
            try
            {
                historyStore = new AllHistoryTypingHistoryStore(historyPath);
                string text = "一二三中国人";
                var first = NewRoundRecord(text, new[] { "一", "二", "三", "中国", "人" },
                    new long[] { 100, 200, 300, 800, 1100 },
                    new long[] { 50, 150, 250, 350, 450, 650, 850, 1000 },
                    retypeType: RetypeType.first,
                    attemptIndex: 0);
                first.AttemptGroupKey = PersonalScorePredictionSnapshot.ComputeTextHash(text);
                historyStore.AppendRound(first);

                var second = NewRoundRecord(text, new[] { "一", "二", "三", "中国人" },
                    new long[] { 100, 200, 300, 700 },
                    new long[] { 50, 150, 250, 350, 450, 650 },
                    retypeType: RetypeType.retype,
                    attemptIndex: 0);
                second.AttemptGroupKey = first.AttemptGroupKey;
                historyStore.AppendRound(second);
                historyStore.Dispose();
                historyStore = new AllHistoryTypingHistoryStore(historyPath);

                profileStore = new PersonalTypingProfileStore(profilePath);
                var service = new PersonalScorePredictionService(
                    profileStore,
                    value => 2.0,
                    value => new string[0],
                    historyStore);

                int rebuilt = service.RebuildProfileFromHistory(firstAttemptsOnly: true);

                PersonalTypingProfile profile = profileStore.Load();
                AssertEqual("rebuilt first attempt count", 1, rebuilt);
                AssertTrue("rebuilt first attempt unit", profile.Units.ContainsKey("中国"));
                AssertFalse("rebuilt skips warmup unit", profile.Units.ContainsKey("一"));
                AssertFalse("rebuilt excludes retype-only unit", profile.Units.ContainsKey("中国人"));
            }
            finally
            {
                if (profileStore != null) profileStore.Dispose();
                if (historyStore != null) historyStore.Dispose();
                DeleteDb(profilePath);
                DeleteDb(PersonalTypingProfileStore.NormalizePathToDb(profilePath));
                DeleteDb(historyPath);
            }
        }

        private static void ExplicitRetypeDoesNotTrainWhenHistoryIsEmpty()
        {
            string profilePath = TempDbPath();
            string historyPath = TempDbPath();
            PersonalTypingProfileStore profileStore = null;
            AllHistoryTypingHistoryStore historyStore = null;
            try
            {
                profileStore = new PersonalTypingProfileStore(profilePath);
                historyStore = new AllHistoryTypingHistoryStore(historyPath);
                var service = new PersonalScorePredictionService(
                    profileStore,
                    value => 2.0,
                    value => new string[0],
                    historyStore);

                string text = "一二三中国";
                var retype = NewRoundRecord(text, new[] { "一", "二", "三", "中国" },
                    new long[] { 100, 200, 300, 800 },
                    new long[] { 50, 150, 250, 350, 450, 650 },
                    retypeType: RetypeType.retype,
                    attemptIndex: 0);
                retype.AttemptGroupKey = PersonalScorePredictionSnapshot.ComputeTextHash(text);
                retype.IsFirstAttempt = false;

                service.RecordHistoryCalibrateAndTrainAsync(
                    retype,
                    new PersonalScorePredictionSnapshot(),
                    retype.ToRoundStats(),
                    retype.TextHash);
                service.FlushPendingWrites();

                AllHistoryRoundSummary summary = historyStore.LoadReplayRoundSummaries().Single();
                PersonalTypingProfile profile = profileStore.Load();
                AssertEqual("explicit retype gets attempt index one in empty history", 1, summary.AttemptIndex);
                AssertFalse("explicit retype is not first attempt", summary.IsFirstAttempt);
                AssertFalse("explicit retype did not train", profile.Units.ContainsKey("中国"));
            }
            finally
            {
                if (profileStore != null) profileStore.Dispose();
                if (historyStore != null) historyStore.Dispose();
                DeleteDb(profilePath);
                DeleteDb(PersonalTypingProfileStore.NormalizePathToDb(profilePath));
                DeleteDb(historyPath);
            }
        }

        private static void StoreSubtractsLongPausesFromUnitElapsedMilliseconds()
        {
            string path = TempDbPath();
            try
            {
                using (var store = new AllHistoryTypingHistoryStore(path))
                {
                    var record = NewRoundRecord(
                        text: "一二三四",
                        commits: new[] { "一", "二", "三", "四" },
                        commitTimes: new long[] { 1000, 2000, 3000, 15000 },
                        keyTimes: new long[] { 500, 1500, 2500, 14000 });

                    long roundId = store.AppendRound(record);
                    AllHistoryUnitSample sample = store.LoadUnitSamples(roundId).Last();

                    AssertEqual("pause-adjusted unit ms", 1000.0, sample.ElapsedMilliseconds, 0.001);
                }
            }
            finally
            {
                DeleteDb(path);
            }
        }

        private static void EmptyProfileBootstrapsFromHistoryOnFirstPrediction()
        {
            string profilePath = TempDbPath();
            string historyPath = TempDbPath();
            PersonalTypingProfileStore profileStore = null;
            AllHistoryTypingHistoryStore historyStore = null;
            try
            {
                historyStore = new AllHistoryTypingHistoryStore(historyPath);
                string text = "一二三中国人";
                var first = NewRoundRecord(text, new[] { "一", "二", "三", "中国", "人" },
                    new long[] { 100, 200, 300, 800, 1100 },
                    new long[] { 50, 150, 250, 350, 450, 650, 850, 1000 },
                    retypeType: RetypeType.first,
                    attemptIndex: 0);
                first.AttemptGroupKey = PersonalScorePredictionSnapshot.ComputeTextHash(text);
                historyStore.AppendRound(first);

                profileStore = new PersonalTypingProfileStore(profilePath);
                var service = new PersonalScorePredictionService(
                    profileStore,
                    value => 2.0,
                    value => new string[0],
                    historyStore);

                service.CreateSnapshot(text, "普(2.00)");

                PersonalTypingProfile profile = profileStore.Load();
                AssertTrue("empty profile bootstraps from all_history", profile.Units.ContainsKey("中国"));
            }
            finally
            {
                if (profileStore != null) profileStore.Dispose();
                if (historyStore != null) historyStore.Dispose();
                DeleteDb(profilePath);
                DeleteDb(PersonalTypingProfileStore.NormalizePathToDb(profilePath));
                DeleteDb(historyPath);
            }
        }

        private static AllHistoryRoundRecord NewRoundRecord(
            string text,
            IEnumerable<string> commits,
            IEnumerable<long> commitTimes,
            IEnumerable<long> keyTimes,
            RetypeType retypeType = RetypeType.first,
            int attemptIndex = 1)
        {
            return new AllHistoryRoundRecord
            {
                CreatedAt = new DateTime(2026, 5, 29, 12, 0, 0, DateTimeKind.Local),
                AppVersion = "test",
                SchemaVersion = 1,
                TargetText = text,
                TextHash = PersonalScorePredictionSnapshot.ComputeTextHash(text),
                ArticleName = "test article",
                Source = "test",
                AttemptGroupKey = PersonalScorePredictionSnapshot.ComputeTextHash(text),
                AttemptIndex = attemptIndex,
                IsFirstAttempt = retypeType == RetypeType.first,
                RetypeReason = retypeType.ToString(),
                TotalWords = new System.Globalization.StringInfo(text).LengthInTextElements,
                InputWords = new System.Globalization.StringInfo(text).LengthInTextElements,
                TotalSeconds = 2.2,
                TotalHits = keyTimes.Count(),
                Speed = 120,
                HitRate = 4,
                Kpw = 2,
                Accuracy = 99,
                Wrong = 0,
                Backs = 0,
                Correction = 0,
                WasteCodes = 0,
                Choose = 0,
                DifficultyText = "普(1.00)",
                DifficultyScore = 1.0,
                CommitTexts = commits.ToArray(),
                CommitTimes = commitTimes.ToArray(),
                KeyTimes = keyTimes.ToArray()
            };
        }

        private static PersonalTypingRoundStats ToRoundStats(this AllHistoryRoundRecord record)
        {
            return new PersonalTypingRoundStats
            {
                TotalWords = record.TotalWords,
                TotalSeconds = record.TotalSeconds,
                TotalHits = record.TotalHits,
                Speed = record.Speed,
                HitRate = record.HitRate,
                Kpw = record.Kpw,
                Accuracy = record.Accuracy,
                Backs = record.Backs,
                Correction = record.Correction,
                WasteCodes = record.WasteCodes,
                Choose = record.Choose
            };
        }

        private static string TempDbPath()
        {
            return Path.Combine(Path.GetTempPath(), "all_history_" + Guid.NewGuid().ToString("N") + ".db");
        }

        private static void DeleteDb(string path)
        {
            TryDelete(path);
            TryDelete(path + "-wal");
            TryDelete(path + "-shm");
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
            }
        }

        private static void AssertTrue(string name, bool condition)
        {
            if (!condition)
                throw new Exception(name + " expected true.");
        }

        private static void AssertFalse(string name, bool condition)
        {
            if (condition)
                throw new Exception(name + " expected false.");
        }

        private static void AssertEqual(string name, string expected, string actual)
        {
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
                throw new Exception(name + " expected [" + expected + "], got [" + actual + "].");
        }

        private static void AssertEqual(string name, int expected, int actual)
        {
            if (expected != actual)
                throw new Exception(name + " expected [" + expected + "], got [" + actual + "].");
        }

        private static void AssertEqual(string name, double expected, double actual, double tolerance)
        {
            if (Math.Abs(expected - actual) > tolerance)
                throw new Exception(name + " expected [" + expected + "], got [" + actual + "].");
        }

        private static void AssertEqual(string name, long expected, long actual)
        {
            if (expected != actual)
                throw new Exception(name + " expected [" + expected + "], got [" + actual + "].");
        }
    }
}
