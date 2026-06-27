using System;
using TypeSunny.UI;

namespace TypeSunny.Tests
{
    internal static class LocalArticleContinuationPolicyTests
    {
        private static int _failures;

        private static int Main()
        {
            Run("next uses typed paragraph instead of preview progress", NextUsesTypedParagraphInsteadOfPreviewProgress);
            Run("next works when preview progress is behind typed paragraph", NextWorksWhenPreviewProgressIsBehindTypedParagraph);
            Run("previous uses typed paragraph instead of preview progress", PreviousUsesTypedParagraphInsteadOfPreviewProgress);
            Run("invalid paragraph falls back to preview progress", InvalidParagraphFallsBackToPreviewProgress);
            Run("target progress is clamped to article bounds", TargetProgressIsClampedToArticleBounds);
            Run("current paragraph progress ignores preview progress", CurrentParagraphProgressIgnoresPreviewProgress);
            Run("recent same direction continuation is suppressed", RecentSameDirectionContinuationIsSuppressed);
            Run("old same direction continuation is allowed", OldSameDirectionContinuationIsAllowed);
            Run("opposite direction continuation is allowed", OppositeDirectionContinuationIsAllowed);
            Run("invalid paragraph continuation is not suppressed", InvalidParagraphContinuationIsNotSuppressed);

            if (_failures == 0)
            {
                Console.WriteLine("All LocalArticleContinuationPolicy tests passed.");
                return 0;
            }

            Console.WriteLine(_failures + " LocalArticleContinuationPolicy test(s) failed.");
            return 1;
        }

        private static void NextUsesTypedParagraphInsteadOfPreviewProgress()
        {
            int actual = LocalArticleContinuationPolicy.ResolveProgressForContinuation(
                currentGlobalProgress: 400,
                sectionSize: 200,
                totalSize: 1000,
                currentParagraph: 1,
                next: true);

            AssertEqual("next from typed paragraph 1", 200, actual);
        }

        private static void NextWorksWhenPreviewProgressIsBehindTypedParagraph()
        {
            int actual = LocalArticleContinuationPolicy.ResolveProgressForContinuation(
                currentGlobalProgress: 0,
                sectionSize: 200,
                totalSize: 1000,
                currentParagraph: 2,
                next: true);

            AssertEqual("next from typed paragraph 2", 400, actual);
        }

        private static void PreviousUsesTypedParagraphInsteadOfPreviewProgress()
        {
            int actual = LocalArticleContinuationPolicy.ResolveProgressForContinuation(
                currentGlobalProgress: 600,
                sectionSize: 200,
                totalSize: 1000,
                currentParagraph: 2,
                next: false);

            AssertEqual("previous from typed paragraph 2", 0, actual);
        }

        private static void InvalidParagraphFallsBackToPreviewProgress()
        {
            int nextActual = LocalArticleContinuationPolicy.ResolveProgressForContinuation(
                currentGlobalProgress: 200,
                sectionSize: 200,
                totalSize: 1000,
                currentParagraph: 0,
                next: true);

            int previousActual = LocalArticleContinuationPolicy.ResolveProgressForContinuation(
                currentGlobalProgress: 200,
                sectionSize: 200,
                totalSize: 1000,
                currentParagraph: 0,
                next: false);

            AssertEqual("fallback next", 400, nextActual);
            AssertEqual("fallback previous", 0, previousActual);
        }

        private static void TargetProgressIsClampedToArticleBounds()
        {
            int afterEnd = LocalArticleContinuationPolicy.ResolveProgressForContinuation(
                currentGlobalProgress: 800,
                sectionSize: 200,
                totalSize: 850,
                currentParagraph: 5,
                next: true);

            int beforeStart = LocalArticleContinuationPolicy.ResolveProgressForContinuation(
                currentGlobalProgress: 0,
                sectionSize: 200,
                totalSize: 850,
                currentParagraph: 1,
                next: false);

            AssertEqual("clamp after end", 849, afterEnd);
            AssertEqual("clamp before start", 0, beforeStart);
        }

        private static void CurrentParagraphProgressIgnoresPreviewProgress()
        {
            int actual = LocalArticleContinuationPolicy.ResolveProgressForParagraph(
                currentGlobalProgress: 600,
                sectionSize: 200,
                totalSize: 1000,
                currentParagraph: 2);

            AssertEqual("paragraph 2 base progress", 200, actual);
        }

        private static void RecentSameDirectionContinuationIsSuppressed()
        {
            DateTime lastAt = new DateTime(2026, 6, 26, 12, 0, 0, DateTimeKind.Utc);

            bool actual = LocalArticleContinuationPolicy.ShouldSuppressRepeatedContinuation(
                currentParagraph: 2,
                next: true,
                lastTargetParagraph: 2,
                lastNext: true,
                lastContinuationAtUtc: lastAt,
                nowUtc: lastAt.AddMilliseconds(120),
                suppressMilliseconds: 700);

            AssertTrue("recent same direction should be suppressed", actual);
        }

        private static void OldSameDirectionContinuationIsAllowed()
        {
            DateTime lastAt = new DateTime(2026, 6, 26, 12, 0, 0, DateTimeKind.Utc);

            bool actual = LocalArticleContinuationPolicy.ShouldSuppressRepeatedContinuation(
                currentParagraph: 2,
                next: true,
                lastTargetParagraph: 2,
                lastNext: true,
                lastContinuationAtUtc: lastAt,
                nowUtc: lastAt.AddMilliseconds(900),
                suppressMilliseconds: 700);

            AssertFalse("old same direction should be allowed", actual);
        }

        private static void OppositeDirectionContinuationIsAllowed()
        {
            DateTime lastAt = new DateTime(2026, 6, 26, 12, 0, 0, DateTimeKind.Utc);

            bool actual = LocalArticleContinuationPolicy.ShouldSuppressRepeatedContinuation(
                currentParagraph: 2,
                next: false,
                lastTargetParagraph: 2,
                lastNext: true,
                lastContinuationAtUtc: lastAt,
                nowUtc: lastAt.AddMilliseconds(120),
                suppressMilliseconds: 700);

            AssertFalse("opposite direction should be allowed", actual);
        }

        private static void InvalidParagraphContinuationIsNotSuppressed()
        {
            DateTime lastAt = new DateTime(2026, 6, 26, 12, 0, 0, DateTimeKind.Utc);

            bool actual = LocalArticleContinuationPolicy.ShouldSuppressRepeatedContinuation(
                currentParagraph: 0,
                next: true,
                lastTargetParagraph: 2,
                lastNext: true,
                lastContinuationAtUtc: lastAt,
                nowUtc: lastAt.AddMilliseconds(120),
                suppressMilliseconds: 700);

            AssertFalse("invalid paragraph should not be suppressed", actual);
        }

        private static void Run(string name, Action test)
        {
            try
            {
                test();
                Console.WriteLine("PASS: " + name);
            }
            catch (Exception ex)
            {
                _failures++;
                Console.WriteLine("FAIL: " + name);
                Console.WriteLine(ex.Message);
            }
        }

        private static void AssertEqual(string name, int expected, int actual)
        {
            if (actual != expected)
                throw new Exception(name + ": expected " + expected + ", got " + actual + ".");
        }

        private static void AssertTrue(string name, bool actual)
        {
            if (!actual)
                throw new Exception(name + ": expected true, got false.");
        }

        private static void AssertFalse(string name, bool actual)
        {
            if (actual)
                throw new Exception(name + ": expected false, got true.");
        }
    }
}
