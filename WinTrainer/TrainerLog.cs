using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace TypeSunny
{
    /// <summary>
    /// 练单器训练日志记录
    /// </summary>
    public class TrainerLog
    {
        private const string LogFolder = "练单日志";
        private const string LogFile = "练单日志/TrainerLog.txt";

        /// <summary>
        /// 训练记录项
        /// </summary>
        public class TrainerRecord
        {
            public DateTime Date { get; set; }          // 日期
            public string ExerciseName { get; set; }    // 练习项（文件名）
            public int TotalWords { get; set; }         // 总字数
            public int ActualWords { get; set; }        // 实际字数（输入字数）
            public double AvgHitRate { get; set; }      // 平均击键
            public double AvgSpeed { get; set; }        // 平均速度
            public double AvgAccuracy { get; set; }     // 总键准
            public double TotalTime { get; set; }       // 总用时（秒）
        }

        /// <summary>
        /// 初始化日志目录
        /// </summary>
        private static void EnsureLogDirectory()
        {
            if (!Directory.Exists(LogFolder))
            {
                Directory.CreateDirectory(LogFolder);
            }
        }

        /// <summary>
        /// 记录训练数据
        /// </summary>
        public static void WriteRecord(TrainerRecord record)
        {
            try
            {
                EnsureLogDirectory();

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("===============================");
                sb.AppendLine($"日期：{record.Date:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"练习项：{record.ExerciseName}");
                sb.AppendLine($"总字数：{record.TotalWords}");
                sb.AppendLine($"实际字数：{record.ActualWords}");
                sb.AppendLine($"平均击键：{record.AvgHitRate:F2}");
                sb.AppendLine($"平均速度：{record.AvgSpeed:F2}");
                sb.AppendLine($"总键准：{record.AvgAccuracy:F2}%");
                sb.AppendLine($"总用时：{record.TotalTime:F2}秒");
                sb.AppendLine();

                File.AppendAllText(LogFile, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"写入练单日志失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 读取所有记录
        /// </summary>
        public static List<TrainerRecord> ReadAllRecords()
        {
            List<TrainerRecord> records = new List<TrainerRecord>();

            try
            {
                if (!File.Exists(LogFile))
                    return records;

                string[] lines = File.ReadAllLines(LogFile, Encoding.UTF8);
                TrainerRecord currentRecord = null;

                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();

                    if (line.StartsWith("日期："))
                    {
                        currentRecord = new TrainerRecord();
                        currentRecord.Date = DateTime.Parse(line.Substring(3));
                    }
                    else if (line.StartsWith("练习项：") && currentRecord != null)
                    {
                        currentRecord.ExerciseName = line.Substring(4);
                    }
                    else if (line.StartsWith("总字数：") && currentRecord != null)
                    {
                        currentRecord.TotalWords = int.Parse(line.Substring(4));
                    }
                    else if (line.StartsWith("实际字数：") && currentRecord != null)
                    {
                        currentRecord.ActualWords = int.Parse(line.Substring(5));
                    }
                    else if (line.StartsWith("平均击键：") && currentRecord != null)
                    {
                        currentRecord.AvgHitRate = double.Parse(line.Substring(5));
                    }
                    else if (line.StartsWith("平均速度：") && currentRecord != null)
                    {
                        currentRecord.AvgSpeed = double.Parse(line.Substring(5));
                    }
                    else if (line.StartsWith("总键准：") && currentRecord != null)
                    {
                        string accStr = line.Substring(4).Replace("%", "");
                        currentRecord.AvgAccuracy = double.Parse(accStr);
                    }
                    else if (line.StartsWith("总用时：") && currentRecord != null)
                    {
                        string timeStr = line.Substring(4).Replace("秒", "");
                        currentRecord.TotalTime = double.Parse(timeStr);

                        records.Add(currentRecord);
                        currentRecord = null;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"读取练单日志失败: {ex.Message}");
            }

            return records;
        }

        /// <summary>
        /// 获取指定练习项的历史记录
        /// </summary>
        public static List<TrainerRecord> GetRecordsByExercise(string exerciseName)
        {
            List<TrainerRecord> allRecords = ReadAllRecords();
            return allRecords.FindAll(r => r.ExerciseName == exerciseName);
        }
    }
}
