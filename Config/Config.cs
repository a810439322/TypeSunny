using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using TypeSunny.Utils;

namespace TypeSunny
{
    static internal  class Config
    {
        static public Dictionary<string, string> dicts = new Dictionary<string, string>();
        static public string Path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.txt");

        static Config()
        {
            for (int i = 0; i + 1 < ConfigList.Length; i += 2)
            {
                dicts[ConfigList[i]] = ConfigList[i + 1];
            }

            ReadConfig();
        }

        static private string[] ConfigList = {
            "窗口高度", "750.4",
            "窗口宽度", "966.4",
            "窗口坐标X", "100",
            "窗口坐标Y", "100",
            "主题模式", "明",  // 明/暗/自定义
            "当前Logo", "sunny",  // 当前使用的logo文件名（不含扩展名）
            "窗体背景色", "F7F7F7",
            "窗体字体色", "5B5B5B",
            "跟打区背景色", "EDEDED",
            "跟打区字体色", "000000",
            "发文区字体色", "000000",
            "打对色", "A2CCD7",
            "打错色", "FF6347",
            "按钮背景色", "EBEBEB",
            "按钮字体色", "000000",
            "菜单背景色", "EBEBEB",
            "菜单字体色", "000000",
            "标题栏进度条颜色", "007ACC",
            "显示进度条", "是",
            "禁止F3重打", "否",
            "速度跟随提示", "是",
            "盲打模式", "否",
            "看打模式", "否",
            "字体", "#霞鹜文楷 GB 屏幕阅读版 R",
            "发文区字体大小", "40",
            "跟打区字体大小", "40",
            "成绩区字体大小", "15",
            "行距", "0.5",
            "贪吃蛇模式", "否",
            "贪吃蛇前显字数", "20",
            "贪吃蛇后显字数", "30",
            "字帖模式", "是",
            "字帖编码高度", "0",
            "字帖候选框高度", "0",
            "字帖错字高度", "0",
            "临摹模式", "否",
            "禁用回改", "否",
            "自动发送成绩", "是",
            "鼠标中键载文", "否",
            "错字重打", "是",
            "错字重复次数", "1",
            "慢字重打", "否",
            "慢字标准(单位:秒)", "2.0",
            "慢字重复次数", "1",
            "重打跳转模式", "手动",  // 自动/手动
            "QQ窗口切换模式(1-2)", "1",
            "载文模式(1-4)", "1",
            "成绩面板展开", "是",
            "成绩面板高度", "120",
            "首页功能按钮顺序", "文来,晴练单,晴双拼,赛文",
            "显示首页文来", "是",
            "显示首页练单", "是",
            "显示首页晴双拼", "是",
            "显示首页赛文", "是",
            "显示首页设置", "是",
            "显示首页本地文章", "是",
            "显示首页重打", "是",
            "显示首页剪贴板载文", "是",
            "显示首页群载文", "是",
            "显示首页选群", "是",
            "一键极简", "否",
            "一键极简后窗口高度", "0",
            "成绩签名", "",
            "成绩显示项", "配置",
            "成绩显示顺序", "速度,击键,键准,字数,难度,打词率,标顶,重打,码长,总键数,键法,回改,禁用回改,退格,废码,选重,用时,错字,盲打正确率,看打正确率,盲打模式,看打模式,签名",
            "启用预测", "是",
            "发文附带预测", "否",
            "预测显示顺序", "速度,难度,用时,击键,码长,总键数,置信",
            "预测显示_速度", "是",
            "预测显示_难度", "是",
            "预测显示_用时", "否",
            "预测显示_击键", "否",
            "预测显示_码长", "否",
            "预测显示_总键数", "否",
            "预测显示_置信", "是",
            // 成绩显示项（布尔值，true=显示，false=不显示）
            // 强制选中：速度、击键、字数、键准（不可取消）
            // 默认选中：难度、重打、打词率、标顶
            // 默认不选中：其他所有项
            "显示_速度", "是",
            "显示_击键", "是",
            "显示_码长", "否",
            "显示_字数", "是",
            "显示_难度", "是",
            "显示_重打", "是",
            "显示_总键数", "否",
            "显示_键法", "否",
            "显示_回改", "否",
            "显示_禁用回改", "是",
            "显示_退格", "否",
            "显示_键准", "是",
            "显示_废码", "否",
            "显示_打词率", "是",
            "显示_选重", "否",
            "显示_标顶", "是",
            "显示_用时", "否",
            "显示_错字", "否",
            "显示_盲打正确率", "否",
            "显示_盲打模式", "否",
            "显示_看打正确率", "否",
            "显示_看打模式", "否",
            "显示_签名", "否",
            "成绩显示时间", "MM-dd HH:mm",
            "软件更新Q群", "715187175",
            "作者邮箱QQ", "810439322",
            "文来字数", "",
            "文来难度", "",
            "文来分类", "",
            "文来换段模式", "手动",  // 自动/手动
            "字数模式", "智能分段",  // 智能分段/精确字数
            //"文来接口地址", "http://127.0.0.1:8000",
            //"赛文服务器地址", "http://127.0.0.1:8000",
            "文来接口地址", "https://qingfawen.fcxxz.com/",
            "赛文服务器地址", "https://qingfawen.fcxxz.com/",
            "赛文服务器配置", "",  // 新增：赛文服务器配置（JSON格式）
            "赛文输入法", "",
            "极速用户名", "",
            "极速密码", "",
            "极速显示名称", "",
            "极速杯用户名", "",
            "极速杯密码", "",
            "极速杯显示名称", "",
            "极速最后载文日期", "",
            "账号体系配置", "",  // 新增：账号体系配置（JSON格式）
            "启用字提", "是",
            "字提字体", "#TumanPUA",
            "字提字体大小", "20",
            "字提方案", "",
            "启用词提", "否",
            "词提方案", "",
            "词提编码下显", "是",
            "词提选重数字角标", "否",
            "词提不拆行", "否",
            "字提编码下显", "否",
            "字提选重数字角标", "是",
            "词提1简色", "#FF0000",
            "词提2简色", "#FF8C00",
            "词提3简色", "#0000FF",
            "词提4码色", "#808080",
            "词提选重色", "#008000",
            "词提关闭所有颜色", "否",
            "发文跟打框比例", "75.0",
            "发文区跟打区比例", "0.56",  // 发文区占(发文+跟打)的比例
            "成绩区高度比例", "0.2",   // 成绩区占总高度的比例
            "当前选群", "",
            "当前版本", "",  // 从 AssemblyInfo 动态读取，不需要保存
            "最新版本", "",
            "上次检查更新时间", "",
            "更新说明", "",
            "更新包地址", "",
            "全量包地址", "",
            "忽略的版本", "",
            "今日不再提醒时间", "",
            "过滤_生效_文来", "是",
            "过滤_生效_本地发文", "是",
            "过滤_生效_练单器", "否",
            "过滤_生效_剪贴板", "否",
            "过滤_文来最大重试", "5",
            "过滤_黑名单关键词", "",
            "过滤_替换关键词", "（求全订）\\n（校对版全本）\\n[四月天VIP完结]\\n[新浪vip完结]\\n　——晋江原创网[作品库]\\n（V文完结）\\n[潇湘vip完结]\\n[红袖vip完结]\\n[腾讯vip完结]\\n[起点vip完结]\\n（精校版全本）\\n（实体版全本）",
            "过滤_黑名单正则", "。{7,}\\n={5,}\\n——{3,}\\n—{5,}\\n。{5,}",
            "过滤_替换正则", ""
        };



        static public void SetDefault(params string[] args) 
        { 
            for (int i = 0; i + 1 < args.Length; i+=2)
            {
                dicts[args[i]] = args[i+1];
            }

        }

        static private Timer WriteTimer = null;
        static private readonly object _writeLock = new object();  // 写入锁

        static private void WriteNow(object obj)
        {
            // 写入完成后清空Timer引用
            Interlocked.Exchange(ref WriteTimer, null);

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
                catch (Exception)
                {
                    // 忽略写入错误
                }
            }
        }

        static public void WriteConfig(int Delay = 0)
        {
            if (Path == "")
                return;

            if (Delay == 0)
            {
                // 立即写入：先停止可能存在的延迟Timer，然后同步写入
                var oldTimer = Interlocked.Exchange(ref WriteTimer, null);
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
                    catch (Exception)
                    {
                        // 忽略写入错误
                    }
                }
            }
            else
            {
                // 延迟写入：防抖模式（拖动滑块时只在停止后Delay毫秒执行一次写入）
                if (WriteTimer == null)
                {
                    // 首次创建Timer
                    WriteTimer = new Timer(WriteNow, null, Delay, Timeout.Infinite);
                }
                else
                {
                    // 重置Timer触发时间（防抖关键：拖动时不断重置，只有停止后才触发）
                    try
                    {
                        WriteTimer.Change(Delay, Timeout.Infinite);
                    }
                    catch
                    {
                        // Timer已释放，重新创建
                        WriteTimer = new Timer(WriteNow, null, Delay, Timeout.Infinite);
                    }
                }
            }
        }

        static public void ReadConfig ()
        {
            try
            {
                //     char[] sp = { '\r', ' ', '\t' };

                if (!File.Exists(Path))
                {
                    EnforceSelectionNumberBadgeMutualExclusion();
                    WriteConfig();
                    return;
                }

                char[] sp1 = { '\n' };

                string[] lines = File.ReadAllText(Path).Split(sp1, StringSplitOptions.RemoveEmptyEntries);


                foreach (string line in lines)
                {
                    if (line.Length == 0)
                        continue;
                    if (line.Substring(0, 1) == "#")
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
                                string key = line_p.Substring(0, pos).Trim();
                                string value = line_p.Substring(pos + 1).Trim();

                                string migratedKey = GetMigratedConfigKey(key);
                                if (dicts.ContainsKey(migratedKey))
                                    dicts[migratedKey] = value;

                                break;
                            }
                        }
                    }



                }


                EnforceSelectionNumberBadgeMutualExclusion();
                WriteConfig();
            }
            catch (Exception ex)
            {
                // 配置文件读取失败，使用默认配置
                System.Diagnostics.Debug.WriteLine($"配置文件读取失败: {ex.Message}");
                EnforceSelectionNumberBadgeMutualExclusion();
                WriteConfig(); // 写入默认配置
            }



        }

        private static string GetMigratedConfigKey(string key)
        {
            if (key == "词提" + "尾码" + "角标")
                return "词提选重数字角标";
            if (key == "字提" + "尾码" + "角标")
                return "字提选重数字角标";
            return key;
        }

        private static void EnforceSelectionNumberBadgeMutualExclusion()
        {
            if (GetBool("词提编码下显"))
                dicts["词提选重数字角标"] = "否";

            if (GetBool("字提编码下显"))
                dicts["字提选重数字角标"] = "否";
        }

        static public bool GetBool (string key)
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

        /// <summary>
        /// 获取密码（自动解密）
        /// </summary>
        static public string GetPassword(string key)
        {
            string cipher = GetString(key);
            return string.IsNullOrWhiteSpace(cipher) ? "" : PasswordCrypto.Decrypt(cipher);
        }

        static public int GetInt(string key)
        {
            if (dicts.ContainsKey(key) && Int32.TryParse(dicts[key], out  int num))
                return num;
            else
                return 0;
        }


        static public double GetDouble(string key)
        {
            if (dicts.ContainsKey(key) && Double.TryParse(dicts[key], out double num))
                return num;
            else
                return 0;
        }

        static public void Set (string key, bool value)
        {
            if (value)
                dicts[key] = "是";
            else
                dicts[key] = "否";

            WriteConfig(3000);
        }
        static public void Set(string key, int value)
        {
            dicts[key] = value.ToString() ;
            WriteConfig(3000);
        }

        static public void Set(string key, string value)
        {
            dicts[key] = value;
            WriteConfig(3000);
        }

        static public void Set(string key, double value, int fraction = -1)
        {
            string f = "F" + fraction.ToString();
            if (fraction > 0)
                dicts[key] = value.ToString(f);
            else
                dicts[key] = value.ToString();

            WriteConfig(3000);

        }

        /// <summary>
        /// 设置密码（自动加密）
        /// </summary>
        static public void SetPassword(string key, string value)
        {
            string cipher = string.IsNullOrWhiteSpace(value) ? "" : PasswordCrypto.Encrypt(value);
            Set(key, cipher);
        }
    }



}
