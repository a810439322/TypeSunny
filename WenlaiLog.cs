using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace TypeSunny
{
    /// <summary>
    /// 文来文章日志记录
    /// </summary>
    public static class WenlaiLog
    {
        private const string LogFolder = "文来日志";
        private const int MinWordCount = 5;  // 最少字数，不超过此数不计入
        private static readonly object _writeLock = new object();  // 文件写入锁

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
        /// 异步记录文来文章数据（使用与 ArticleLog 相同的 ArticleRecord 格式）
        /// </summary>
        public static void WriteRecord(ArticleLog.ArticleRecord record)
        {
            // 过滤不超过5个字的记录
            if (record.TotalWords < MinWordCount)
            {
                return;
            }

            // 异步写入，不阻塞主线程
            Task.Run(() =>
            {
                // 使用锁防止并发写入问题
                lock (_writeLock)
                {
                    try
                    {
                        EnsureLogDirectory();

                        string logFile = GetLogFilePath(record.Time);

                        List<ArticleLog.ArticleRecord> records = new List<ArticleLog.ArticleRecord>();

                        // 读取现有记录
                        if (File.Exists(logFile))
                        {
                            string json = File.ReadAllText(logFile);
                            if (!string.IsNullOrWhiteSpace(json))
                            {
                                try
                                {
                                    records = JsonConvert.DeserializeObject<List<ArticleLog.ArticleRecord>>(json);
                                    records ??= new List<ArticleLog.ArticleRecord>();
                                }
                                catch
                                {
                                    records = new List<ArticleLog.ArticleRecord>();
                                }
                            }
                        }

                        // 添加新记录
                        records.Add(record);

                        // 写入文件
                        var settings = new JsonSerializerSettings { Formatting = Formatting.Indented };
                        string jsonOutput = JsonConvert.SerializeObject(records, settings);
                        File.WriteAllText(logFile, jsonOutput, Encoding.UTF8);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"写入文来日志失败: {ex.Message}");
                    }
                }
            });
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
    }
}
