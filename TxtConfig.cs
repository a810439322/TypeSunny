using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TypeSunny
{
    internal class TxtConfig
    {
        public Dictionary<string, string> dicts = new Dictionary<string, string>();
        public string Path = "";
        private readonly object _writeLock = new object();  // 写入锁
        private Timer _writeTimer = null;  // 单一延迟写入Timer（防抖）

        public void SetDefault(params string[] args)
        {
            for (int i = 0; i + 1 < args.Length; i += 2)
            {
                dicts[args[i]] = args[i + 1];
            }
        }

        public TxtConfig(string SaveFilePath)
        {
            Path = SaveFilePath;
        }

        /// <summary>
        /// 实际执行写入的方法（Timer回调）
        /// </summary>
        private void WriteNow(object obj)
        {
            // 写入完成后清空Timer引用
            Interlocked.Exchange(ref _writeTimer, null);

            if (Path == "")
                return;

            lock (_writeLock)
            {
                try
                {
                    using (StreamWriter sw = new StreamWriter(Path))
                    {
                        foreach (var c in dicts)
                        {
                            sw.WriteLine(c.Key + "\t" + c.Value);
                        }
                        sw.Flush();
                    }
                }
                catch { }
            }
        }

        /// <summary>
        /// 写入配置文件
        /// </summary>
        /// <param name="Delay">延迟毫秒数，0表示立即写入，大于0使用防抖模式</param>
        public void WriteConfig(int Delay = 0)
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
                        using (StreamWriter sw = new StreamWriter(Path))
                        {
                            foreach (var c in dicts)
                            {
                                sw.WriteLine(c.Key + "\t" + c.Value);
                            }
                            sw.Flush();
                        }
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

        public void ReadConfig()
        {
            if (!File.Exists(Path))
            {
                WriteConfig();
                return;
            }

            string[] lines;
            lock (_writeLock)  // 加锁防止读写冲突
            {
                char[] sp1 = { '\n' };
                lines = File.ReadAllText(Path).Split(sp1, StringSplitOptions.RemoveEmptyEntries);
            }

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
                            dicts[key] = value;
                            break;
                        }
                    }
                }
            }

            WriteConfig();
        }

        public bool GetBool(string key)
        {
            if (dicts.ContainsKey(key) && dicts[key] == "是")
                return true;
            else
                return false;
        }

        public string GetString(string key)
        {
            if (dicts.ContainsKey(key))
                return dicts[key];
            else
                return "";
        }

        public int GetInt(string key)
        {
            if (dicts.ContainsKey(key) && Int32.TryParse(dicts[key], out int num))
                return num;
            else
                return 0;
        }

        public double GetDouble(string key)
        {
            if (dicts.ContainsKey(key) && Double.TryParse(dicts[key], out double num))
                return num;
            else
                return 0;
        }

        public void Set(string key, bool value)
        {
            if (value)
                dicts[key] = "是";
            else
                dicts[key] = "否";

            WriteConfig(3000);
        }

        public void Set(string key, int value)
        {
            dicts[key] = value.ToString();
            WriteConfig(3000);
        }

        public void Set(string key, string value)
        {
            dicts[key] = value;
            WriteConfig(3000);
        }

        public void Set(string key, double value, int fraction = -1)
        {
            string f = "F" + fraction.ToString();
            if (fraction > 0)
                dicts[key] = value.ToString(f);
            else
                dicts[key] = value.ToString();

            WriteConfig(3000);
        }
    }
}
