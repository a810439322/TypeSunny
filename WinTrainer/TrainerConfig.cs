using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TypeSunny
{
    internal class TrainerConfig
    {
        static public Dictionary<string, string> dicts = new Dictionary<string, string>();
        static public string Path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TrainerConfig.txt");
        static private readonly object _writeLock = new object();  // 写入锁
        static private Timer _writeTimer = null;  // 单一延迟写入Timer（防抖）
        private const int FileAccessRetryCount = 20;
        private const int FileAccessRetryDelayMs = 50;

        static public void SetDefault(params string[] args)
        {
            for (int i = 0; i + 1 < args.Length; i += 2)
            {
                dicts[args[i]] = args[i + 1];
            }
        }

        /// <summary>
        /// 实际执行写入的方法（Timer回调）
        /// </summary>
        static private void WriteNow(object obj)
        {
            // 写入完成后清空Timer引用
            Interlocked.Exchange(ref _writeTimer, null);

            if (Path == "")
                return;

            lock (_writeLock)
            {
                try
                {
                    WriteValuesLocked(dicts);
                }
                catch { }
            }
        }

        /// <summary>
        /// 写入配置文件
        /// </summary>
        /// <param name="Delay">延迟毫秒数，0表示立即写入，大于0使用防抖模式</param>
        static public void WriteConfig(int Delay = 0)
        {
            if (Path == "")
                return;

            if (Delay == 0)
            {
                // 立即写入：先停止可能存在的延迟Timer，然后同步写入
                var oldTimer = Interlocked.Exchange(ref _writeTimer, null);
                if (oldTimer != null)
                {
                    try { oldTimer.Dispose(); }
                    catch { }
                }

                lock (_writeLock)
                {
                    try
                    {
                        WriteValuesLocked(dicts);
                    }
                    catch { }
                }
            }
            else
            {
                // 延迟写入：防抖模式（拖动滑块时只在停止后Delay毫秒执行一次写入）
                if (_writeTimer == null)
                {
                    // 首次创建Timer
                    _writeTimer = new Timer(WriteNow, null, Delay, Timeout.Infinite);
                }
                else
                {
                    // 重置Timer触发时间（防抖关键：拖动时不断重置，只有停止后才触发）
                    try
                    {
                        _writeTimer.Change(Delay, Timeout.Infinite);
                    }
                    catch
                    {
                        // Timer已释放，重新创建
                        _writeTimer = new Timer(WriteNow, null, Delay, Timeout.Infinite);
                    }
                }
            }
        }

        static public void WriteValues(Dictionary<string, string> values)
        {
            if (Path == "")
                return;

            lock (_writeLock)
            {
                WriteValuesLocked(values);
            }
        }

        static private void WriteValuesLocked(Dictionary<string, string> values)
        {
            ExecuteWithFileAccessRetry(() =>
            {
                using (var stream = new FileStream(Path, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (var sw = new StreamWriter(stream, Encoding.UTF8))
                {
                    foreach (var c in values)
                    {
                        sw.WriteLine(c.Key + "\t" + c.Value);
                    }
                }
            });
        }

        static public void ReadInto(Dictionary<string, string> values)
        {
            if (Path == "")
                return;

            if (!File.Exists(Path))
            {
                WriteValues(values);
                return;
            }

            string[] lines;
            lock (_writeLock)
            {
                lines = ReadAllLinesLocked();
            }

            ApplyLines(values, lines);
        }

        static public void ReadConfig()
        {
            //     char[] sp = { '\r', ' ', '\t' };

            if (!File.Exists(Path))
            {
                WriteConfig();
                return;
            }

            string[] lines;
            lock (_writeLock)  // 加锁防止读写冲突
            {
                lines = ReadAllLinesLocked();
            }

            ApplyLines(dicts, lines);

            WriteConfig();
        }

        static private string[] ReadAllLinesLocked()
        {
            return ExecuteWithFileAccessRetry(() =>
            {
                using (var stream = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(stream, Encoding.UTF8, true))
                {
                    char[] sp1 = { '\n' };
                    return reader.ReadToEnd().Split(sp1, StringSplitOptions.RemoveEmptyEntries);
                }
            });
        }

        static private void ApplyLines(Dictionary<string, string> values, string[] lines)
        {
            foreach (string line in lines)
            {
                if (line.Length == 0) continue;
                if (line[0] == '#')
                    continue;

                string line_p = line.Replace("\r", "").Replace("\n", "");
                string[] sp = { "\t", " ", "," };

                foreach (string s in sp)
                {
                    if (line_p.Contains(s))
                    {
                        int pos = line_p.IndexOf(s);
                        if (pos >= 1 && pos <= line_p.Length - 2)
                        {
                            string key = line_p.Substring(0, pos);
                            string value = line_p.Substring(pos + 1);
                            values[key] = value;
                            break;
                        }
                    }
                }
            }
        }

        static private void ExecuteWithFileAccessRetry(Action action)
        {
            ExecuteWithFileAccessRetry<object>(() =>
            {
                action();
                return null;
            });
        }

        static private T ExecuteWithFileAccessRetry<T>(Func<T> action)
        {
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    return action();
                }
                catch (IOException) when (attempt < FileAccessRetryCount)
                {
                    Thread.Sleep(FileAccessRetryDelayMs);
                }
                catch (UnauthorizedAccessException) when (attempt < FileAccessRetryCount)
                {
                    Thread.Sleep(FileAccessRetryDelayMs);
                }
            }
        }

        static public bool GetBool(string key)
        {
            if (dicts.ContainsKey(key) && dicts[key] == "是")
                return true;
            else
                return false;
        }

        static public string GetString(string key)
        {
            if (dicts.ContainsKey(key))
                return dicts[key];
            else
                return "";
        }

        static public int GetInt(string key)
        {
            int rt = 0;
            if (dicts.ContainsKey(key))
                int.TryParse(dicts[key], out rt);
            return rt;
        }

        static public double GetDouble(string key)
        {
            double rt = 0;
            if (dicts.ContainsKey(key))
                double.TryParse(dicts[key], out rt);
            return rt;
        }

        static public void Set(string key, string value)
        {
            dicts[key] = value;
        }

        static public void Set(string key, int value)
        {
            dicts[key] = value.ToString();
        }

        static public void Set(string key, bool value)
        {
            dicts[key] = value ? "是" : "否";
        }

        static public void Set(string key, double value)
        {
            dicts[key] = value.ToString();
        }
    }
}
