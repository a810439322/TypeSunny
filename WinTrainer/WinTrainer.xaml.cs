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
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using CoreTextInfo = TypeSunny.Core.TextInfo;
using TypeSunny.Core;
using TypeSunny.Logs;
using TypeSunny.UI;
using TypeSunny.Utils;
using Colors = TypeSunny.Utils.Colors;


namespace TypeSunny
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 




    public partial class WinTrainer : Window
    {
        // 调试日志 - 写入文件
        private static void WriteDebugLog(string message)
        {
            // 日志已禁用
        }

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
            {"换段键准", "100" },




             {"上次打开的文件", "" },
             {"上次的段数", "0" },
             // 新增配置项：字体大小和窗口状态
             {"练单器字体大小", "24" },
             {"练单器窗口宽度", "620" },
             {"练单器窗口高度", "450" },
             {"练单器窗口左边", "0" },
             {"练单器窗口顶边", "0" },
             {"练单器最大化状态", "False" },
             {"练单发文后关闭窗口", "否" },

        };

        bool CfgInit;
        bool SliderInit;
        private bool _isRefreshingFileList;
        private bool _isUpdatingTrainerMainWindowMemoryCheckBox;
        private bool _isApplyingArticleSettings;
        private readonly TrainerAutoSendPolicy autoSendPolicy = new TrainerAutoSendPolicy();
        private readonly ArticleSendKeyboardPolicy keyboardPolicy = new ArticleSendKeyboardPolicy();
        private Dictionary<string, string> defaultArticleSettings = new Dictionary<string, string>();

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
        public string CurrentExerciseName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(TxtFile))
                    return TxtFile;
                if (FileSelector != null && FileSelector.SelectedItem != null)
                    return GetActualFileName(FileSelector.SelectedItem.ToString());
                return "";
            }
        }

        // 显示名 → 实际文件名（不含.txt）的映射
        Dictionary<string, string> displayToFileName = new Dictionary<string, string>();

        /// <summary>
        /// 去掉文件名开头的排序序号（如 "01." "2." "10."），返回显示名
        /// </summary>
        private string GetDisplayName(string fileNameWithoutExt)
        {
            // 匹配开头的数字+点，如 "01." "2." "10."
            var match = System.Text.RegularExpressions.Regex.Match(fileNameWithoutExt, @"^\d+\.\s*");
            if (match.Success)
                return fileNameWithoutExt.Substring(match.Length);
            return fileNameWithoutExt;
        }

        /// <summary>
        /// 从显示名获取实际文件名（不含.txt）
        /// </summary>
        private string GetActualFileName(string displayName)
        {
            if (displayToFileName.ContainsKey(displayName))
                return displayToFileName[displayName];
            return displayName; // 没有映射时直接返回（兼容无序号的文件）
        }

        // 本轮练习统计数据
        private int roundTotalWords = 0;      // 本轮总字数（训练文总字数，开始时计算一次）
        private int roundActualWords = 0;     // 本轮实际字数（包括所有重打的输入）
        private double roundCorrectWords = 0;    // 本轮打对字数
        private double roundTotalTime = 0;    // 本轮总用时（秒）
        private int roundCompletedGroups = 0; // 本轮完成段数
        private List<double> roundHitRates = new List<double>();   // 本轮每段击键率
        private List<double> roundSpeeds = new List<double>();     // 本轮每段速度
        private bool hasStartedPractice = false;  // 是否已经开始练习（有有效成绩）

        // 本轮键准累计数据
        private int roundTotalHit = 0;         // 本轮累计总击键
        private int roundTotalBacks = 0;       // 本轮累计退格
        private int roundTotalCorrection = 0;  // 本轮累计回改
        private int roundAccWordCount = 0;     // 本轮累计字数（用于键准公式）

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
            public double RoundCorrectWords { get; set; }
            public double RoundTotalTime { get; set; }
            public int RoundCompletedGroups { get; set; }
            public List<double> RoundHitRates { get; set; }
            public List<double> RoundSpeeds { get; set; }
            public bool HasStartedPractice { get; set; }
            public int LastSection { get; set; }  // 上次练习到的段号
            public int RetypeCount { get; set; }  // 重打次数
            public double MaxHitRate { get; set; }  // 最高击键率
            public List<List<string>> DisplayRoot { get; set; }  // 乱序后的文章内容
            public int RoundTotalHit { get; set; }
            public int RoundTotalBacks { get; set; }
            public int RoundTotalCorrection { get; set; }
            public int RoundAccWordCount { get; set; }
            public string TargetHitSetting { get; set; }
            public string HitDecreaseSetting { get; set; }
            public string GroupSizeSetting { get; set; }
            public string TargetAccuracySetting { get; set; }

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
            fld.Foreground = Colors.DisplayForeground;





        }

        /// <summary>
        /// 字体大小调整功能（Ctrl+滚轮）
        /// </summary>
        private void Fld_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                ftsize += e.Delta > 0 ? 2 : -2;
                ftsize = Math.Max(10, Math.Min(72, ftsize));  // 限制范围10-72
                fld.FontSize = ftsize;
                cfg["练单器字体大小"] = ftsize.ToString();
                WriteCfg();
                e.Handled = true;
            }
        }

   


        private void InitSlider()
        {
            SliderInit = false;
            sld.Minimum = 1;
            sld.Maximum = TotalGroup;
            sld.Value = Convert.ToInt32(cfg["上次的段数"]) + 1;



            SliderInit = true;
        }

        private void ReadTxt(bool forceReload = false, bool skipInGroupRand = false) //从文件重新读取码表
        {
            // 保存当前文章的统计数据（如果不是第一次加载）
            if (!string.IsNullOrEmpty(TxtFile))
            {
                SaveCurrentArticleStatistics();
            }

            TxtFile = GetActualFileName(FileSelector.SelectedItem.ToString());
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
            // forceReload 时强制从文件重新读取
            // 如果每组字数发生了变化，也需要重新读取
            bool groupSizeChanged = false;
            if (articleStatisticsDict.ContainsKey(TxtFile) &&
                articleStatisticsDict[TxtFile].DisplayRoot != null &&
                articleStatisticsDict[TxtFile].DisplayRoot.Count > 1)
            {
                int cachedFirstGroupSize = articleStatisticsDict[TxtFile].DisplayRoot[0].Count;
                int currentGroupSize = Convert.ToInt32(cfg["每组字数"]);
                // fixed模式：每段字数应等于每组字数（最后一段除外，所以检查第一段）
                if (cachedFirstGroupSize != currentGroupSize)
                {
                    groupSizeChanged = true;
                }
            }

            if (!forceReload && !groupSizeChanged &&
                articleStatisticsDict.ContainsKey(TxtFile) &&
                articleStatisticsDict[TxtFile].DisplayRoot != null &&
                articleStatisticsDict[TxtFile].DisplayRoot.Count > 0)
            {
                // DisplayRoot 已在 LoadArticleStatistics 中恢复
                // 过滤掉空段（防止历史缓存中的空段）
                DisplayRoot.RemoveAll(g => string.IsNullOrWhiteSpace(string.Join("", g)));
                // 重新计算 TotalGroup
                TotalGroup = DisplayRoot.Count;
            }
            else
            {
                // 没有保存的数据，从文件读取文章内容
                string mbtxt = File.ReadAllText(filename).Trim().Replace("\r", "");//.Replace(" ", "\t");

                if (RegexFilter.IsEnabled("练单器"))
                {
                    var filterResult = RegexFilter.Apply(mbtxt);
                    if (filterResult.IsBlocked)
                    {
                        MessageBox.Show($"该练习内容被过滤规则屏蔽：{filterResult.BlockReason}", "过滤提示", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                    mbtxt = filterResult.Text;
                }

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


                    int groupSize = Convert.ToInt32(cfg["每组字数"]);
                    TotalGroup = (RootList.Count + groupSize - 1) / groupSize;

                    MaxGroupSize = groupSize;

                    int k = 0;

                    for (int i = 0; i < TotalGroup; i++)
                    {
                        DisplayRoot.Add(new List<string>());

                        int jmax;
                        if (i < TotalGroup - 1)
                        {
                            jmax = groupSize;
                        }
                        else
                        {
                            jmax = RootList.Count - groupSize * (TotalGroup - 1);
                        }
                        for (int j = 0; j < jmax; j++)
                        {
                            DisplayRoot[i].Add(RootList[k]);
                            k++;
                        }
                    }

                // 过滤掉空段（内容为空或纯空白的段）
                DisplayRoot.RemoveAll(g => string.IsNullOrWhiteSpace(string.Join("", g)));
                TotalGroup = DisplayRoot.Count;
                }
            }

            // JumpGroup() 会覆盖已恢复的段号，所以不需要调用
            // LoadArticleStatistics() 已经恢复了段号，InitSlider() 和 InitGroup() 会使用它

            // RetypeCount 和 MaxHitRate 已在 LoadArticleStatistics 中恢复或初始化，不要重置

            // 确保段数索引不超出 DisplayRoot 范围
            if (DisplayRoot.Count > 0)
            {
                int savedIndex = Convert.ToInt32(cfg["上次的段数"]);
                if (savedIndex >= DisplayRoot.Count)
                {
                    cfg["上次的段数"] = (DisplayRoot.Count - 1).ToString();
                }
            }
            else
            {
                cfg["上次的段数"] = "0";
            }

            InitSlider();

            if (DisplayRoot.Count > 0)
                InitGroup(skipInGroupRand);


        }

        private void ReadTxt_old() //从文件重新读取码表
        {
            TxtFile = GetActualFileName(FileSelector.SelectedItem.ToString());
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
            WriteDebugLog("[GetNextRound] 方法开始");

            // 累加用时和字数（无论通过与否都记录）。
            // 晴练单实际字数按目标字位计，避免把误上屏的字母串算成多个字。
            int actualWordsDelta = GetCurrentGroupWordCount();
            roundTotalTime += Score.Time.TotalSeconds;
            roundActualWords += actualWordsDelta;

            // 累加键准相关数据（无论通过与否）
            roundTotalHit += Score.Hit;
            roundTotalBacks += Score.Backs;
            roundTotalCorrection += Score.Correction;
            roundAccWordCount += Score.TotalWordCount;
            roundCorrectWords += Score.TotalWordCount * accuracy;

            double targetAccuracy = Convert.ToDouble(cfg["换段键准"]) / 100.0;
            bool passed = wrong == 0 && accuracy >= targetAccuracy && hitrate >= TargetHit;
            WriteDebugLog($"[GetNextRound] 条件判断 accuracy={accuracy:F4}, hitRate={hitrate:F2}, TargetHit={TargetHit:F2}, targetAccuracy={targetAccuracy:F4}, wrong={wrong}, passed={passed}");

            if (passed)
            {
                WriteDebugLog("[GetNextRound] 进入 if 分支");

                // 通过条件：累加段数、击键、速度
                roundCompletedGroups++;
                roundHitRates.Add(hitrate);
                roundSpeeds.Add(Score.Speed);
                WriteDebugLog("[GetNextRound] 累加统计完成");

                bool wasNotStarted = !hasStartedPractice;
                if (wasNotStarted)
                {
                    // 首次通过时，计算训练文总字数（只算一次，不随重打累加）
                    roundTotalWords = 0;
                    foreach (var g in DisplayRoot)
                    {
                        string groupText = string.Join("", g);
                        roundTotalWords += new System.Globalization.StringInfo(groupText).LengthInTextElements;
                    }
                    WriteDebugLog($"[GetNextRound] 训练文总字数={roundTotalWords}");
                }
                hasStartedPractice = true;
                WriteDebugLog($"[GetNextRound] wasNotStarted={wasNotStarted}");

                // 直接在主线程执行（参考 F3 模式）
                string fileName = "未知";
                WriteDebugLog("[GetNextRound] 准备获取 fileName");

                try
                {
                    // 使用 WinTrainer 自己的 Dispatcher（不是 MainWindow 的）
                    var dispatcher = this.Dispatcher;
                    if (dispatcher.CheckAccess())
                    {
                        // 已经在 UI 线程，直接访问
                        WriteDebugLog("[GetNextRound] 已在 UI 线程，直接访问 FileSelector");
                        int selectedIndex = FileSelector.SelectedIndex;
                        WriteDebugLog($"[GetNextRound] selectedIndex={selectedIndex}");
                        if (selectedIndex >= 0 && selectedIndex < FileSelector.Items.Count)
                        {
                            var item = FileSelector.Items[selectedIndex];
                            if (item != null)
                            {
                                fileName = item.ToString();
                                WriteDebugLog($"[GetNextRound] fileName from Items={fileName}");
                            }
                        }
                    }
                    else
                    {
                        // 不在 UI 线程，使用 Dispatcher.Invoke
                        WriteDebugLog("[GetNextRound] 不在 UI 线程，使用 Dispatcher.Invoke");
                        dispatcher.Invoke(new Action(() =>
                        {
                            WriteDebugLog("[GetNextRound] Dispatcher.Invoke 内部");
                            int selectedIndex = FileSelector.SelectedIndex;
                            WriteDebugLog($"[GetNextRound] selectedIndex={selectedIndex}");
                            if (selectedIndex >= 0 && selectedIndex < FileSelector.Items.Count)
                            {
                                var item = FileSelector.Items[selectedIndex];
                                if (item != null)
                                {
                                    fileName = item.ToString();
                                    WriteDebugLog($"[GetNextRound] fileName from Items={fileName}");
                                }
                            }
                        }));
                    }
                }
                catch (Exception ex)
                {
                    WriteDebugLog($"[GetNextRound] 获取 fileName 异常: {ex.Message}");
                }

                WriteDebugLog($"[GetNextRound] 最终 fileName={fileName}");

                string t = "击键 " + hitrate.ToString("F2") + "/" + TargetHit.ToString("0.00");

                // 将所有 UI 操作放到 Dispatcher.Invoke 中在 UI 线程执行
                WriteDebugLog("[GetNextRound] 调用 Dispatcher.Invoke 执行 UI 操作");
                this.Dispatcher.Invoke(new Action(() =>
                {
                    WriteDebugLog("[GetNextRound] Dispatcher.Invoke 内部，调用 AutoNextGroup");
                    string roundRecord;
                    bool roundCompleted = AutoNextGroup(out roundRecord);
                    WriteDebugLog("[GetNextRound] AutoNextGroup 完成");

                    if (roundCompleted)
                    {
                        // 一轮完成：用SendQQMessageD一次性发最后一段成绩+总成绩
                        if (!string.IsNullOrEmpty(roundRecord))
                        {
                            MainWindow.Current.SendContentToClipboardOrQQ(result, roundRecord, true, 150);
                        }
                        else
                        {
                            MainWindow.Current.SendContentToClipboardOrQQ(result, true, 150);
                        }
                        MainWindow.Current.UpdateTopStatusText("本轮练习完成，请手动发文开始下一轮");

                        ShowRoundStatisticsDialogLater(roundRecord);
                    }
                    else
                    {
                        // 正常换段：用SendQQMessageD一次性发成绩+新文本（避免剪贴板覆盖）
                        WriteDebugLog("[GetNextRound] Dispatcher.Invoke 内部，调用 GetMatchText");
                        string matchText = GetMatchText(fileName);
                        WriteDebugLog($"[GetNextRound] GetMatchText 完成，长度={matchText?.Length ?? 0}");

                        WriteDebugLog("[GetNextRound] Dispatcher.Invoke 内部，调用 LoadText");
                        MainWindow.Current.LoadText(matchText, RetypeType.first, TxtSource.trainer, false, true);
                        MainWindow.Current.FocusInput();
                        WriteDebugLog("[GetNextRound] LoadText 完成");

                        WriteDebugLog("[GetNextRound] Dispatcher.Invoke 内部，调用 UpdateTopStatusText");
                        MainWindow.Current.UpdateTopStatusText(t);

                        WriteDebugLog("[GetNextRound] Dispatcher.Invoke 内部，调用 SendQQMessageD");
                        MainWindow.Current.SendContentToClipboardOrQQ(result, matchText, true, 150);
                        WriteDebugLog("[GetNextRound] SendQQMessageD 完成");
                    }

                    if (!roundCompleted)
                    {
                        WriteDebugLog("[GetNextRound] Dispatcher.Invoke 内部，调用 UpdateRoundStatus");
                        UpdateRoundStatus();
                    }

                    if (wasNotStarted)
                    {
                        WriteDebugLog("[GetNextRound] Dispatcher.Invoke 内部，调用 UpdateUIState");
                        UpdateUIState();
                    }

                    WriteDebugLog("[GetNextRound] Dispatcher.Invoke 所有操作完成");
                }));
                WriteDebugLog("[GetNextRound] Dispatcher.Invoke 调用完成");

                WriteDebugLog("[GetNextRound] if 分支完成");
            }
            else
            {
                WriteDebugLog("[GetNextRound] 进入 else 分支");

                // 未通过条件：重打
                string fileName = "未知";

                try
                {
                    // 使用 WinTrainer 自己的 Dispatcher
                    var dispatcher = this.Dispatcher;
                    if (dispatcher.CheckAccess())
                    {
                        // 已经在 UI 线程，直接访问
                        int selectedIndex = FileSelector.SelectedIndex;
                        if (selectedIndex >= 0 && selectedIndex < FileSelector.Items.Count)
                        {
                            var item = FileSelector.Items[selectedIndex];
                            if (item != null)
                                fileName = item.ToString();
                        }
                    }
                    else
                    {
                        // 不在 UI 线程，使用 Dispatcher.Invoke
                        dispatcher.Invoke(new Action(() =>
                        {
                            int selectedIndex = FileSelector.SelectedIndex;
                            if (selectedIndex >= 0 && selectedIndex < FileSelector.Items.Count)
                            {
                                var item = FileSelector.Items[selectedIndex];
                                if (item != null)
                                    fileName = item.ToString();
                            }
                        }));
                    }
                }
                catch (Exception ex)
                {
                    WriteDebugLog($"[GetNextRound] else 分支异常: {ex.Message}");
                }

                WriteDebugLog($"[GetNextRound] fileName={fileName}");

                string t = "击键 " + hitrate.ToString("F2") + "/" + TargetHit.ToString("0.00");

                // 将所有 UI 操作放到 Dispatcher.Invoke 中在 UI 线程执行
                WriteDebugLog("[GetNextRound] else 分支，调用 Dispatcher.Invoke 执行 UI 操作");
                this.Dispatcher.Invoke(new Action(() =>
                {
                    WriteDebugLog("[GetNextRound] Dispatcher.Invoke 内部，调用 RetypeGroup");
                    RetypeGroup(true, true);
                    WriteDebugLog("[GetNextRound] RetypeGroup 完成");

                    WriteDebugLog("[GetNextRound] Dispatcher.Invoke 内部，调用 GetMatchText");
                    string retypeText = GetMatchText(fileName);
                    WriteDebugLog($"[GetNextRound] GetMatchText 完成，长度={retypeText?.Length ?? 0}");

                    WriteDebugLog("[GetNextRound] Dispatcher.Invoke 内部，调用 LoadText");
                    MainWindow.Current.LoadText(retypeText, RetypeType.retype, TxtSource.trainer, false, true);
                    MainWindow.Current.FocusInput();
                    WriteDebugLog("[GetNextRound] LoadText 完成");

                    WriteDebugLog("[GetNextRound] Dispatcher.Invoke 内部，调用 UpdateTopStatusText");
                    MainWindow.Current.UpdateTopStatusText(t);

                    if (hitrate >= MaxHitRate)
                    {
                        MaxHitRate = hitrate;
                    }

                    WriteDebugLog("[GetNextRound] Dispatcher.Invoke else 分支操作完成");
                }));
                WriteDebugLog("[GetNextRound] else 分支 Dispatcher.Invoke 调用完成");

                WriteDebugLog("[GetNextRound] else 分支完成");
            }

            WriteDebugLog("[GetNextRound] 方法结束");
        }

        /// <summary>
        /// 记录部分进度（用于F3重打时统计打到一半的用时）
        /// </summary>
        public void RecordPartialProgress(int inputWordCount, double timeSeconds, double accuracy)
        {
            if (timeSeconds > 0)
            {
                roundTotalTime += timeSeconds;
            }
            int actualWords = GetCurrentPartialActualWordCount(inputWordCount);
            if (actualWords > 0)
            {
                roundActualWords += actualWords;
            }
            // 累加键准相关数据
            roundTotalHit += Score.Hit;
            roundTotalBacks += Score.Backs;
            roundTotalCorrection += Score.Correction;
            roundAccWordCount += inputWordCount;
        }

        internal void RefreshTitleWordStats(TrainerTitleWordStatsSnapshot snapshot)
        {
            if (snapshot == null)
                return;

            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(new Action(() => RefreshTitleWordStats(snapshot)));
                return;
            }

            _displayedDate = DateTime.Now.ToString("yyyy-MM-dd");
            _displayedTodayWords = snapshot.TodayWords;
            _displayedTotalWords = snapshot.TotalWords;
            UpdateTitleBarStats();
        }

        private int GetCurrentPartialActualWordCount(int fallbackInputTextElements)
        {
            int section = Convert.ToInt32(cfg["上次的段数"]);
            if (section < 0 || section >= DisplayRoot.Count)
                return 0;

            return TrainerActualWordCounter.CountPartialWords(
                Score.CommitText,
                DisplayRoot[section],
                fallbackInputTextElements);
        }

        private int GetCurrentGroupWordCount()
        {
            int section = Convert.ToInt32(cfg["上次的段数"]);
            if (section < 0 || section >= DisplayRoot.Count)
                return 0;

            string text = string.Join("", DisplayRoot[section]);
            if (string.IsNullOrEmpty(text))
                return 0;

            return new StringInfo(text).LengthInTextElements;
        }

        public void F3()
        {

      //      RetypeGroup(false, false);
            MainWindow.Current.LoadText(GetMatchText(), RetypeType.retype, TxtSource.trainer, false, true);
            MainWindow.Current.FocusInput();
            MainWindow.Current.UpdateTopStatusText("重打");
        }

        public void CtrlL()
        {

            RetypeGroup(true, false);
            MainWindow.Current.LoadText(GetMatchText(), RetypeType.retype, TxtSource.trainer, false, true);
            MainWindow.Current.FocusInput();
            MainWindow.Current.UpdateTopStatusText("乱序");
        }

        private void InternalHotkeyCtrlL(object sender, ExecutedRoutedEventArgs e)
        {
            if (DisplayRoot == null || DisplayRoot.Count == 0)
                return;

            int section = Convert.ToInt32(cfg["上次的段数"]);
            if (section >= DisplayRoot.Count || DisplayRoot[section].Count == 0)
                return;

            InGroupRand();
            ShowWords();
            e.Handled = true;
        }

        private void InternalHotkeyCtrlShiftL(object sender, ExecutedRoutedEventArgs e)
        {
            RandAllGroup();
            e.Handled = true;
        }

        private void InternalHotkeyCtrlShiftU(object sender, ExecutedRoutedEventArgs e)
        {
            RestoreOrder();
            e.Handled = true;
        }

        private void DisplayHit()
        {

            TBHitrate.Text = "换段击键 " + TargetHit.ToString("0.00");

        }

        private void DisplayHit(double hitrate)
        {

            TBHitrate.Text = "击键 "+ hitrate.ToString("F2") + "/" + TargetHit.ToString("0.00");

        }

        private double GetRoundAccuracy()
        {
            if (roundTotalHit <= 0 || roundAccWordCount <= 0)
                return 1.0;
            double hit = roundTotalHit;
            double backs = roundTotalBacks;
            double correction = roundTotalCorrection;
            double totalWords = roundAccWordCount;
            return (hit - correction - backs * 2.0) / (totalWords + correction) * totalWords / hit;
        }

        /// <summary>
        /// 更新本轮统计显示（实时显示均速、均击、字数等）
        /// </summary>
        private void UpdateRoundStatus()
        {
            ApplyRoundStatusText(BuildRoundStatusText());
        }

        private string BuildRoundStatusText()
        {
            if (!hasStartedPractice)
                return "";

            double avgHitRate = 0;
            double avgSpeed = 0;

            if (roundHitRates.Count > 0)
                avgHitRate = roundHitRates.Average();
            if (roundSpeeds.Count > 0)
                avgSpeed = roundSpeeds.Average();

            double progress = TotalGroup > 0 ? (double)roundCompletedGroups / TotalGroup * 100 : 0;

            return string.Format("{0} 均击{1:F2} 均速{2:F2} 字数{3} 实际{4} 进度{5:F0}%",
                TxtFile, avgHitRate, avgSpeed, roundTotalWords, roundActualWords, progress);
        }

        private void ApplyRoundStatusText(string statText)
        {
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
                MessageBox.Show($"晴练单目录不存在: {Folder}\n请确保该目录存在并包含练习文件。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 记住当前选中的实际文件名，刷新后恢复
            string previousActualFile = null;
            if (FileSelector.SelectedItem != null)
                previousActualFile = GetActualFileName(FileSelector.SelectedItem.ToString());

            DirectoryInfo folder = new DirectoryInfo(Folder);

            // 按自然数排序（让 2.xxx 排在 10.xxx 前面）
            var files = folder.GetFiles("*.txt").OrderBy(f => f.Name, new NaturalStringComparer()).ToArray();

            displayToFileName.Clear();
            FileSelector.Items.Clear();
            foreach (FileInfo file in files)
            {
                string fileNameWithoutExt = file.Name.Substring(0, file.Name.Length - 4);
                string displayName = GetDisplayName(fileNameWithoutExt);

                // 如果显示名重复，保留原名避免冲突
                if (displayToFileName.ContainsKey(displayName))
                    displayName = fileNameWithoutExt;

                displayToFileName[displayName] = fileNameWithoutExt;
                FileSelector.Items.Add(displayName);
            }

            // 恢复之前选中的文件
            bool restored = false;
            if (previousActualFile != null)
            {
                for (int i = 0; i < FileSelector.Items.Count; i++)
                {
                    if (GetActualFileName(FileSelector.Items[i].ToString()) == previousActualFile)
                    {
                        FileSelector.SelectedIndex = i;
                        restored = true;
                        break;
                    }
                }
            }
            if (!restored && FileSelector.Items.Count > 0)
                FileSelector.SelectedIndex = 0;
        }

        /// <summary>
        /// 刷新文件列表（供外部调用，如每次显示窗口时）
        /// </summary>
        public void RefreshFileList()
        {
            autoSendPolicy.BeginProgrammaticRefresh();
            _isRefreshingFileList = true;
            try
            {
                UpdateFileList();
            }
            finally
            {
                _isRefreshingFileList = false;
                autoSendPolicy.EndProgrammaticRefresh();
            }
        }

        /// <summary>
        /// 自然排序比较器，让 "2.xxx" 排在 "10.xxx" 前面
        /// </summary>
        private class NaturalStringComparer : IComparer<string>
        {
            [System.Runtime.InteropServices.DllImport("shlwapi.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
            private static extern int StrCmpLogicalW(string x, string y);

            public int Compare(string x, string y)
            {
                return StrCmpLogicalW(x, y);
            }
        }

        private void PushTrainerSectionToMain()
        {
            if (MainWindow.Current == null)
                return;
            if (TotalGroup <= 0)
            {
                MainWindow.Current.UpdateTrainerSection("");
                return;
            }
            int currentSection = Convert.ToInt32(cfg["上次的段数"]) + 1;
            MainWindow.Current.UpdateTrainerSection($"第{currentSection}/{TotalGroup}段");
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
            PushTrainerSectionToMain();

            // 更新本轮统计显示
            UpdateRoundStatus();
        }
        private void InitGroup(bool skipInGroupRand = false) //初始化组
        {
            // 不要重置 RetypeCount 和 MaxHitRate，因为 LoadArticleStatistics() 可能已经恢复了它们
            // RetypeCount = 0;  // 已移除
            // MaxHitRate = 0;    // 已移除

            int section = Convert.ToInt32(cfg["上次的段数"]);
            // 防御：如果当前段为空或不存在，跳过载文
            if (section >= DisplayRoot.Count || DisplayRoot[section].Count == 0)
                return;

            if (!skipInGroupRand)
                InGroupRand();
            ShowWords();
            LoadText(autoSendPolicy.ConsumeShouldSendToMainWindow());

            WriteCfg();

            TargetHit = Convert.ToDouble(cfg["换段击键"]) - Convert.ToDouble(cfg["每轮降击"]) * (RetypeCount);
            if (mode == "varible")
                TargetHit = Math.Round((float)(TargetHit * Math.Pow(AverageGroupSize / (double)DisplayRoot[Convert.ToInt32(cfg["上次的段数"])].Count, 0.35)), 2);

            if (TargetHit < 0.01)
                TargetHit = 0.01;
   


                DisplayHit();

            stattxt.Text = "第 " + (Convert.ToInt32(cfg["上次的段数"]) + 1) + "/" + TotalGroup + " 段";
            PushTrainerSectionToMain();

            // 更新本轮统计显示（初始化时清空）
            UpdateRoundStatus();

            // 更新UI状态（进度条/重置按钮/按钮文字）
            UpdateUIState();


        }



        /// <summary>
        /// 自动前进到下一段，返回 true 表示一整轮已完成（需要停下来）
        /// </summary>
        public bool AutoNextGroup(out string roundResultRecord)
        {
            WriteDebugLog("[AutoNextGroup] 方法开始");
            roundResultRecord = null;

            cfg["上次的段数"] = (Convert.ToInt32(cfg["上次的段数"]) + 1).ToString();
            WriteDebugLog("[AutoNextGroup] 段号更新完成");

            // 段号更新后保存统计数据（确保保存的是最新的段号）
            WriteDebugLog("[AutoNextGroup] 调用 SaveCurrentArticleStatistics 前");
            SaveCurrentArticleStatistics();
            WriteDebugLog("[AutoNextGroup] SaveCurrentArticleStatistics 完成");

            // 检查是否完成一整轮
            if (Convert.ToInt32(cfg["上次的段数"]) == TotalGroup)
            {
                WriteDebugLog("[AutoNextGroup] 完成一轮，停下来等待用户操作");
                string completedRoundStatus = BuildRoundStatusText();
                ApplyRoundStatusText(completedRoundStatus);

                // 完成一轮，获取总成绩（不弹窗、不发QQ，由调用方处理）
                roundResultRecord = ShowRoundStatistics();
                RecordRoundLog();

                // 重置段号到第一段，但不自动开始
                cfg["上次的段数"] = "0";
                ResetRoundStatistics(clearVisibleStatus: false);

                SliderInit = false; // 防止触发 Slider_ValueChanged 导致自动加载
                sld.Value = 1;
                SliderInit = true;

                // 重置重打次数
                RetypeCount = 0;
                MaxHitRate = 0;

                // 初始化第一段的显示（但不加载到主窗口打字区）
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
                PushTrainerSectionToMain();
                UpdateUIState();

                WriteDebugLog("[AutoNextGroup] 一轮处理完成，等待用户手动发文");
                return true; // 一轮完成
            }

            WriteDebugLog("[AutoNextGroup] 设置 sld.Value 前");
            SliderInit = false;
            sld.Value = Convert.ToInt32(cfg["上次的段数"]) + 1;
            SliderInit = true;
            WriteDebugLog("[AutoNextGroup] sld.Value 设置完成");

            // 新段重置重打次数和最高击键率
            RetypeCount = 0;
            MaxHitRate = 0;

            WriteDebugLog("[AutoNextGroup] 调用 InitGroup 前");
            InitGroup();
            WriteDebugLog("[AutoNextGroup] InitGroup 完成");

            WriteDebugLog("[AutoNextGroup] 方法结束");
            return false; // 继续下一段
        }

        /// <summary>
        /// 重置本轮统计数据
        /// </summary>
        private void ResetRoundStatistics(bool clearVisibleStatus = true)
        {
            roundTotalWords = 0;
            roundActualWords = 0;
            roundCorrectWords = 0;
            roundTotalTime = 0;
            roundCompletedGroups = 0;
            roundHitRates.Clear();
            roundSpeeds.Clear();
            hasStartedPractice = false;
            roundTotalHit = 0;
            roundTotalBacks = 0;
            roundTotalCorrection = 0;
            roundAccWordCount = 0;
            RetypeCount = 0;
            MaxHitRate = 0;

            if (clearVisibleStatus)
            {
                ApplyRoundStatusText("");
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
                RoundTotalHit = roundTotalHit,
                RoundTotalBacks = roundTotalBacks,
                RoundTotalCorrection = roundTotalCorrection,
                RoundAccWordCount = roundAccWordCount,
                TargetHitSetting = cfg["换段击键"],
                HitDecreaseSetting = cfg["每轮降击"],
                GroupSizeSetting = cfg["每组字数"],
                TargetAccuracySetting = cfg["换段键准"],
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
                roundTotalHit = data.RoundTotalHit;
                roundTotalBacks = data.RoundTotalBacks;
                roundTotalCorrection = data.RoundTotalCorrection;
                roundAccWordCount = data.RoundAccWordCount;

                // 恢复段号
                cfg["上次的段数"] = data.LastSection.ToString();
                // 恢复重打次数和最高击键率
                RetypeCount = data.RetypeCount;
                MaxHitRate = data.MaxHitRate;

                ApplyArticleSettings(data);

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
                roundTotalHit = 0;
                roundTotalBacks = 0;
                roundTotalCorrection = 0;
                roundAccWordCount = 0;
                RetypeCount = 0;
                MaxHitRate = 0;
                ApplyDefaultArticleSettings();
            }

            UpdateUIState();
        }

        private void ApplyArticleSettings(ArticleStatisticsData data)
        {
            if (data == null)
                return;

            string targetHit = string.IsNullOrWhiteSpace(data.TargetHitSetting) ? GetDefaultArticleSetting("换段击键") : data.TargetHitSetting;
            string hitDecrease = string.IsNullOrWhiteSpace(data.HitDecreaseSetting) ? GetDefaultArticleSetting("每轮降击") : data.HitDecreaseSetting;
            string groupSize = string.IsNullOrWhiteSpace(data.GroupSizeSetting) ? GetDefaultArticleSetting("每组字数") : data.GroupSizeSetting;
            string targetAccuracy = string.IsNullOrWhiteSpace(data.TargetAccuracySetting) ? GetDefaultArticleSetting("换段键准") : data.TargetAccuracySetting;

            ApplyArticleSettings(targetHit, hitDecrease, groupSize, targetAccuracy);
        }

        private void ApplyDefaultArticleSettings()
        {
            ApplyArticleSettings(
                GetDefaultArticleSetting("换段击键"),
                GetDefaultArticleSetting("每轮降击"),
                GetDefaultArticleSetting("每组字数"),
                GetDefaultArticleSetting("换段键准"));
        }

        private string GetDefaultArticleSetting(string key)
        {
            if (defaultArticleSettings.ContainsKey(key))
                return defaultArticleSettings[key];
            return cfg.ContainsKey(key) ? cfg[key] : "";
        }

        private void ApplyArticleSettings(string targetHit, string hitDecrease, string groupSize, string targetAccuracy)
        {
            _isApplyingArticleSettings = true;
            try
            {
                cfg["换段击键"] = targetHit;
                cfg["每轮降击"] = hitDecrease;
                cfg["每组字数"] = groupSize;
                cfg["换段键准"] = targetAccuracy;

                if (speedDisplay != null)
                    speedDisplay.Text = targetHit;
                if (hitDecreaseDisplay != null)
                    hitDecreaseDisplay.Text = hitDecrease;
                if (numDisplay != null)
                    numDisplay.Text = groupSize;
                if (accuracyDisplay != null)
                    accuracyDisplay.Text = targetAccuracy;
            }
            finally
            {
                _isApplyingArticleSettings = false;
            }
        }

        /// <summary>
        /// 保存统计数据到文件（异步执行，避免阻塞）
        /// </summary>
        private void SaveStatisticsToFile()
        {
            // 使用 Task.Run 在后台线程保存，避免阻塞主线程
            System.Threading.Tasks.Task.Run(() =>
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
                        writer.WriteLine($"C\t{data.TargetHitSetting}\t{data.HitDecreaseSetting}\t{data.GroupSizeSetting}\t{data.TargetAccuracySetting}");

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
            });
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
                        else if (line.StartsWith("C\t"))
                        {
                            // 每篇文本独立设置
                            if (data != null)
                            {
                                var parts = line.Split('\t');
                                if (parts.Length >= 5)
                                {
                                    data.TargetHitSetting = parts[1];
                                    data.HitDecreaseSetting = parts[2];
                                    data.GroupSizeSetting = parts[3];
                                    data.TargetAccuracySetting = parts[4];
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
                                    RoundCorrectWords = double.TryParse(parts[3], out double correctWords) ? correctWords : 0,
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
        /// <summary>
        /// 计算本轮练习统计，返回总成绩字符串和弹窗内容（不发QQ、不弹窗）
        /// </summary>
        private string ShowRoundStatistics()
        {
            if (roundCompletedGroups == 0)
                return null;

            double avgHitRate = 0;
            double avgSpeed = 0;
            double avgAccuracy = 0;

            if (roundHitRates.Count > 0)
                avgHitRate = roundHitRates.Average();
            if (roundSpeeds.Count > 0)
                avgSpeed = roundSpeeds.Average();

            // 总键准 = 累计Hit/Backs/Correction代入键准公式
            avgAccuracy = GetRoundAccuracy() * 100;

            // 生成成绩记录格式，添加到主窗口成绩区
            string resultRecord = string.Format(Score.TrainerSummaryPrefix + " {0} 均击{1:F2} 均速{2:F2} 均准{3:F2}% 字数{4} 实际{5} 用时{6}",
                TxtFile, avgHitRate, avgSpeed, avgAccuracy, roundTotalWords, roundActualWords, Score.FormatTime(roundTotalTime));
            if (MainWindow.Current != null)
            {
                MainWindow.Current.UpdateTypingStat(resultRecord);
            }

            return resultRecord;
        }

        private void ShowRoundStatisticsDialogLater(string roundRecord)
        {
            if (string.IsNullOrEmpty(roundRecord))
                return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                MessageBox.Show(roundRecord, "练习统计", MessageBoxButton.OK, MessageBoxImage.Information);
            }), System.Windows.Threading.DispatcherPriority.ContextIdle);
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

            // 总键准 = 累计Hit/Backs/Correction代入键准公式
            avgAccuracy = GetRoundAccuracy() * 100;

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
                Wrong = (int)(roundActualWords - roundCorrectWords),  // 错字数 = 实际字数 - 打对字数
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
                DifficultyName = "",  // 打单器没有难度名称
                TargetHit = TargetHit  // 当轮换段击键阈值
            };

            TrainerLog.WriteRecord(record);
        }
        
        string GetMatchText()
        {
            // 默认方法：从 UI 控件获取文件名
            return GetMatchText(FileSelector.SelectedItem.ToString());
        }

        string GetMatchText(string fileName)
        {
            // 重载方法：使用传入的文件名（用于后台线程调用）
            int section = Convert.ToInt32(cfg["上次的段数"]);
            if (section >= DisplayRoot.Count)
                return "";

            string txt = string.Join("", DisplayRoot[section]);
            // 如果实际文本内容为空，不生成载文文本
            if (string.IsNullOrWhiteSpace(txt))
                return "";

            StringBuilder sb = new StringBuilder();
            string name = fileName + " " + "目标" + Convert.ToDouble(cfg["换段击键"]).ToString("F2");

            if (Convert.ToDouble(cfg["每轮降击"]) > 0.000001)
                name += "-" + Convert.ToDouble(cfg["每轮降击"]).ToString("F2");
            sb.Append(name);
            sb.AppendLine();
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

            sb.Append("晴练单");
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

            this.EnableEscapeToClose();

            // 应用主题颜色
            ApplyThemeColors();
            ApplyCurrentLogo();

            UpdateFileList();
            InitCfg();

            // 加载持久化的统计数据
            LoadStatisticsFromFile();

            autoSendPolicy.SuppressNextGroupSend();
            ReadTxt();
            ShowWords();
            LoadText(false);



        }

        private void FileSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CfgInit && FileSelector.SelectedItem != null)
                ReadTxt(skipInGroupRand: _isRefreshingFileList);

        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (IsModifiedShortcut(e) || IsTextInputFocused())
                return;

            switch (e.Key)
            {
                case Key.Up:
                    e.Handled = true;
                    HandleArticleSelectionKey(keyboardPolicy.HandleKey(ArticleSendKeyboardKey.Up));
                    break;

                case Key.Down:
                    e.Handled = true;
                    HandleArticleSelectionKey(keyboardPolicy.HandleKey(ArticleSendKeyboardKey.Down));
                    break;

                case Key.Enter:
                    e.Handled = true;
                    var enterAction = keyboardPolicy.HandleKey(ArticleSendKeyboardKey.Enter);
                    if (enterAction == ArticleSendKeyboardAction.SendArticle)
                        BtnSend_Click(null, null);
                    else if (enterAction == ArticleSendKeyboardAction.ConfirmArticleSelection)
                        FocusTrainerPreview();
                    break;

                case Key.Space:
                    if (keyboardPolicy.HandleKey(ArticleSendKeyboardKey.Space) == ArticleSendKeyboardAction.ConfirmArticleSelection)
                    {
                        e.Handled = true;
                        FocusTrainerPreview();
                    }
                    break;
            }
        }

        private static bool IsModifiedShortcut(KeyEventArgs e)
        {
            return (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Windows)) != 0;
        }

        private bool IsTextInputFocused()
        {
            return Keyboard.FocusedElement == speedDisplay
                || Keyboard.FocusedElement == numDisplay
                || Keyboard.FocusedElement == hitDecreaseDisplay
                || Keyboard.FocusedElement == accuracyDisplay;
        }

        private void HandleArticleSelectionKey(ArticleSendKeyboardAction action)
        {
            if (action == ArticleSendKeyboardAction.SelectPreviousArticle)
            {
                MoveFileSelection(-1);
                return;
            }

            if (action == ArticleSendKeyboardAction.SelectNextArticle)
            {
                MoveFileSelection(1);
            }
        }

        private void MoveFileSelection(int delta)
        {
            if (FileSelector.Items.Count <= 0)
                return;

            int currentIndex = FileSelector.SelectedIndex;
            if (currentIndex < 0)
                currentIndex = 0;

            int nextIndex = Math.Max(0, Math.Min(FileSelector.Items.Count - 1, currentIndex + delta));
            if (nextIndex != FileSelector.SelectedIndex)
                FileSelector.SelectedIndex = nextIndex;

            FileSelector.Focus();
            FileSelector.IsDropDownOpen = true;
        }

        private void FocusTrainerPreview()
        {
            fld.Focus();
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
                        roundTotalHit = 0;
                        roundTotalBacks = 0;
                        roundTotalCorrection = 0;
                        roundAccWordCount = 0;
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

            if (DisplayRoot.Count == 0)
                return;

            // 防止段号超出范围
            if (currentSection >= DisplayRoot.Count)
                currentSection = 0;

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
                int groupSize = Convert.ToInt32(cfg["每组字数"]);
                int remainingGroups = (count + groupSize - 1) / groupSize;
                for (int i = 0; i < remainingGroups; i++)
                {
                    DisplayRoot.Add(new List<string>());

                    int jmax;
                    if (i < remainingGroups - 1)
                    {
                        jmax = groupSize;
                    }
                    else
                    {
                        jmax = count - groupSize * (remainingGroups - 1);
                    }
                    for (int j = 0; j < jmax && k < RootList.Count; j++)
                    {
                        DisplayRoot[currentSection + i].Add(RootList[k]);

                        k++;
                    }
                }

                // 过滤掉空段
                DisplayRoot.RemoveAll(g => string.IsNullOrWhiteSpace(string.Join("", g)));
                TotalGroup = DisplayRoot.Count;
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
            RestoreOrder();
        }

        private void RestoreOrder()
        {
            ReadTxt(true, skipInGroupRand: true);
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
            if (File.Exists(TrainerConfig.Path))
            {
                StreamReader sr = new StreamReader(TrainerConfig.Path);
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
                if (cfg["上次打开的文件"] == GetActualFileName(FileSelector.Items[i].ToString()) + ".txt")
                {
                    FileSelector.SelectedIndex = i;
                }
            }

            // 设置数值显示
            speedDisplay.Text = cfg["换段击键"];
            numDisplay.Text = cfg["每组字数"];
            hitDecreaseDisplay.Text = cfg["每轮降击"];
            accuracyDisplay.Text = cfg["换段键准"];
            defaultArticleSettings["换段击键"] = cfg["换段击键"];
            defaultArticleSettings["每组字数"] = cfg["每组字数"];
            defaultArticleSettings["每轮降击"] = cfg["每轮降击"];
            defaultArticleSettings["换段键准"] = cfg["换段键准"];
            CbCloseAfterSend.IsChecked = cfg["练单发文后关闭窗口"] == "否" ? false : true;
            CbTrainerMainWindowMemory.IsChecked = Config.GetBool(TrainerMainWindowConfigScope.EnabledConfigKey);

            // 恢复字体大小
            if (double.TryParse(cfg["练单器字体大小"], out double savedFtsize) && savedFtsize >= 10 && savedFtsize <= 72)
            {
                ftsize = savedFtsize;
            }

            // 恢复窗口大小和位置
            if (double.TryParse(cfg["练单器窗口宽度"], out double width) && width >= 620)
                this.Width = width;
            if (double.TryParse(cfg["练单器窗口高度"], out double height) && height >= 200)
                this.Height = height;
            if (double.TryParse(cfg["练单器窗口左边"], out double left))
                this.Left = left;
            else
                this.Left = MainWindow.Current.Left - this.Width;
            if (double.TryParse(cfg["练单器窗口顶边"], out double top))
                this.Top = top;
            else
                this.Top = MainWindow.Current.Top;

            // 恢复最大化状态
            if (bool.TryParse(cfg["练单器最大化状态"], out bool isMaximized) && isMaximized)
            {
                // 保存当前状态作为恢复边界
                _restoreBounds = new Rect(this.Left, this.Top, this.Width, this.Height);
                // 最大化窗口
                var workArea = SystemParameters.WorkArea;
                this.Left = workArea.Left;
                this.Top = workArea.Top;
                this.Width = workArea.Width;
                this.Height = workArea.Height;
                _isCustomMaximized = true;
                BtnMaximize.Content = "◰";
            }

            CfgInit = true;

        }





        private void WriteCfg()
        {

            cfg["删除此文件即可重置设置"] = "获取更新加Q群：" + Config.GetString("软件更新Q群");

            try
            {
                string configPath = TrainerConfig.Path;
                StreamWriter sr = new StreamWriter(configPath);
                foreach (var item in cfg)
                {
                    sr.WriteLine(item.Key + "\t" + item.Value);
                }
                sr.Flush();
                sr.Close();
                System.Diagnostics.Debug.WriteLine($"晴练单配置已保存到: {Path.GetFullPath(configPath)}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存晴练单配置失败: {ex.Message}");
                MessageBox.Show($"保存晴练单配置失败: {ex.Message}\n\n请检查程序目录是否有写入权限。\n\n当前目录: {Environment.CurrentDirectory}", "配置保存错误", MessageBoxButton.OK, MessageBoxImage.Warning);
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



        private void LoadText(bool sendToMainWindow)
        {
            if (!sendToMainWindow)
            {
                return;
            }

            MainWindow.Current.LoadText(GetMatchText(), RetypeType.first, TxtSource.trainer, false,true);
            MainWindow.Current.FocusInput();
        }

        private void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            string matchText = GetMatchText();
            MainWindow.Current.LoadText(matchText, RetypeType.first, TxtSource.trainer, false, true);
            MainWindow.Current.FocusInput();
            MainWindow.Current.SendContentToClipboardOrQQ(matchText, true, 150);
            CloseTrainerWindowAfterSendIfNeeded();
        }

        private void CbCloseAfterSend_Checked(object sender, RoutedEventArgs e)
        {
            SaveCloseAfterSendSetting();
        }

        private void CbCloseAfterSend_Unchecked(object sender, RoutedEventArgs e)
        {
            SaveCloseAfterSendSetting();
        }

        private void CbTrainerMainWindowMemory_Checked(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingTrainerMainWindowMemoryCheckBox || !CfgInit)
                return;

            Config.Set(TrainerMainWindowConfigScope.EnabledConfigKey, true);
            MainWindow.Current?.RefreshTrainerMainWindowMemoryMode();
        }

        private void CbTrainerMainWindowMemory_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingTrainerMainWindowMemoryCheckBox || !CfgInit)
                return;

            if (!ConfirmResetTrainerMainWindowMemoryOnDisable())
            {
                _isUpdatingTrainerMainWindowMemoryCheckBox = true;
                try
                {
                    CbTrainerMainWindowMemory.IsChecked = true;
                }
                finally
                {
                    _isUpdatingTrainerMainWindowMemoryCheckBox = false;
                }
                return;
            }

            Config.Set(TrainerMainWindowConfigScope.EnabledConfigKey, false);
            MainWindow.Current?.RefreshTrainerMainWindowMemoryMode();
            MainWindow.Current?.ResetTrainerMainWindowMemory();
        }

        private void SaveCloseAfterSendSetting()
        {
            if (!CfgInit)
                return;

            cfg["练单发文后关闭窗口"] = CbCloseAfterSend.IsChecked == true ? "是" : "否";
            WriteCfg();
        }

        private bool ConfirmResetTrainerMainWindowMemoryOnDisable()
        {
            var dialog = new Window
            {
                Title = "关闭确认",
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SizeToContent = SizeToContent.Height,
                Width = 380,
                MinWidth = 340,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
                Icon = this.Icon
            };

            var root = new Grid
            {
                Margin = new Thickness(18, 16, 18, 18)
            };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var message = new TextBlock
            {
                Text = "关闭主窗口单独记忆会清空练单场景下已单独保存的主窗口记忆。确定要关闭并清空吗？",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 14,
                LineHeight = 22,
                Margin = new Thickness(0, 0, 0, 18)
            };
            Grid.SetRow(message, 0);
            root.Children.Add(message);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetRow(buttonPanel, 1);

            var confirmButton = new Button
            {
                Content = "确定",
                ToolTip = "确认关闭并清空",
                MinWidth = 76,
                Padding = new Thickness(12, 6, 12, 6),
                Margin = new Thickness(0, 0, 8, 0)
            };

            var cancelButton = new Button
            {
                Content = "取消",
                ToolTip = "取消",
                IsCancel = true,
                MinWidth = 76,
                Padding = new Thickness(12, 6, 12, 6)
            };

            confirmButton.Click += (s, args) =>
            {
                dialog.DialogResult = true;
                dialog.Close();
            };

            cancelButton.Click += (s, args) =>
            {
                dialog.DialogResult = false;
                dialog.Close();
            };

            buttonPanel.Children.Add(confirmButton);
            buttonPanel.Children.Add(cancelButton);
            root.Children.Add(buttonPanel);

            dialog.Content = root;
            DialogTheming.ApplyChromelessTheme(dialog);

            return dialog.ShowDialog() == true;
        }

        private void CloseTrainerWindowAfterSendIfNeeded()
        {
            if (CbCloseAfterSend.IsChecked == true)
                Close();
        }

        private WinTrainerHistoryWindow _historyWindow;

        private void BtnHistory_Click(object sender, RoutedEventArgs e)
        {
            // 已打开则激活，避免多开
            if (_historyWindow != null && _historyWindow.IsLoaded)
            {
                if (_historyWindow.WindowState == WindowState.Minimized)
                    _historyWindow.WindowState = WindowState.Normal;
                _historyWindow.Activate();
                return;
            }

            string currentTitle = TxtFile;
            if (string.IsNullOrWhiteSpace(currentTitle) && FileSelector.SelectedItem != null)
                currentTitle = GetActualFileName(FileSelector.SelectedItem.ToString());

            _historyWindow = new WinTrainerHistoryWindow(currentTitle)
            {
                Owner = this
            };
            _historyWindow.Closed += (_, _) => _historyWindow = null;
            _historyWindow.Show();
        }

        // ===== 标题栏字数统计 =====
        private int _displayedTodayWords;
        private int _displayedTotalWords;
        private string _displayedDate = "";

        private void InitializeTitleStats()
        {
            try
            {
                _displayedDate = DateTime.Now.ToString("yyyy-MM-dd");
                var snapshot = TrainerTitleWordStats.Read();
                _displayedTodayWords = snapshot.TodayWords;
                _displayedTotalWords = snapshot.TotalWords;
                UpdateTitleBarStats();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[InitializeTitleStats] 失败: {ex.Message}");
            }
        }

        private void UpdateTitleBarStats()
        {
            if (TitleBarStats == null) return;
            TitleBarStats.Text = $"今日练单 {_displayedTodayWords:N0} 字，累计 {_displayedTotalWords:N0} 字";
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

                // 重置段号到第一段
                cfg["上次的段数"] = "0";
                sld.Value = 1;
                InitGroup();
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // 保存窗口状态（包括最大化状态）
            cfg["练单器窗口宽度"] = this.Width.ToString();
            cfg["练单器窗口高度"] = this.Height.ToString();
            cfg["练单器窗口左边"] = this.Left.ToString();
            cfg["练单器窗口顶边"] = this.Top.ToString();
            cfg["练单器最大化状态"] = _isCustomMaximized.ToString();

            if (CoreTextInfo.Exit)
            {
                // 退出程序时保存统计数据
                SaveCurrentArticleStatistics();
                WriteCfg();
                e.Cancel = false;
            }
            else
            {
                // 窗口隐藏前保存统计数据
                SaveCurrentArticleStatistics();
                WriteCfg();
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
                ShowWords();
                PushTrainerSectionToMain();
                InitializeTitleStats();
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
            // 保存最大化状态
            cfg["练单器最大化状态"] = _isCustomMaximized.ToString();
            WriteCfg();
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
                    ReadTxt(true);
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
                    ReadTxt(true);
                }
                WriteCfg();
            }
        }

        // 字数显示TextChanged事件
        private void NumDisplay_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isApplyingArticleSettings)
                return;

            if (CfgInit && numDisplay.Text.Length > 0)
            {
                if (int.TryParse(numDisplay.Text, out int value))
                {
                    if (value < 1) value = 1;
                    if (value > 99999) value = 99999;
                    cfg["每组字数"] = value.ToString();

                    if (DisplayRoot != null)
                    {
                        ReadTxt(true);
                    }
                    WriteCfg();
                }
            }
        }

        // 换段击键显示TextChanged事件
        private void SpeedDisplay_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isApplyingArticleSettings)
                return;

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
            if (_isApplyingArticleSettings)
                return;

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

        // 换段键准调节
        private void AccuracyUp(object sender, RoutedEventArgs e)
        {
            if (CfgInit && int.TryParse(accuracyDisplay.Text, out int value))
            {
                value += 1;
                if (value > 100) value = 100;
                accuracyDisplay.Text = value.ToString();
                cfg["换段键准"] = value.ToString();
                WriteCfg();
            }
        }

        private void AccuracyDown(object sender, RoutedEventArgs e)
        {
            if (CfgInit && int.TryParse(accuracyDisplay.Text, out int value))
            {
                value -= 1;
                if (value < 0) value = 0;
                accuracyDisplay.Text = value.ToString();
                cfg["换段键准"] = value.ToString();
                WriteCfg();
            }
        }

        private void AccuracyDisplay_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isApplyingArticleSettings)
                return;

            if (CfgInit && accuracyDisplay.Text.Length > 0)
            {
                if (int.TryParse(accuracyDisplay.Text, out int value))
                {
                    if (value < 0) value = 0;
                    if (value > 100) value = 100;
                    cfg["换段键准"] = value.ToString();
                    WriteCfg();
                }
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
            ApplyCurrentLogo();
        }

        /// <summary>
        /// 应用当前选中的 Logo 到窗口和标题栏图标
        /// </summary>
        private void ApplyCurrentLogo()
        {
            try
            {
                string currentLogo = Config.GetString("当前Logo");
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "ico", $"{currentLogo}.ico");
                if (!File.Exists(iconPath))
                {
                    Debug.WriteLine($"晴练单Logo文件不存在: {iconPath}");
                    return;
                }

                var iconUri = new Uri(iconPath, UriKind.Absolute);
                this.Icon = new BitmapImage(iconUri);
                TitleBarIcon.Source = new BitmapImage(iconUri);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"晴练单应用Logo失败: {ex.Message}");
            }
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
                string displayFgColor = Config.GetString("发文区字体色");
                string accentColor = Config.GetString("标题栏进度条颜色");

                // 转换颜色
                var bgBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#" + windowBgColor));
                var fgBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#" + windowFgColor));
                var displayBgBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#" + displayBgColor));
                var displayFgBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#" + displayFgColor));
                var accentColorBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#" + accentColor));

                // 计算派生颜色
                var borderBrush = ThemeColorHelper.CreateSubtleBorderBrush(bgBrush);

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
                Colors.DisplayForeground = displayFgBrush;

                // 更新DisplayGrid的背景色
                if (DisplayGrid != null)
                {
                    DisplayGrid.Background = displayBgBrush;
                }

                // 更新fld的前景色
                if (fld != null)
                {
                    fld.Background = displayBgBrush;
                    fld.Foreground = displayFgBrush;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"应用主题颜色失败: {ex.Message}");
            }
        }
    }
}
