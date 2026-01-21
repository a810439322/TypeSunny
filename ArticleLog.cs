using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace TypeSunny
{
    /// <summary>
    /// 文章打字日志记录
    /// </summary>
    public static class ArticleLog
    {
        private const string LogFolder = "文章日志";
        private const int MinWordCount = 5;  // 最少字数，不超过此数不计入

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
        /// 异步记录文章数据
        /// </summary>
        public static void WriteRecord(ArticleRecord record)
        {
            // 过滤不超过5个字的记录
            if (record.TotalWords < MinWordCount)
            {
                return;
            }

            // 异步写入，不阻塞主线程
            Task.Run(() =>
            {
                try
                {
                    EnsureLogDirectory();

                    string logFile = GetLogFilePath(record.Time);

                    List<ArticleRecord> records = new List<ArticleRecord>();

                    // 读取现有记录
                    if (File.Exists(logFile))
                    {
                        string json = File.ReadAllText(logFile);
                        if (!string.IsNullOrWhiteSpace(json))
                        {
                            try
                            {
                                records = JsonConvert.DeserializeObject<List<ArticleRecord>>(json);
                                records ??= new List<ArticleRecord>();
                            }
                            catch
                            {
                                records = new List<ArticleRecord>();
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
                    System.Diagnostics.Debug.WriteLine($"写入文章日志失败: {ex.Message}");
                }
            });
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
    }
}
