using System.Collections.Generic;
using System.Globalization;

namespace TypeSunny.Core
{
    internal static class SlowRetypeDetector
    {
        public static Dictionary<int, string> BuildSlowRecords(
            IList<string> targetWords,
            IList<long> commitTimes,
            IList<string> commitTexts,
            IList<int> commitTargetPositions,
            double slowThresholdMilliseconds,
            string excludePuncts,
            string wrongExclude)
        {
            var records = new Dictionary<int, string>();
            if (targetWords == null || commitTimes == null || commitTexts == null || commitTargetPositions == null)
                return records;
            var elapsedByTarget = new Dictionary<int, double>();

            int count = commitTimes.Count;
            if (commitTexts.Count < count)
                count = commitTexts.Count;
            if (commitTargetPositions.Count < count)
                count = commitTargetPositions.Count;

            for (int i = 0; i < count; i++)
            {
                string groupText = commitTexts[i] ?? "";
                int validCharCount = CountValidTextElements(groupText, excludePuncts);
                if (validCharCount <= 0)
                    continue;

                long previousCommitTime = i > 0 ? commitTimes[i - 1] : 0;
                double avgTimePerChar = (double)(commitTimes[i] - previousCommitTime) / validCharCount;

                int textPos = commitTargetPositions[i];
                int groupLength = new StringInfo(groupText).LengthInTextElements;
                for (int j = 0; j < groupLength; j++)
                {
                    int targetIndex = textPos + j;
                    if (targetIndex < 0 || targetIndex >= targetWords.Count)
                        continue;

                    string word = targetWords[targetIndex];
                    if (!string.IsNullOrEmpty(wrongExclude) && wrongExclude.Contains(word))
                        continue;

                    double elapsed = avgTimePerChar;
                    if (elapsedByTarget.ContainsKey(targetIndex))
                        elapsed += elapsedByTarget[targetIndex];
                    elapsedByTarget[targetIndex] = elapsed;

                    if (elapsed > slowThresholdMilliseconds)
                        records[targetIndex] = word;
                }
            }

            return records;
        }

        private static int CountValidTextElements(string text, string excludePuncts)
        {
            int count = 0;
            var si = new StringInfo(text ?? "");
            for (int i = 0; i < si.LengthInTextElements; i++)
            {
                string ch = si.SubstringByTextElements(i, 1);
                if (string.IsNullOrEmpty(excludePuncts) || !excludePuncts.Contains(ch))
                    count++;
            }

            return count;
        }
    }
}
