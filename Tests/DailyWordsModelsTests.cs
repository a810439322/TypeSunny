using System;
using TypeSunny.Net;

namespace TypeSunny.Tests
{
    internal static class DailyWordsModelsTests
    {
        private static int failures;

        private static int Main()
        {
            CombinesSingleKeystrokeBySingleWordCount();
            CombinesArticleSpeedByArticleWordCount();
            AccumulatorBuildsWeightedTrainerReport();
            AccumulatorBuildsWeightedTrainerReportFromGroupMetrics();
            AccumulatorHoldsTrainerWordsUntilKeystrokeMetricExists();
            AccumulatorUsesSourceWordWeightForTrainerMetricAfterRetries();
            AccumulatorBuildsWeightedArticleReport();
            AccumulatorDoesNotOverrideTrainerKeystrokeWithFinalMetric();
            AccumulatorFlushReturnsEmptyAfterFlush();

            if (failures > 0)
            {
                Console.Error.WriteLine(failures + " DailyWordsModels test(s) failed.");
                return 1;
            }

            Console.WriteLine("All DailyWordsModels tests passed.");
            return 0;
        }

        private static void CombinesSingleKeystrokeBySingleWordCount()
        {
            var date = new DateTime(2026, 6, 26);
            var left = new DailyWordsReport(10, date, 10, 0, 0, 4);
            var right = new DailyWordsReport(30, date, 30, 0, 0, 8);

            DailyWordsReport combined = DailyWordsReport.Combine(left, right);

            AssertEqual("combined total count", 40, combined.Count);
            AssertEqual("combined single word count", 40, combined.SingleWordCount);
            AssertNear("single keystroke weighted average", 7, combined.SingleAvgKeystroke, 0.0001);
        }

        private static void CombinesArticleSpeedByArticleWordCount()
        {
            var date = new DateTime(2026, 6, 26);
            var left = new DailyWordsReport(20, date, 0, 20, 60, 0);
            var right = new DailyWordsReport(10, date, 0, 10, 120, 0);

            DailyWordsReport combined = DailyWordsReport.Combine(left, right);

            AssertEqual("combined article word count", 30, combined.ArticleWordCount);
            AssertNear("article speed weighted average", 80, combined.ArticleAvgSpeed, 0.0001);
        }

        private static void AccumulatorBuildsWeightedTrainerReport()
        {
            var date = new DateTime(2026, 6, 26);
            var accumulator = new DailyWordsTypedStatisticsAccumulator();

            accumulator.Add(10, date, DailyWordsTypingKind.Single, articleAvgSpeed: 0, singleAvgKeystroke: 4);
            accumulator.Add(30, date, DailyWordsTypingKind.Single, articleAvgSpeed: 0, singleAvgKeystroke: 8);

            DailyWordsReport report = accumulator.Flush();

            AssertEqual("trainer report total count", 40, report.Count);
            AssertEqual("trainer report single count", 40, report.SingleWordCount);
            AssertEqual("trainer report article count", 0, report.ArticleWordCount);
            AssertNear("trainer report weighted keystroke", 7, report.SingleAvgKeystroke, 0.0001);
        }

        private static void AccumulatorBuildsWeightedTrainerReportFromGroupMetrics()
        {
            var date = new DateTime(2026, 6, 26);
            var accumulator = new DailyWordsTypedStatisticsAccumulator();

            accumulator.Add(40, date, DailyWordsTypingKind.Single, articleAvgSpeed: 0, singleAvgKeystroke: 0);
            accumulator.AddSingleKeystrokeMetric(10, date, 4);
            accumulator.AddSingleKeystrokeMetric(30, date, 8);

            DailyWordsReport report = accumulator.Flush(articleAvgSpeed: 0, singleAvgKeystroke: 9.5);

            AssertEqual("trainer metric report total count", 40, report.Count);
            AssertEqual("trainer metric report single count", 40, report.SingleWordCount);
            AssertNear("trainer metric report source-word weighted keystroke", 7, report.SingleAvgKeystroke, 0.0001);
        }

        private static void AccumulatorHoldsTrainerWordsUntilKeystrokeMetricExists()
        {
            var date = new DateTime(2026, 6, 26);
            var accumulator = new DailyWordsTypedStatisticsAccumulator();

            accumulator.Add(10, date, DailyWordsTypingKind.Single, articleAvgSpeed: 0, singleAvgKeystroke: 0);

            DailyWordsReport first = accumulator.Flush(articleAvgSpeed: 0, singleAvgKeystroke: 0);

            AssertEqual("trainer words without metric are held", 0, first.Count);

            accumulator.AddSingleKeystrokeMetric(10, date, 5);
            DailyWordsReport second = accumulator.Flush(articleAvgSpeed: 0, singleAvgKeystroke: 0);

            AssertEqual("held trainer words report after metric", 10, second.Count);
            AssertEqual("held trainer single count after metric", 10, second.SingleWordCount);
            AssertNear("held trainer keystroke after metric", 5, second.SingleAvgKeystroke, 0.0001);
        }

        private static void AccumulatorUsesSourceWordWeightForTrainerMetricAfterRetries()
        {
            var date = new DateTime(2026, 6, 26);
            var accumulator = new DailyWordsTypedStatisticsAccumulator();

            accumulator.Add(10, date, DailyWordsTypingKind.Single, articleAvgSpeed: 0, singleAvgKeystroke: 0);
            accumulator.Flush(articleAvgSpeed: 0, singleAvgKeystroke: 0);
            accumulator.Add(10, date, DailyWordsTypingKind.Single, articleAvgSpeed: 0, singleAvgKeystroke: 0);
            accumulator.AddSingleKeystrokeMetric(10, date, 6);

            DailyWordsReport report = accumulator.Flush(articleAvgSpeed: 0, singleAvgKeystroke: 0);

            AssertEqual("retry trainer total count includes attempts", 20, report.Count);
            AssertEqual("retry trainer metric weight uses source words", 10, report.SingleWordCount);
            AssertNear("retry trainer source weighted keystroke", 6, report.SingleAvgKeystroke, 0.0001);
        }

        private static void AccumulatorBuildsWeightedArticleReport()
        {
            var date = new DateTime(2026, 6, 26);
            var accumulator = new DailyWordsTypedStatisticsAccumulator();

            accumulator.Add(15, date, DailyWordsTypingKind.Article, articleAvgSpeed: 90, singleAvgKeystroke: 0);
            accumulator.Add(5, date, DailyWordsTypingKind.Article, articleAvgSpeed: 150, singleAvgKeystroke: 0);

            DailyWordsReport report = accumulator.Flush();

            AssertEqual("article report total count", 20, report.Count);
            AssertEqual("article report single count", 0, report.SingleWordCount);
            AssertEqual("article report article count", 20, report.ArticleWordCount);
            AssertNear("article report weighted speed", 105, report.ArticleAvgSpeed, 0.0001);
        }

        private static void AccumulatorDoesNotOverrideTrainerKeystrokeWithFinalMetric()
        {
            var date = new DateTime(2026, 6, 26);
            var accumulator = new DailyWordsTypedStatisticsAccumulator();

            accumulator.Add(10, date, DailyWordsTypingKind.Single, articleAvgSpeed: 0, singleAvgKeystroke: 4);
            accumulator.Add(30, date, DailyWordsTypingKind.Single, articleAvgSpeed: 0, singleAvgKeystroke: 8);

            DailyWordsReport report = accumulator.Flush(articleAvgSpeed: 0, singleAvgKeystroke: 9.5);

            AssertEqual("trainer source word count", 40, report.Count);
            AssertNear("trainer keystroke keeps source-word weighted average", 7, report.SingleAvgKeystroke, 0.0001);
        }

        private static void AccumulatorFlushReturnsEmptyAfterFlush()
        {
            var date = new DateTime(2026, 6, 26);
            var accumulator = new DailyWordsTypedStatisticsAccumulator();
            accumulator.Add(3, date, DailyWordsTypingKind.Article, articleAvgSpeed: 60, singleAvgKeystroke: 0);
            accumulator.Flush();

            DailyWordsReport second = accumulator.Flush();

            AssertEqual("second flush count", 0, second.Count);
        }

        private static void AssertEqual(string name, int expected, int actual)
        {
            if (expected == actual)
                return;

            failures++;
            Console.Error.WriteLine(name + ": expected " + expected + ", got " + actual);
        }

        private static void AssertNear(string name, double expected, double actual, double tolerance)
        {
            if (Math.Abs(expected - actual) <= tolerance)
                return;

            failures++;
            Console.Error.WriteLine(name + ": expected " + expected + ", got " + actual);
        }
    }
}
