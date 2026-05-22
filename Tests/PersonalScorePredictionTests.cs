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
                FormatterDefaultsConfidenceVisibleButNotForced();
                FormatterForcesSpeedAndDifficultyOnlyByDefault();
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
            try
            {
                var store = new PersonalTypingProfileStore(tempPath);
                store.Save(CreatePredictionProfile());
                var service = new PersonalScorePredictionService(store, text => 2.0);

                string displayText = service.AppendPrediction("中国人", "");

                AssertTrue("service appends predicted speed", displayText.Contains("预测速度180.00"));
                AssertTrue("service appends personal difficulty", displayText.Contains("个难普(1.33)"));
            }
            finally
            {
                if (System.IO.File.Exists(tempPath))
                    System.IO.File.Delete(tempPath);
            }
        }

        private static void PredictionServiceLearnsDifficultySegmentFromSingleCharacterCommits()
        {
            string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
            try
            {
                var store = new PersonalTypingProfileStore(tempPath);
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
                if (System.IO.File.Exists(tempPath))
                    System.IO.File.Delete(tempPath);
            }
        }

        private static void PredictionServiceUsesDifficultySegmentsWhenPredicting()
        {
            string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
            try
            {
                var profile = new PersonalTypingProfile();
                profile.EffectiveStatCharacters = 1000;
                profile.BaselineSpeed = 120;
                profile.BaselineKpw = 2;

                var store = new PersonalTypingProfileStore(tempPath);
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
                if (System.IO.File.Exists(tempPath))
                    System.IO.File.Delete(tempPath);
            }
        }

        private static void PredictionServicePersistsCalibrationFromSnapshot()
        {
            string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
            try
            {
                var store = new PersonalTypingProfileStore(tempPath);
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
                if (System.IO.File.Exists(tempPath))
                    System.IO.File.Delete(tempPath);
            }
        }

        private static void PredictorUsesLearnedUnitWhenItHasLowerPersonalCost()
        {
            var profile = CreatePredictionProfile();

            var prediction = PersonalScorePredictor.Predict("中国人", profile, 2.0);

            AssertEqual("first predicted unit", "中国", prediction.Units[0]);
            AssertEqual("second predicted unit", "人", prediction.Units[1]);
            AssertEqual("predicted hits", 6.0, prediction.PredictedTotalHits, 0.001);
            AssertEqual("predicted seconds", 1.0, prediction.PredictedSeconds, 0.001);
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

            AssertEqual("predicted speed", 180.0, prediction.PredictedSpeed, 0.001);
            AssertEqual("predicted kpw", 2.0, prediction.PredictedKpw, 0.001);
            AssertEqual("predicted hit rate", 6.0, prediction.PredictedHitRate, 0.001);
            AssertEqual("personal difficulty score", 1.33, prediction.PersonalDifficultyScore, 0.01);
            AssertTrue("formatted text has speed", prediction.FormatScoreLine().Contains("预测速度180.00"));
            AssertTrue("formatted text has difficulty", prediction.FormatScoreLine().Contains("个难普(1.33)"));
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

        private static void FormatterForcesSpeedAndDifficultyOnlyByDefault()
        {
            var prediction = PersonalScorePredictor.Predict("中国人", CreatePredictionProfile(), 2.0);
            string formatted = PersonalScorePredictionFormatter.Format(
                prediction,
                PersonalScorePredictionFormatter.DefaultOrder,
                item => false);

            AssertTrue("forced speed", formatted.Contains("预测速度180.00"));
            AssertTrue("forced difficulty", formatted.Contains("个难普(1.33)"));
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
