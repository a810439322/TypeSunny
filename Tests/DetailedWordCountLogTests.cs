using System;
using System.IO;
using System.Linq;
using TypeSunny.Core;
using TypeSunny.Logs;

namespace TypeSunny.Tests
{
    internal static class DetailedWordCountLogTests
    {
        private static int Main()
        {
            try
            {
                FirstMigrationStoresLegacyTotalAsHistory();
                MigrationRunsOnlyOnce();
                WenlaiInputAddsCategoryAndDifficultyWords();
                LocalArticleInputAddsCategoryAndDifficultyWords();
                TrainerInputAddsCategoryOnly();
                RaceInputCountsAttemptOncePerLoadedContext();
                RaceInputCountsAnotherAttemptForNewLoadedContext();
                NewItemsKeepOriginalStartDate();
                TypingDaysCountDistinctInputDates();
                SnapshotSeparatesArticleAndTrainerWords();
                CategorySelectorItemsKeepHistoryButDefaultChartExcludesHistory();
                CategoryChartExcludesHistoryByDefault();
                CategoryChartMergesItemsAfterTopEight();
                CategoryChartUsesTrainerDisplayNames();
                CategoryDisplayItemsCanMergeSameProject();
                DifficultyRowsUseFixedOrder();

                Console.WriteLine("All DetailedWordCountLog tests passed.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        private static void FirstMigrationStoresLegacyTotalAsHistory()
        {
            string path = NewTempPath();
            try
            {
                DetailedWordCountLog.ConfigureForTests(path);
                DetailedWordCountLog.EnsureMigrated(1234, new DateTime(2026, 5, 26));

                var snapshot = DetailedWordCountLog.LoadSnapshot(1234, new DateTime(2026, 5, 26));
                var history = snapshot.CategoryItems.Single(i => i.Key == DetailedWordCountLog.HistoryCategoryKey);

                AssertEqual("history words", 1234, history.Words);
                AssertEqual("history display", "历史数据", history.DisplayName);
                AssertEqual("history start date", "2026-05-26", history.StartDate);
                AssertEqual("category total", 1234, snapshot.CategoryTotalWords);
                AssertTrue("snapshot aligned", snapshot.IsAligned);
            }
            finally
            {
                DetailedWordCountLog.ResetForTests();
                TryDelete(path);
            }
        }

        private static void MigrationRunsOnlyOnce()
        {
            string path = NewTempPath();
            try
            {
                DetailedWordCountLog.ConfigureForTests(path);
                DetailedWordCountLog.EnsureMigrated(100, new DateTime(2026, 5, 26));
                DetailedWordCountLog.EnsureMigrated(999, new DateTime(2026, 5, 27));

                var snapshot = DetailedWordCountLog.LoadSnapshot(100, new DateTime(2026, 5, 27));
                var history = snapshot.CategoryItems.Single(i => i.Key == DetailedWordCountLog.HistoryCategoryKey);

                AssertEqual("migration once words", 100, history.Words);
                AssertEqual("migration once start date", "2026-05-26", history.StartDate);
            }
            finally
            {
                DetailedWordCountLog.ResetForTests();
                TryDelete(path);
            }
        }

        private static void WenlaiInputAddsCategoryAndDifficultyWords()
        {
            string path = NewTempPath();
            try
            {
                DetailedWordCountLog.ConfigureForTests(path);
                DetailedWordCountLog.EnsureMigrated(0, new DateTime(2026, 5, 26));

                var context = new TypingWordCountContext(
                    TxtSource.articlesender,
                    "category:wenlai:普",
                    "文来 / 普",
                    true,
                    "普");

                DetailedWordCountLog.AddTypedWords(12, context, new DateTime(2026, 5, 26, 8, 0, 0));

                var snapshot = DetailedWordCountLog.LoadSnapshot(12, new DateTime(2026, 5, 26));
                AssertEqual("wenlai category words", 12, snapshot.CategoryItems.Single(i => i.Key == "category:wenlai:普").Words);
                AssertEqual("wenlai difficulty words", 12, snapshot.DifficultyItems.Single(i => i.Key == "difficulty:普").Words);
                AssertEqual("wenlai category total", 12, snapshot.CategoryTotalWords);
                AssertEqual("wenlai difficulty total", 12, snapshot.DifficultyTotalWords);
            }
            finally
            {
                DetailedWordCountLog.ResetForTests();
                TryDelete(path);
            }
        }

        private static void LocalArticleInputAddsCategoryAndDifficultyWords()
        {
            string path = NewTempPath();
            try
            {
                DetailedWordCountLog.ConfigureForTests(path);
                DetailedWordCountLog.EnsureMigrated(0, new DateTime(2026, 5, 26));

                var context = new TypingWordCountContext(
                    TxtSource.book,
                    "category:book:三国演义",
                    "本地文章 / 三国演义",
                    true,
                    "难");

                DetailedWordCountLog.AddTypedWords(20, context, new DateTime(2026, 5, 26, 8, 0, 0));

                var snapshot = DetailedWordCountLog.LoadSnapshot(20, new DateTime(2026, 5, 26));
                AssertEqual("book category words", 20, snapshot.CategoryItems.Single(i => i.Key == "category:book:三国演义").Words);
                AssertEqual("book difficulty words", 20, snapshot.DifficultyItems.Single(i => i.Key == "difficulty:难").Words);
            }
            finally
            {
                DetailedWordCountLog.ResetForTests();
                TryDelete(path);
            }
        }

        private static void TrainerInputAddsCategoryOnly()
        {
            string path = NewTempPath();
            try
            {
                DetailedWordCountLog.ConfigureForTests(path);
                DetailedWordCountLog.EnsureMigrated(0, new DateTime(2026, 5, 26));

                var context = new TypingWordCountContext(
                    TxtSource.trainer,
                    "category:trainer:1.前500",
                    "晴练单 / 1.前500",
                    false,
                    "普");

                DetailedWordCountLog.AddTypedWords(8, context, new DateTime(2026, 5, 26));

                var snapshot = DetailedWordCountLog.LoadSnapshot(8, new DateTime(2026, 5, 26));
                AssertEqual("trainer category words", 8, snapshot.CategoryItems.Single(i => i.Key == "category:trainer:1.前500").Words);
                AssertEqual("trainer difficulty count", 0, snapshot.DifficultyItems.Count(i => i.Words > 0));
            }
            finally
            {
                DetailedWordCountLog.ResetForTests();
                TryDelete(path);
            }
        }

        private static void RaceInputCountsAttemptOncePerLoadedContext()
        {
            string path = NewTempPath();
            try
            {
                DetailedWordCountLog.ConfigureForTests(path);
                DetailedWordCountLog.EnsureMigrated(0, new DateTime(2026, 5, 26));

                var context = new TypingWordCountContext(
                    TxtSource.jbs,
                    "category:race:jbs",
                    "赛文 / 锦标赛",
                    true,
                    "普",
                    true);

                DetailedWordCountLog.AddTypedWords(5, context, new DateTime(2026, 5, 26));
                DetailedWordCountLog.AddTypedWords(7, context, new DateTime(2026, 5, 26));

                var snapshot = DetailedWordCountLog.LoadSnapshot(12, new DateTime(2026, 5, 26));
                var item = snapshot.CategoryItems.Single(i => i.Key == "category:race:jbs");
                AssertEqual("race words accumulated", 12, item.Words);
                AssertEqual("race attempts once", 1, item.Attempts);
                AssertEqual("race attempt summary", 1, snapshot.RaceAttemptCount);
            }
            finally
            {
                DetailedWordCountLog.ResetForTests();
                TryDelete(path);
            }
        }

        private static void RaceInputCountsAnotherAttemptForNewLoadedContext()
        {
            string path = NewTempPath();
            try
            {
                DetailedWordCountLog.ConfigureForTests(path);
                DetailedWordCountLog.EnsureMigrated(0, new DateTime(2026, 5, 26));

                var firstContext = new TypingWordCountContext(
                    TxtSource.jisucup,
                    "category:race:jisucup",
                    "赛文 / 极速杯",
                    true,
                    "难",
                    true);
                var secondContext = new TypingWordCountContext(
                    TxtSource.jisucup,
                    "category:race:jisucup",
                    "赛文 / 极速杯",
                    true,
                    "难",
                    true);

                DetailedWordCountLog.AddTypedWords(10, firstContext, new DateTime(2026, 5, 26));
                DetailedWordCountLog.AddTypedWords(15, secondContext, new DateTime(2026, 5, 27));

                var snapshot = DetailedWordCountLog.LoadSnapshot(25, new DateTime(2026, 5, 27));
                var item = snapshot.CategoryItems.Single(i => i.Key == "category:race:jisucup");
                AssertEqual("race reload words accumulated", 25, item.Words);
                AssertEqual("race reload attempts", 2, item.Attempts);
                AssertEqual("race reload attempt summary", 2, snapshot.RaceAttemptCount);
            }
            finally
            {
                DetailedWordCountLog.ResetForTests();
                TryDelete(path);
            }
        }

        private static void NewItemsKeepOriginalStartDate()
        {
            string path = NewTempPath();
            try
            {
                DetailedWordCountLog.ConfigureForTests(path);
                DetailedWordCountLog.EnsureMigrated(0, new DateTime(2026, 5, 26));

                var context = new TypingWordCountContext(
                    TxtSource.clipboard,
                    "category:clipboard",
                    "剪贴板载文",
                    true,
                    "易");

                DetailedWordCountLog.AddTypedWords(5, context, new DateTime(2026, 5, 26));
                DetailedWordCountLog.AddTypedWords(7, context, new DateTime(2026, 5, 27));

                var snapshot = DetailedWordCountLog.LoadSnapshot(12, new DateTime(2026, 5, 27));
                var item = snapshot.CategoryItems.Single(i => i.Key == "category:clipboard");
                AssertEqual("start date preserved", "2026-05-26", item.StartDate);
                AssertEqual("words accumulated", 12, item.Words);
            }
            finally
            {
                DetailedWordCountLog.ResetForTests();
                TryDelete(path);
            }
        }

        private static void CategoryChartMergesItemsAfterTopEight()
        {
            var items = Enumerable.Range(1, 10)
                .Select(i => new DetailedWordCountItem
                {
                    Key = "category:item:" + i,
                    DisplayName = "项目" + i,
                    Words = i,
                    StartDate = "2026-05-26"
                })
                .ToList();

            var chartItems = DetailedWordCountLog.BuildCategoryChartItems(items, 8);

            AssertEqual("chart item count", 9, chartItems.Count);
            AssertTrue("chart has others", chartItems.Any(i => i.DisplayName == "其他" && i.Words == 3));
            AssertEqual("top item first", "项目10", chartItems[0].DisplayName);
        }

        private static void CategoryChartUsesTrainerDisplayNames()
        {
            var items = new[]
            {
                new DetailedWordCountItem { Key = "category:trainer:1.前500", DisplayName = "晴练单 / 1.前500", Words = 10, StartDate = "2026-05-26" },
                new DetailedWordCountItem { Key = "category:trainer:02. 中500", DisplayName = "晴练单 / 02. 中500", Words = 8, StartDate = "2026-05-26" }
            };

            var separated = DetailedWordCountLog.BuildCategoryDisplayItems(items, false);
            var merged = DetailedWordCountLog.BuildCategoryDisplayItems(items, true);
            var chartItems = DetailedWordCountLog.BuildCategoryChartItems(separated, 8);

            AssertTrue("separated trainer display strips qing prefix", separated.Any(i => i.DisplayName == "练单 / 前500"));
            AssertTrue("separated trainer display strips numeric prefix", separated.Any(i => i.DisplayName == "练单 / 中500"));
            AssertEqual("merged trainer display", "练单", merged.Single().DisplayName);
            AssertTrue("chart trainer display", chartItems.Any(i => i.DisplayName == "练单 / 前500"));
            AssertTrue("chart should not show qing trainer name", chartItems.All(i => !i.DisplayName.Contains("晴练单")));
            AssertTrue("chart should not show trainer numeric prefix", chartItems.All(i => !i.DisplayName.Contains("/ 1.") && !i.DisplayName.Contains("/ 02.")));
        }

        private static void CategorySelectorItemsKeepHistoryButDefaultChartExcludesHistory()
        {
            string path = NewTempPath();
            try
            {
                DetailedWordCountLog.ConfigureForTests(path);
                DetailedWordCountLog.EnsureMigrated(100, new DateTime(2026, 5, 26));

                var context = new TypingWordCountContext(
                    TxtSource.articlesender,
                    "category:wenlai:普",
                    "文来 / 普",
                    true,
                    "普");

                DetailedWordCountLog.AddTypedWords(20, context, new DateTime(2026, 5, 27));

                var snapshot = DetailedWordCountLog.LoadSnapshot(120, new DateTime(2026, 5, 27));

                AssertTrue("raw category items keep history", snapshot.CategoryItems.Any(i => i.Key == DetailedWordCountLog.HistoryCategoryKey));
                AssertTrue("project selector items keep history", snapshot.VisibleCategoryItems.Any(i => i.Key == DetailedWordCountLog.HistoryCategoryKey));
                AssertEqual("project selector sum includes history", 120, snapshot.VisibleCategoryItems.Sum(i => i.Words));
                AssertTrue("default chart items hide history", snapshot.CategoryChartItems.All(i => i.Key != DetailedWordCountLog.HistoryCategoryKey));
                AssertEqual("category total remains aligned", 120, snapshot.CategoryTotalWords);
                AssertTrue("snapshot remains aligned", snapshot.IsAligned);
            }
            finally
            {
                DetailedWordCountLog.ResetForTests();
                TryDelete(path);
            }
        }

        private static void TypingDaysCountDistinctInputDates()
        {
            string path = NewTempPath();
            try
            {
                DetailedWordCountLog.ConfigureForTests(path);
                DetailedWordCountLog.EnsureMigrated(100, new DateTime(2026, 5, 26));

                var context = new TypingWordCountContext(
                    TxtSource.clipboard,
                    "category:clipboard",
                    "剪贴板载文",
                    true,
                    "易");

                DetailedWordCountLog.AddTypedWords(5, context, new DateTime(2026, 5, 27));
                DetailedWordCountLog.AddTypedWords(7, context, new DateTime(2026, 5, 27, 23, 0, 0));
                DetailedWordCountLog.AddTypedWords(9, context, new DateTime(2026, 5, 28));

                var snapshot = DetailedWordCountLog.LoadSnapshot(121, new DateTime(2026, 5, 28));

                AssertEqual("typing days distinct", 3, snapshot.TypingDays);
            }
            finally
            {
                DetailedWordCountLog.ResetForTests();
                TryDelete(path);
            }
        }

        private static void SnapshotSeparatesArticleAndTrainerWords()
        {
            string path = NewTempPath();
            try
            {
                DetailedWordCountLog.ConfigureForTests(path);
                DetailedWordCountLog.EnsureMigrated(100, new DateTime(2026, 5, 26));

                var articleContext = new TypingWordCountContext(
                    TxtSource.articlesender,
                    "category:wenlai:普",
                    "文来 / 普",
                    true,
                    "普");
                var trainerContext = new TypingWordCountContext(
                    TxtSource.trainer,
                    "category:trainer:1.前500",
                    "晴练单 / 1.前500",
                    false,
                    "");

                DetailedWordCountLog.AddTypedWords(20, articleContext, new DateTime(2026, 5, 26));
                DetailedWordCountLog.AddTypedWords(8, trainerContext, new DateTime(2026, 5, 26));

                var snapshot = DetailedWordCountLog.LoadSnapshot(128, new DateTime(2026, 5, 26));

                AssertEqual("article words", 20, snapshot.ArticleWords);
                AssertEqual("trainer words", 8, snapshot.TrainerWords);
            }
            finally
            {
                DetailedWordCountLog.ResetForTests();
                TryDelete(path);
            }
        }

        private static void CategoryChartExcludesHistoryByDefault()
        {
            string path = NewTempPath();
            try
            {
                DetailedWordCountLog.ConfigureForTests(path);
                DetailedWordCountLog.EnsureMigrated(100, new DateTime(2026, 5, 26));

                var context = new TypingWordCountContext(
                    TxtSource.book,
                    "category:book:三国演义",
                    "本地文章 / 三国演义",
                    true,
                    "难");

                DetailedWordCountLog.AddTypedWords(30, context, new DateTime(2026, 5, 27));

                var snapshot = DetailedWordCountLog.LoadSnapshot(130, new DateTime(2026, 5, 27));

                AssertTrue("chart items hide history", snapshot.CategoryChartItems.All(i => i.Key != DetailedWordCountLog.HistoryCategoryKey));
                AssertEqual("chart item count without history", 1, snapshot.CategoryChartItems.Count);
                AssertEqual("chart non-history words", 30, snapshot.CategoryChartItems[0].Words);
            }
            finally
            {
                DetailedWordCountLog.ResetForTests();
                TryDelete(path);
            }
        }

        private static void CategoryDisplayItemsCanMergeSameProject()
        {
            var items = new[]
            {
                new DetailedWordCountItem { Key = DetailedWordCountLog.HistoryCategoryKey, DisplayName = "历史数据", Words = 100, StartDate = "2026-05-25" },
                new DetailedWordCountItem { Key = "category:wenlai:普", DisplayName = "文来 / 普", Words = 20, Attempts = 1, StartDate = "2026-05-26" },
                new DetailedWordCountItem { Key = "category:wenlai:难", DisplayName = "文来 / 难", Words = 30, Attempts = 2, StartDate = "2026-05-27" },
                new DetailedWordCountItem { Key = "category:trainer:1.前500", DisplayName = "晴练单 / 1.前500", Words = 8, Attempts = 0, StartDate = "2026-05-29" },
                new DetailedWordCountItem { Key = "category:trainer:2.中500", DisplayName = "晴练单 / 2.中500", Words = 12, Attempts = 0, StartDate = "2026-05-30" },
                new DetailedWordCountItem { Key = "category:book:三国演义", DisplayName = "本地文章 / 三国演义", Words = 5, Attempts = 0, StartDate = "2026-05-28" }
            };

            var separated = DetailedWordCountLog.BuildCategoryDisplayItems(items, false);
            var merged = DetailedWordCountLog.BuildCategoryDisplayItems(items, true);
            var wenlai = merged.Single(i => i.DisplayName == "文来");
            var trainer = merged.Single(i => i.DisplayName == "练单");

            AssertEqual("separated keeps history", 6, separated.Count);
            AssertTrue("separated includes history", separated.Any(i => i.Key == DetailedWordCountLog.HistoryCategoryKey));
            AssertTrue("separated keeps wenlai difficulty", separated.Any(i => i.DisplayName == "文来 / 普"));
            AssertEqual("merged item count", 4, merged.Count);
            AssertEqual("merged wenlai words", 50, wenlai.Words);
            AssertEqual("merged wenlai attempts", 3, wenlai.Attempts);
            AssertEqual("merged wenlai earliest start", "2026-05-26", wenlai.StartDate);
            AssertEqual("merged trainer words", 20, trainer.Words);
            AssertEqual("merged trainer earliest start", "2026-05-29", trainer.StartDate);
            AssertTrue("merged keeps history as separate item", merged.Any(i => i.Key == DetailedWordCountLog.HistoryCategoryKey));
        }

        private static void DifficultyRowsUseFixedOrder()
        {
            var items = new[]
            {
                new DetailedWordCountItem { Key = "difficulty:难", DisplayName = "难", Words = 30, StartDate = "2026-05-26" },
                new DetailedWordCountItem { Key = "difficulty:水", DisplayName = "水", Words = 10, StartDate = "2026-05-26" }
            };

            var rows = DetailedWordCountLog.BuildDifficultyRows(items);

            AssertEqual("difficulty row count", 6, rows.Count);
            AssertEqual("difficulty row 0", "淼", rows[0].DisplayName);
            AssertEqual("difficulty row 1", "水", rows[1].DisplayName);
            AssertEqual("difficulty row 2", "易", rows[2].DisplayName);
            AssertEqual("difficulty row 3", "普", rows[3].DisplayName);
            AssertEqual("difficulty row 4", "难", rows[4].DisplayName);
            AssertEqual("difficulty row 5", "虐", rows[5].DisplayName);
            AssertEqual("difficulty water words", 10, rows[1].Words);
            AssertEqual("difficulty hard words", 30, rows[4].Words);
        }

        private static string NewTempPath()
        {
            return Path.Combine(Path.GetTempPath(), "typesunny-detailed-word-count-" + Guid.NewGuid().ToString("N") + ".json");
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
                throw new Exception(name + " expected true, got false.");
        }

        private static void AssertEqual(string name, int expected, int actual)
        {
            if (expected != actual)
                throw new Exception(name + " expected [" + expected + "], got [" + actual + "].");
        }

        private static void AssertEqual(string name, string expected, string actual)
        {
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
                throw new Exception(name + " expected [" + expected + "], got [" + actual + "].");
        }
    }
}
