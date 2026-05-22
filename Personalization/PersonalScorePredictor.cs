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

        private static CandidateCost GetCost(string unit, int charCount, PersonalTypingProfile profile)
        {
            PersonalTypingUnitStats stats;
            if (profile.Units != null && profile.Units.TryGetValue(unit, out stats) && stats.Count > 0)
            {
                return new CandidateCost
                {
                    Milliseconds = Math.Max(1, stats.AverageMilliseconds),
                    Keys = Math.Max(1, stats.AverageKeys),
                    IsMature = stats.Count >= 3 || stats.ObservedCharacters >= 8
                };
            }

            double speed = profile.BaselineSpeed > 0 ? profile.BaselineSpeed : 120;
            double kpw = profile.BaselineKpw > 0 ? profile.BaselineKpw : 4;
            return new CandidateCost
            {
                Milliseconds = Math.Max(1, charCount * 60000.0 / speed),
                Keys = Math.Max(1, charCount * kpw),
                IsMature = false
            };
        }

        private static bool HasLearnedUnit(string unit, PersonalTypingProfile profile)
        {
            PersonalTypingUnitStats stats;
            return profile != null
                && profile.Units != null
                && profile.Units.TryGetValue(unit, out stats)
                && stats != null
                && stats.Count > 0;
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
