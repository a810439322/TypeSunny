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
    /// 文章打字日志记录（使用后台线程队列）
    /// </summary>
    public static class ArticleLog
    {
        private const string LogFolder = "文章日志";
        private const int MinWordCount = 5;  // 最少字数，不超过此数不计入

        // 队列和锁
        private static readonly Queue<ArticleRecord> _recordQueue = new Queue<ArticleRecord>();
        private static readonly object _queueLock = new object();
        private static readonly object _writeLock = new object();

        // 后台线程控制
        private static Thread _writeThread;
        private static bool _isRunning = true;
        private static readonly AutoResetEvent _hasRecords = new AutoResetEvent(false);

        // 迁移状态
        private static bool _migrationInProgress = false;

        static ArticleLog()
        {
            // 启动后台写入线程
            _writeThread = new Thread(WriteLoop)
            {
                IsBackground = true,
                Name = "ArticleLogWriter"
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
                    ArticleRecord record;
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
        /// 将记录写入文件（内部方法）- 使用增量更新统计
        /// </summary>
        private static void WriteRecordToFile(ArticleRecord record)
        {
            lock (_writeLock)
            {
                try
                {
                    EnsureLogDirectory();

                    // 1. 读取现有统计数据和最近记录
                    var summaryData = LoadSummaryData() ?? new StatisticsData();
                    var recentData = LoadRecentData() ?? new RecentRecords();

                    // 2. 获取分组键（ArticleName 用于文章日志）
                    string groupKey = string.IsNullOrWhiteSpace(record.ArticleName) ? "未命名" : record.ArticleName;

                    // 3. 查找或创建分组汇总
                    var summary = summaryData.Summaries.FirstOrDefault(s => s.GroupKey == groupKey);
                    if (summary == null)
                    {
                        summary = new StatisticsSummary
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
                    System.Diagnostics.Debug.WriteLine($"写入文章日志失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 文章记录项
        /// </summary>
        public class ArticleRecord
        {
            public DateTime Time { get; set; }              // 完成时间
            public string ArticleName { get; set; }         // 文章名
            public int TotalWords { get; set; }             // 总字数
            public int InputWords { get; set; }             // 输入字数
            public double Speed { get; set; }               // 速度（字/分）
            public double HitRate { get; set; }             // 击键
            public double Accuracy { get; set; }            // 键准
            public int Wrong { get; set; }                  // 错字数
            public int Backs { get; set; }                  // 退格数
            public double Correction { get; set; }          // 回改数
            public double KPW { get; set; }                 // 码长
            public double LRRatio { get; set; }             // 左右键比
            public int TotalHit { get; set; }               // 总键数
            public double TotalSeconds { get; set; }        // 总用时（秒）
            public string ArticleMark { get; set; }         // 文来段号（如 "2-12743"）
            public int WasteCodes { get; set; }             // 废码数
            public double CiRatio { get; set; }             // 打词率
            public int Choose { get; set; }                 // 选重
            public int BiaoDing { get; set; }               // 标顶
            public string DifficultyName { get; set; }      // 难度名称（从文来API接口获取）
        }

        /// <summary>
        /// 统计汇总数据（按分组键汇总）
        /// </summary>
        public class StatisticsSummary
        {
            public string GroupKey { get; set; }  // 分组键（DifficultyName 或 ArticleName）
            public int Count { get; set; }
            public double SumSpeedWeighted { get; set; }
            public double SumHitRateWeighted { get; set; }
            public double SumAccuracyWeighted { get; set; }
            public double SumKPWWeighted { get; set; }
            public double SumCiRatioWeighted { get; set; }
            public double SumCorrection { get; set; }
            public int TotalBacks { get; set; }
            public int TotalWords { get; set; }
            public double MaxSpeed { get; set; }
            public double MinSpeed { get; set; }
            public DateTime LastUpdateTime { get; set; }
        }

        /// <summary>
        /// 统计数据容器
        /// </summary>
        public class StatisticsData
        {
            public List<StatisticsSummary> Summaries { get; set; } = new List<StatisticsSummary>();
            public DateTime LastCleanTime { get; set; }
        }

        /// <summary>
        /// 最近记录列表
        /// </summary>
        public class RecentRecords
        {
            public List<ArticleRecord> Records { get; set; } = new List<ArticleRecord>();
            public DateTime LastUpdateTime { get; set; }
        }

        /// <summary>
        /// 文来统计项
        /// </summary>
        public class WenlaiStatisticsItem
        {
            public string DifficultyName { get; set; }  // 难度名称
            public int Count { get; set; }              // 次数
            public double AvgSpeed { get; set; }        // 均速（字/分）
            public double AvgHitRate { get; set; }      // 均击（键/秒）
            public double AvgAccuracy { get; set; }     // 键准（%）
            public double AvgKPW { get; set; }          // 码长
            public double AvgCorrection { get; set; }   // 回改
            public int TotalBacks { get; set; }         // 总退格
            public double AvgCiRatio { get; set; }      // 打词率（%）
            public double MaxSpeed { get; set; }        // 最高速
            public double MinSpeed { get; set; }        // 最低速
            public int TotalWords { get; set; }         // 总字数
        }

        /// <summary>
        /// 本地文章统计项
        /// </summary>
        public class LocalArticleStatisticsItem
        {
            public string BookName { get; set; }        // 书名
            public int Count { get; set; }              // 次数
            public double AvgSpeed { get; set; }        // 均速（字/分）
            public double AvgHitRate { get; set; }      // 均击（键/秒）
            public double AvgAccuracy { get; set; }     // 键准（%）
            public double AvgKPW { get; set; }          // 码长
            public double AvgCorrection { get; set; }   // 回改
            public int TotalBacks { get; set; }         // 总退格
            public double AvgCiRatio { get; set; }      // 打词率（%）
            public double MaxSpeed { get; set; }        // 最高速
            public double MinSpeed { get; set; }        // 最低速
            public int TotalWords { get; set; }         // 总字数
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
        /// 记录文章数据（异步，加入队列）
        /// </summary>
        public static void WriteRecord(ArticleRecord record)
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
        public static List<ArticleRecord> ReadRecords(DateTime date)
        {
            List<ArticleRecord> records = new List<ArticleRecord>();

            try
            {
                string logFile = GetLogFilePath(date);
                if (!File.Exists(logFile))
                    return records;

                string json = File.ReadAllText(logFile, Encoding.UTF8);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    records = JsonConvert.DeserializeObject<List<ArticleRecord>>(json);
                    records ??= new List<ArticleRecord>();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"读取文章日志失败: {ex.Message}");
            }

            return records;
        }

        /// <summary>
        /// 读取日期范围内的所有记录
        /// </summary>
        public static List<ArticleRecord> ReadRecordsInRange(DateTime startDate, DateTime endDate)
        {
            List<ArticleRecord> allRecords = new List<ArticleRecord>();

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
                            var records = JsonConvert.DeserializeObject<List<ArticleRecord>>(json);
                            if (records != null)
                                allRecords.AddRange(records);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"读取文章日志失败 ({date:yyyy-MM-dd}): {ex.Message}");
                    }
                }
            }

            return allRecords;
        }

        /// <summary>
        /// 获取指定文章名的所有记录
        /// </summary>
        public static List<ArticleRecord> GetRecordsByArticleName(string articleName)
        {
            List<ArticleRecord> result = new List<ArticleRecord>();

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
                            var records = JsonConvert.DeserializeObject<List<ArticleRecord>>(json);
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
                System.Diagnostics.Debug.WriteLine($"读取文章日志失败: {ex.Message}");
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

        #region 新增：统计数据加载/保存方法

        private static string GetSummaryFilePath() => Path.Combine(LogFolder, "summary.json");
        private static string GetRecentFilePath() => Path.Combine(LogFolder, "recent.json");

        private static StatisticsData LoadSummaryData()
        {
            try
            {
                string file = GetSummaryFilePath();
                if (File.Exists(file))
                {
                    string json = File.ReadAllText(file, Encoding.UTF8);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        var data = JsonConvert.DeserializeObject<StatisticsData>(json);
                        return data ?? new StatisticsData();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载统计数据失败: {ex.Message}");
            }
            return new StatisticsData();
        }

        private static void SaveSummaryData(StatisticsData data)
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

        private static RecentRecords LoadRecentData()
        {
            try
            {
                string file = GetRecentFilePath();
                if (File.Exists(file))
                {
                    string json = File.ReadAllText(file, Encoding.UTF8);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        var data = JsonConvert.DeserializeObject<RecentRecords>(json);
                        return data ?? new RecentRecords();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载最近记录失败: {ex.Message}");
            }
            return new RecentRecords();
        }

        private static void SaveRecentData(RecentRecords data)
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

        /// <summary>
        /// 清理旧记录（严格24小时内，最多30条）
        /// </summary>
        private static void CleanOldRecords(RecentRecords recentData)
        {
            DateTime cutoffTime = DateTime.Now.AddHours(-24);

            // 严格24小时内，按时间倒序，最多30条
            var validRecords = recentData.Records
                .Where(r => r.Time >= cutoffTime)
                .OrderByDescending(r => r.Time)
                .Take(30)
                .ToList();

            recentData.Records = validRecords;
        }

        /// <summary>
        /// 判断是否应该清理旧的详细记录文件
        /// </summary>
        private static bool ShouldCleanOldDetailFiles(StatisticsData summaryData)
        {
            // 每天清理一次，或者距离上次清理超过24小时
            return summaryData.LastCleanTime == default ||
                   (DateTime.Now - summaryData.LastCleanTime).TotalHours >= 24;
        }

        /// <summary>
        /// 清理旧的按日期的详细记录文件
        /// </summary>
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
        public static List<LocalArticleStatisticsItem> ReadStatistics()
        {
            var summaryData = LoadSummaryData();
            if (summaryData == null || summaryData.Summaries.Count == 0)
                return new List<LocalArticleStatisticsItem>();

            return summaryData.Summaries
                .Select(s => new LocalArticleStatisticsItem
                {
                    BookName = s.GroupKey,
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
                .OrderBy(s => s.BookName)
                .ToList();
        }

        /// <summary>
        /// 读取最近记录
        /// </summary>
        public static List<ArticleRecord> ReadRecentRecords(int count = 30)
        {
            var recentData = LoadRecentData();
            if (recentData == null)
                return new List<ArticleRecord>();

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
                return; // 已迁移过，跳过

            _migrationInProgress = true;

            await Task.Run(() =>
            {
                try
                {
                    // 读取所有历史详细记录
                    var allRecords = ReadAllHistoricalRecords();

                    if (allRecords.Count == 0)
                        return;

                    var summaryData = new StatisticsData();
                    var recentData = new RecentRecords();

                    DateTime cutoffTime = DateTime.Now.AddHours(-24);

                    foreach (var record in allRecords)
                    {
                        // 增量更新统计数据
                        string groupKey = string.IsNullOrWhiteSpace(record.ArticleName) ? "未命名" : record.ArticleName;
                        var summary = summaryData.Summaries.FirstOrDefault(s => s.GroupKey == groupKey);

                        if (summary == null)
                        {
                            summary = new StatisticsSummary
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

                        // 24小时内的记录添加到最近记录
                        if (record.Time >= cutoffTime)
                        {
                            recentData.Records.Add(record);
                        }
                    }

                    // 清理最近记录
                    CleanOldRecords(recentData);

                    // 保存新格式
                    SaveSummaryData(summaryData);
                    SaveRecentData(recentData);

                    System.Diagnostics.Debug.WriteLine($"文章日志迁移完成: {allRecords.Count} 条记录");
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

        /// <summary>
        /// 读取所有历史详细记录（用于迁移）
        /// </summary>
        private static List<ArticleRecord> ReadAllHistoricalRecords()
        {
            List<ArticleRecord> allRecords = new List<ArticleRecord>();

            if (!Directory.Exists(LogFolder))
                return allRecords;

            foreach (string file in Directory.GetFiles(LogFolder, "*.json"))
            {
                // 跳过新的统计文件
                string fileName = Path.GetFileNameWithoutExtension(file);
                if (fileName == "summary" || fileName == "recent")
                    continue;

                try
                {
                    string json = File.ReadAllText(file, Encoding.UTF8);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        var records = JsonConvert.DeserializeObject<List<ArticleRecord>>(json);
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
