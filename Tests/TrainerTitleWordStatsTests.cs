using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TypeSunny.Logs;

namespace TypeSunny.Tests
{
    internal static class TrainerTitleWordStatsTests
    {
        private static int Main()
        {
            string originalDirectory = Directory.GetCurrentDirectory();
            string dataDir = Path.Combine(Path.GetTempPath(), "typesunny-trainer-title-stats-" + Guid.NewGuid().ToString("N"));

            try
            {
                Directory.CreateDirectory(dataDir);
                Directory.SetCurrentDirectory(dataDir);

                string statsPath = Path.Combine(dataDir, "title-words.json");
                TrainerTitleWordStats.ConfigureForTests(statsPath);

                SeedTrainerSummary();

                var first = TrainerTitleWordStats.Read(new DateTime(2026, 5, 26, 8, 0, 0));
                AssertEqual("migrated today words", 7, first.TodayWords);
                AssertEqual("migrated total words", 17, first.TotalWords);

                var afterAdd = TrainerTitleWordStats.AddWords(3, new DateTime(2026, 5, 26, 9, 0, 0));
                AssertEqual("today after add", 10, afterAdd.TodayWords);
                AssertEqual("total after add", 20, afterAdd.TotalWords);

                TrainerTitleWordStats.ResetForTests();
                TrainerTitleWordStats.ConfigureForTests(statsPath);

                var reloaded = TrainerTitleWordStats.Read(new DateTime(2026, 5, 26, 10, 0, 0));
                AssertEqual("reloaded today does not migrate twice", 10, reloaded.TodayWords);
                AssertEqual("reloaded total does not migrate twice", 20, reloaded.TotalWords);

                var synced = TrainerTitleWordStats.EnsureTotalAtLeast(35, new DateTime(2026, 5, 26, 11, 0, 0));
                AssertEqual("sync raises total to detailed trainer words", 35, synced.TotalWords);
                AssertEqual("sync does not pollute today words", 10, synced.TodayWords);

                var unchanged = TrainerTitleWordStats.EnsureTotalAtLeast(30, new DateTime(2026, 5, 26, 12, 0, 0));
                AssertEqual("sync does not lower total", 35, unchanged.TotalWords);
                AssertEqual("sync still does not pollute today words", 10, unchanged.TodayWords);

                Console.WriteLine("All TrainerTitleWordStats tests passed.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
            finally
            {
                TrainerTitleWordStats.ResetForTests();
                Directory.SetCurrentDirectory(originalDirectory);
                TryDelete(dataDir);
            }
        }

        private static void SeedTrainerSummary()
        {
            Directory.CreateDirectory("练单日志");
            var summary = new TrainerLog.DailyStatisticsData();
            summary.DailySummaries.Add(BuildDay("2026-05-25", 10));
            summary.DailySummaries.Add(BuildDay("2026-05-26", 7));

            File.WriteAllText(
                Path.Combine("练单日志", "summary.json"),
                JsonConvert.SerializeObject(summary));
        }

        private static ArticleLog.StatisticsData BuildDay(string date, int inputWords)
        {
            return new ArticleLog.StatisticsData
            {
                Date = date,
                Summaries =
                {
                    new ArticleLog.StatisticsSummary
                    {
                        GroupKey = "sample",
                        TotalWords = inputWords,
                        TotalInputWords = inputWords
                    }
                }
            };
        }

        private static void AssertEqual(string name, int expected, int actual)
        {
            if (actual != expected)
                throw new Exception(name + ": expected " + expected + ", got " + actual + ".");
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
            }
            catch
            {
            }
        }
    }
}
