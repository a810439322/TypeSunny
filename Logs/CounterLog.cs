using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace TypeSunny.Logs
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
        static private int _addCountSinceCleanup = 0;

        // 队列和锁（用于异步写入统计文件）
        private static readonly Queue<bool> _writeQueue = new Queue<bool>();
        private static readonly object _writeLock = new object();
        private static Thread _writeThread;
        private static bool _isWriteThreadRunning = true;
        private static readonly AutoResetEvent _hasWriteRequest = new AutoResetEvent(false);

        // 静态构造函数：启动后台写入线程
        static CounterLog()
        {
            _writeThread = new Thread(WriteLoop)
            {
                IsBackground = true,
                Name = "CounterLogWriter"
            };
            _writeThread.Start();
        }

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

        /// <summary>
        /// 后台写入线程主循环
        /// </summary>
        private static void WriteLoop()
        {
            while (_isWriteThreadRunning)
            {
                // 等待写入请求
                _hasWriteRequest.WaitOne();

                // 处理所有待写入的请求
                while (_isWriteThreadRunning)
                {
                    bool hasWork;
                    lock (_writeQueue)
                    {
                        hasWork = _writeQueue.Count > 0;
                        if (hasWork)
                            _writeQueue.Dequeue();
                    }

                    if (!hasWork)
                        break;

                    // 执行写入
                    WriteToFile();
                }
            }
        }

        /// <summary>
        /// 异步请求写入统计文件
        /// </summary>
        private static void RequestWrite()
        {
            lock (_writeQueue)
            {
                _writeQueue.Enqueue(true);
            }
            _hasWriteRequest.Set();
        }

        /// <summary>
        /// 同步写入统计文件到磁盘（内部方法）
        /// </summary>
        private static void WriteToFile()
        {
            lock (_writeLock)
            {
                try
                {
                    using (StreamWriter sw = new StreamWriter(Path))
                    {
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
                    }
                }
                catch (Exception)
                {
                    // 忽略写入错误
                }
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


            RequestWrite();  // 异步写入
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

            RequestWrite();  // 异步写入
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

        /// <summary>
        /// 同步写入统计文件（兼容旧代码，内部调用异步写入）
        /// </summary>
        static public void Write()
        {
            RequestWrite();
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

                var all = new List<ResultRecord>();
                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    string[] parts = line.Split(new char[] { '\t' }, 2);
                    if (parts.Length == 2 && long.TryParse(parts[0], out long timestamp))
                    {
                        all.Add(new ResultRecord(timestamp, parts[1]));
                    }
                }

                all.Sort((a, b) => b.Timestamp.CompareTo(a.Timestamp));

                // 过滤：24h内全保留 + 超过24h最近30条
                var filtered = new List<ResultRecord>();
                int oldCount = 0;
                foreach (var record in all)
                {
                    if (record.Timestamp >= twentyFourHoursAgo)
                        filtered.Add(record);
                    else if (oldCount < 30)
                    {
                        filtered.Add(record);
                        oldCount++;
                    }
                }

                // 写回过滤后的文件
                var filteredLines = new List<string>();
                foreach (var record in filtered)
                    filteredLines.Add(record.ToString());
                File.WriteAllLines(ResultPath, filteredLines);

                // 内存只保留最新30条
                DailyResults = filtered.Count > 30 ? filtered.GetRange(0, 30) : filtered;
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
            var record = new ResultRecord(timestamp, result);
            DailyResults.Insert(0, record);
            if (DailyResults.Count > 30)
                DailyResults.RemoveAt(DailyResults.Count - 1);

            // 追加写文件
            try
            {
                File.AppendAllText(ResultPath, record.ToString() + Environment.NewLine);
            }
            catch (Exception) { }

            // 每100条触发一次文件清理（异步）
            _addCountSinceCleanup++;
            if (_addCountSinceCleanup >= 100)
            {
                _addCountSinceCleanup = 0;
                System.Threading.Tasks.Task.Run(() => CleanupResultFile());
            }
        }

        /// <summary>清理成绩文件：删除超过24h且超出30条的旧记录</summary>
        static private void CleanupResultFile()
        {
            if (!File.Exists(ResultPath))
                return;

            try
            {
                string[] lines = File.ReadAllLines(ResultPath);
                long twentyFourHoursAgo = GetCurrentTimestamp() - 24 * 3600;

                var filtered = new List<string>();
                int oldCount = 0;

                // 先解析排序
                var all = new List<ResultRecord>();
                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;
                    string[] parts = line.Split(new char[] { '\t' }, 2);
                    if (parts.Length == 2 && long.TryParse(parts[0], out long ts))
                        all.Add(new ResultRecord(ts, parts[1]));
                }
                all.Sort((a, b) => b.Timestamp.CompareTo(a.Timestamp));

                foreach (var r in all)
                {
                    if (r.Timestamp >= twentyFourHoursAgo)
                        filtered.Add(r.ToString());
                    else if (oldCount < 30)
                    {
                        filtered.Add(r.ToString());
                        oldCount++;
                    }
                }

                File.WriteAllLines(ResultPath, filtered);
            }
            catch (Exception) { }
        }

        /// <summary>获取内存中的成绩记录（最多30条）</summary>
        static public string GetDailyResults()
        {
            StringBuilder sb = new StringBuilder();

            foreach (var record in DailyResults)
            {
                sb.AppendLine(record.Content);
            }

            return sb.ToString();
        }

        /// <summary>获取内存中的成绩记录（带时间戳）</summary>
        static public List<(long timestamp, string content)> GetDailyResultsWithTimestamp()
        {
            var results = new List<(long, string)>();
            foreach (var record in DailyResults)
                results.Add((record.Timestamp, record.Content));
            return results;
        }

        /// <summary>从文件分页读取更多成绩记录（带时间戳）</summary>
        static public List<(long timestamp, string content)> LoadMoreResults(int skip, int count)
        {
            var results = new List<(long, string)>();
            if (!File.Exists(ResultPath))
                return results;

            try
            {
                string[] lines = File.ReadAllLines(ResultPath);
                var all = new List<ResultRecord>();
                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;
                    string[] parts = line.Split(new char[] { '\t' }, 2);
                    if (parts.Length == 2 && long.TryParse(parts[0], out long timestamp))
                        all.Add(new ResultRecord(timestamp, parts[1]));
                }
                all.Sort((a, b) => b.Timestamp.CompareTo(a.Timestamp));

                for (int i = skip; i < all.Count && results.Count < count; i++)
                    results.Add((all[i].Timestamp, all[i].Content));
            }
            catch (Exception) { }

            return results;
        }

        /// <summary>获取文件中的总记录数</summary>
        static public int GetTotalResultCount()
        {
            if (!File.Exists(ResultPath))
                return 0;
            try
            {
                int count = 0;
                foreach (string line in File.ReadAllLines(ResultPath))
                    if (!string.IsNullOrWhiteSpace(line)) count++;
                return count;
            }
            catch { return 0; }
        }

        /// <summary>同步保存成绩记录（启动时清理用）</summary>
        static public void SaveDailyResults()
        {
            // 不再从内存写回，因为内存只有30条
            // 文件由 AddDailyResult 追加写入，LoadDailyResults 启动时清理
        }

        /// <summary>异步保存成绩记录（兼容旧代码）</summary>
        static public System.Threading.Tasks.Task SaveDailyResultsAsync()
        {
            return System.Threading.Tasks.Task.Run(() => SaveDailyResults());
        }

        /// <summary>
        /// 刷新队列，确保所有写入请求都被处理（程序退出时调用）
        /// </summary>
        static public void Flush()
        {
            // 等待队列清空
            while (true)
            {
                int count;
                lock (_writeQueue)
                {
                    count = _writeQueue.Count;
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
        static public void Shutdown()
        {
            Flush();  // 先等待队列清空

            _isWriteThreadRunning = false;
            _hasWriteRequest.Set();

            if (_writeThread != null && _writeThread.IsAlive)
            {
                _writeThread.Join(1000); // 等待最多1秒
            }
        }
    }
}
