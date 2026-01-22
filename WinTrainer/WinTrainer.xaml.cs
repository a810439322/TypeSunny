using LibB;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Interop;


namespace TypeSunny
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 




    public partial class WinTrainer : Window
    {
        // 自定义最大化状态
        private bool _isCustomMaximized = false;
        private Rect _restoreBounds = new Rect();

        // Win32 API for resize
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_LEFT = 10;
        private const int HT_RIGHT = 11;
        private const int HT_TOP = 12;
        private const int HT_TOPLEFT = 13;
        private const int HT_TOPRIGHT = 14;
        private const int HT_BOTTOM = 15;
        private const int HT_BOTTOMLEFT = 16;
        private const int HT_BOTTOMRIGHT = 17;

        public const string Folder = "练单器/";
        public static WinTrainer Current
        {
            get
            {
                foreach (var s in App.Current.Windows)
                {
                    if (s is WinTrainer)
                    {
                        return (WinTrainer)s;

                    }

                }

                return null;
            }

        }

     //   Dictionary<string, int> log = new Dictionary<string, int>();

        Dictionary<string, string> cfg = new Dictionary<string, string>
        {

            {"换段击键", "6" },
             {"每轮降击","0.05" },
            {"每组字数", "10" },




             {"上次打开的文件", "" },
             {"上次的段数", "0" },


        };

        bool CfgInit;
        bool SliderInit;

   //     List<string> InputWords = new List<string>();
        bool Jumped = false;


        string mode = "fixed";
        public static double TargetHit = 0;



        List<List<string>> DisplayRoot = new List<List<string>>();


        int TotalGroup;




        int MaxGroupSize;
        int RetypeCount = 0;
        double MaxHitRate = 0;
        double AverageGroupSize;


        string TxtFile;

        // 本轮练习统计数据
        private int roundTotalWords = 0;      // 本轮总字数（不重复计算）
        private int roundActualWords = 0;     // 本轮实际字数（包括所有重打的输入）
        private int roundCorrectWords = 0;    // 本轮打对字数
        private double roundTotalTime = 0;    // 本轮总用时（秒）
        private int roundCompletedGroups = 0; // 本轮完成段数
        private List<double> roundHitRates = new List<double>();   // 本轮每段击键率
        private List<double> roundSpeeds = new List<double>();     // 本轮每段速度
        private bool hasStartedPractice = false;  // 是否已经开始练习（有有效成绩）

        // 文章独立统计数据
        private Dictionary<string, ArticleStatisticsData> articleStatisticsDict = new Dictionary<string, ArticleStatisticsData>();

        /// <summary>
        /// 文章统计数据结构
        /// </summary>
        [Serializable]
        private class ArticleStatisticsData
        {
            public int RoundTotalWords { get; set; }
            public int RoundActualWords { get; set; }
            public int RoundCorrectWords { get; set; }
            public double RoundTotalTime { get; set; }
            public int RoundCompletedGroups { get; set; }
            public List<double> RoundHitRates { get; set; }
            public List<double> RoundSpeeds { get; set; }
            public bool HasStartedPractice { get; set; }
            public int LastSection { get; set; }  // 上次练习到的段号
            public int RetypeCount { get; set; }  // 重打次数
            public double MaxHitRate { get; set; }  // 最高击键率
            public List<List<string>> DisplayRoot { get; set; }  // 乱序后的文章内容

            public ArticleStatisticsData()
            {
                RoundHitRates = new List<double>();
                RoundSpeeds = new List<double>();
                DisplayRoot = new List<List<string>>();
            }
        }

        private const string StatisticsFileName = "TrainerStatistics.dat";















        double ftsize = 24;
        private void ShowWords()
        {
            // var sList = DisplayInfo; 


            fld.FontSize = ftsize;
            fld.Text = string.Join("", DisplayRoot[Convert.ToInt32(cfg["上次的段数"])]);
            fld.FontFamily = MainWindow.Current.GetCurrentFontFamily();
            fld.Background = MainWindow.Current.BdDisplay.Background;
            fld.Foreground = Colors.DisplayForeground;


    



        }

   


        private void InitSlider()
        {
            SliderInit = false;
            sld.Minimum = 1;
            sld.Maximum = TotalGroup;
            sld.Value = Convert.ToInt32(cfg["上次的段数"]) + 1;



            SliderInit = true;
        }

        private void ReadTxt() //从文件重新读取码表
        {
            // 保存当前文章的统计数据（如果不是第一次加载）
            if (!string.IsNullOrEmpty(TxtFile))
            {
                SaveCurrentArticleStatistics();
            }

            TxtFile = FileSelector.SelectedItem.ToString();
            string filename = Folder + TxtFile + ".txt";
            if (CfgInit)
            {
                if (File.Exists(filename))
                {
                    cfg["上次打开的文件"] = TxtFile + ".txt";
                    WriteCfg();
                }
            }

            // 加载新文章的统计数据（不重置，保留每个文章的独立记录）
            // 必须在解析文章内容之前加载，因为 LoadArticleStatistics 会恢复 DisplayRoot
            LoadArticleStatistics(TxtFile);

            // 如果已经有保存的 DisplayRoot（包括乱序状态），跳过文章解析
            if (articleStatisticsDict.ContainsKey(TxtFile) &&
                articleStatisticsDict[TxtFile].DisplayRoot != null &&
                articleStatisticsDict[TxtFile].DisplayRoot.Count > 0)
            {
                // DisplayRoot 已在 LoadArticleStatistics 中恢复
                // 重新计算 TotalGroup
                TotalGroup = DisplayRoot.Count;
            }
            else
            {
                // 没有保存的数据，从文件读取文章内容
                string mbtxt = File.ReadAllText(filename).Trim().Replace("\r", "");//.Replace(" ", "\t");
                do
                {
                    mbtxt = mbtxt.Replace("\n\n", "\n");
                } while (mbtxt.Contains("\n\n"));

                do
                {
                    mbtxt = mbtxt.Replace("  ", " ");
                } while (mbtxt.Contains("  "));

                string[] lines = mbtxt.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);



                DisplayRoot.Clear();



                int MaxLineLen = (from line in lines select line.Length).Max();
                const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

                if (!chars.Contains( lines[0].Substring(0,1)) && MaxLineLen> 4) //变长
                {
                    mode = "varible";

                    int group = 0;
                    foreach (string line in lines)
                    {
                        DisplayRoot.Add(new List<string>());

                        StringInfo si = new StringInfo(line);

                        for (int i = 0; i < si.LengthInTextElements; i++)
                        {
                            string name = si.SubstringByTextElements(i, 1);
                            DisplayRoot[group].Add(name);

                        }

                        group++;
                    }

                    TotalGroup = group;


                    MaxGroupSize = 0;
                    AverageGroupSize = 0;
                    foreach (var g in DisplayRoot)
                    {
                        AverageGroupSize += g.Count;
                        if (MaxGroupSize < g.Count)
                            MaxGroupSize = g.Count;
                    }
                    AverageGroupSize /= TotalGroup;

                }
                else
                {
                    List<String> RootList = new List<String>();
                    mode = "fixed";




                    foreach (string line in lines)
                    {
                        if (line.Length >= 1)
                            RootList.Add(line);

                    }


                    TotalGroup = (RootList.Count + Convert.ToInt32(cfg["每组字数"]) - 1) / Convert.ToInt32(cfg["每组字数"]);



                    MaxGroupSize = Convert.ToInt32(cfg["每组字数"]);

                    int k = 0;

                    for (int i = 0; i < TotalGroup; i++)
                    {
                        DisplayRoot.Add(new List<string>());

                        int jmax;
                        if (i < TotalGroup - 1)
                        {
                            jmax = Convert.ToInt32(cfg["每组字数"]);
                        }
                        else
                        {
                            jmax = RootList.Count - Convert.ToInt32(cfg["每组字数"]) * (TotalGroup - 1);
                        }
                        for (int j = 0; j < jmax; j++)
                        {
                            DisplayRoot[i].Add(RootList[k]);

                            k++;
                        }
                    }
                }
            }

            // JumpGroup() 会覆盖已恢复的段号，所以不需要调用
            // LoadArticleStatistics() 已经恢复了段号，InitSlider() 和 InitGroup() 会使用它

            // RetypeCount 和 MaxHitRate 已在 LoadArticleStatistics 中恢复或初始化，不要重置
            InitSlider();

            InitGroup();


        }

        private void ReadTxt_old() //从文件重新读取码表
        {
            TxtFile = FileSelector.SelectedItem.ToString();
            string filename = Folder  + TxtFile + ".txt";
            if (CfgInit)
            {
                if (File.Exists(filename))
                {
                    cfg["上次打开的文件"] = TxtFile + ".txt";
                    WriteCfg();
                }
            }

            string mbtxt = File.ReadAllText(filename).Trim().Replace("\r", "").Replace(" ", "\t");
            do
            {
                mbtxt = mbtxt.Replace("\n\n", "\n");
            } while (mbtxt.Contains("\n\n"));

            do
            {
                mbtxt = mbtxt.Replace("\t\t", "\t");
            } while (mbtxt.Contains("\t\t"));

            string[] lines = mbtxt.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            //         List<word> TrainTable = new();



            DisplayRoot.Clear();


            if (!lines[0].Contains("\t") && lines[0].Length >= 4)
            {
                mode = "varible";

                int group = 0;
                foreach (string line in lines)
                {
                    DisplayRoot.Add(new List<string>());

                    StringInfo si = new StringInfo(line);

                    for (int i = 0; i < si.LengthInTextElements; i++)
                    {
                        string name = si.SubstringByTextElements(i, 1);
                        DisplayRoot[group].Add(name);

                    }

                    group++;
                }

                TotalGroup = group;


                MaxGroupSize = 0;
                AverageGroupSize = 0;
                foreach (var g in DisplayRoot)
                {
                    AverageGroupSize += g.Count;
                    if (MaxGroupSize < g.Count)
                        MaxGroupSize = g.Count;
                }
                AverageGroupSize /= TotalGroup;

            }
            else
            {
                List<String> RootList = new List<String>();
                mode = "fixed";


                

                foreach (string line in lines)
                {
                    if (line.Length >= 1)
                        RootList.Add(line);

                }


                TotalGroup = (RootList.Count + Convert.ToInt32(cfg["每组字数"]) - 1) / Convert.ToInt32(cfg["每组字数"]);



                MaxGroupSize = Convert.ToInt32(cfg["每组字数"]);

                int k = 0;

                for (int i = 0; i < TotalGroup; i++)
                {
                    DisplayRoot.Add(new List<string>());

                    int jmax;
                    if (i < TotalGroup - 1)
                    {
                        jmax = Convert.ToInt32(cfg["每组字数"]);
                    }
                    else
                    {
                        jmax = RootList.Count - Convert.ToInt32(cfg["每组字数"]) * (TotalGroup - 1);
                    }
                    for (int j = 0; j < jmax; j++)
                    {
                        DisplayRoot[i].Add(RootList[k]);

                        k++;
                    }
                }
            }



            JumpGroup();

            RetypeCount = 0;
            MaxHitRate = 0;
            InitSlider();

            InitGroup();


        }

        public static IEnumerable<T> Randomize<T>(IEnumerable<T> source)
        {
            Random rnd = new Random();
            return source.OrderBy((item) => rnd.Next());
        }
        private void InGroupRand() // 组内重排
        {
            DisplayRoot[Convert.ToInt32(cfg["上次的段数"])] = Randomize(DisplayRoot[Convert.ToInt32(cfg["上次的段数"])]).ToList() ;  

        }









        private int CalWordCount()
        {
            int sum = 0;
            foreach (var item in DisplayRoot[Convert.ToInt32(cfg["上次的段数"])])
            {
                sum += new StringInfo(item).LengthInTextElements;
            }

            return sum;
        }



        public void GetNextRound(double accuracy, double hitrate, int wrong, string result)
        {
            // 每次打完都累加实际字数（包括重打）
            roundActualWords += Score.InputWordCount;
            // 每次打完都累加正确字数和用时（不管是否通过，包括打到一半就重打的情况）
            roundCorrectWords += (int)(Score.InputWordCount * accuracy);
            roundTotalTime += Score.Time.TotalSeconds;

            if (accuracy >= 0.9999 && hitrate >= TargetHit && wrong == 0)
            {
                // 累加本组统计数据
                roundTotalWords += Score.TotalWordCount;
                roundCompletedGroups++;
                roundHitRates.Add(hitrate);
                roundSpeeds.Add(Score.Speed);
                bool wasNotStarted = !hasStartedPractice;
                hasStartedPractice = true;  // 标记已开始练习

                string t =  "击键 " + hitrate.ToString("F2") + "/" + TargetHit.ToString("0.00");
                AutoNextGroup();  // 这里会保存统计数据（段号已更新）
                string matchText = GetMatchText();
                MainWindow.Current.LoadText(matchText, RetypeType.first, TxtSource.trainer, false, true);

                MainWindow.Current.UpdateTopStatusText(t);

                QQHelper.SendQQMessageD(MainWindow.Current.QQGroupName, result, matchText, 150, MainWindow.Current);

                // 更新本轮统计显示
                UpdateRoundStatus();

                // 更新UI状态
                if (wasNotStarted)
                {
                    UpdateUIState();
                }
            }
            else
            {
                string t = "击键 " + hitrate.ToString("F2") + "/" + TargetHit.ToString("0.00");
                RetypeGroup(true,true);
                MainWindow.Current.LoadText(GetMatchText(), RetypeType.retype, TxtSource.trainer, false, true);
                MainWindow.Current.UpdateTopStatusText(t);
                if (hitrate >= MaxHitRate && accuracy >= 0.9999 && wrong == 0)
                {
                    QQHelper.SendQQMessage(MainWindow.Current.QQGroupName, result, 150, MainWindow.Current);
                    MaxHitRate = hitrate;

                }
         //       if (RetypeCount + 1 >= 5 && (RetypeCount + 1) % 5 == 0) //重打5次发一下成绩

                // 练习失败后也要保存统计数据（但段号不变）
                SaveCurrentArticleStatistics();
            }
        }

        /// <summary>
        /// 记录部分进度（用于F3重打时统计打到一半的数据）
        /// </summary>
        public void RecordPartialProgress(int inputWordCount, double timeSeconds, double accuracy)
        {
            if (inputWordCount > 0 && timeSeconds > 0)
            {
                roundActualWords += inputWordCount;
                roundCorrectWords += (int)(inputWordCount * accuracy);
                roundTotalTime += timeSeconds;
            }
        }

        public void F3()
        {

      //      RetypeGroup(false, false);
            MainWindow.Current.LoadText(GetMatchText(), RetypeType.retype, TxtSource.trainer, false, true);
            MainWindow.Current.UpdateTopStatusText("重打");
        }

        public void CtrlL()
        {

            RetypeGroup(true, false);
            MainWindow.Current.LoadText(GetMatchText(), RetypeType.retype, TxtSource.trainer, false, true);
            MainWindow.Current.UpdateTopStatusText("乱序");
        }

        private void DisplayHit()
        {

            TBHitrate.Text = "换段击键 " + TargetHit.ToString("0.00");

        }

        private void DisplayHit(double hitrate)
        {

            TBHitrate.Text = "击键 "+ hitrate.ToString("F2") + "/" + TargetHit.ToString("0.00");

        }

        /// <summary>
        /// 更新本轮统计显示（实时显示均速、均击、字数等）
        /// </summary>
        private void UpdateRoundStatus()
        {
            string statText = "";

            // 只有开始练习时才显示统计
            if (hasStartedPractice)
            {
                double avgHitRate = 0;
                double avgSpeed = 0;
                double avgAccuracy = 0;

                if (roundHitRates.Count > 0)
                    avgHitRate = roundHitRates.Average();
                if (roundSpeeds.Count > 0)
                    avgSpeed = roundSpeeds.Average();

                // 总键准 = 打对字数 / 实际字数（包括所有重打）
                if (roundActualWords > 0)
                    avgAccuracy = (double)roundCorrectWords / roundActualWords * 100;

                // 进度百分比
                double progress = (double)roundCompletedGroups / TotalGroup * 100;

                statText = string.Format("{0} 均击{1:F2} 均速{2:F2} 字数{3} 实际{4} 进度{5:F0}%",
                    TxtFile, avgHitRate, avgSpeed, roundTotalWords, roundActualWords, progress);
            }

            // 更新练单器窗口内的显示
            stattxt2.Text = statText;

            // 更新主窗口成绩栏的显示
            if (MainWindow.Current != null)
            {
                MainWindow.Current.UpdateTrainerStat(statText);
            }
        }

        private void UpdateFileList()
        {
            if (!Directory.Exists(Folder))
            {
                MessageBox.Show($"练单器目录不存在: {Folder}\n请确保该目录存在并包含练习文件。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            DirectoryInfo folder = new DirectoryInfo(Folder);

            foreach (FileInfo file in folder.GetFiles("*.txt"))
                FileSelector.Items.Add(file.Name.Substring(0, file.Name.Length - 4));

            if (FileSelector.Items.Count > 0)
                FileSelector.SelectedIndex = 0;
        }

        private void RetypeGroup(bool rand, bool count) //重打本组
        {
            if (count)
                RetypeCount++;

            if (rand)
                InGroupRand();

            ShowWords();


            WriteCfg();

            TargetHit = Convert.ToDouble(cfg["换段击键"]) - Convert.ToDouble(cfg["每轮降击"]) * (RetypeCount);
            if (mode == "varible")
                TargetHit = Math.Round((float)(TargetHit * Math.Pow(AverageGroupSize / (double)DisplayRoot[Convert.ToInt32(cfg["上次的段数"])].Count, 0.35)), 2);

            if (TargetHit < 0.01)
                TargetHit = 0.01;

            DisplayHit();

            stattxt.Text = "第 " + (Convert.ToInt32(cfg["上次的段数"]) + 1) + "/" + TotalGroup + " 段";

            // 更新本轮统计显示
            UpdateRoundStatus();
        }
        private void InitGroup() //初始化组
        {
            // 不要重置 RetypeCount 和 MaxHitRate，因为 LoadArticleStatistics() 可能已经恢复了它们
            // RetypeCount = 0;  // 已移除
            // MaxHitRate = 0;    // 已移除


            InGroupRand();
            ShowWords();
            LoadText();
  
            WriteCfg();

            TargetHit = Convert.ToDouble(cfg["换段击键"]) - Convert.ToDouble(cfg["每轮降击"]) * (RetypeCount);
            if (mode == "varible")
                TargetHit = Math.Round((float)(TargetHit * Math.Pow(AverageGroupSize / (double)DisplayRoot[Convert.ToInt32(cfg["上次的段数"])].Count, 0.35)), 2);

            if (TargetHit < 0.01)
                TargetHit = 0.01;
   


                DisplayHit();
            
            stattxt.Text = "第 " + (Convert.ToInt32(cfg["上次的段数"]) + 1) + "/" + TotalGroup + " 段";

            // 更新本轮统计显示（初始化时清空）
            UpdateRoundStatus();

            // 更新UI状态（进度条/重置按钮/按钮文字）
            UpdateUIState();


        }



        public void AutoNextGroup()
        {

            cfg["上次的段数"] = (Convert.ToInt32(cfg["上次的段数"]) + 1).ToString();

            // 段号更新后保存统计数据（确保保存的是最新的段号）
            SaveCurrentArticleStatistics();

            // 检查是否完成一整轮
            if (Convert.ToInt32(cfg["上次的段数"]) == TotalGroup)
            {
                // 完成一轮，显示统计并记录日志
                ShowRoundStatistics();
                RecordRoundLog();

                cfg["上次的段数"] = "0";

                // 重置统计数据，准备下一轮
                ResetRoundStatistics();
                // 清空统计显示
                UpdateRoundStatus();
            }

            sld.Value = Convert.ToInt32(cfg["上次的段数"]) + 1;


            InitGroup();




        }

        /// <summary>
        /// 重置本轮统计数据
        /// </summary>
        private void ResetRoundStatistics()
        {
            roundTotalWords = 0;
            roundActualWords = 0;
            roundCorrectWords = 0;
            roundTotalTime = 0;
            roundCompletedGroups = 0;
            roundHitRates.Clear();
            roundSpeeds.Clear();
            hasStartedPractice = false;

            // 清空练单器窗口内的成绩显示
            stattxt2.Text = "";

            // 清空主窗口成绩栏的显示
            if (MainWindow.Current != null)
            {
                MainWindow.Current.UpdateTrainerStat("");
            }

            // 重置后也要保存
            SaveCurrentArticleStatistics();
            UpdateUIState();
        }

        /// <summary>
        /// 保存当前文章的统计数据到字典
        /// </summary>
        private void SaveCurrentArticleStatistics()
        {
            if (string.IsNullOrEmpty(TxtFile))
                return;

            var data = new ArticleStatisticsData
            {
                RoundTotalWords = roundTotalWords,
                RoundActualWords = roundActualWords,
                RoundCorrectWords = roundCorrectWords,
                RoundTotalTime = roundTotalTime,
                RoundCompletedGroups = roundCompletedGroups,
                RoundHitRates = new List<double>(roundHitRates),
                RoundSpeeds = new List<double>(roundSpeeds),
                HasStartedPractice = hasStartedPractice,
                LastSection = Convert.ToInt32(cfg["上次的段数"]),
                RetypeCount = RetypeCount,
                MaxHitRate = MaxHitRate,
                // 深拷贝 DisplayRoot
                DisplayRoot = DisplayRoot.Select(section => new List<string>(section)).ToList()
            };

            articleStatisticsDict[TxtFile] = data;
            SaveStatisticsToFile();
        }

        /// <summary>
        /// 加载指定文章的统计数据
        /// </summary>
        private void LoadArticleStatistics(string articleName)
        {
            if (string.IsNullOrEmpty(articleName))
                return;

            if (articleStatisticsDict.ContainsKey(articleName))
            {
                var data = articleStatisticsDict[articleName];
                roundTotalWords = data.RoundTotalWords;
                roundActualWords = data.RoundActualWords;
                roundCorrectWords = data.RoundCorrectWords;
                roundTotalTime = data.RoundTotalTime;
                roundCompletedGroups = data.RoundCompletedGroups;
                roundHitRates = new List<double>(data.RoundHitRates);
                roundSpeeds = new List<double>(data.RoundSpeeds);
                hasStartedPractice = data.HasStartedPractice;

                // 恢复段号
                cfg["上次的段数"] = data.LastSection.ToString();
                // 恢复重打次数和最高击键率
                RetypeCount = data.RetypeCount;
                MaxHitRate = data.MaxHitRate;
                // 恢复文章内容（包括乱序状态）
                if (data.DisplayRoot != null && data.DisplayRoot.Count > 0)
                {
                    DisplayRoot.Clear();
                    foreach (var section in data.DisplayRoot)
                    {
                        DisplayRoot.Add(new List<string>(section));
                    }
                }
            }
            else
            {
                // 新文章，初始化为空数据
                roundTotalWords = 0;
                roundActualWords = 0;
                roundCorrectWords = 0;
                roundTotalTime = 0;
                roundCompletedGroups = 0;
                roundHitRates = new List<double>();
                roundSpeeds = new List<double>();
                hasStartedPractice = false;
                RetypeCount = 0;
                MaxHitRate = 0;
            }

            UpdateUIState();
        }

        /// <summary>
        /// 保存统计数据到文件
        /// </summary>
        private void SaveStatisticsToFile()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(StatisticsFileName))
                {
                    foreach (var kvp in articleStatisticsDict)
                    {
                        var data = kvp.Value;
                        writer.WriteLine($"{kvp.Key}\t{data.RoundTotalWords}\t{data.RoundActualWords}\t{data.RoundCorrectWords}\t{data.RoundTotalTime}\t{data.RoundCompletedGroups}\t{data.HasStartedPractice}\t{data.LastSection}\t{data.RetypeCount}\t{data.MaxHitRate}");

                        // 保存击键率和速度列表
                        writer.WriteLine($"H\t{string.Join(",", data.RoundHitRates)}");
                        writer.WriteLine($"S\t{string.Join(",", data.RoundSpeeds)}");

                        // 保存 DisplayRoot（乱序后的文章内容）
                        // 格式：D\t段数\t每段的字数（逗号分隔）
                        writer.Write("D\t");
                        writer.Write(data.DisplayRoot.Count);
                        foreach (var section in data.DisplayRoot)
                        {
                            writer.Write($"\t{string.Join("|", section)}");
                        }
                        writer.WriteLine();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存统计数据失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从文件加载统计数据
        /// </summary>
        private void LoadStatisticsFromFile()
        {
            if (!File.Exists(StatisticsFileName))
                return;

            try
            {
                using (StreamReader reader = new StreamReader(StatisticsFileName))
                {
                    string line;
                    string currentArticle = null;
                    ArticleStatisticsData data = null;

                    while ((line = reader.ReadLine()) != null)
                    {
                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        if (line.StartsWith("H\t"))
                        {
                            // 击键率列表
                            if (data != null)
                            {
                                var hitRateStr = line.Substring(2);
                                if (!string.IsNullOrEmpty(hitRateStr))
                                {
                                    data.RoundHitRates = hitRateStr.Split(',').Select(s => double.TryParse(s, out double val) ? val : 0).ToList();
                                }
                            }
                        }
                        else if (line.StartsWith("S\t"))
                        {
                            // 速度列表
                            if (data != null)
                            {
                                var speedStr = line.Substring(2);
                                if (!string.IsNullOrEmpty(speedStr))
                                {
                                    data.RoundSpeeds = speedStr.Split(',').Select(s => double.TryParse(s, out double val) ? val : 0).ToList();
                                }
                            }
                        }
                        else if (line.StartsWith("D\t"))
                        {
                            // DisplayRoot（乱序后的文章内容）
                            if (data != null)
                            {
                                var parts = line.Split('\t');
                                if (parts.Length >= 2)
                                {
                                    int sectionCount = int.TryParse(parts[1], out int count) ? count : 0;
                                    data.DisplayRoot = new List<List<string>>();
                                    for (int i = 0; i < sectionCount && i + 2 < parts.Length; i++)
                                    {
                                        var sectionStr = parts[i + 2];
                                        if (!string.IsNullOrEmpty(sectionStr))
                                        {
                                            var section = sectionStr.Split('|').Where(s => !string.IsNullOrEmpty(s)).ToList();
                                            data.DisplayRoot.Add(section);
                                        }
                                        else
                                        {
                                            data.DisplayRoot.Add(new List<string>());
                                        }
                                    }
                                }
                                // DisplayRoot 是最后一行，保存数据
                                articleStatisticsDict[currentArticle] = data;
                            }
                        }
                        else
                        {
                            // 文章数据行
                            var parts = line.Split('\t');
                            if (parts.Length >= 10)
                            {
                                currentArticle = parts[0];
                                data = new ArticleStatisticsData
                                {
                                    RoundTotalWords = int.TryParse(parts[1], out int totalWords) ? totalWords : 0,
                                    RoundActualWords = int.TryParse(parts[2], out int actualWords) ? actualWords : 0,
                                    RoundCorrectWords = int.TryParse(parts[3], out int correctWords) ? correctWords : 0,
                                    RoundTotalTime = double.TryParse(parts[4], out double totalTime) ? totalTime : 0,
                                    RoundCompletedGroups = int.TryParse(parts[5], out int completedGroups) ? completedGroups : 0,
                                    HasStartedPractice = bool.TryParse(parts[6], out bool started) ? started : false,
                                    LastSection = int.TryParse(parts[7], out int lastSection) ? lastSection : 0,
                                    RetypeCount = int.TryParse(parts[8], out int retypeCount) ? retypeCount : 0,
                                    MaxHitRate = double.TryParse(parts[9], out double maxHitRate) ? maxHitRate : 0
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载统计数据失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 显示本轮统计弹窗
        /// </summary>
        private void ShowRoundStatistics()
        {
            if (roundCompletedGroups == 0)
                return;

            double avgHitRate = 0;
            double avgSpeed = 0;
            double avgAccuracy = 0;

            if (roundHitRates.Count > 0)
                avgHitRate = roundHitRates.Average();
            if (roundSpeeds.Count > 0)
                avgSpeed = roundSpeeds.Average();

            // 总键准 = 打对字数 / 实际字数（包括所有重打）
            if (roundActualWords > 0)
                avgAccuracy = (double)roundCorrectWords / roundActualWords * 100;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("【本轮练习完成】");
            sb.AppendLine();
            sb.AppendLine($"练习项：{TxtFile}");
            sb.AppendLine($"总字数：{roundTotalWords}");
            sb.AppendLine($"实际字数：{roundActualWords}");
            sb.AppendLine($"平均击键：{avgHitRate:F2}");
            sb.AppendLine($"平均速度：{avgSpeed:F2}");
            sb.AppendLine($"总键准：{avgAccuracy:F2}%");
            sb.AppendLine($"总用时：{Score.FormatTime(roundTotalTime)}");

            MessageBox.Show(sb.ToString(), "练习统计", MessageBoxButton.OK, MessageBoxImage.Information);

            // 生成成绩记录格式，添加到主窗口成绩区
            string resultRecord = string.Format("[练单] {0} 击键{1:F2} 速度{2:F2} 字数{3} 实际{4} 键准{5:F2}% 用时{6}",
                TxtFile, avgHitRate, avgSpeed, roundTotalWords, roundActualWords, avgAccuracy, Score.FormatTime(roundTotalTime));
            if (MainWindow.Current != null)
            {
                MainWindow.Current.UpdateTypingStat(resultRecord);
            }

            // 根据"自动发送成绩"开关决定是否复制成绩到剪贴板
            if (Config.GetBool("自动发送成绩"))
            {
                MainWindow.Win32SetText(resultRecord);
            }
        }

        /// <summary>
        /// 记录本轮练习到日志
        /// </summary>
        private void RecordRoundLog()
        {
            if (roundCompletedGroups == 0)
                return;

            double avgHitRate = 0;
            double avgSpeed = 0;
            double avgAccuracy = 0;

            if (roundHitRates.Count > 0)
                avgHitRate = roundHitRates.Average();
            if (roundSpeeds.Count > 0)
                avgSpeed = roundSpeeds.Average();

            // 总键准 = 打对字数 / 实际字数（包括所有重打）
            if (roundActualWords > 0)
                avgAccuracy = (double)roundCorrectWords / roundActualWords * 100;

            // 使用与文章日志相同的 ArticleRecord 格式
            ArticleLog.ArticleRecord record = new ArticleLog.ArticleRecord
            {
                Time = DateTime.Now,
                ArticleName = TxtFile,
                TotalWords = roundTotalWords,
                InputWords = roundActualWords,
                Speed = avgSpeed,
                HitRate = avgHitRate,
                Accuracy = avgAccuracy / 100,  // 转换为小数形式
                Wrong = roundActualWords - roundCorrectWords,  // 错字数 = 实际字数 - 打对字数
                Backs = 0,  // 打单器不跟踪退格
                Correction = 0,  // 打单器不跟踪回改
                KPW = avgSpeed > 0 ? avgHitRate / avgSpeed * 60 : 0,  // 码长 = 击键/速度*60
                LRRatio = 0,  // 打单器不跟踪左右键比
                TotalHit = (int)(avgHitRate * roundTotalTime),  // 总键数
                TotalSeconds = roundTotalTime,
                ArticleMark = "",  // 打单器没有段号
                WasteCodes = 0,  // 打单器不跟踪废码
                CiRatio = 0,  // 打单器不跟踪打词率
                Choose = 0,  // 打单器不跟踪选重
                BiaoDing = 0,  // 打单器不跟踪标顶
                DifficultyName = ""  // 打单器没有难度名称
            };

            TrainerLog.WriteRecord(record);
        }
        
        string GetMatchText ()
        {
            StringBuilder sb = new StringBuilder();
            string name = FileSelector.SelectedItem.ToString() + " " + "目标" + Convert.ToDouble(cfg["换段击键"]).ToString("F2");

            if (Convert.ToDouble(cfg["每轮降击"]) > 0.000001)
                name += "-" + Convert.ToDouble(cfg["每轮降击"]).ToString("F2");
            sb.Append(name);
            sb.AppendLine();
            string txt = string.Join("", DisplayRoot[Convert.ToInt32(cfg["上次的段数"])]); 
            sb.Append(txt);
            sb.AppendLine();
            sb.Append("-----第");
            sb.Append(Convert.ToInt32(cfg["上次的段数"]) + 1);
            sb.Append("段");


            sb.Append("-");

            sb.Append(" 共");
            sb.Append(TotalGroup);
            sb.Append("段 ");

            /*
            sb.Append(" 进度 ");
            sb.Append((Index - 1) * SectionSize);
            sb.Append("/");
            sb.Append(display);
            sb.Append("字 ");
*/
            sb.Append(" 本段");
            sb.Append(new StringInfo(txt).LengthInTextElements);
            sb.Append("字 ");

            sb.Append("练单器");
            return sb.ToString();
        }

        private void JumpGroup()
        {

            if (Jumped)
            {
                cfg["上次的段数"] = "0";
                return;
            }

            else
                Jumped = true;



            if (Convert.ToInt32(cfg["上次的段数"]) > 0 && Convert.ToInt32(cfg["上次的段数"]) < TotalGroup)
            {

                sld.Value = Convert.ToInt32(cfg["上次的段数"]) + 1;
    
                InitGroup();

                return;
            }
            else
            {
                cfg["上次的段数"] = "0";
                return;
            }

        }

        public WinTrainer()
        {

            InitializeComponent();

            // 应用主题颜色
            ApplyThemeColors();

            UpdateFileList();
            InitCfg();

            // 加载持久化的统计数据
            LoadStatisticsFromFile();

            ReadTxt();
            ShowWords();
            LoadText();



        }

        private void FileSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CfgInit)
                ReadTxt();

        }


     


        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (CfgInit && SliderInit)
            {
                int newSection = Convert.ToInt32(sld.Value) - 1;
                int oldSection = Convert.ToInt32(cfg["上次的段数"]);

                // 如果用户已经开始练习，并且改变了段数
                if (hasStartedPractice && newSection != oldSection)
                {
                    if (newSection == 0)
                    {
                        // 拖到第一段，重新开始计分
                        MessageBox.Show("当前分数已作废，从第一段重新开始计分", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                        ResetRoundStatistics();
                    }
                    else
                    {
                        // 拖到其他段，分数作废但不重新计分
                        MessageBox.Show("当前分数已作废", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                        hasStartedPractice = false;
                        roundTotalWords = 0;
                        roundActualWords = 0;
                        roundCorrectWords = 0;
                        roundTotalTime = 0;
                        roundCompletedGroups = 0;
                        roundHitRates.Clear();
                        roundSpeeds.Clear();
                    }
                }

                cfg["上次的段数"] = newSection.ToString();

                InitGroup();
            }
        }





        private void RandAllClick(object sender, RoutedEventArgs e)
        {
            RandAllGroup();
        }


        private void RandAllGroup()
        {
            // 获取当前段号，只对剩余段乱序
            int currentSection = Convert.ToInt32(cfg["上次的段数"]);

            if (mode == "fixed")
            {
                List<string> RootList = new List<string>();

                // 只收集从当前段开始到最后的段
                for (int i = currentSection; i < DisplayRoot.Count; i++)
                {
                    foreach (string s in DisplayRoot[i])
                    {
                        RootList.Add(s);
                    }
                }

                int count = RootList.Count;

                if (count == 0)
                    return;


                int[] arr = new int[count];

                for (int j = 0; j < count; j++)
                {
                    arr[j] = j;
                }

                int[] arr2 = new int[count];


                Random rand = new Random();

                for (int j = 0; j < count; j++)
                {
                    int rd_rng = count - j;
                    int r = rand.Next(rd_rng);
                    arr2[j] = arr[r];
                    arr[r] = arr[rd_rng - 1];

                }





                string[] tmpstr = new string[count];

                for (int j = 0; j < count; j++)
                {
                    tmpstr[j] = RootList[arr2[j]];
                }

                for (int j = 0; j < count; j++)
                {
                    RootList[j] = tmpstr[j];
                }



                int k = 0;
                // 保留已打过的段，只重新生成从当前段开始的段
                List<List<string>> oldSections = new List<List<string>>();
                for (int i = 0; i < currentSection; i++)
                {
                    oldSections.Add(DisplayRoot[i]);
                }

                DisplayRoot.Clear();

                // 添加已打过的段（保持不变）
                for (int i = 0; i < currentSection; i++)
                {
                    DisplayRoot.Add(oldSections[i]);
                }

                // 添加乱序后的剩余段
                for (int i = currentSection; i < TotalGroup; i++)
                {
                    DisplayRoot.Add(new List<string>());

                    int jmax;
                    if (i < TotalGroup - 1)
                    {
                        jmax = Convert.ToInt32(cfg["每组字数"]);
                    }
                    else
                    {
                        jmax = count - Convert.ToInt32(cfg["每组字数"]) * (TotalGroup - 1 - currentSection);
                    }
                    for (int j = 0; j < jmax; j++)
                    {
                        DisplayRoot[i].Add(RootList[k]);

                        k++;
                    }
                }
                InitGroup();
                InitSlider();


            }
            else if (mode == "varible")
            {
                int count = DisplayRoot.Count;

                // 只对剩余段进行乱序
                int remainingCount = count - currentSection;
                if (remainingCount <= 0)
                    return;

                int[] arr = new int[remainingCount];

                for (int j = 0; j < remainingCount; j++)
                {
                    arr[j] = currentSection + j;
                }

                int[] arr2 = new int[remainingCount];


                Random rand = new Random();

                for (int j = 0; j < remainingCount; j++)
                {
                    int rd_rng = remainingCount - j;
                    int r = rand.Next(rd_rng);
                    arr2[j] = arr[r];
                    arr[r] = arr[rd_rng - 1 + currentSection];

                }



                // 保存旧段顺序
                List<List<string>> tmpstr = new List<List<string>>();
                for (int i = 0; i < count; i++)
                {
                    tmpstr.Add(new List<string>(DisplayRoot[i]));
                }


                // 重新排列剩余段
                for (int j = 0; j < remainingCount; j++)
                {
                    DisplayRoot[currentSection + j] = tmpstr[arr2[j]];
                }

                InitGroup();

                InitSlider();



            }


        }

        private void norm_Click(object sender, RoutedEventArgs e)
        {
            ReadTxt();
        }







        // 旧的TextBox事件处理方法已被滚轮选择器替代
        // private void speed_TextChanged(object sender, TextChangedEventArgs e)
        // {
        //     if (CfgInit)
        //     {
        //         cfg["换段击键"] = speed.Text;
        //         if (DisplayRoot != null)
        //         {
        //             InitGroup();
        //         }
        //         WriteCfg();
        //     }
        // }





        private void InitCfg()
        {
            char[] s2 = { '\t', '\r', '\n' };
            if (File.Exists("TrainerConfig.txt"))
            {
                StreamReader sr = new StreamReader("TrainerConfig.txt");
                string[] lines = sr.ReadToEnd().Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string line in lines)
                {
                    string[] ls = line.Split(s2, StringSplitOptions.RemoveEmptyEntries);

                    if (ls.Length < 2)
                        continue;

                    cfg[ls[0]] = ls[1];
                }
                sr.Close();

            }
            else
            {
                WriteCfg();
            }


            for (int i = 0; i < FileSelector.Items.Count; i++)
            {
                if (cfg["上次打开的文件"] == FileSelector.Items[i].ToString() + ".txt")
                {
                    FileSelector.SelectedIndex = i;
                }
            }

            // 设置数值显示
            speedDisplay.Text = cfg["换段击键"];
            numDisplay.Text = cfg["每组字数"];
            hitDecreaseDisplay.Text = cfg["每轮降击"];

            this.Top = MainWindow.Current.Top;
            this.Left = MainWindow.Current.Left - this.Width;

            CfgInit = true;

        }





        private void WriteCfg()
        {

            cfg["删除此文件即可重置设置"] = "获取更新加Q群：" + Config.GetString("软件更新Q群");

            try
            {
                StreamWriter sr = new StreamWriter("TrainerConfig.txt");
                foreach (var item in cfg)
                {
                    sr.WriteLine(item.Key + "\t" + item.Value);
                }
                sr.Flush();
                sr.Close();
            }
            catch (Exception)
            {

                
            }

        }


 



        // 旧的TextBox事件处理方法已被滚轮选择器替代
        // private void TextNum_TextChanged(object sender, TextChangedEventArgs e)
        // {
        //     if (CfgInit)
        //     {
        //         if (int.TryParse(TextNum.Text, out int tmp2))
        //         {
        //             cfg["每组字数"] = tmp2.ToString();
        //             if (DisplayRoot != null)
        //             {
        //                 ReadTxt();
        //                 ShowWords();
        //                 LoadText();
        //             }
        //             WriteCfg();
        //         }
        //         else
        //         {
        //             TextNum.Text = cfg["每组字数"];
        //         }
        //     }
        // }

        // private void TextHitDecrease_TextChanged(object sender, TextChangedEventArgs e)
        // {
        //     double tmp2;
        //     if (CfgInit)
        //     {
        //         if (double.TryParse(TextHitDecrease.Text, out tmp2))
        //         {
        //             cfg["每轮降击"] = tmp2.ToString();
        //             if (DisplayRoot != null)
        //             {
        //                 InitGroup();
        //             }
        //             WriteCfg();
        //         }
        //         else
        //         {
        //             TextHitDecrease.Text = cfg["每轮降击"];
        //         }
        //     }
        // }


        private int GetCharCount(List<StringInfo> siList)
        {
            var lens = from si in siList select si.LengthInTextElements;

            return lens.Sum();


        }

        private int GetCharCount(string s)
        {
            return new StringInfo(s).LengthInTextElements;
        }



        private void LoadText()
        {
            
            MainWindow.Current.LoadText(GetMatchText(), RetypeType.first, TxtSource.trainer, false,true);
        }

        private void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.Current.LoadText(GetMatchText(), RetypeType.first, TxtSource.trainer, false, true);
            QQHelper.SendQQMessage(MainWindow.Current.QQGroupName, GetMatchText(), 150, MainWindow.Current);
        }

        /// <summary>
        /// 重置统计数据按钮点击事件
        /// </summary>
        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("确定要重置当前文章的统计数据吗？", "重置确认", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                ResetRoundStatistics();
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {

            if (TextInfo.Exit)
                e.Cancel = false;
            else
            {
                // 窗口隐藏前保存统计数据
                SaveCurrentArticleStatistics();
                e.Cancel = true;//取消这次关闭事件
                Hide();//隐藏窗口，以便下次调用show
            }


        }

        private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // 窗口显示时加载统计数据
            if (this.IsVisible && CfgInit)
            {
                LoadArticleStatistics(TxtFile);
            }
        }

        // ==================== 窗口控制相关方法 ====================

        // 标题栏拖动
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                BtnMaximize_Click(sender, e);
            }
            else
            {
                this.DragMove();
            }
        }

        // 最小化
        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        // 最大化
        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
        {
            if (_isCustomMaximized)
            {
                // 恢复窗口
                this.Left = _restoreBounds.X;
                this.Top = _restoreBounds.Y;
                this.Width = _restoreBounds.Width;
                this.Height = _restoreBounds.Height;
                _isCustomMaximized = false;
                BtnMaximize.Content = "◻";
            }
            else
            {
                // 保存当前窗口位置和大小
                _restoreBounds = new Rect(this.Left, this.Top, this.Width, this.Height);

                // 使用工作区（不含任务栏）进行最大化
                var workArea = SystemParameters.WorkArea;
                this.Left = workArea.Left;
                this.Top = workArea.Top;
                this.Width = workArea.Width;
                this.Height = workArea.Height;
                _isCustomMaximized = true;
                BtnMaximize.Content = "◰";
            }
        }

        // 关闭
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        // 窗口resize处理
        private void ResizeBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var border = sender as FrameworkElement;
            if (border == null) return;

            var windowHandle = new WindowInteropHelper(this).Handle;
            if (windowHandle == IntPtr.Zero) return;

            ReleaseCapture();

            int direction = 0;
            string borderName = border.Name;

            switch (borderName)
            {
                case "ResizeTop": direction = HT_TOP; break;
                case "ResizeBottom": direction = HT_BOTTOM; break;
                case "ResizeLeft": direction = HT_LEFT; break;
                case "ResizeRight": direction = HT_RIGHT; break;
                default: return;
            }

            SendMessage(windowHandle, WM_NCLBUTTONDOWN, (IntPtr)direction, IntPtr.Zero);
        }

        private void ResizeBorder_MouseMove(object sender, MouseEventArgs e)
        {
            var border = sender as FrameworkElement;
            if (border == null) return;

            string borderName = border.Name;

            switch (borderName)
            {
                case "ResizeTop":
                case "ResizeBottom":
                    this.Cursor = Cursors.SizeNS;
                    break;
                case "ResizeLeft":
                case "ResizeRight":
                    this.Cursor = Cursors.SizeWE;
                    break;
                default:
                    this.Cursor = Cursors.Arrow;
                    break;
            }
        }

        private void ResizeBorder_MouseLeave(object sender, MouseEventArgs e)
        {
            this.Cursor = Cursors.Arrow;
        }

        // ==================== 数值调节按钮事件 ====================

        // 击键速度调节
        private void SpeedUp(object sender, RoutedEventArgs e)
        {
            if (CfgInit && double.TryParse(speedDisplay.Text, out double value))
            {
                value += 0.5;
                if (value > 100) value = 100;
                speedDisplay.Text = value.ToString("F1");
                cfg["换段击键"] = value.ToString("F1");

                // 检查并调整每轮降击
                if (double.TryParse(hitDecreaseDisplay.Text, out double decreaseValue))
                {
                    if (decreaseValue > value)
                    {
                        hitDecreaseDisplay.Text = value.ToString("F1");
                        cfg["每轮降击"] = value.ToString("F1");
                    }
                }

                if (DisplayRoot != null)
                    InitGroup();
                WriteCfg();
            }
        }

        private void SpeedDown(object sender, RoutedEventArgs e)
        {
            if (CfgInit && double.TryParse(speedDisplay.Text, out double value))
            {
                value -= 0.5;
                if (value < 0) value = 0;
                speedDisplay.Text = value.ToString("F1");
                cfg["换段击键"] = value.ToString("F1");

                if (DisplayRoot != null)
                    InitGroup();
                WriteCfg();
            }
        }

        // 字数组调节
        private void NumUp(object sender, RoutedEventArgs e)
        {
            if (CfgInit && int.TryParse(numDisplay.Text, out int value))
            {
                value += 1;
                if (value > 9999) value = 9999;
                numDisplay.Text = value.ToString();
                cfg["每组字数"] = value.ToString();

                if (DisplayRoot != null)
                {
                    ReadTxt();
                    ShowWords();
                    LoadText();
                }
                WriteCfg();
            }
        }

        private void NumDown(object sender, RoutedEventArgs e)
        {
            if (CfgInit && int.TryParse(numDisplay.Text, out int value))
            {
                value -= 1;
                if (value < 1) value = 1;
                numDisplay.Text = value.ToString();
                cfg["每组字数"] = value.ToString();

                if (DisplayRoot != null)
                {
                    ReadTxt();
                    ShowWords();
                    LoadText();
                }
                WriteCfg();
            }
        }

        // 字数显示TextChanged事件
        private void NumDisplay_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (CfgInit && numDisplay.Text.Length > 0)
            {
                if (int.TryParse(numDisplay.Text, out int value))
                {
                    if (value < 1) value = 1;
                    if (value > 99999) value = 99999;
                    cfg["每组字数"] = value.ToString();

                    if (DisplayRoot != null)
                    {
                        ReadTxt();
                        ShowWords();
                        LoadText();
                    }
                    WriteCfg();
                }
            }
        }

        // 换段击键显示TextChanged事件
        private void SpeedDisplay_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (CfgInit && speedDisplay.Text.Length > 0)
            {
                if (double.TryParse(speedDisplay.Text, out double value))
                {
                    if (value < 0) value = 0;
                    if (value > 100) value = 100;
                    cfg["换段击键"] = value.ToString("F1");

                    // 检查并调整每轮降击
                    if (double.TryParse(hitDecreaseDisplay.Text, out double decreaseValue))
                    {
                        if (decreaseValue > value)
                        {
                            hitDecreaseDisplay.Text = value.ToString("F1");
                            cfg["每轮降击"] = value.ToString("F1");
                        }
                    }

                    if (DisplayRoot != null)
                        InitGroup();
                    WriteCfg();
                }
            }
        }

        // 每轮降击显示TextChanged事件
        private void HitDecreaseDisplay_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (CfgInit && hitDecreaseDisplay.Text.Length > 0)
            {
                if (double.TryParse(hitDecreaseDisplay.Text, out double value))
                {
                    if (value < 0) value = 0;
                    // 限制不能超过击键值
                    if (double.TryParse(speedDisplay.Text, out double hitValue))
                    {
                        if (value > hitValue) value = hitValue;
                    }
                    cfg["每轮降击"] = value.ToString("F2");

                    if (DisplayRoot != null)
                        InitGroup();
                    WriteCfg();
                }
            }
        }

        // 每轮降击调节
        private void HitDecreaseUp(object sender, RoutedEventArgs e)
        {
            if (CfgInit && double.TryParse(hitDecreaseDisplay.Text, out double value))
            {
                value += 0.05;
                // 限制不能超过击键值
                if (double.TryParse(speedDisplay.Text, out double hitValue))
                {
                    if (value > hitValue) value = hitValue;
                }
                hitDecreaseDisplay.Text = value.ToString("F2");
                cfg["每轮降击"] = value.ToString("F2");

                if (DisplayRoot != null)
                    InitGroup();
                WriteCfg();
            }
        }

        private void HitDecreaseDown(object sender, RoutedEventArgs e)
        {
            if (CfgInit && double.TryParse(hitDecreaseDisplay.Text, out double value))
            {
                value -= 0.05;
                if (value < 0) value = 0;
                hitDecreaseDisplay.Text = value.ToString("F2");
                cfg["每轮降击"] = value.ToString("F2");

                if (DisplayRoot != null)
                    InitGroup();
                WriteCfg();
            }
        }

        // ==================== UI状态控制方法 ====================

        /// <summary>
        /// 根据练习状态更新UI：进度条/重置按钮可见性、按钮文字
        /// </summary>
        private void UpdateUIState()
        {
            if (hasStartedPractice)
            {
                // 练习开始后：隐藏进度条，显示重置按钮，按钮改为"余字乱序"
                sld.Visibility = Visibility.Collapsed;
                BtnReset.Visibility = Visibility.Visible;
                BtnRandAll.Content = "余字乱序";
            }
            else
            {
                // 未开始练习：显示进度条，隐藏重置按钮，按钮为"全体乱序"
                sld.Visibility = Visibility.Visible;
                BtnReset.Visibility = Visibility.Collapsed;
                BtnRandAll.Content = "全体乱序";
            }
        }

        // ==================== 主题颜色应用方法 ====================

        /// <summary>
        /// 刷新主题颜色（公共方法，供外部调用）
        /// </summary>
        public void RefreshTheme()
        {
            ApplyThemeColors();
        }

        /// <summary>
        /// 应用主题颜色到练单器窗口
        /// </summary>
        private void ApplyThemeColors()
        {
            try
            {
                // 获取主题颜色
                string windowBgColor = Config.GetString("窗体背景色");
                string windowFgColor = Config.GetString("窗体字体色");
                string displayBgColor = Config.GetString("跟打区背景色");
                string accentColor = Config.GetString("标题栏进度条颜色");

                // 转换颜色
                var bgBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#" + windowBgColor));
                var fgBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#" + windowFgColor));
                var displayBgBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#" + displayBgColor));
                var accentColorBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#" + accentColor));

                // 计算派生颜色
                var borderBrush = new SolidColorBrush(Color.FromRgb(
                    (byte)Math.Max(0, bgBrush.Color.R - 30),
                    (byte)Math.Max(0, bgBrush.Color.G - 30),
                    (byte)Math.Max(0, bgBrush.Color.B - 30)
                ));

                var toolbarBgBrush = new SolidColorBrush(Color.FromRgb(
                    (byte)Math.Max(0, bgBrush.Color.R - 15),
                    (byte)Math.Max(0, bgBrush.Color.G - 15),
                    (byte)Math.Max(0, bgBrush.Color.B - 15)
                ));

                var buttonBgBrush = new SolidColorBrush(Color.FromRgb(
                    (byte)Math.Min(255, bgBrush.Color.R + 20),
                    (byte)Math.Min(255, bgBrush.Color.G + 20),
                    (byte)Math.Min(255, bgBrush.Color.B + 20)
                ));

                var buttonHoverBrush = new SolidColorBrush(Color.FromRgb(
                    (byte)Math.Min(255, buttonBgBrush.Color.R + 15),
                    (byte)Math.Min(255, buttonBgBrush.Color.G + 15),
                    (byte)Math.Min(255, buttonBgBrush.Color.B + 15)
                ));

                // 更新资源字典中的颜色
                this.Resources["WindowBackground"] = bgBrush;
                this.Resources["WindowBorderBrush"] = borderBrush;
                this.Resources["TextForeground"] = fgBrush;
                this.Resources["ToolbarBackground"] = toolbarBgBrush;
                this.Resources["TypingAreaBackground"] = displayBgBrush;
                this.Resources["BorderBrush"] = borderBrush;
                this.Resources["ButtonBackground"] = buttonBgBrush;
                this.Resources["ButtonHoverBackground"] = buttonHoverBrush;
                this.Resources["AccentColor"] = accentColorBrush;

                // 更新DisplayGrid的背景色
                if (DisplayGrid != null)
                {
                    DisplayGrid.Background = displayBgBrush;
                }

                // 更新fld的前景色
                if (fld != null)
                {
                    fld.Foreground = fgBrush;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"应用主题颜色失败: {ex.Message}");
            }
        }
    }
}

