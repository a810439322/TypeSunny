using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace TypeSunny
{
    static internal  class Config
    {
        static public Dictionary<string, string> dicts = new Dictionary<string, string>();
        static public string Path = "config.txt";

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
            "发文区字体大小", "40",
            "跟打区字体大小", "40",
            "成绩区字体大小", "15",
            "主题模式", "明",  // 明/暗/自定义
            "窗体背景色", "f7f7f7",
            "窗体字体色", "D3D3D3",
            "跟打区背景色", "ededed",
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
            "极速用户名", "",
            "极速密码", "",
            "极速显示名称", "",
            "极速最后载文日期", "",
            "极速杯用户名", "",
            "极速杯密码", "",
            "极速杯显示名称", "",
            "赛文用户名", "",
            "赛文密码", "",
            "赛文显示名称", "",
            "赛文用户ID", "",
            "赛文当前文章ID", "",
            "赛文最后载文日期", "",
            "禁止F3重打", "否",
            "速度跟随提示", "是",
            "盲打模式", "否",
            "看打模式", "否",
            "字体", "霞鹜文楷 GB 屏幕阅读版",
            "行距", "0.35",
            "贪吃蛇模式", "否",
            "贪吃蛇前显字数", "20",
            "贪吃蛇后显字数", "30",
            "自动发送成绩", "是",
            "鼠标中键载文", "否",
            "错字重打", "是",
            "错字重复次数", "1",
            "慢字重打", "否",
            "慢字标准(单位:秒)", "2.0",
            "慢字重复次数", "1",
            "QQ窗口切换模式(1-2)", "1",
            "载文模式(1-4)", "1",
            "成绩面板展开", "是",
            "成绩签名", "",
            "成绩屏蔽(逗号分隔)", "无",
            "软件更新Q群", "715187175",
            "作者邮箱QQ", "810439322",
            "文来字数", "",
            "文来难度", "",
            //"文来接口地址", "http://127.0.0.1:8000",
            //"赛文服务器地址", "http://127.0.0.1:8000",
            "文来接口地址", "https://typing.fcxxz.com/",
            "赛文服务器地址", "https://typing.fcxxz.com/",
            "赛文服务器配置", "",  // 新增：赛文服务器配置（JSON格式）
            "赛文输入法", "",
            "账号体系配置", "",  // 新增：账号体系配置（JSON格式）
            "启用字提", "否"
        };



        static public void SetDefault(params string[] args) 
        { 
            for (int i = 0; i + 1 < args.Length; i+=2)
            {
                dicts[args[i]] = args[i+1];
            }

        }


        static private Timer WriteTimer = null;


        static private void WriteNow(object obj)
        {

            if (Path == "")
                return;

            try
            {
                using (StreamWriter sw = new StreamWriter(Path))
                {
                    foreach (var c in dicts)
                    {
                        sw.WriteLine(c.Key + "\t" + c.Value);  // 改为同步方法
                    }
                    sw.Flush();  // 确保写入
                }  // using会自动Close

                if (WriteTimer != null)
                {
                    WriteTimer.Dispose();
                    WriteTimer = null;
                }
            }
            catch (Exception)
            {


            }
            finally
            {

            }




        }
    
        static public void WriteConfig (int Delay = 0)
        {

            if (Path == "")
                return;

            try
            {
                if (Delay == 0)
                {
                    if (WriteTimer != null)
                    {
                        WriteTimer.Dispose();
                        WriteTimer = null;
                    }

                    using (StreamWriter sw = new StreamWriter(Path))
                    {
                        foreach (var c in dicts)
                        {
                            sw.WriteLine(c.Key + "\t" + c.Value);  // 改为同步方法
                        }
                        sw.Flush();  // 确保写入
                    }  // using会自动Close
                }
                else if (Delay > 0)
                {
                    if (WriteTimer == null)
                    {
                        WriteTimer = new Timer(WriteNow, null, Delay, Timeout.Infinite);
                    }
                    else
                    {
                        WriteTimer.Dispose();
                        WriteTimer = new Timer(WriteNow, null, Delay, Timeout.Infinite);
                        //    WriteTimer.Change(Delay, Timeout.Infinite);

                    }
                }
            }
            catch (Exception)
            {

                
            }
            finally { }
           

        }

        static public void ReadConfig ()
        {
            try
            {
                //     char[] sp = { '\r', ' ', '\t' };

                if (!File.Exists(Path))
                {
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

                                if (dicts.ContainsKey(key))
                                {
                                    dicts[key] = value;
                                }

                                break;
                            }
                        }
                    }



                }


                WriteConfig();
            }
            catch (Exception ex)
            {
                // 配置文件读取失败，使用默认配置
                System.Diagnostics.Debug.WriteLine($"配置文件读取失败: {ex.Message}");
                WriteConfig(); // 写入默认配置
            }



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
    }



}
