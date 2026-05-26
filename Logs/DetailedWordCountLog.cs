using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Newtonsoft.Json;
using TypeSunny.Core;

namespace TypeSunny.Logs
{
    public static class DetailedWordCountLog
    {
        public const string CategoryDimension = "Category";
        public const string DifficultyDimension = "Difficulty";
        public const string HistoryCategoryKey = "category:history";
        public const string HistoryCategoryDisplayName = "历史数据";

        private const int SchemaVersion = 1;
        private const int SaveDelayMs = 1000;
        private static readonly string[] DifficultyOrder = { "淼", "水", "易", "普", "难", "虐" };
        private static readonly object SyncRoot = new object();
        private static string _storePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "详细字数统计.json");
        private static DetailedWordCountStore _cachedStore;
        private static bool _dirty;
        private static Timer _saveTimer;

        public static void ConfigureForTests(string path)
        {
            lock (SyncRoot)
            {
                FlushLocked();
                _storePath = path;
                _cachedStore = null;
                _dirty = false;
            }
        }

        public static void ResetForTests()
        {
            lock (SyncRoot)
            {
                FlushLocked();
                _storePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "详细字数统计.json");
                _cachedStore = null;
                _dirty = false;
            }
        }

        public static void EnsureMigrated(int legacyTotalWords, DateTime now)
        {
            lock (SyncRoot)
            {
                var store = LoadStoreLocked();
                if (store.MigratedLegacyTotalWords)
                    return;

                store.SchemaVersion = SchemaVersion;
                store.MigratedLegacyTotalWords = true;
                store.MigrationDate = ToDateString(now);
                if (legacyTotalWords > 0)
                    RecordTypingDate(store, store.MigrationDate);

                if (legacyTotalWords > 0)
                {
                    var history = FindOrCreateItem(
                        store.CategoryItems,
                        CategoryDimension,
                        HistoryCategoryKey,
                        HistoryCategoryDisplayName,
                        ToDateString(now));

                    history.Words += legacyTotalWords;
                    history.LastUpdateTime = now;
                }

                store.LastUpdateTime = now;
                SaveStoreLocked(store);
            }
        }

        public static void AddTypedWords(int words, TypingWordCountContext context)
        {
            AddTypedWords(words, context, DateTime.Now);
        }

        public static void AddTypedWords(int words, TypingWordCountContext context, DateTime now)
        {
            if (words <= 0 || context == null || string.IsNullOrWhiteSpace(context.CategoryKey))
                return;

            lock (SyncRoot)
            {
                var store = LoadStoreLocked();
                string today = ToDateString(now);
                RecordTypingDate(store, today);

                var category = FindOrCreateItem(
                    store.CategoryItems,
                    CategoryDimension,
                    context.CategoryKey,
                    context.CategoryDisplayName,
                    today);
                category.Words += words;
                if (context.TryConsumeAttempt())
                    category.Attempts += 1;
                category.LastUpdateTime = now;

                if (context.IncludeDifficulty && !string.IsNullOrWhiteSpace(context.DifficultyLabel))
                {
                    string difficultyLabel = NormalizeDifficultyLabel(context.DifficultyLabel);
                    if (!string.IsNullOrWhiteSpace(difficultyLabel))
                    {
                        var difficulty = FindOrCreateItem(
                            store.DifficultyItems,
                            DifficultyDimension,
                            "difficulty:" + difficultyLabel,
                            difficultyLabel,
                            today);
                        difficulty.Words += words;
                        difficulty.LastUpdateTime = now;
                    }
                }

                store.LastUpdateTime = now;
                RequestSaveLocked(store);
            }
        }

        public static DetailedWordCountSnapshot LoadSnapshot(int totalWords, DateTime now)
        {
            lock (SyncRoot)
            {
                FlushLocked();
                var store = LoadStoreLocked();
                int categoryTotal = store.CategoryItems.Sum(i => i.Words);
                if (categoryTotal < totalWords)
                {
                    int diff = totalWords - categoryTotal;
                    var history = FindOrCreateItem(
                        store.CategoryItems,
                        CategoryDimension,
                        HistoryCategoryKey,
                        HistoryCategoryDisplayName,
                        ToDateString(now));
                    history.Words += diff;
                    history.LastUpdateTime = now;
                    RecordTypingDate(store, ToDateString(now));
                    store.LastUpdateTime = now;
                    SaveStoreLocked(store);
                    categoryTotal = store.CategoryItems.Sum(i => i.Words);
                }

                return BuildSnapshot(store, totalWords);
            }
        }

        public static void Flush()
        {
            lock (SyncRoot)
            {
                FlushLocked();
            }
        }

        public static List<DetailedWordCountChartItem> BuildCategoryChartItems(IEnumerable<DetailedWordCountItem> items, int maxItems)
        {
            if (items == null)
                return new List<DetailedWordCountChartItem>();

            if (maxItems <= 0)
                maxItems = 8;

            var ordered = items
                .Where(i => i != null && i.Words > 0)
                .OrderByDescending(i => i.Words)
                .ThenBy(i => i.DisplayName, StringComparer.Ordinal)
                .ToList();

            var result = ordered
                .Take(maxItems)
                .Select(i => new DetailedWordCountChartItem
                {
                    Key = i.Key,
                    DisplayName = FormatCategoryDisplayName(i.Key, i.DisplayName),
                    Words = i.Words
                })
                .ToList();

            int otherWords = ordered.Skip(maxItems).Sum(i => i.Words);
            if (otherWords > 0)
            {
                result.Add(new DetailedWordCountChartItem
                {
                    Key = "category:chart:other",
                    DisplayName = "其他",
                    Words = otherWords
                });
            }

            return result;
        }

        public static List<DetailedWordCountItem> BuildCategoryDisplayItems(IEnumerable<DetailedWordCountItem> items, bool mergeSameProject)
        {
            var visibleItems = CloneItems(items)
                .Where(i => i.Words > 0)
                .Select(NormalizeCategoryDisplayItem)
                .ToList();

            if (!mergeSameProject)
                return SortCategoryItems(visibleItems);

            return visibleItems
                .GroupBy(GetCategoryProjectKey)
                .Select(g => new DetailedWordCountItem
                {
                    Dimension = CategoryDimension,
                    Key = g.Key,
                    DisplayName = GetCategoryProjectDisplayName(g.First()),
                    Words = g.Sum(i => i.Words),
                    Attempts = g.Sum(i => i.Attempts),
                    StartDate = GetEarliestStartDate(g.ToList()),
                    LastUpdateTime = g.Max(i => i.LastUpdateTime)
                })
                .OrderByDescending(i => i.Words)
                .ThenBy(i => i.DisplayName, StringComparer.Ordinal)
                .ToList();
        }

        public static List<DetailedWordCountItem> BuildDifficultyRows(IEnumerable<DetailedWordCountItem> items)
        {
            var byLabel = (items ?? Enumerable.Empty<DetailedWordCountItem>())
                .Where(i => i != null)
                .GroupBy(i => NormalizeDifficultyLabel(i.DisplayName))
                .ToDictionary(g => g.Key, g => g.Sum(i => i.Words));

            var result = new List<DetailedWordCountItem>();
            foreach (string label in DifficultyOrder)
            {
                int words = byLabel.ContainsKey(label) ? byLabel[label] : 0;
                result.Add(new DetailedWordCountItem
                {
                    Dimension = DifficultyDimension,
                    Key = "difficulty:" + label,
                    DisplayName = label,
                    Words = words,
                    StartDate = FindDifficultyStartDate(items, label)
                });
            }

            return result;
        }

        public static string NormalizeDifficultyLabel(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "";

            string trimmed = value.Trim();
            foreach (string label in DifficultyOrder)
            {
                if (trimmed.StartsWith(label, StringComparison.Ordinal))
                    return label;
            }

            return "";
        }

        private static DetailedWordCountSnapshot BuildSnapshot(DetailedWordCountStore store, int totalWords)
        {
            var categoryItems = CloneItems(store.CategoryItems)
                .OrderByDescending(i => i.Key == HistoryCategoryKey)
                .ThenByDescending(i => i.Words)
                .ThenBy(i => i.DisplayName, StringComparer.Ordinal)
                .ToList();
            var visibleCategoryItems = BuildCategoryDisplayItems(categoryItems, false);

            var difficultyRows = BuildDifficultyRows(store.DifficultyItems);
            int categoryTotal = categoryItems.Sum(i => i.Words);
            int difficultyTotal = difficultyRows.Sum(i => i.Words);
            int nonHistoryWords = categoryItems
                .Where(i => i.Key != HistoryCategoryKey)
                .Sum(i => i.Words);
            int trainerWords = categoryItems
                .Where(i => IsTrainerCategoryKey(i.Key))
                .Sum(i => i.Words);
            int raceAttemptCount = categoryItems
                .Where(i => IsRaceCategoryKey(i.Key))
                .Sum(i => i.Attempts);

            return new DetailedWordCountSnapshot
            {
                TotalWords = totalWords,
                CategoryTotalWords = categoryTotal,
                DifficultyTotalWords = difficultyTotal,
                ArticleWords = Math.Max(0, nonHistoryWords - trainerWords),
                TrainerWords = trainerWords,
                RaceAttemptCount = raceAttemptCount,
                TypingDays = store.TypingDates.Count,
                Difference = categoryTotal - totalWords,
                IsAligned = categoryTotal == totalWords,
                CategoryItems = categoryItems,
                VisibleCategoryItems = visibleCategoryItems,
                DifficultyItems = difficultyRows,
                CategoryChartItems = BuildCategoryChartItems(
                    visibleCategoryItems.Where(i => i.Key != HistoryCategoryKey),
                    8),
                LastUpdateTime = store.LastUpdateTime,
                StartDate = GetEarliestStartDate(categoryItems)
            };
        }

        private static List<DetailedWordCountItem> SortCategoryItems(IEnumerable<DetailedWordCountItem> items)
        {
            return (items ?? Enumerable.Empty<DetailedWordCountItem>())
                .OrderByDescending(i => i.Words)
                .ThenBy(i => i.DisplayName, StringComparer.Ordinal)
                .ToList();
        }

        private static string GetCategoryProjectKey(DetailedWordCountItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Key))
                return "category:unknown";

            if (item.Key.StartsWith("category:wenlai:", StringComparison.Ordinal))
                return "category:wenlai";

            if (item.Key.StartsWith("category:trainer:", StringComparison.Ordinal))
                return "category:trainer";

            if (item.Key.StartsWith("category:race:", StringComparison.Ordinal)
                || item.Key.StartsWith("category:raceapi:", StringComparison.Ordinal))
                return "category:race";

            return item.Key;
        }

        private static string GetCategoryProjectDisplayName(DetailedWordCountItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Key))
                return "未分类";

            if (item.Key.StartsWith("category:wenlai:", StringComparison.Ordinal))
                return "文来";

            if (item.Key.StartsWith("category:trainer:", StringComparison.Ordinal))
                return "练单";

            if (item.Key.StartsWith("category:race:", StringComparison.Ordinal)
                || item.Key.StartsWith("category:raceapi:", StringComparison.Ordinal))
                return "赛文";

            return item.DisplayName;
        }

        public static string FormatTrainerCategoryDisplayName(string title)
        {
            return "练单 / " + GetTrainerExerciseDisplayName(title);
        }

        private static DetailedWordCountItem NormalizeCategoryDisplayItem(DetailedWordCountItem item)
        {
            item.DisplayName = FormatCategoryDisplayName(item.Key, item.DisplayName);
            return item;
        }

        private static string FormatCategoryDisplayName(string key, string displayName)
        {
            if (!IsTrainerCategoryKey(key))
                return displayName;

            string title = ExtractCategoryDetailName(displayName);
            return FormatTrainerCategoryDisplayName(title);
        }

        private static string ExtractCategoryDetailName(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                return "";

            int separator = displayName.IndexOf(" / ", StringComparison.Ordinal);
            return separator >= 0
                ? displayName.Substring(separator + 3)
                : displayName;
        }

        private static string GetTrainerExerciseDisplayName(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return "未命名";

            string trimmed = title.Trim();
            var match = Regex.Match(trimmed, @"^\d+\.\s*");
            return match.Success
                ? trimmed.Substring(match.Length)
                : trimmed;
        }

        private static string GetEarliestStartDate(List<DetailedWordCountItem> items)
        {
            return items
                .Where(i => !string.IsNullOrWhiteSpace(i.StartDate))
                .Select(i => i.StartDate)
                .OrderBy(i => i, StringComparer.Ordinal)
                .FirstOrDefault() ?? "";
        }

        private static string FindDifficultyStartDate(IEnumerable<DetailedWordCountItem> items, string label)
        {
            return (items ?? Enumerable.Empty<DetailedWordCountItem>())
                .Where(i => string.Equals(NormalizeDifficultyLabel(i.DisplayName), label, StringComparison.Ordinal)
                    && !string.IsNullOrWhiteSpace(i.StartDate))
                .Select(i => i.StartDate)
                .OrderBy(i => i, StringComparer.Ordinal)
                .FirstOrDefault() ?? "";
        }

        private static bool IsRaceCategoryKey(string key)
        {
            return !string.IsNullOrWhiteSpace(key)
                && key.StartsWith("category:race", StringComparison.Ordinal);
        }

        private static bool IsTrainerCategoryKey(string key)
        {
            return !string.IsNullOrWhiteSpace(key)
                && key.StartsWith("category:trainer:", StringComparison.Ordinal);
        }

        private static List<DetailedWordCountItem> CloneItems(IEnumerable<DetailedWordCountItem> items)
        {
            return (items ?? Enumerable.Empty<DetailedWordCountItem>())
                .Select(i => new DetailedWordCountItem
                {
                    Dimension = i.Dimension,
                    Key = i.Key,
                    DisplayName = i.DisplayName,
                    Words = i.Words,
                    Attempts = i.Attempts,
                    StartDate = i.StartDate,
                    LastUpdateTime = i.LastUpdateTime
                })
                .ToList();
        }

        private static DetailedWordCountItem FindOrCreateItem(
            List<DetailedWordCountItem> items,
            string dimension,
            string key,
            string displayName,
            string startDate)
        {
            var item = items.FirstOrDefault(i => string.Equals(i.Key, key, StringComparison.Ordinal));
            if (item != null)
            {
                if (string.IsNullOrWhiteSpace(item.DisplayName))
                    item.DisplayName = displayName;
                if (string.IsNullOrWhiteSpace(item.StartDate))
                    item.StartDate = startDate;
                if (string.IsNullOrWhiteSpace(item.Dimension))
                    item.Dimension = dimension;
                return item;
            }

            item = new DetailedWordCountItem
            {
                Dimension = dimension,
                Key = key,
                DisplayName = displayName,
                StartDate = startDate,
                Words = 0,
                Attempts = 0
            };
            items.Add(item);
            return item;
        }

        private static DetailedWordCountStore LoadStoreLocked()
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
                        var store = JsonConvert.DeserializeObject<DetailedWordCountStore>(json);
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
                System.Diagnostics.Debug.WriteLine($"加载详细字数统计失败: {ex.Message}");
            }

            var empty = new DetailedWordCountStore { SchemaVersion = SchemaVersion };
            NormalizeStore(empty);
            _cachedStore = empty;
            return _cachedStore;
        }

        private static void NormalizeStore(DetailedWordCountStore store)
        {
            store.SchemaVersion = store.SchemaVersion <= 0 ? SchemaVersion : store.SchemaVersion;
            if (store.CategoryItems == null)
                store.CategoryItems = new List<DetailedWordCountItem>();
            if (store.DifficultyItems == null)
                store.DifficultyItems = new List<DetailedWordCountItem>();
            if (store.TypingDates == null)
                store.TypingDates = new List<string>();
            store.TypingDates = store.TypingDates
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(d => d, StringComparer.Ordinal)
                .ToList();
        }

        private static void RecordTypingDate(DetailedWordCountStore store, string date)
        {
            if (store == null || string.IsNullOrWhiteSpace(date))
                return;

            if (store.TypingDates == null)
                store.TypingDates = new List<string>();

            if (!store.TypingDates.Contains(date, StringComparer.Ordinal))
                store.TypingDates.Add(date);
        }

        private static void RequestSaveLocked(DetailedWordCountStore store)
        {
            _cachedStore = store;
            _dirty = true;

            if (_saveTimer == null)
                _saveTimer = new Timer(SaveTimerCallback, null, SaveDelayMs, Timeout.Infinite);
            else
                _saveTimer.Change(SaveDelayMs, Timeout.Infinite);
        }

        private static void SaveStoreLocked(DetailedWordCountStore store)
        {
            try
            {
                string dir = Path.GetDirectoryName(_storePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var settings = new JsonSerializerSettings { Formatting = Formatting.Indented };
                File.WriteAllText(_storePath, JsonConvert.SerializeObject(store, settings), Encoding.UTF8);
                _cachedStore = store;
                _dirty = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存详细字数统计失败: {ex.Message}");
            }
        }

        private static void FlushLocked()
        {
            if (_saveTimer != null)
                _saveTimer.Change(Timeout.Infinite, Timeout.Infinite);

            if (_dirty && _cachedStore != null)
                SaveStoreLocked(_cachedStore);
        }

        private static void SaveTimerCallback(object state)
        {
            lock (SyncRoot)
            {
                FlushLocked();
            }
        }

        private static string ToDateString(DateTime value)
        {
            return value.ToString("yyyy-MM-dd");
        }
    }

    public sealed class TypingWordCountContext
    {
        public TypingWordCountContext(
            TxtSource source,
            string categoryKey,
            string categoryDisplayName,
            bool includeDifficulty,
            string difficultyLabel)
        {
            Source = source;
            CategoryKey = categoryKey;
            CategoryDisplayName = categoryDisplayName;
            IncludeDifficulty = includeDifficulty;
            DifficultyLabel = difficultyLabel;
        }

        public TypingWordCountContext(
            TxtSource source,
            string categoryKey,
            string categoryDisplayName,
            bool includeDifficulty,
            string difficultyLabel,
            bool countAttempt)
            : this(source, categoryKey, categoryDisplayName, includeDifficulty, difficultyLabel)
        {
            CountAttempt = countAttempt;
        }

        public TxtSource Source { get; private set; }
        public string CategoryKey { get; private set; }
        public string CategoryDisplayName { get; private set; }
        public bool IncludeDifficulty { get; private set; }
        public string DifficultyLabel { get; private set; }
        public bool CountAttempt { get; private set; }

        internal bool TryConsumeAttempt()
        {
            if (!CountAttempt || attemptConsumed)
                return false;

            attemptConsumed = true;
            return true;
        }

        private bool attemptConsumed;
    }

    public sealed class DetailedWordCountStore
    {
        public int SchemaVersion { get; set; }
        public bool MigratedLegacyTotalWords { get; set; }
        public string MigrationDate { get; set; }
        public DateTime LastUpdateTime { get; set; }
        public List<string> TypingDates { get; set; } = new List<string>();
        public List<DetailedWordCountItem> CategoryItems { get; set; } = new List<DetailedWordCountItem>();
        public List<DetailedWordCountItem> DifficultyItems { get; set; } = new List<DetailedWordCountItem>();
    }

    public sealed class DetailedWordCountItem
    {
        public string Dimension { get; set; }
        public string Key { get; set; }
        public string DisplayName { get; set; }
        public int Words { get; set; }
        public int Attempts { get; set; }
        public string StartDate { get; set; }
        public DateTime LastUpdateTime { get; set; }
    }

    public sealed class DetailedWordCountChartItem
    {
        public string Key { get; set; }
        public string DisplayName { get; set; }
        public int Words { get; set; }
    }

    public sealed class DetailedWordCountSnapshot
    {
        public int TotalWords { get; set; }
        public int CategoryTotalWords { get; set; }
        public int DifficultyTotalWords { get; set; }
        public int ArticleWords { get; set; }
        public int TrainerWords { get; set; }
        public int RaceAttemptCount { get; set; }
        public int TypingDays { get; set; }
        public int Difference { get; set; }
        public bool IsAligned { get; set; }
        public string StartDate { get; set; }
        public DateTime LastUpdateTime { get; set; }
        public List<DetailedWordCountItem> CategoryItems { get; set; } = new List<DetailedWordCountItem>();
        public List<DetailedWordCountItem> VisibleCategoryItems { get; set; } = new List<DetailedWordCountItem>();
        public List<DetailedWordCountItem> DifficultyItems { get; set; } = new List<DetailedWordCountItem>();
        public List<DetailedWordCountChartItem> CategoryChartItems { get; set; } = new List<DetailedWordCountChartItem>();
    }
}
