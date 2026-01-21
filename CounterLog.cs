using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibB
{
    static internal class CounterLog
    {
        static public string Path = "统计.txt";
        static public string ResultPath = "当日成绩.txt";
        static public string SumKey = "合计";
        static public int HourThresh = 6;
        static public bool Loaded = false;
        static private Dictionary<string, Dictionary<string, int>> Dict = new Dictionary<string, Dictionary<string, int>>();
        static public int[] Buffer = new int[1000];
        static private List<ResultRecord> DailyResults = new List<ResultRecord>();

        /// <summary>成绩记录（带时间戳）</summary>
        private class ResultRecord
        {
            public long Timestamp { get; set; }
            public string Content { get; set; }

            public ResultRecord(long timestamp, string content)
            {
                Timestamp = timestamp;
                Content = content;
            }

            public override string ToString()
            {
                return Timestamp + "\t" + Content;
            }
        }


        public static int GetCurrent(string key)
        {
            if (!Loaded)
                Read();

            int hour = DateTime.Now.Hour;

            string date = "";
            if (hour < HourThresh)
                date = DateTime.Now.AddDays(-1).ToString("d");
            else
                date = DateTime.Now.ToString("d");


            if (!Dict.ContainsKey(date))
            {
                Dict[date] = new Dictionary<string, int>();
            }

            if (!Dict[date].ContainsKey(key))
                Dict[date].Add(key, 0);


            Write();
            return Dict[date][key];
        }

        public static int GetSum(string key)
        {
            if (!Dict.ContainsKey(SumKey))
            {
                Dict[SumKey] = new Dictionary<string, int>();
            }

            if (!Dict[SumKey].ContainsKey(key))
                Dict[SumKey].Add(key, 0);


            return Dict[SumKey][key];
        }
        static public void Add(string key, int value)
        {
            if (!Loaded)
                Read();

            int hour = DateTime.Now.Hour;

            string date = "";
            if (hour < HourThresh)
                date = DateTime.Now.AddDays(-1).ToString("d");
            else
                date = DateTime.Now.ToString("d");


            if (!Dict.ContainsKey(date))
            {
                Dict[date] = new Dictionary<string, int>();
            }

            if (!Dict[date].ContainsKey(key))
                Dict[date].Add(key, value);
            else
                Dict[date][key] = Dict[date][key] + value;

            if (!Dict.ContainsKey(SumKey))
            {
                Dict[SumKey] = new Dictionary<string, int>();
            }

            if (!Dict[SumKey].ContainsKey(key))
                Dict[SumKey].Add(key, value);
            else
                Dict[SumKey][key] = Dict[SumKey][key] + value;

            Write();

        }

        static private void Read()
        {
            Loaded = true;

            if (!File.Exists(Path))
                return;

            string txt = File.ReadAllText(Path).Replace("\r", "");
            string[] lines = txt.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);


            string date = "";
            foreach (string line in lines)
            {
                string[] ls = line.Split(new char[] { '\t', ' ', ',', '，' }, StringSplitOptions.RemoveEmptyEntries);

                if (ls.Length == 1)
                {
                    Dict[ls[0]] = new Dictionary<string, int>();
                    date = ls[0];
                }
                else if (ls.Length >= 2)
                {
                    Int32.TryParse(ls[1], out int value);
                    if (value > 0)
                        Dict[date][ls[0]] = value;
                }
            }

        }

        static public void Write()
        {

            if (!Loaded)
                Read();

            try
            {
                StreamWriter sw = new StreamWriter(Path);


                Dictionary<string, int> sum = new Dictionary<string, int>();


                if (Dict.ContainsKey(SumKey))
                {
                    sw.WriteLine(SumKey);
                    foreach (var Record in Dict[SumKey])
                        sw.WriteLine(Record.Key + "\t" + Record.Value);
                }
                sw.WriteLine();

                foreach (var DayRecord in Dict)
                {
                    if (DayRecord.Key == SumKey)
                        continue;

                    sw.WriteLine(DayRecord.Key);
                    foreach (var Record in DayRecord.Value)
                        sw.WriteLine(Record.Key + "\t" + Record.Value);


                }
                sw.WriteLine();

                sw.Close();
            }
            catch (Exception)
            {

                
            }
            finally
            {

            }

        }

        /// <summary>获取当前Unix时间戳（秒）</summary>
        static private long GetCurrentTimestamp()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        /// <summary>加载成绩记录：24小时内全部保留，超过24小时只保留最近30条</summary>
        static public void LoadDailyResults()
        {
            DailyResults.Clear();

            if (!File.Exists(ResultPath))
                return;

            try
            {
                string[] lines = File.ReadAllLines(ResultPath);
                long now = GetCurrentTimestamp();
                long twentyFourHoursAgo = now - 24 * 3600;

                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    // 格式：时间戳\t成绩内容
                    string[] parts = line.Split(new char[] { '\t' }, 2);
                    if (parts.Length == 2 && long.TryParse(parts[0], out long timestamp))
                    {
                        DailyResults.Add(new ResultRecord(timestamp, parts[1]));
                    }
                }

                // 按时间戳倒序排列（最新的在前面）
                DailyResults.Sort((a, b) => b.Timestamp.CompareTo(a.Timestamp));

                // 24小时内的全部保留 + 超过24小时的最近30条
                List<ResultRecord> filtered = new List<ResultRecord>();
                int oldCount = 0;

                foreach (var record in DailyResults)
                {
                    if (record.Timestamp >= twentyFourHoursAgo)
                    {
                        filtered.Add(record);
                    }
                    else if (oldCount < 30)
                    {
                        filtered.Add(record);
                        oldCount++;
                    }
                }

                DailyResults = filtered;
            }
            catch (Exception)
            {
                DailyResults.Clear();
            }
        }

        /// <summary>添加一条成绩记录</summary>
        static public void AddDailyResult(string result)
        {
            if (string.IsNullOrWhiteSpace(result))
                return;

            long timestamp = GetCurrentTimestamp();
            DailyResults.Insert(0, new ResultRecord(timestamp, result));
        }

        /// <summary>获取所有成绩记录（不含时间戳）</summary>
        static public string GetDailyResults()
        {
            StringBuilder sb = new StringBuilder();

            foreach (var record in DailyResults)
            {
                sb.AppendLine(record.Content);
            }

            return sb.ToString();
        }

        /// <summary>异步保存成绩记录</summary>
        static public async Task SaveDailyResultsAsync()
        {
            try
            {
                // 异步写入文件
                await Task.Run(() =>
                {
                    try
                    {
                        List<string> lines = new List<string>();
                        foreach (var record in DailyResults)
                        {
                            lines.Add(record.ToString());
                        }
                        File.WriteAllLines(ResultPath, lines);
                    }
                    catch (Exception)
                    {
                        // 忽略写入错误
                    }
                });
            }
            catch (Exception)
            {
                // 忽略异常
            }
        }
    }

}
