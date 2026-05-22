using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TypeSunny.Personalization
{
    internal static class PersonalTypingSessionBuilder
    {
        private const int WarmupCharacters = 3;
        private const int MaxUnitCharacters = 4;
        private const long PauseThresholdMilliseconds = 10000;

        public static PersonalTypingSession Build(
            string targetText,
            IEnumerable<string> commitTexts,
            IEnumerable<long> commitTimes,
            IEnumerable<long> keyTimes,
            IEnumerable<string> segmentedUnits = null)
        {
            string[] commits = (commitTexts ?? Enumerable.Empty<string>()).ToArray();
            long[] commitMs = (commitTimes ?? Enumerable.Empty<long>()).ToArray();
            long[] keyMs = (keyTimes ?? Enumerable.Empty<long>()).OrderBy(t => t).ToArray();

            var session = new PersonalTypingSession();
            string[] targetElements = SplitTextElements(targetText);
            int targetLength = CountTextElements(targetText);
            session.EffectiveStatCharacters = Math.Max(0, targetLength - WarmupCharacters);

            int targetPos = 0;
            long previousCommitTime = 0;
            long[] charStartTimes = new long[targetLength];
            long[] charEndTimes = new long[targetLength];
            var commitSampleKeys = new HashSet<string>();
            int count = Math.Min(commits.Length, commitMs.Length);
            for (int i = 0; i < count; i++)
            {
                string commit = commits[i] ?? "";
                int commitLength = CountTextElements(commit);
                long commitTime = commitMs[i];
                int commitStart = targetPos;
                int commitEnd = targetPos + commitLength;

                double effectiveMs = EffectiveMillisecondsBetween(previousCommitTime, commitTime, keyMs);
                int effectivePart = Math.Max(0, commitEnd - Math.Max(WarmupCharacters, commitStart));
                if (effectivePart > 0)
                    session.EffectiveMilliseconds += effectiveMs;

                for (int pos = Math.Max(0, commitStart); pos < Math.Min(targetLength, commitEnd); pos++)
                {
                    charStartTimes[pos] = previousCommitTime;
                    charEndTimes[pos] = commitTime;
                }

                bool startsAfterWarmup = commitStart >= WarmupCharacters;
                if (startsAfterWarmup
                    && commitLength > 0
                    && commitLength <= MaxUnitCharacters
                    && IsPureChineseUnit(commit))
                {
                    double keyCount = CountKeysBetween(previousCommitTime, commitTime, keyMs);
                    session.Samples.Add(new PersonalTypingUnitSample(commit, effectiveMs, keyCount));
                    commitSampleKeys.Add(MakeSampleKey(commitStart, commitEnd, commit));
                }

                targetPos = commitEnd;
                previousCommitTime = commitTime;
            }

            AddSegmentSamples(session, targetElements, segmentedUnits, charStartTimes, charEndTimes, keyMs, commitSampleKeys);

            return session;
        }

        private static int CountTextElements(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;
            return new StringInfo(text).LengthInTextElements;
        }

        private static string[] SplitTextElements(string text)
        {
            if (string.IsNullOrEmpty(text))
                return new string[0];

            var info = new StringInfo(text);
            string[] result = new string[info.LengthInTextElements];
            for (int i = 0; i < result.Length; i++)
                result[i] = info.SubstringByTextElements(i, 1);
            return result;
        }

        private static void AddSegmentSamples(
            PersonalTypingSession session,
            string[] targetElements,
            IEnumerable<string> segmentedUnits,
            long[] charStartTimes,
            long[] charEndTimes,
            long[] keyTimes,
            HashSet<string> commitSampleKeys)
        {
            if (session == null || targetElements == null || segmentedUnits == null)
                return;

            int searchStart = WarmupCharacters;
            foreach (string rawSegment in segmentedUnits)
            {
                string segment = rawSegment ?? "";
                int segmentLength = CountTextElements(segment);
                if (segmentLength <= 1 || segmentLength > MaxUnitCharacters || !IsPureChineseUnit(segment))
                    continue;

                int start = FindSegmentStart(targetElements, segment, searchStart);
                if (start < 0)
                    continue;

                int end = start + segmentLength;
                searchStart = end;
                if (start < WarmupCharacters
                    || end > targetElements.Length
                    || end > charEndTimes.Length
                    || commitSampleKeys.Contains(MakeSampleKey(start, end, segment)))
                {
                    continue;
                }

                long startTime = charStartTimes[start];
                long endTime = charEndTimes[end - 1];
                if (endTime <= startTime)
                    continue;

                double effectiveMs = EffectiveMillisecondsBetween(startTime, endTime, keyTimes);
                double keyCount = CountKeysBetween(startTime, endTime, keyTimes);
                session.Samples.Add(new PersonalTypingUnitSample(segment, effectiveMs, keyCount));
            }
        }

        private static int FindSegmentStart(string[] targetElements, string segment, int searchStart)
        {
            string[] segmentElements = SplitTextElements(segment);
            if (segmentElements.Length == 0 || targetElements == null || targetElements.Length < segmentElements.Length)
                return -1;

            for (int i = Math.Max(0, searchStart); i <= targetElements.Length - segmentElements.Length; i++)
            {
                bool matched = true;
                for (int j = 0; j < segmentElements.Length; j++)
                {
                    if (!string.Equals(targetElements[i + j], segmentElements[j], StringComparison.Ordinal))
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

        private static string MakeSampleKey(int start, int end, string text)
        {
            return start.ToString(CultureInfo.InvariantCulture)
                + ":"
                + end.ToString(CultureInfo.InvariantCulture)
                + ":"
                + (text ?? "");
        }

        private static int CountKeysBetween(long startExclusive, long endInclusive, long[] keyTimes)
        {
            int count = 0;
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
            for (int i = 0; i < keyTimes.Length; i++)
            {
                if (keyTimes[i] > start && keyTimes[i] < end)
                    events.Add(keyTimes[i]);
            }
            events.Add(end);

            double effective = end - start;
            for (int i = 1; i < events.Count; i++)
            {
                long gap = events[i] - events[i - 1];
                if (gap > PauseThresholdMilliseconds)
                    effective -= gap;
            }

            return Math.Max(0, effective);
        }
    }
}
