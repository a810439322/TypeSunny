using System;
using System.Collections.Generic;
using System.Globalization;

namespace TypeSunny.Personalization
{
    internal sealed class PersonalScorePrediction
    {
        public List<string> Units { get; set; }
        public double PredictedSeconds { get; set; }
        public double PredictedTotalHits { get; set; }
        public double PredictedSpeed { get; set; }
        public double PredictedHitRate { get; set; }
        public double PredictedKpw { get; set; }
        public double PersonalDifficultyScore { get; set; }
        public double Confidence { get; set; }

        public PersonalScorePrediction()
        {
            Units = new List<string>();
        }

        public string FormatScoreLine()
        {
            return PersonalScorePredictionFormatter.Format(
                this,
                PersonalScorePredictionFormatter.DefaultOrder,
                _ => true);
        }
    }

    internal static class PersonalScorePredictor
    {
        private const int MaxUnitCharacters = 4;

        private sealed class Node
        {
            public double Milliseconds;
            public double Keys;
            public int Prev;
            public string Unit;
            public int MatureCharacters;
            public int PreferenceScore;
        }

        public static PersonalScorePrediction Predict(string text, PersonalTypingProfile profile, double baseDifficultyScore)
        {
            return Predict(text, profile, baseDifficultyScore, null);
        }

        public static PersonalScorePrediction Predict(
            string text,
            PersonalTypingProfile profile,
            double baseDifficultyScore,
            IEnumerable<string> fallbackSegments)
        {
            if (profile == null)
                profile = new PersonalTypingProfile();

            string[] chars = SplitTextElements(text);
            int n = chars.Length;
            HashSet<string> fallbackSegmentKeys = BuildFallbackSegmentKeys(chars, fallbackSegments);
            Node[] dp = new Node[n + 1];
            dp[0] = new Node { Milliseconds = 0, Keys = 0, Prev = -1, Unit = "", MatureCharacters = 0, PreferenceScore = 0 };

            for (int i = 0; i < n; i++)
            {
                if (dp[i] == null)
                    continue;

                int maxLen = Math.Min(MaxUnitCharacters, n - i);
                for (int len = 1; len <= maxLen; len++)
                {
                    string unit = Concat(chars, i, len);
                    if (len > 1 && !IsPureChineseUnit(unit))
                        continue;

                    bool learnedUnit = HasLearnedUnit(unit, profile);
                    bool fallbackUnit = len > 1 && fallbackSegmentKeys.Contains(MakeSegmentKey(i, i + len, unit));
                    if (len > 1 && !learnedUnit && !fallbackUnit)
                        continue;

                    CandidateCost cost = GetCost(unit, len, profile);
                    double candidateMs = dp[i].Milliseconds + cost.Milliseconds;
                    int end = i + len;
                    int preferenceScore = dp[i].PreferenceScore + GetSegmentationPreference(len, learnedUnit, fallbackUnit);
                    if (ShouldReplace(dp[end], candidateMs, preferenceScore))
                    {
                        dp[end] = new Node
                        {
                            Milliseconds = candidateMs,
                            Keys = dp[i].Keys + cost.Keys,
                            Prev = i,
                            Unit = unit,
                            MatureCharacters = dp[i].MatureCharacters + (cost.IsMature ? len : 0),
                            PreferenceScore = preferenceScore
                        };
                    }
                }
            }

            var prediction = new PersonalScorePrediction();
            if (n == 0 || dp[n] == null || dp[n].Milliseconds <= 0)
                return prediction;

            var units = new List<string>();
            int pos = n;
            while (pos > 0)
            {
                Node node = dp[pos];
                units.Add(node.Unit);
                pos = node.Prev;
            }
            units.Reverse();

            PersonalPredictionCalibration calibration = profile.Calibration ?? new PersonalPredictionCalibration();

            prediction.Units = units;
            prediction.PredictedSeconds = calibration.ApplySeconds(dp[n].Milliseconds / 1000.0);
            prediction.PredictedTotalHits = calibration.ApplyKeys(dp[n].Keys);
            prediction.PredictedSpeed = n / (prediction.PredictedSeconds / 60.0);
            prediction.PredictedHitRate = prediction.PredictedTotalHits / prediction.PredictedSeconds;
            prediction.PredictedKpw = prediction.PredictedTotalHits / n;
            prediction.Confidence = n > 0 ? (double)dp[n].MatureCharacters / n : 0;

            double baselineSpeed = profile.BaselineSpeed > 0 ? profile.BaselineSpeed : prediction.PredictedSpeed;
            prediction.PersonalDifficultyScore = Math.Round(baseDifficultyScore * baselineSpeed / prediction.PredictedSpeed, 2);
            return prediction;
        }

        private static bool ShouldReplace(Node current, double candidateMs, int candidatePreferenceScore)
        {
            if (current == null)
                return true;
            if (candidatePreferenceScore > current.PreferenceScore)
                return true;
            if (candidatePreferenceScore < current.PreferenceScore)
                return false;
            if (candidateMs < current.Milliseconds)
                return true;
            return false;
        }

        private static int GetSegmentationPreference(int charCount, bool learnedUnit, bool fallbackUnit)
        {
            if (charCount <= 1)
                return 0;
            if (learnedUnit)
                return charCount * 2;
            if (fallbackUnit)
                return charCount;
            return 0;
        }

        private sealed class CandidateCost
        {
            public double Milliseconds;
            public double Keys;
            public bool IsMature;
        }

        /// <summary>
        /// 贝叶斯收缩"先验权重"：count=1 时 (1/(1+K)) 真实 + (K/(1+K)) 基线。
        /// K=3 让单次样本只贡献 25%，count=3 时 50/50，count=10 时真实占 77%。
        /// 目的：防止偶然一次卡顿/走神的样本完全主导 DP。
        /// </summary>
        private const double LearnedUnitPriorWeight = 3.0;

        private static CandidateCost GetCost(string unit, int charCount, PersonalTypingProfile profile)
        {
            double baselineSpeed = profile.BaselineSpeed > 0 ? profile.BaselineSpeed : 120;
            double baselineKpw = profile.BaselineKpw > 0 ? profile.BaselineKpw : 4;
            double baselineMsForUnit = charCount * 60000.0 / baselineSpeed;
            double baselineKeysForUnit = charCount * baselineKpw;

            PersonalTypingUnitStats stats;
            if (profile.Units != null && profile.Units.TryGetValue(unit, out stats) && stats.Count > 0)
            {
                // 贝叶斯收缩：用 (真实 * count + 基线 * K) / (count + K) 平滑
                double count = stats.Count;
                double denom = count + LearnedUnitPriorWeight;
                double smoothedMs = (stats.AverageMilliseconds * count + baselineMsForUnit * LearnedUnitPriorWeight) / denom;
                double smoothedKeys = (stats.AverageKeys * count + baselineKeysForUnit * LearnedUnitPriorWeight) / denom;
                return new CandidateCost
                {
                    Milliseconds = Math.Max(1, smoothedMs),
                    Keys = Math.Max(1, smoothedKeys),
                    IsMature = stats.Count >= 3 || stats.ObservedCharacters >= 8
                };
            }

            return new CandidateCost
            {
                Milliseconds = Math.Max(1, baselineMsForUnit),
                Keys = Math.Max(1, baselineKeysForUnit),
                IsMature = false
            };
        }

        /// <summary>
        /// 是否将该 unit 加入 DP 多字段候选。要求 <c>Count &gt;= 2</c> 才"已学过"——
        /// 单次样本太容易是噪声（卡顿/走神），不让它直接主导切分偏好。
        /// （注意：即便不在 DP 候选里，单字仍会走 fallback 代价，且 GetCost 仍会平滑使用学过的统计；
        ///  这里只影响"是否给该多字段切分加分"。）
        /// </summary>
        private const int MinSamplesToConsiderLearned = 2;

        private static bool HasLearnedUnit(string unit, PersonalTypingProfile profile)
        {
            PersonalTypingUnitStats stats;
            return profile != null
                && profile.Units != null
                && profile.Units.TryGetValue(unit, out stats)
                && stats != null
                && stats.Count >= MinSamplesToConsiderLearned;
        }

        private static HashSet<string> BuildFallbackSegmentKeys(string[] chars, IEnumerable<string> fallbackSegments)
        {
            var result = new HashSet<string>();
            if (chars == null || fallbackSegments == null)
                return result;

            int searchStart = 0;
            foreach (string rawSegment in fallbackSegments)
            {
                string segment = rawSegment ?? "";
                int len = SplitTextElements(segment).Length;
                if (len <= 0)
                    continue;

                int start = FindSegmentStart(chars, segment, searchStart);
                if (start < 0)
                    continue;

                int end = start + len;
                searchStart = end;
                if (len > 1 && len <= MaxUnitCharacters && IsPureChineseUnit(segment))
                    result.Add(MakeSegmentKey(start, end, segment));
            }

            return result;
        }

        private static int FindSegmentStart(string[] chars, string segment, int searchStart)
        {
            string[] segmentElements = SplitTextElements(segment);
            if (chars == null || segmentElements.Length == 0 || chars.Length < segmentElements.Length)
                return -1;

            for (int i = Math.Max(0, searchStart); i <= chars.Length - segmentElements.Length; i++)
            {
                bool matched = true;
                for (int j = 0; j < segmentElements.Length; j++)
                {
                    if (!string.Equals(chars[i + j], segmentElements[j], StringComparison.Ordinal))
                    {
                        matched = false;
                        break;
                    }
                }

                if (matched)
                    return i;
            }

            return -1;
        }

        private static string[] SplitTextElements(string text)
        {
            if (string.IsNullOrEmpty(text))
                return new string[0];

            var si = new StringInfo(text);
            string[] result = new string[si.LengthInTextElements];
            for (int i = 0; i < result.Length; i++)
                result[i] = si.SubstringByTextElements(i, 1);
            return result;
        }

        private static string Concat(string[] chars, int start, int count)
        {
            if (count == 1)
                return chars[start];

            var result = "";
            for (int i = start; i < start + count; i++)
                result += chars[i];
            return result;
        }

        private static bool IsPureChineseUnit(string text)
        {
            string[] elements = SplitTextElements(text);
            if (elements.Length == 0)
                return false;

            foreach (string element in elements)
            {
                if (element.Length != 1 || !IsCjkUnifiedIdeograph(element[0]))
                    return false;
            }

            return true;
        }

        private static bool IsCjkUnifiedIdeograph(char ch)
        {
            return (ch >= '\u3400' && ch <= '\u9FFF') || (ch >= '\uF900' && ch <= '\uFAFF');
        }

        private static string MakeSegmentKey(int start, int end, string text)
        {
            return start.ToString(CultureInfo.InvariantCulture)
                + ":"
                + end.ToString(CultureInfo.InvariantCulture)
                + ":"
                + (text ?? "");
        }
    }
}
