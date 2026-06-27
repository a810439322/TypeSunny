using System;

namespace TypeSunny.UI
{
    internal static class LocalArticleContinuationPolicy
    {
        public static int ResolveProgressForContinuation(
            int currentGlobalProgress,
            int sectionSize,
            int totalSize,
            int currentParagraph,
            bool next)
        {
            if (totalSize <= 0)
                return 0;

            int maxProgress = Math.Max(0, totalSize - 1);
            if (sectionSize <= 0)
                return Clamp(currentGlobalProgress, 0, maxProgress);

            long baseProgress = currentParagraph > 0
                ? (long)(currentParagraph - 1) * sectionSize
                : currentGlobalProgress;
            long targetProgress = baseProgress + (next ? sectionSize : -sectionSize);

            return Clamp(targetProgress, 0, maxProgress);
        }

        public static int ResolveProgressForParagraph(
            int currentGlobalProgress,
            int sectionSize,
            int totalSize,
            int currentParagraph)
        {
            if (totalSize <= 0)
                return 0;

            int maxProgress = Math.Max(0, totalSize - 1);
            if (sectionSize <= 0 || currentParagraph <= 0)
                return Clamp(currentGlobalProgress, 0, maxProgress);

            return Clamp((long)(currentParagraph - 1) * sectionSize, 0, maxProgress);
        }

        public static bool ShouldSuppressRepeatedContinuation(
            int currentParagraph,
            bool next,
            int lastTargetParagraph,
            bool lastNext,
            DateTime lastContinuationAtUtc,
            DateTime nowUtc,
            int suppressMilliseconds)
        {
            if (currentParagraph <= 0 || lastTargetParagraph <= 0 || suppressMilliseconds <= 0)
                return false;

            if (currentParagraph != lastTargetParagraph || next != lastNext)
                return false;

            TimeSpan elapsed = nowUtc - lastContinuationAtUtc;
            return elapsed >= TimeSpan.Zero && elapsed.TotalMilliseconds < suppressMilliseconds;
        }

        private static int Clamp(long value, int min, int max)
        {
            if (value < min)
                return min;
            if (value > max)
                return max;
            return (int)value;
        }
    }
}
