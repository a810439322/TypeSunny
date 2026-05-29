using System;
using System.Collections.Generic;
using System.Linq;
using TypeSunny.Personalization;

namespace TypeSunny.Tests
{
    internal static class PersonalScorePredictionTests
    {
        private static int Main()
        {
            try
            {
                QingDifficultyScaleUsesServerThresholds();
                SessionBuilderExcludesFirstThreeTargetCharacters();
                SessionBuilderDoesNotCreateUnitsLongerThanFourCharacters();
                SessionBuilderDoesNotTreatBiaoDingAsWordUnit();
                SessionBuilderSubtractsPausesLongerThanTenSeconds();
                ProfileMaturityUsesEffectiveStatCharacters();
                ProfileRemovesInvalidPunctuationUnitsDuringUpdate();
                UnitStatsBlendLongTermAndRecentTracks();
                ProfileKeepsAllObservedUnitsWithoutCompaction();
                PredictionServiceAppendsPredictionOutsideMainWindow();
                PredictionServiceLearnsDifficultySegmentFromSingleCharacterCommits();
                PredictionServiceUsesDifficultySegmentsWhenPredicting();
                PredictionServicePersistsCalibrationFromSnapshot();
                PredictorUsesLearnedUnitWhenItHasLowerPersonalCost();
                PredictorUsesDifficultySegmentsForUnlearnedWords();
                PredictorPrefersUserLearnedWordsOverDifficultySegments();
                PredictorIgnoresPunctuationPairUnits();
                PredictorFormatsWholeScorePrediction();
                CalibrationAdjustsFuturePredictionTowardActualScore();
                CalibrationWeightsLongRunsMoreThanShortRuns();
                CalibrationClampedSampleHasReducedWeight();
                CalibrationServiceSkipsBelowColdStartThreshold();
                CalibrationServiceSkipsWhenTextHashMismatches();
                CalibrateAndTrainAsyncRunsSequentiallyOffUiThread();
                PredictorIgnoresSingletonLearnedUnit();
                FormatterShowsLowConfidencePrediction();
                FormatterDefaultsConfidenceVisibleButNotForced();
                FormatterForcesOnlySpeedByDefault();
                FormatterRequiresConfidenceAboveEightyPercentForScoreAttachment();

                Console.WriteLine("All PersonalScorePrediction tests passed.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("FAIL: " + ex.Message);
                return 1;
            }
        }

        private static void QingDifficultyScaleUsesServerThresholds()
        {
            AssertEqual("0.09 label", "淼", QingDifficultyScale.GetLabel(0.09));
            AssertEqual("0.10 label", "水", QingDifficultyScale.GetLabel(0.10));
            AssertEqual("0.30 label", "易", QingDifficultyScale.GetLabel(0.30));
            AssertEqual("0.80 label", "普", QingDifficultyScale.GetLabel(0.80));
            AssertEqual("5.00 label", "难", QingDifficultyScale.GetLabel(5.00));
            AssertEqual("15.00 label", "虐", QingDifficultyScale.GetLabel(15.00));
            AssertEqual("formatted score", "难(5.00)", QingDifficultyScale.Format(5.0));
        }

        private static void SessionBuilderExcludesFirstThreeTargetCharacters()
        {
            var session = PersonalTypingSessionBuilder.Build(
                "一二三四五",
                new[] { "一", "二", "三", "四", "五" },
                new long[] { 1000, 2000, 3000, 4000, 5000 },
                new long[] { 500, 1500, 2500, 3500, 4500 });

            AssertEqual("effective stat chars", 2, session.EffectiveStatCharacters);
            AssertFalse("first char sample excluded", session.Samples.Any(s => s.Text == "一"));
            AssertTrue("fourth char sample included", session.Samples.Any(s => s.Text == "四"));
            AssertTrue("fifth char sample included", session.Samples.Any(s => s.Text == "五"));
        }

        private static void SessionBuilderDoesNotCreateUnitsLongerThanFourCharacters()
        {
            var session = PersonalTypingSessionBuilder.Build(
                "一二三四五六七八",
                new[] { "一二三", "四五六七八" },
                new long[] { 1000, 3000 },
                new long[] { 500, 2600 });

            AssertEqual("effective stat chars include post-warmup long commit", 5, session.EffectiveStatCharacters);
            AssertFalse("long unit excluded", session.Samples.Any(s => s.Text == "四五六七八"));
        }

        private static void SessionBuilderDoesNotTreatBiaoDingAsWordUnit()
        {
            var session = PersonalTypingSessionBuilder.Build(
                "一二三儿，四",
                new[] { "一", "二", "三", "儿，", "四" },
                new long[] { 1000, 2000, 3000, 4000, 5000 },
                new long[] { 800, 1800, 2800, 3600, 3800, 4800 });

            AssertFalse("biao ding pair excluded", session.Samples.Any(s => s.Text == "儿，"));
            AssertFalse("no punctuation unit", session.Samples.Any(s => s.Text.Contains("，")));
            AssertTrue("following single char still included", session.Samples.Any(s => s.Text == "四"));
        }

        private static void SessionBuilderSubtractsPausesLongerThanTenSeconds()
        {
            var session = PersonalTypingSessionBuilder.Build(
                "一二三四",
                new[] { "一二三", "四" },
                new long[] { 1000, 15000 },
                new long[] { 500, 14000 });

            var sample = session.Samples.Single(s => s.Text == "四");
            AssertEqual("pause-adjusted milliseconds", 1000.0, sample.EffectiveMilliseconds, 0.001);
        }

        private static void ProfileMaturityUsesEffectiveStatCharacters()
        {
            var profile = new PersonalTypingProfile();
            var session = new PersonalTypingSession();
            session.EffectiveStatCharacters = 8;
            session.Samples.Add(new PersonalTypingUnitSample("中国", 800, 4));
            session.Samples.Add(new PersonalTypingUnitSample("人", 400, 2));

            profile.Update(session, new PersonalTypingRoundStats
            {
                TotalWords = 11,
                TotalSeconds = 6,
                TotalHits = 30,
                Speed = 110,
                HitRate = 5,
                Kpw = 2.7,
                Accuracy = 99,
                Backs = 1,
                Correction = 0,
                WasteCodes = 0,
                Choose = 0
            });

            AssertEqual("global effective chars", 8, profile.EffectiveStatCharacters);
            AssertTrue("unit exists", profile.Units.ContainsKey("中国"));
            AssertEqual("unit observed chars", 2, profile.Units["中国"].ObservedCharacters);
        }

        private static void ProfileRemovesInvalidPunctuationUnitsDuringUpdate()
        {
            var profile = new PersonalTypingProfile();
            profile.Units["儿，"] = new PersonalTypingUnitStats("儿，")
            {
                Count = 1,
                ObservedCharacters = 2,
                TotalMilliseconds = 435,
                TotalKeys = 3
            };

            var session = new PersonalTypingSession { EffectiveStatCharacters = 1 };
            session.Samples.Add(new PersonalTypingUnitSample("四", 400, 1));

            profile.Update(session, new PersonalTypingRoundStats
            {
                TotalWords = 1,
                TotalSeconds = 1,
                TotalHits = 1,
                Speed = 60,
                HitRate = 1,
                Kpw = 1,
                Accuracy = 99
            });

            AssertFalse("invalid punctuation unit removed", profile.Units.ContainsKey("儿，"));
            AssertTrue("valid unit retained", profile.Units.ContainsKey("四"));
        }

        private static void UnitStatsBlendLongTermAndRecentTracks()
        {
            var stats = new PersonalTypingUnitStats("中国");
            for (int i = 0; i < 20; i++)
                stats.Add(new PersonalTypingUnitSample("中国", 1000, 8));
            stats.Add(new PersonalTypingUnitSample("中国", 100, 4));

            AssertTrue("recent track should react to latest sample", stats.RecentAverageMilliseconds < stats.LongTermAverageMilliseconds);
            AssertTrue("prediction should not be dominated by one latest sample", stats.AverageMilliseconds > 800);
            AssertTrue("prediction should still move below old long-term value", stats.AverageMilliseconds < 1000);
            AssertTrue("recent keys should move faster than long-term keys", stats.RecentAverageKeys < stats.LongTermAverageKeys);
        }

        private static void ProfileKeepsAllObservedUnitsWithoutCompaction()
        {
            var profile = new PersonalTypingProfile();
            var session = new PersonalTypingSession { EffectiveStatCharacters = 50001 };
            int codePoint = 0x4E00;
            string firstUnit = null;
            string lastUnit = null;

            while (session.Samples.Count < 50001)
            {
                if (codePoint >= 0xD800 && codePoint <= 0xDFFF)
                {
                    codePoint = 0xE000;
                    continue;
                }

                string unit = char.ConvertFromUtf32(codePoint);
                firstUnit = firstUnit ?? unit;
                lastUnit = unit;
                session.Samples.Add(new PersonalTypingUnitSample(unit, 500, 2));
                codePoint++;
            }

            profile.Update(session, new PersonalTypingRoundStats
            {
                TotalWords = 50001,
                TotalSeconds = 600,
                TotalHits = 100002,
                Speed = 5000,
                HitRate = 8,
                Kpw = 2,
                Accuracy = 99
            });

            AssertEqual("all units retained", 50001, profile.Units.Count);
            AssertTrue("first one-off unit retained", profile.Units.ContainsKey(firstUnit));
            AssertTrue("last one-off unit retained", profile.Units.ContainsKey(lastUnit));
        }

        private static void PredictionServiceAppendsPredictionOutsideMainWindow()
        {
            string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
            PersonalTypingProfileStore store = null;
            try
            {
                store = new PersonalTypingProfileStore(tempPath);
                store.Save(CreatePredictionProfile());
                var service = new PersonalScorePredictionService(store, text => 2.0);

                string displayText = service.AppendPrediction("中国人", "");

                // 数字会随贝叶斯收缩等算法常数微调而变化，这里只断言形状
                AssertTrue("service appends predicted speed", System.Text.RegularExpressions.Regex.IsMatch(displayText, "预测速度\\d+\\.\\d{2}"));
                AssertTrue("service appends personal difficulty", System.Text.RegularExpressions.Regex.IsMatch(displayText, "个难[淼水易普难虐]\\(\\d+\\.\\d{2}\\)"));
            }
            finally
            {
                if (store != null) store.Dispose();
                DeleteTempStoreFiles(tempPath);
            }
        }

        private static void PredictionServiceLearnsDifficultySegmentFromSingleCharacterCommits()
        {
            string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
            PersonalTypingProfileStore store = null;
            try
            {
                store = new PersonalTypingProfileStore(tempPath);
                var service = new PersonalScorePredictionService(
                    store,
                    text => 2.0,
                    text => new[] { "中国" });

                service.Train(
                    "一二三中国人",
                    new[] { "一", "二", "三", "中", "国", "人" },
                    new long[] { 1000, 2000, 3000, 4000, 5000, 6000 },
                    new long[] { 800, 1800, 2800, 3800, 4800, 5800 },
                    new PersonalTypingRoundStats
                    {
                        TotalWords = 6,
                        TotalSeconds = 6,
                        TotalHits = 6,
                        Speed = 60,
                        HitRate = 1,
                        Kpw = 1,
                        Accuracy = 99
                    });

                PersonalTypingProfile profile = store.Load();

                AssertTrue("difficulty segment learned", profile.Units.ContainsKey("中国"));
                AssertTrue("single character still learned", profile.Units.ContainsKey("中"));
                AssertFalse("punctuation-free segment only", profile.Units.Keys.Any(k => k.Contains("，")));
            }
            finally
            {
                if (store != null) store.Dispose();
                DeleteTempStoreFiles(tempPath);
            }
        }

        private static void PredictionServiceUsesDifficultySegmentsWhenPredicting()
        {
            string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
            PersonalTypingProfileStore store = null;
            try
            {
                var profile = new PersonalTypingProfile();
                profile.EffectiveStatCharacters = 1000;
                profile.BaselineSpeed = 120;
                profile.BaselineKpw = 2;

                store = new PersonalTypingProfileStore(tempPath);
                store.Save(profile);
                var service = new PersonalScorePredictionService(
                    store,
                    text => 2.0,
                    text => new[] { "中国", "人" });

                PersonalScorePrediction prediction = service.Predict("中国人", "");
                PersonalScorePredictionSnapshot snapshot = service.CreateSnapshot("中国人", "");

                AssertEqual("service fallback segment count", 2, prediction.Units.Count);
                AssertEqual("service fallback first segment", "中国", prediction.Units[0]);
                AssertEqual("snapshot fallback segment count", 2, snapshot.Units.Count);
                AssertEqual("snapshot fallback first segment", "中国", snapshot.Units[0]);
            }
            finally
            {
                if (store != null) store.Dispose();
                DeleteTempStoreFiles(tempPath);
            }
        }

        private static void PredictionServicePersistsCalibrationFromSnapshot()
        {
            string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
            PersonalTypingProfileStore store = null;
            try
            {
                store = new PersonalTypingProfileStore(tempPath);
                store.Save(CreatePredictionProfile());
                var service = new PersonalScorePredictionService(store, text => 2.0);

                PersonalScorePredictionSnapshot snapshot = service.CreateSnapshot("中国人", "");
                service.Calibrate(snapshot, new PersonalTypingRoundStats
                {
                    TotalWords = 3,
                    TotalSeconds = 1.5,
                    TotalHits = 9,
                    Speed = 120,
                    HitRate = 6,
                    Kpw = 3,
                    Accuracy = 98
                });

                PersonalScorePrediction calibrated = service.Predict("中国人", "");

                AssertTrue("service persisted calibrated seconds", calibrated.PredictedSeconds > snapshot.PredictedSeconds);
                AssertTrue("service persisted calibrated hits", calibrated.PredictedTotalHits > snapshot.PredictedTotalHits);
            }
            finally
            {
                if (store != null) store.Dispose();
                DeleteTempStoreFiles(tempPath);
            }
        }

        private static void PredictorUsesLearnedUnitWhenItHasLowerPersonalCost()
        {
            var profile = CreatePredictionProfile();

            var prediction = PersonalScorePredictor.Predict("中国人", profile, 2.0);

            AssertEqual("first predicted unit", "中国", prediction.Units[0]);
            AssertEqual("second predicted unit", "人", prediction.Units[1]);
            // 贝叶斯收缩后：keys = (4*10+8*3)/13 + (2*10+4*3)/13 ≈ 4.92 + 2.46 ≈ 7.38
            //                seconds = ((600*10+1000*3) + (400*10+500*3)) / 13 / 1000 ≈ 1.115
            AssertEqual("predicted hits", 7.38, prediction.PredictedTotalHits, 0.05);
            AssertEqual("predicted seconds", 1.115, prediction.PredictedSeconds, 0.01);
        }

        private static void PredictorUsesDifficultySegmentsForUnlearnedWords()
        {
            var profile = new PersonalTypingProfile();
            profile.EffectiveStatCharacters = 1000;
            profile.BaselineSpeed = 120;
            profile.BaselineKpw = 2;

            var prediction = PersonalScorePredictor.Predict("中国人", profile, 2.0, new[] { "中国", "人" });

            AssertEqual("fallback segment count", 2, prediction.Units.Count);
            AssertEqual("fallback first segment", "中国", prediction.Units[0]);
            AssertEqual("fallback second segment", "人", prediction.Units[1]);
        }

        private static void PredictorPrefersUserLearnedWordsOverDifficultySegments()
        {
            var profile = new PersonalTypingProfile();
            profile.EffectiveStatCharacters = 1000;
            profile.BaselineSpeed = 120;
            profile.BaselineKpw = 2;
            profile.Units["中国人"] = new PersonalTypingUnitStats("中国人")
            {
                Count = 10,
                ObservedCharacters = 30,
                TotalMilliseconds = 30000,
                TotalKeys = 6
            };

            var prediction = PersonalScorePredictor.Predict("中国人", profile, 2.0, new[] { "中国", "人" });

            AssertEqual("user learned word wins", 1, prediction.Units.Count);
            AssertEqual("user learned segment", "中国人", prediction.Units[0]);
        }

        private static void PredictorIgnoresPunctuationPairUnits()
        {
            var profile = CreatePredictionProfile();
            profile.Units["儿，"] = new PersonalTypingUnitStats("儿，")
            {
                Count = 10,
                ObservedCharacters = 20,
                TotalMilliseconds = 10,
                TotalKeys = 10
            };

            var prediction = PersonalScorePredictor.Predict("儿，", profile, 2.0);

            AssertFalse("punctuation pair is not used as a unit", prediction.Units.Contains("儿，"));
        }

        private static void PredictorFormatsWholeScorePrediction()
        {
            var profile = CreatePredictionProfile();

            var prediction = PersonalScorePredictor.Predict("中国人", profile, 2.0);

            // 数值精确到贝叶斯收缩公式：
            // GetCost("中国",2): (600*10 + 1000*3)/13 ≈ 692.31 ms, (4*10 + 8*3)/13 ≈ 4.92 keys
            // GetCost("人", 1): (400*10 + 500*3)/13 ≈ 423.08 ms, (2*10 + 4*3)/13 ≈ 2.46 keys
            // 总 ms ≈ 1115.38 ⇒ Speed = 3/(1.1154/60) ≈ 161.38, kpw ≈ 2.46, hitRate ≈ 6.62
            // PersonalDifficulty = 2 × 120/161.38 ≈ 1.49
            AssertEqual("predicted speed", 161.38, prediction.PredictedSpeed, 0.1);
            AssertEqual("predicted kpw", 2.46, prediction.PredictedKpw, 0.05);
            AssertEqual("predicted hit rate", 6.62, prediction.PredictedHitRate, 0.05);
            AssertEqual("personal difficulty score", 1.49, prediction.PersonalDifficultyScore, 0.02);
            AssertTrue("formatted text has speed", prediction.FormatScoreLine().Contains("预测速度161"));
            AssertTrue("formatted text has difficulty", prediction.FormatScoreLine().Contains("个难普("));
        }

        private static void CalibrationAdjustsFuturePredictionTowardActualScore()
        {
            var profile = CreatePredictionProfile();
            var prediction = PersonalScorePredictor.Predict("中国人", profile, 2.0);
            var snapshot = PersonalScorePredictionSnapshot.FromPrediction("中国人", 2.0, prediction);

            profile.UpdateCalibration(snapshot, new PersonalTypingRoundStats
            {
                TotalWords = 3,
                TotalSeconds = 1.5,
                TotalHits = 9,
                Speed = 120,
                HitRate = 6,
                Kpw = 3,
                Accuracy = 98
            });

            var calibrated = PersonalScorePredictor.Predict("中国人", profile, 2.0);

            AssertTrue("calibrated seconds moves toward actual", calibrated.PredictedSeconds > prediction.PredictedSeconds);
            AssertTrue("calibrated hits moves toward actual", calibrated.PredictedTotalHits > prediction.PredictedTotalHits);
            AssertTrue("calibrated speed moves toward actual", calibrated.PredictedSpeed < prediction.PredictedSpeed);
            AssertTrue("calibration keeps confidence", calibrated.Confidence >= prediction.Confidence);
        }

        private static void CalibrationWeightsLongRunsMoreThanShortRuns()
        {
            // 关键场景：两条样本，ratio 不同，混到同一个 Calibration 里。
            // - 短局：chars=10，ratio=1.0 (actual 与 predicted 等)
            // - 长局：chars=1000，ratio=2.0 (actual 是 predicted 的 2 倍)
            // 旧实现（按"轮"等权）TimeFactor ≈ (1 + 2) / 2 = 1.5
            // 新实现（按字数加权）TimeFactor ≈ (1*10 + 2*1000) / (10 + 1000) ≈ 1.99
            var shortRun = NewSnapshotWith(chars: 10, predictedSeconds: 10, predictedHits: 100);
            var longRun = NewSnapshotWith(chars: 1000, predictedSeconds: 1000, predictedHits: 10000);

            var cal = new PersonalPredictionCalibration();
            cal.Add(shortRun, new PersonalTypingRoundStats { TotalWords = 10, TotalSeconds = 10, TotalHits = 100 }, 10);
            cal.Add(longRun, new PersonalTypingRoundStats { TotalWords = 1000, TotalSeconds = 2000, TotalHits = 20000 }, 1000);

            // 长局应主导：TimeFactor 接近 2.0 而不是 (1+2)/2=1.5
            AssertTrue("long run dominates TimeFactor",
                cal.TimeFactor > 1.8 && cal.TimeFactor <= 2.0);
        }

        private static void CalibrationClampedSampleHasReducedWeight()
        {
            // 一条正常样本 (ratio=1.5, chars=100) 后跟一条极端样本 (ratio 钳到 4, chars=100):
            // - 正常样本权重 = 100
            // - 极端样本权重 = 100 * 0.25 = 25 (ClampedSampleWeightFactor 折扣)
            // → 加权平均 ≈ (1.5*100 + 4*25) / (100+25) ≈ 2.0
            // 如果没有折扣，会 = (1.5+4)/2 = 2.75（按等权）或 (1.5*100+4*100)/200 = 2.75（按字数等权）
            var normalRun = NewSnapshotWith(chars: 100, predictedSeconds: 100, predictedHits: 500);
            var extremeRun = NewSnapshotWith(chars: 100, predictedSeconds: 100, predictedHits: 500);

            var cal = new PersonalPredictionCalibration();
            cal.Add(normalRun, new PersonalTypingRoundStats { TotalWords = 100, TotalSeconds = 150, TotalHits = 750 }, 100);
            cal.Add(extremeRun, new PersonalTypingRoundStats { TotalWords = 100, TotalSeconds = 1000, TotalHits = 5000 }, 100); // ratio=10 钳到 4

            // 钳过的极端样本权重被压制 → TimeFactor 不会跳到接近 4，而是被正常样本拉住
            AssertTrue("clamped sample's weight is reduced relative to unclamped",
                cal.TimeFactor < 2.5 && cal.TimeFactor > 1.5);
        }

        private static void CalibrationServiceSkipsBelowColdStartThreshold()
        {
            string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
            PersonalTypingProfileStore store = null;
            try
            {
                // 故意做一个 EffectiveStatCharacters 远低于 200 阈值的 profile
                var profile = new PersonalTypingProfile
                {
                    EffectiveStatCharacters = 50,
                    BaselineSpeed = 100,
                    BaselineKpw = 2
                };
                store = new PersonalTypingProfileStore(tempPath);
                store.Save(profile);

                var service = new PersonalScorePredictionService(store, text => 2.0);
                var snapshot = NewSnapshotWith(chars: 5, predictedSeconds: 5, predictedHits: 20);

                service.Calibrate(snapshot, new PersonalTypingRoundStats
                {
                    TotalWords = 5,
                    TotalSeconds = 30,   // 6x predicted —— 如果不门控会被钳到 4 并污染 factor
                    TotalHits = 200,
                });

                PersonalTypingProfile reloaded = store.Load();
                AssertEqual("cold start skips calibration count", 0, reloaded.Calibration.Count);
                AssertTrue("cold start preserves default TimeFactor", reloaded.Calibration.TimeFactor == 1.0);
            }
            finally
            {
                if (store != null) store.Dispose();
                DeleteTempStoreFiles(tempPath);
            }
        }

        private static void CalibrationServiceSkipsWhenTextHashMismatches()
        {
            string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
            PersonalTypingProfileStore store = null;
            try
            {
                store = new PersonalTypingProfileStore(tempPath);
                store.Save(CreatePredictionProfile()); // EffectiveStatCharacters=20000 已过冷启动门控

                var service = new PersonalScorePredictionService(store, text => 2.0);

                // 拍一个 "中国人" 的 snapshot
                var prediction = PersonalScorePredictor.Predict("中国人", CreatePredictionProfile(), 2.0);
                var snapshot = PersonalScorePredictionSnapshot.FromPrediction("中国人", 2.0, prediction);

                // 用 "完全不同的文章" 的 hash 调用 Calibrate —— 应该被拒绝
                service.Calibrate(snapshot,
                    new PersonalTypingRoundStats
                    {
                        TotalWords = 100,
                        TotalSeconds = 50,
                        TotalHits = 600,
                    },
                    expectedTextHash: PersonalScorePredictionSnapshot.ComputeTextHash("完全不同的文章"));

                PersonalTypingProfile reloaded = store.Load();
                AssertEqual("mismatched hash skips calibration", 0, reloaded.Calibration.Count);

                // 用正确 hash 再调一次，应该被接受
                service.Calibrate(snapshot,
                    new PersonalTypingRoundStats
                    {
                        TotalWords = 3,
                        TotalSeconds = 2,
                        TotalHits = 10,
                    },
                    expectedTextHash: PersonalScorePredictionSnapshot.ComputeTextHash("中国人"));

                reloaded = store.Load();
                AssertEqual("matched hash accepts calibration", 1, reloaded.Calibration.Count);
            }
            finally
            {
                if (store != null) store.Dispose();
                DeleteTempStoreFiles(tempPath);
            }
        }

        private static void CalibrateAndTrainAsyncRunsSequentiallyOffUiThread()
        {
            string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
            PersonalTypingProfileStore store = null;
            try
            {
                store = new PersonalTypingProfileStore(tempPath);
                store.Save(CreatePredictionProfile());
                var service = new PersonalScorePredictionService(store, text => 2.0);

                var prediction = PersonalScorePredictor.Predict("中国人", CreatePredictionProfile(), 2.0);
                var snapshot = PersonalScorePredictionSnapshot.FromPrediction("中国人", 2.0, prediction);

                // 同时调两次，验证 FIFO 串行（第二次必须等第一次写完）
                System.Threading.Tasks.Task t1 = service.CalibrateAndTrainAsync(
                    snapshot,
                    new PersonalTypingRoundStats { TotalWords = 3, TotalSeconds = 2, TotalHits = 10, Speed = 90, HitRate = 5, Kpw = 3, Accuracy = 98 },
                    PersonalScorePredictionSnapshot.ComputeTextHash("中国人"),
                    "中国人",
                    new[] { "中", "国", "人" },
                    new long[] { 1000, 1500, 2000 },
                    new long[] { 900, 1400, 1900 });

                System.Threading.Tasks.Task t2 = service.CalibrateAndTrainAsync(
                    snapshot,
                    new PersonalTypingRoundStats { TotalWords = 3, TotalSeconds = 2.5, TotalHits = 11, Speed = 80, HitRate = 4.4, Kpw = 3.67, Accuracy = 97 },
                    PersonalScorePredictionSnapshot.ComputeTextHash("中国人"),
                    "中国人",
                    new[] { "中", "国", "人" },
                    new long[] { 3000, 3500, 4000 },
                    new long[] { 2900, 3400, 3900 });

                // 调用立即返回（即便 SQLite 还没写完）
                AssertTrue("returns tasks without blocking", t1 != null && t2 != null);

                // 等齐两个任务
                System.Threading.Tasks.Task.WaitAll(new[] { t1, t2 }, 10000);
                AssertTrue("t1 completes", t1.IsCompleted);
                AssertTrue("t2 completes", t2.IsCompleted);

                PersonalTypingProfile reloaded = store.Load();
                // 两次 Calibrate 都生效，Count = 2
                AssertEqual("two calibrations applied in sequence", 2, reloaded.Calibration.Count);
                // Train 也跑过，至少保留原 4 个 unit
                AssertTrue("train applied", reloaded.Units.Count >= 4);
            }
            finally
            {
                if (store != null) store.Dispose();
                DeleteTempStoreFiles(tempPath);
            }
        }

        private static void PredictorIgnoresSingletonLearnedUnit()
        {
            // 一个 unit 仅打过 1 次的样本不算"已学过"——不应让 DP 优先把它拆成多字段
            var profile = new PersonalTypingProfile();
            profile.EffectiveStatCharacters = 1000;
            profile.BaselineSpeed = 120;
            profile.BaselineKpw = 4;
            profile.Units["中国"] = new PersonalTypingUnitStats("中国")
            {
                Count = 1,
                ObservedCharacters = 2,
                TotalMilliseconds = 100,  // 一个超快异常样本
                TotalKeys = 4
            };

            var prediction = PersonalScorePredictor.Predict("中国人", profile, 2.0);

            // 单次样本不让 "中国" 进 DP 多字候选 —— Units 应该是 [中, 国, 人] 而非 [中国, 人]
            AssertEqual("singleton learned unit not used for multi-char segmentation",
                3, prediction.Units.Count);
        }

        private static void FormatterShowsLowConfidencePrediction()
        {
            var prediction = new PersonalScorePrediction
            {
                Units = new List<string> { "中" },
                PredictedSeconds = 1,
                PredictedTotalHits = 4,
                PredictedSpeed = 60,
                PredictedHitRate = 4,
                PredictedKpw = 4,
                PersonalDifficultyScore = 1,
                Confidence = 0.10
            };

            string formatted = PersonalScorePredictionFormatter.Format(
                prediction,
                PersonalScorePredictionFormatter.DefaultOrder,
                _ => true);

            AssertTrue("low confidence still shows speed", formatted.Contains("预测速度60.00"));
            AssertTrue("low confidence still shows confidence", formatted.Contains("置信10%"));
        }

        private static PersonalScorePredictionSnapshot NewSnapshotWith(int chars, double predictedSeconds, double predictedHits)
        {
            return new PersonalScorePredictionSnapshot
            {
                TargetCharacters = chars,
                PredictedSeconds = predictedSeconds,
                PredictedTotalHits = predictedHits,
                PredictedSpeed = chars / (predictedSeconds / 60.0),
                PredictedHitRate = predictedHits / predictedSeconds,
                PredictedKpw = predictedHits / chars,
                PredictedPersonalDifficultyScore = 1.0,
                Confidence = 1.0
            };
        }

        private static void FormatterForcesOnlySpeedByDefault()
        {
            var prediction = PersonalScorePredictor.Predict("中国人", CreatePredictionProfile(), 2.0);
            string formatted = PersonalScorePredictionFormatter.Format(
                prediction,
                PersonalScorePredictionFormatter.DefaultOrder,
                item => false);

            AssertTrue("forced speed", formatted.Contains("预测速度161"));
            AssertFalse("difficulty can be hidden by user", formatted.Contains("个难"));
            AssertFalse("difficulty is not forced", PersonalScorePredictionFormatter.IsForceShowItem("难度"));
            AssertFalse("optional time hidden", formatted.Contains("用时"));
            AssertFalse("optional confidence hidden", formatted.Contains("置信"));
        }

        private static void FormatterDefaultsConfidenceVisibleButNotForced()
        {
            AssertTrue("confidence default visible", PersonalScorePredictionFormatter.IsDefaultVisible("置信"));
            AssertFalse("confidence can be hidden by user", PersonalScorePredictionFormatter.IsForceShowItem("置信"));
        }

        private static void FormatterRequiresConfidenceAboveEightyPercentForScoreAttachment()
        {
            var prediction = PersonalScorePredictor.Predict("中国人", CreatePredictionProfile(), 2.0);
            prediction.Confidence = 0.80;
            AssertFalse("80 percent is not enough", PersonalScorePredictionFormatter.CanAttachToScore(prediction));

            prediction.Confidence = 0.81;
            AssertTrue("above 80 percent can attach", PersonalScorePredictionFormatter.CanAttachToScore(prediction));
        }

        private static PersonalTypingProfile CreatePredictionProfile()
        {
            var profile = new PersonalTypingProfile();
            profile.EffectiveStatCharacters = 20000;
            profile.BaselineSpeed = 120;
            profile.BaselineAccuracy = 98.5;
            profile.BaselineBacksPerChar = 0.01;
            profile.BaselineCorrectionPerChar = 0.005;
            profile.BaselineWasteCodesPerChar = 0.002;
            profile.BaselineChoosePerChar = 0.003;

            profile.Units["中国"] = new PersonalTypingUnitStats("中国")
            {
                Count = 10,
                ObservedCharacters = 20,
                TotalMilliseconds = 6000,
                TotalKeys = 40
            };
            profile.Units["中"] = new PersonalTypingUnitStats("中")
            {
                Count = 10,
                ObservedCharacters = 10,
                TotalMilliseconds = 9000,
                TotalKeys = 30
            };
            profile.Units["国"] = new PersonalTypingUnitStats("国")
            {
                Count = 10,
                ObservedCharacters = 10,
                TotalMilliseconds = 9000,
                TotalKeys = 30
            };
            profile.Units["人"] = new PersonalTypingUnitStats("人")
            {
                Count = 10,
                ObservedCharacters = 10,
                TotalMilliseconds = 4000,
                TotalKeys = 20
            };
            return profile;
        }

        /// <summary>
        /// 清理一组测试 store 文件。SQLite 化后单个 "<c>xxx.json</c>" tempPath 实际对应
        /// "<c>xxx.db</c>" + 可能的 WAL/SHM 伴随文件 "<c>xxx.db-wal</c>"/"<c>xxx.db-shm</c>"。
        /// </summary>
        private static void DeleteTempStoreFiles(string tempPath)
        {
            if (string.IsNullOrEmpty(tempPath))
                return;

            string dbPath = PersonalTypingProfileStore.NormalizePathToDb(tempPath);
            TryDelete(tempPath); // 老 .json 路径，若 store 没创建也无副作用
            TryDelete(dbPath);
            TryDelete(dbPath + "-wal");
            TryDelete(dbPath + "-shm");
            TryDelete(dbPath + ".migrating");
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (System.IO.File.Exists(path))
                    System.IO.File.Delete(path);
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
    }
}
