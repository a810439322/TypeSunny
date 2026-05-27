using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace TypeSunny
{
    internal static class TrainerTitleWordStats
    {
        private static readonly object SyncRoot = new object();
        private static string _storePath = Path.Combine("练单日志", "title-words.json");
        private static TrainerTitleWordStatsStore _cachedStore;

        public static TrainerTitleWordStatsSnapshot Read()
        {
            return Read(DateTime.Now);
        }

        public static TrainerTitleWordStatsSnapshot Read(DateTime now)
        {
            lock (SyncRoot)
            {
                var store = LoadStoreLocked();
                MigrateFromTrainerLogIfNeededLocked(store);
                SaveStoreLocked(store);
                return CreateSnapshot(store, now);
            }
        }

        public static TrainerTitleWordStatsSnapshot AddWords(int words)
        {
            return AddWords(words, DateTime.Now);
        }

        public static TrainerTitleWordStatsSnapshot AddWords(int words, DateTime now)
        {
            lock (SyncRoot)
            {
                var store = LoadStoreLocked();
                MigrateFromTrainerLogIfNeededLocked(store);

                if (words > 0)
                {
                    string today = ToDateString(now);
                    if (!store.DailyWords.ContainsKey(today))
                        store.DailyWords[today] = 0;
                    store.DailyWords[today] += words;
                    store.TotalWords += words;
                    store.LastUpdateTime = now;
                }

                SaveStoreLocked(store);
                return CreateSnapshot(store, now);
            }
        }

        public static TrainerTitleWordStatsSnapshot EnsureTotalAtLeast(int minimumTotalWords)
        {
            return EnsureTotalAtLeast(minimumTotalWords, DateTime.Now);
        }

        public static TrainerTitleWordStatsSnapshot EnsureTotalAtLeast(int minimumTotalWords, DateTime now)
        {
            lock (SyncRoot)
            {
                var store = LoadStoreLocked();
                MigrateFromTrainerLogIfNeededLocked(store);

                if (minimumTotalWords > store.TotalWordsFloor)
                    store.TotalWordsFloor = minimumTotalWords;

                int dailyTotal = store.DailyWords.Sum(kvp => kvp.Value);
                int resolvedTotal = Math.Max(dailyTotal, store.TotalWordsFloor);
                if (resolvedTotal > store.TotalWords)
                {
                    store.TotalWords = resolvedTotal;
                    store.LastUpdateTime = now;
                    SaveStoreLocked(store);
                }

                return CreateSnapshot(store, now);
            }
        }

        public static void ConfigureForTests(string path)
        {
            lock (SyncRoot)
            {
                _storePath = path;
                _cachedStore = null;
            }
        }

        public static void ResetForTests()
        {
            lock (SyncRoot)
            {
                _storePath = Path.Combine("练单日志", "title-words.json");
                _cachedStore = null;
            }
        }

        private static void MigrateFromTrainerLogIfNeededLocked(TrainerTitleWordStatsStore store)
        {
            if (store.MigratedFromTrainerLog)
                return;

            var migratedDaily = TrainerLog.ReadDailyInputWordTotals();
            foreach (var item in migratedDaily)
            {
                if (!store.DailyWords.ContainsKey(item.Key))
                    store.DailyWords[item.Key] = 0;
                store.DailyWords[item.Key] += item.Value;
            }

            store.TotalWords = store.DailyWords.Sum(kvp => kvp.Value);
            store.MigratedFromTrainerLog = true;
            store.MigrationTime = DateTime.Now;
            store.LastUpdateTime = DateTime.Now;
        }

        private static TrainerTitleWordStatsSnapshot CreateSnapshot(TrainerTitleWordStatsStore store, DateTime now)
        {
            string today = ToDateString(now);
            int todayWords = store.DailyWords.TryGetValue(today, out int words) ? words : 0;

            return new TrainerTitleWordStatsSnapshot
            {
                TodayWords = todayWords,
                TotalWords = store.TotalWords
            };
        }

        private static TrainerTitleWordStatsStore LoadStoreLocked()
        {
            if (_cachedStore != null)
                return _cachedStore;

            try
            {
                if (File.Exists(_storePath))
                {
                    string json = File.ReadAllText(_storePath, Encoding.UTF8);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        var store = JsonConvert.DeserializeObject<TrainerTitleWordStatsStore>(json);
                        if (store != null)
                        {
                            NormalizeStore(store);
                            _cachedStore = store;
                            return _cachedStore;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"读取晴练单标题统计失败: {ex.Message}");
            }

            var empty = new TrainerTitleWordStatsStore();
            NormalizeStore(empty);
            _cachedStore = empty;
            return _cachedStore;
        }

        private static void SaveStoreLocked(TrainerTitleWordStatsStore store)
        {
            try
            {
                string dir = Path.GetDirectoryName(_storePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                File.WriteAllText(
                    _storePath,
                    JsonConvert.SerializeObject(store, Formatting.Indented),
                    Encoding.UTF8);
                _cachedStore = store;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存晴练单标题统计失败: {ex.Message}");
            }
        }

        private static void NormalizeStore(TrainerTitleWordStatsStore store)
        {
            if (store.DailyWords == null)
                store.DailyWords = new Dictionary<string, int>(StringComparer.Ordinal);
            else
                store.DailyWords = new Dictionary<string, int>(store.DailyWords, StringComparer.Ordinal);

            int dailyTotal = store.DailyWords.Sum(kvp => kvp.Value);
            store.TotalWords = Math.Max(store.TotalWords, Math.Max(dailyTotal, store.TotalWordsFloor));
        }

        private static string ToDateString(DateTime value)
        {
            return value.ToString("yyyy-MM-dd");
        }
    }

    internal sealed class TrainerTitleWordStatsSnapshot
    {
        public int TodayWords { get; set; }
        public int TotalWords { get; set; }
    }

    internal sealed class TrainerTitleWordStatsStore
    {
        public bool MigratedFromTrainerLog { get; set; }
        public DateTime MigrationTime { get; set; }
        public DateTime LastUpdateTime { get; set; }
        public int TotalWords { get; set; }
        public int TotalWordsFloor { get; set; }
        public Dictionary<string, int> DailyWords { get; set; } = new Dictionary<string, int>(StringComparer.Ordinal);
    }
}
