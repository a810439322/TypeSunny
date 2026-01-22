using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace TypeSunny
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

        // 迁移状态
        private static bool _migrationInProgress = false;

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
        /// 将记录写入文件（内部方法）- 使用增量更新统计（按DifficultyName分组）
        /// </summary>
        private static void WriteRecordToFile(ArticleLog.ArticleRecord record)
        {
            lock (_writeLock)
            {
                try
                {
                    EnsureLogDirectory();

                    // 1. 读取现有统计数据和最近记录
                    var summaryData = LoadSummaryData() ?? new ArticleLog.StatisticsData();
                    var recentData = LoadRecentData() ?? new ArticleLog.RecentRecords();

                    // 2. 获取分组键（DifficultyName 用于文来日志）
                    string groupKey = string.IsNullOrWhiteSpace(record.DifficultyName) ? "未分类" : record.DifficultyName;

                    // 3. 查找或创建分组汇总
                    var summary = summaryData.Summaries.FirstOrDefault(s => s.GroupKey == groupKey);
                    if (summary == null)
                    {
                        summary = new ArticleLog.StatisticsSummary
                        {
                            GroupKey = groupKey,
                            MaxSpeed = record.Speed,
                            MinSpeed = record.Speed
                        };
                        summaryData.Summaries.Add(summary);
                    }

                    // 4. 增量更新统计数据
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

                    // 5. 添加到最近记录并清理
                    recentData.Records.Add(record);
                    CleanOldRecords(recentData);
                    recentData.LastUpdateTime = DateTime.Now;

                    // 6. 定期删除旧的按日期的详细记录文件
                    if (ShouldCleanOldDetailFiles(summaryData))
                    {
                        CleanOldDetailFiles();
                        summaryData.LastCleanTime = DateTime.Now;
                    }

                    // 7. 保存统计数据和最近记录
                    SaveSummaryData(summaryData);
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

        private static string GetSummaryFilePath() => Path.Combine(LogFolder, "summary.json");
        private static string GetRecentFilePath() => Path.Combine(LogFolder, "recent.json");

        private static ArticleLog.StatisticsData LoadSummaryData()
        {
            try
            {
                string file = GetSummaryFilePath();
                if (File.Exists(file))
                {
                    string json = File.ReadAllText(file, Encoding.UTF8);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        var data = JsonConvert.DeserializeObject<ArticleLog.StatisticsData>(json);
                        return data ?? new ArticleLog.StatisticsData();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载统计数据失败: {ex.Message}");
            }
            return new ArticleLog.StatisticsData();
        }

        private static void SaveSummaryData(ArticleLog.StatisticsData data)
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

                    if (fileName == "summary" || fileName == "recent")
                        continue;

                    if (DateTime.TryParseExact(fileName, "yyyy-MM-dd", null,
                        System.Globalization.DateTimeStyles.None, out DateTime fileDate))
                    {
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
            var summaryData = LoadSummaryData();
            if (summaryData == null || summaryData.Summaries.Count == 0)
                return new List<ArticleLog.WenlaiStatisticsItem>();

            return summaryData.Summaries
                .Select(s => new ArticleLog.WenlaiStatisticsItem
                {
                    DifficultyName = s.GroupKey,
                    Count = s.Count,
                    AvgSpeed = s.TotalWords > 0 ? s.SumSpeedWeighted / s.TotalWords : 0,
                    AvgHitRate = s.TotalWords > 0 ? s.SumHitRateWeighted / s.TotalWords : 0,
                    AvgAccuracy = s.TotalWords > 0 ? s.SumAccuracyWeighted / s.TotalWords : 0,
                    AvgKPW = s.TotalWords > 0 ? s.SumKPWWeighted / s.TotalWords : 0,
                    AvgCorrection = s.Count > 0 ? s.SumCorrection / s.Count : 0,
                    TotalBacks = s.TotalBacks,
                    AvgCiRatio = s.TotalWords > 0 ? s.SumCiRatioWeighted / s.TotalWords : 0,
                    MaxSpeed = s.MaxSpeed,
                    MinSpeed = s.MinSpeed,
                    TotalWords = s.TotalWords
                })
                .OrderBy(s => s.DifficultyName)
                .ToList();
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

        /// <summary>
        /// 后台静默迁移旧数据
        /// </summary>
        public static async Task MigrateOldDataAsync()
        {
            if (_migrationInProgress)
                return;

            string summaryFile = GetSummaryFilePath();
            if (File.Exists(summaryFile))
                return;

            _migrationInProgress = true;

            await Task.Run(() =>
            {
                try
                {
                    var allRecords = ReadAllHistoricalRecords();

                    if (allRecords.Count == 0)
                        return;

                    var summaryData = new ArticleLog.StatisticsData();
                    var recentData = new ArticleLog.RecentRecords();

                    DateTime cutoffTime = DateTime.Now.AddHours(-24);

                    foreach (var record in allRecords)
                    {
                        string groupKey = string.IsNullOrWhiteSpace(record.DifficultyName) ? "未分类" : record.DifficultyName;
                        var summary = summaryData.Summaries.FirstOrDefault(s => s.GroupKey == groupKey);

                        if (summary == null)
                        {
                            summary = new ArticleLog.StatisticsSummary
                            {
                                GroupKey = groupKey,
                                MaxSpeed = record.Speed,
                                MinSpeed = record.Speed
                            };
                            summaryData.Summaries.Add(summary);
                        }

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

                        if (record.Time >= cutoffTime)
                        {
                            recentData.Records.Add(record);
                        }
                    }

                    CleanOldRecords(recentData);

                    SaveSummaryData(summaryData);
                    SaveRecentData(recentData);

                    System.Diagnostics.Debug.WriteLine($"文来日志迁移完成: {allRecords.Count} 条记录");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"数据迁移失败: {ex.Message}");
                }
                finally
                {
                    _migrationInProgress = false;
                }
            });
        }

        private static List<ArticleLog.ArticleRecord> ReadAllHistoricalRecords()
        {
            List<ArticleLog.ArticleRecord> allRecords = new List<ArticleLog.ArticleRecord>();

            if (!Directory.Exists(LogFolder))
                return allRecords;

            foreach (string file in Directory.GetFiles(LogFolder, "*.json"))
            {
                string fileName = Path.GetFileNameWithoutExtension(file);
                if (fileName == "summary" || fileName == "recent")
                    continue;

                try
                {
                    string json = File.ReadAllText(file, Encoding.UTF8);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        var records = JsonConvert.DeserializeObject<List<ArticleLog.ArticleRecord>>(json);
                        if (records != null)
                            allRecords.AddRange(records);
                    }
                }
                catch { }
            }

            return allRecords;
        }

        #endregion
    }
}
