using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace TypeSunny.Logs
{
    /// <summary>
    /// 文来文章日志记录（使用后台线程队列）
    /// </summary>
    public static class WenlaiLog
    {
        private const string LogFolder = "文来日志";
        private const int MinWordCount = 5; // 最少字数，不超过此数不计入

        // 队列和锁
        private static readonly Queue<ArticleLog.ArticleRecord> _recordQueue = new Queue<ArticleLog.ArticleRecord>();
        private static readonly object _queueLock = new object();
        private static readonly object _writeLock = new object();

        // 后台线程控制
        private static Thread _writeThread;
        private static bool _isRunning = true;
        private static readonly AutoResetEvent _hasRecords = new AutoResetEvent(false);

        static WenlaiLog()
        {
            // 启动后台写入线程
            _writeThread = new Thread(WriteLoop)
            {
                IsBackground = true,
                Name = "WenlaiLogWriter"
            };
            _writeThread.Start();
        }

        /// <summary>
        /// 后台写入线程主循环
        /// </summary>
        private static void WriteLoop()
        {
            while (_isRunning)
            {
                // 等待队列中有记录
                _hasRecords.WaitOne();

                // 处理所有待写入的记录
                while (true)
                {
                    ArticleLog.ArticleRecord record;
                    lock (_queueLock)
                    {
                        if (_recordQueue.Count == 0)
                            break;
                        record = _recordQueue.Dequeue();
                    }

                    // 写入单条记录
                    WriteRecordToFile(record);
                }
            }
        }

        /// <summary>
        /// 将记录写入文件（内部方法）- 使用单文件存储，按 Date 字段区分天数
        /// </summary>
        private static void WriteRecordToFile(ArticleLog.ArticleRecord record)
        {
            lock (_writeLock)
            {
                try
                {
                    EnsureLogDirectory();

                    // 获取今天的日期
                    string today = DateTime.Now.ToString("yyyy-MM-dd");

                    // 1. 读取所有统计数据和最近记录
                    var allStatistics = LoadAllStatistics();
                    var recentData = LoadRecentData() ?? new ArticleLog.RecentRecords();

                    // 2. 获取或创建今天的统计数据
                    var todayData = GetStatisticsForDate(allStatistics, today);

                    // 3. 获取分组键（DifficultyName 用于文来日志）
                    string groupKey = string.IsNullOrWhiteSpace(record.DifficultyName) ? "未分类" : record.DifficultyName;

                    // 4. 查找或创建分组汇总
                    var summary = todayData.Summaries.FirstOrDefault(s => s.GroupKey == groupKey);
                    if (summary == null)
                    {
                        summary = new ArticleLog.StatisticsSummary
                        {
                            GroupKey = groupKey,
                            MaxSpeed = record.Speed,
                            MinSpeed = record.Speed
                        };
                        todayData.Summaries.Add(summary);
                    }

                    // 5. 增量更新统计数据
                    summary.Count++;
                    summary.SumSpeedWeighted += record.Speed * record.TotalWords;
                    summary.SumHitRateWeighted += record.HitRate * record.TotalWords;
                    summary.SumAccuracyWeighted += record.Accuracy * record.TotalWords;
                    summary.SumKPWWeighted += record.KPW * record.TotalWords;
                    summary.SumCiRatioWeighted += record.CiRatio * record.TotalWords;
                    summary.SumCorrection += record.Correction;
                    summary.TotalBacks += record.Backs;
                    summary.TotalWords += record.TotalWords;
                    summary.MaxSpeed = Math.Max(summary.MaxSpeed, record.Speed);
                    summary.MinSpeed = Math.Min(summary.MinSpeed, record.Speed);
                    summary.LastUpdateTime = DateTime.Now;

                    // 6. 更新或添加今天的数据到列表中
                    var existingIndex = allStatistics.DailySummaries.FindIndex(d => d.Date == today);
                    if (existingIndex >= 0)
                    {
                        allStatistics.DailySummaries[existingIndex] = todayData;
                    }
                    else
                    {
                        allStatistics.DailySummaries.Add(todayData);
                    }

                    // 7. 添加到最近记录并清理
                    recentData.Records.Add(record);
                    CleanOldRecords(recentData);
                    recentData.LastUpdateTime = DateTime.Now;

                    // 8. 定期删除旧的按日期的详细记录文件
                    if (ShouldCleanOldDetailFiles(todayData))
                    {
                        CleanOldDetailFiles();
                        todayData.LastCleanTime = DateTime.Now;
                    }

                    // 9. 保存统计数据和最近记录
                    SaveAllStatistics(allStatistics);
                    SaveRecentData(recentData);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"写入文来日志失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 确保日志目录存在
        /// </summary>
        private static void EnsureLogDirectory()
        {
            if (!Directory.Exists(LogFolder))
            {
                Directory.CreateDirectory(LogFolder);
            }
        }

        /// <summary>
        /// 获取指定日期的日志文件路径
        /// </summary>
        private static string GetLogFilePath(DateTime date)
        {
            return Path.Combine(LogFolder, date.ToString("yyyy-MM-dd") + ".json");
        }

        /// <summary>
        /// 记录文来文章数据（异步，加入队列）
        /// </summary>
        public static void WriteRecord(ArticleLog.ArticleRecord record)
        {
            // 过滤不超过5个字的记录
            if (record.TotalWords < MinWordCount)
                return;

            lock (_queueLock)
            {
                _recordQueue.Enqueue(record);
            }

            // 通知后台线程有新记录
            _hasRecords.Set();
        }

        /// <summary>
        /// 刷新队列，确保所有记录都被写入（程序退出时调用）
        /// </summary>
        public static void Flush()
        {
            // 等待队列清空
            while (true)
            {
                int count;
                lock (_queueLock)
                {
                    count = _recordQueue.Count;
                }

                if (count == 0)
                    break;

                // 等待一下，让后台线程处理
                Thread.Sleep(50);
            }

            // 再等待一下确保写入完成
            Thread.Sleep(100);
        }

        /// <summary>
        /// 停止后台线程（程序退出时调用）
        /// </summary>
        public static void Shutdown()
        {
            _isRunning = false;
            _hasRecords.Set();

            if (_writeThread != null && _writeThread.IsAlive)
            {
                _writeThread.Join(1000); // 等待最多1秒
            }
        }

        /// <summary>
        /// 读取指定日期的记录
        /// </summary>
        public static List<ArticleLog.ArticleRecord> ReadRecords(DateTime date)
        {
            List<ArticleLog.ArticleRecord> records = new List<ArticleLog.ArticleRecord>();

            try
            {
                string logFile = GetLogFilePath(date);
                if (!File.Exists(logFile))
                    return records;

                string json = File.ReadAllText(logFile, Encoding.UTF8);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    records = JsonConvert.DeserializeObject<List<ArticleLog.ArticleRecord>>(json);
                    records ??= new List<ArticleLog.ArticleRecord>();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"读取文来日志失败: {ex.Message}");
            }

            return records;
        }

        /// <summary>
        /// 读取日期范围内的所有记录
        /// </summary>
        public static List<ArticleLog.ArticleRecord> ReadRecordsInRange(DateTime startDate, DateTime endDate)
        {
            List<ArticleLog.ArticleRecord> allRecords = new List<ArticleLog.ArticleRecord>();

            for (DateTime date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
            {
                string logFile = GetLogFilePath(date);
                if (File.Exists(logFile))
                {
                    try
                    {
                        string json = File.ReadAllText(logFile, Encoding.UTF8);
                        if (!string.IsNullOrWhiteSpace(json))
                        {
                            var records = JsonConvert.DeserializeObject<List<ArticleLog.ArticleRecord>>(json);
                            if (records != null)
                                allRecords.AddRange(records);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"读取文来日志失败 ({date:yyyy-MM-dd}): {ex.Message}");
                    }
                }
            }

            return allRecords;
        }

        /// <summary>
        /// 获取指定文章名的所有记录
        /// </summary>
        public static List<ArticleLog.ArticleRecord> GetRecordsByArticleName(string articleName)
        {
            List<ArticleLog.ArticleRecord> result = new List<ArticleLog.ArticleRecord>();

            if (!Directory.Exists(LogFolder))
                return result;

            try
            {
                foreach (string file in Directory.GetFiles(LogFolder, "*.json"))
                {
                    try
                    {
                        string json = File.ReadAllText(file, Encoding.UTF8);
                        if (!string.IsNullOrWhiteSpace(json))
                        {
                            var records = JsonConvert.DeserializeObject<List<ArticleLog.ArticleRecord>>(json);
                            if (records != null)
                            {
                                foreach (var record in records)
                                {
                                    if (record.ArticleName == articleName)
                                        result.Add(record);
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"读取文来日志失败: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// 获取所有日期（有记录的日期）
        /// </summary>
        public static List<DateTime> GetAvailableDates()
        {
            List<DateTime> dates = new List<DateTime>();

            if (!Directory.Exists(LogFolder))
                return dates;

            try
            {
                foreach (string file in Directory.GetFiles(LogFolder, "*.json"))
                {
                    string fileName = Path.GetFileNameWithoutExtension(file);
                    if (DateTime.TryParseExact(fileName, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out DateTime date))
                    {
                        dates.Add(date);
                    }
                }
            }
            catch { }

            dates.Sort();
            return dates;
        }

        #region 新增：统计数据方法

        /// <summary>
        /// 所有统计数据容器（单文件存储）
        /// </summary>
        public class DailyStatisticsData
        {
            public List<ArticleLog.StatisticsData> DailySummaries { get; set; } = new List<ArticleLog.StatisticsData>();
        }

        private static string GetSummaryFilePath()
        {
            return Path.Combine(LogFolder, "summary.json");
        }
        private static string GetRecentFilePath() => Path.Combine(LogFolder, "recent.json");

        /// <summary>
        /// 加载所有统计数据（单文件）
        /// </summary>
        private static DailyStatisticsData LoadAllStatistics()
        {
            try
            {
                string file = GetSummaryFilePath();
                if (File.Exists(file))
                {
                    string json = File.ReadAllText(file, Encoding.UTF8);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        var data = JsonConvert.DeserializeObject<DailyStatisticsData>(json);
                        return data ?? new DailyStatisticsData();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载统计数据失败: {ex.Message}");
            }
            return new DailyStatisticsData();
        }

        /// <summary>
        /// 保存所有统计数据（单文件）
        /// </summary>
        private static void SaveAllStatistics(DailyStatisticsData data)
        {
            try
            {
                EnsureLogDirectory();
                var settings = new JsonSerializerSettings { Formatting = Formatting.Indented };
                string json = JsonConvert.SerializeObject(data, settings);
                File.WriteAllText(GetSummaryFilePath(), json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存统计数据失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取指定日期的统计数据（从全部数据中查找）
        /// </summary>
        private static ArticleLog.StatisticsData GetStatisticsForDate(DailyStatisticsData allData, string date)
        {
            return allData.DailySummaries.FirstOrDefault(d => d.Date == date) ?? new ArticleLog.StatisticsData { Date = date };
        }

        private static ArticleLog.RecentRecords LoadRecentData()
        {
            try
            {
                string file = GetRecentFilePath();
                if (File.Exists(file))
                {
                    string json = File.ReadAllText(file, Encoding.UTF8);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        var data = JsonConvert.DeserializeObject<ArticleLog.RecentRecords>(json);
                        return data ?? new ArticleLog.RecentRecords();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载最近记录失败: {ex.Message}");
            }
            return new ArticleLog.RecentRecords();
        }

        private static void SaveRecentData(ArticleLog.RecentRecords data)
        {
            try
            {
                EnsureLogDirectory();
                var settings = new JsonSerializerSettings { Formatting = Formatting.Indented };
                string json = JsonConvert.SerializeObject(data, settings);
                File.WriteAllText(GetRecentFilePath(), json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存最近记录失败: {ex.Message}");
            }
        }

        private static void CleanOldRecords(ArticleLog.RecentRecords recentData)
        {
            DateTime cutoffTime = DateTime.Now.AddHours(-24);

            var validRecords = recentData.Records
                .Where(r => r.Time >= cutoffTime)
                .OrderByDescending(r => r.Time)
                .Take(30)
                .ToList();

            recentData.Records = validRecords;
        }

        private static bool ShouldCleanOldDetailFiles(ArticleLog.StatisticsData summaryData)
        {
            return summaryData.LastCleanTime == default ||
                   (DateTime.Now - summaryData.LastCleanTime).TotalHours >= 24;
        }

        private static void CleanOldDetailFiles()
        {
            try
            {
                if (!Directory.Exists(LogFolder))
                    return;

                DateTime cutoffTime = DateTime.Now.AddHours(-24);

                foreach (string file in Directory.GetFiles(LogFolder, "*.json"))
                {
                    string fileName = Path.GetFileNameWithoutExtension(file);

                    // 跳过新的统计文件
                    if (fileName == "summary" || fileName == "recent")
                        continue;

                    // 尝试解析日期文件名
                    if (DateTime.TryParseExact(fileName, "yyyy-MM-dd", null,
                        System.Globalization.DateTimeStyles.None, out DateTime fileDate))
                    {
                        // 删除24小时前的文件
                        if (fileDate < cutoffTime.Date)
                        {
                            try
                            {
                                File.Delete(file);
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"删除旧日志文件失败 ({file}): {ex.Message}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"清理旧日志文件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 读取统计数据（返回预计算的统计数据）
        /// </summary>
        public static List<ArticleLog.WenlaiStatisticsItem> ReadStatistics()
        {
            // 读取所有可用日期的统计数据并合并
            var dates = GetAvailableSummaryDates();
            if (dates.Count == 0)
                return new List<ArticleLog.WenlaiStatisticsItem>();

            return ReadStatisticsInRange(dates.Min(), dates.Max());
        }

        /// <summary>
        /// 读取指定时间范围内的统计数据（合并多天的统计）
        /// </summary>
        public static List<ArticleLog.WenlaiStatisticsItem> ReadStatisticsInRange(DateTime startDate, DateTime endDate)
        {
            var allStats = new Dictionary<string, ArticleLog.AggregatedStatistics>();

            // 1. 读取所有统计数据
            var allStatistics = LoadAllStatistics();

            // 2. 按日期范围筛选并合并
            foreach (var dailyData in allStatistics.DailySummaries)
            {
                // 检查日期是否在范围内
                if (!DateTime.TryParseExact(dailyData.Date, "yyyy-MM-dd", null,
                    System.Globalization.DateTimeStyles.None, out DateTime dateValue))
                    continue;

                if (dateValue < startDate.Date || dateValue > endDate.Date)
                    continue;

                // 合并统计数据
                if (dailyData.Summaries != null)
                {
                    foreach (var summary in dailyData.Summaries)
                    {
                        if (!allStats.ContainsKey(summary.GroupKey))
                        {
                            allStats[summary.GroupKey] = new ArticleLog.AggregatedStatistics
                            {
                                MaxSpeed = summary.MaxSpeed,
                                MinSpeed = summary.MinSpeed
                            };
                        }

                        var agg = allStats[summary.GroupKey];
                        agg.Count += summary.Count;
                        agg.SumSpeedWeighted += summary.SumSpeedWeighted;
                        agg.SumHitRateWeighted += summary.SumHitRateWeighted;
                        agg.SumAccuracyWeighted += summary.SumAccuracyWeighted;
                        agg.SumKPWWeighted += summary.SumKPWWeighted;
                        agg.SumCiRatioWeighted += summary.SumCiRatioWeighted;
                        agg.SumCorrection += summary.SumCorrection;
                        agg.TotalBacks += summary.TotalBacks;
                        agg.TotalWords += summary.TotalWords;
                        agg.MaxSpeed = Math.Max(agg.MaxSpeed, summary.MaxSpeed);
                        agg.MinSpeed = Math.Min(agg.MinSpeed, summary.MinSpeed);
                    }
                }
            }

            // 3. 转换为结果
            return allStats
                .Select(kvp => new ArticleLog.WenlaiStatisticsItem
                {
                    DifficultyName = kvp.Key,
                    Count = kvp.Value.Count,
                    AvgSpeed = kvp.Value.TotalWords > 0 ? kvp.Value.SumSpeedWeighted / kvp.Value.TotalWords : 0,
                    AvgHitRate = kvp.Value.TotalWords > 0 ? kvp.Value.SumHitRateWeighted / kvp.Value.TotalWords : 0,
                    AvgAccuracy = kvp.Value.TotalWords > 0 ? kvp.Value.SumAccuracyWeighted / kvp.Value.TotalWords : 0,
                    AvgKPW = kvp.Value.TotalWords > 0 ? kvp.Value.SumKPWWeighted / kvp.Value.TotalWords : 0,
                    AvgCorrection = kvp.Value.Count > 0 ? kvp.Value.SumCorrection / kvp.Value.Count : 0,
                    TotalBacks = kvp.Value.TotalBacks,
                    AvgCiRatio = kvp.Value.TotalWords > 0 ? kvp.Value.SumCiRatioWeighted / kvp.Value.TotalWords : 0,
                    MaxSpeed = kvp.Value.MaxSpeed,
                    MinSpeed = kvp.Value.MinSpeed,
                    TotalWords = kvp.Value.TotalWords
                })
                .OrderBy(s => s.DifficultyName)
                .ToList();
        }

        /// <summary>
        /// 获取有统计数据文件的日期列表（从单文件中获取）
        /// </summary>
        public static List<DateTime> GetAvailableSummaryDates()
        {
            List<DateTime> dates = new List<DateTime>();

            try
            {
                var allStatistics = LoadAllStatistics();

                foreach (var dailyData in allStatistics.DailySummaries)
                {
                    if (DateTime.TryParseExact(dailyData.Date, "yyyy-MM-dd", null,
                        System.Globalization.DateTimeStyles.None, out DateTime date))
                    {
                        dates.Add(date);
                    }
                }
            }
            catch { }

            return dates;
        }

        /// <summary>
        /// 读取最近记录
        /// </summary>
        public static List<ArticleLog.ArticleRecord> ReadRecentRecords(int count = 30)
        {
            var recentData = LoadRecentData();
            if (recentData == null)
                return new List<ArticleLog.ArticleRecord>();

            return recentData.Records.Take(count).ToList();
        }

        #endregion
    }
}
