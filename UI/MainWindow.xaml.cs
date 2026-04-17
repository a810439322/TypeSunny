using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Windows.Documents;
using System.Windows.Markup;


using Diff;

using System.Reflection;
using Interop.UIAutomationClient;

using Net;
using Colors = TypeSunny.Utils.Colors;
using CorePage = TypeSunny.Core.Page;
using TypeSunny.Core;
using TypeSunny.Logs;
using TypeSunny.Utils;
using TypeSunny.UI;
using TypeSunny.Difficulty;
using TypeSunny.ArticleSender;
using TypeSunny.UI.Modes;



namespace TypeSunny.UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 






    public partial class MainWindow : Window
    {
        public static MainWindow Current => Application.Current.MainWindow as MainWindow;

        /// <summary>
        /// 获取配置的字体大小，如果未配置则返回默认值40
        /// </summary>
        internal static double DisplayFontSize => Config.GetDouble("发文区字体大小") > 0 ? Config.GetDouble("发文区字体大小") : 40.0;

        private readonly SnakeMode _snakeMode;
        internal CopybookMode _copybookMode;
        internal TracingMode _tracingMode;

        // 线程安全的调试日志锁对象
        private static readonly object _debugLogLock = new object();

        /// <summary>
        /// 写入调试日志到文件（线程安全）
        /// </summary>
        private static void WriteDebugLog(string message)
        {
            // 日志已禁用
        }


        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetWindowRect(IntPtr hWnd, ref RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left; //最左坐标
            public int Top; //最上坐标
            public int Right; //最右坐标
            public int Bottom; //最下坐标
        }

        enum MouseEventFlag : uint
        {
            Move = 0x0001,
            LeftDown = 0x0002,
            LeftUp = 0x0004,
            RightDown = 0x0008,
            RightUp = 0x0010,
            MiddleDown = 0x0020,
            MiddleUp = 0x0040,
            XDown = 0x0080,
            XUp = 0x0100,
            Wheel = 0x0800,
            VirtualDesk = 0x4000,
            Absolute = 0x8000
        }

        [DllImport("User32")]
        public extern static bool GetCursorPos(ref System.Drawing.Point cPoint);

        [DllImport("user32.dll", EntryPoint = "GetForegroundWindow")]
        public static extern IntPtr fGetForegroundWindow();

        [DllImport("user32.dll")]
        static extern void mouse_event(MouseEventFlag flags, int dx, int dy, uint data, IntPtr extraInfo);

        [DllImport("User32")]
        public extern static void SetCursorPos(int x, int y);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_LEFT = 10;
        private const int HT_RIGHT = 11;
        private const int HT_TOP = 12;
        private const int HT_TOPLEFT = 13;
        private const int HT_TOPRIGHT = 14;
        private const int HT_BOTTOM = 15;
        private const int HT_BOTTOMLEFT = 16;
        private const int HT_BOTTOMRIGHT = 17;

        // 移除 Windows 11 窗口圆角
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_DONOTROUND = 1;








        internal Stopwatch sw = new Stopwatch();
        DifficultyDict difficultyDict = new DifficultyDict();

        // 保存窗口恢复时的位置和大小
        private Rect _restoreBounds = new Rect();
        private bool _isCustomMaximized = false;
        private string currentDifficultyText = "";

        private enum UpdateLevel
        {

            Progress = 1,
            PageArrange = 2
        };
        //       static Brush myBrush = new SolidColorBrush(Color.FromArgb(0xff, 0x95, 0xb0, 0xe3));


        static string[] NoneHeadPuncts =
        {
            "=",
            "，",
            "-",
            "。",
            "·",

            "、",
            "】",
            "、",
            "；",
            "）",
            "！",
            "@",
            "#",
            "￥",
            "%",
            "…",
            "&",
            "*",
            "”",
            "’",
            "'",
            "+",

            "—",
            "》",
            "~",

            "|",
            "",
            "？",
            "：",
            "=",
            ",",
            "-",
            ".",
            "`",

            "\\",
            "]",
            "/",
            ";",
            "\'",
            ")",
            "!",
            "@",
            "#",
            "$",
            "%",
            "^",
            "&",
            "*",
            "(",
            "+",

            "_",
            ">",
            "~",

            "|",
            "",
            "?",
            ":",
            "\"",
            " "
        };

        static string[] NoneTailPuncts =
        {
            "【",
            "（",
            "《",
            "{",
            "[",
            "<",
            "{",
            "“",
            "‘",

        };


        static List<string> AZ = new List<string>
        {
            "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U",
            "V", "W", "X", "Y", "Z", "a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k", "l", "m", "n", "o", "p",
            "q", "r", "s", "t", "u", "v", "w", "x", "y", "z", "'"
        };

        private void UpdateDisplay(UpdateLevel updateLevel)
        {
            // 行距过小会导致文字溢出背景，强制最小0.5
            if (Config.GetDouble("行距") < 0.5)
                Config.dicts["行距"] = "0.5";

            // 模式切换：先 Disable 旧模式，再 Enable 新模式，避免 Disable 覆盖 Enable 的隐藏操作
            bool copybookEnabled = Config.GetBool("字帖模式");
            bool tracingEnabled = Config.GetBool("临摹模式");

            if (!copybookEnabled && _copybookMode.IsActive)
                _copybookMode.Disable();
            if (!tracingEnabled && _tracingMode.IsActive)
                _tracingMode.Disable();

            if (copybookEnabled && !_copybookMode.IsActive)
                _copybookMode.Enable();
            if (tracingEnabled && !_tracingMode.IsActive)
                _tracingMode.Enable();

            if (IsLookingType && StateManager.LastType)
            {
                BlindDiff();
                return;
            }



            if (updateLevel >= UpdateLevel.PageArrange)
                PageReArrange();

            if (updateLevel >= UpdateLevel.Progress)
                PageProgressUpdate();

            // TextBlocks 重建后，通知字帖/临摹模式重新定位光标和重建镜像行
            if (_copybookMode != null && _copybookMode.IsActive)
                _copybookMode.ScheduleUpdatePosition();
            if (_tracingMode != null && _tracingMode.IsActive)
                _tracingMode.ScheduleInsertMirrorBlocks();


            void PageReArrange()
            {
                // 如果从贪吃蛇模式切换到普通模式，需要触发页面重新渲染（赛文API除外，它强制使用贪吃蛇）
                if (!Config.GetBool("贪吃蛇模式") && StateManager.txtSource != TxtSource.trainer &&
                    StateManager.txtSource != TxtSource.articlesender && StateManager.txtSource != TxtSource.raceApi)
                {
                    // 检测之前是否是贪吃蛇模式（Blocks.Count == Words.Count）
                    if (TextInfo.Blocks.Count == TextInfo.Words.Count && TextInfo.Blocks.Count > 0)
                    {
                        // 只设置 PageNum = -1，让 PageProgressUpdate 中的逻辑来处理清空和重建
                        TextInfo.PageNum = -1;
                        // 恢复滚动条可见性
                        ScDisplay.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                        return; // 提前返回，避免后续逻辑干扰
                    }
                }

                // 贪吃蛇模式（除打单器外）或 赛文API强制使用贪吃蛇
                if ((Config.GetBool("贪吃蛇模式") && StateManager.txtSource != TxtSource.trainer) ||
                    StateManager.txtSource == TxtSource.raceApi)
                {
                    TextInfo.PageNum = -1;
                    return;
                }

                // 文来使用简化渲染模式（无需分页，性能优化）
                //if (StateManager.txtSource == TxtSource.articlesender)
                //{
                //    TextInfo.PageNum = -1;
                //    return;
                //}

                // 计算可用高度（窗口高度 - 标题栏 - 按钮区1 - GridSplitter - 预留边距）
                double topBarHeight = TopBarGrid.ActualHeight;
                double availableHeight = this.ActualHeight - 30 - topBarHeight - 12 - 20;
                if (!_isResultsExpanded)
                {
                    // 成绩区已收起，可用高度更多
                }
                else
                {
                    // 成绩区已展开，减去成绩区和GridSplitter的高度
                    var resultsArea = this.FindName("resultsTextBoxGrid") as Border;
                    if (resultsArea != null)
                        availableHeight -= 5 + resultsArea.ActualHeight;
                }

                double y = availableHeight * 0.75;
                double x = (this.ActualWidth - 52);
                Paginator.ArrangePage(x, y, 40.0, TextInfo.Words.Count);

                TextInfo.PageNum = -1;
            }

            void PageProgressUpdate()
            {
                //计算页码
                int nextToType;
                if (_copybookMode != null && _copybookMode.IsActive)
                    nextToType = _copybookMode.CurrentIndex;
                else if (_tracingMode != null && _tracingMode.IsActive)
                    nextToType = _tracingMode.CurrentIndex;
                else
                    nextToType = new System.Globalization.StringInfo(TbxInput.Text).LengthInTextElements;
                if (nextToType > TextInfo.Words.Count)
                    nextToType = TextInfo.Words.Count;

                // 更新进度条（所有模式）
                int totalWords = TextInfo.Words.Count;
                int typedWords = nextToType;
                if (typedWords < 0) typedWords = 0;
                if (typedWords > totalWords) typedWords = totalWords;

                double percentage = totalWords > 0 ? (double)typedWords / totalWords : 0;
                if (Config.GetBool("显示进度条"))
                    TitleProgressBar.Width = this.ActualWidth * percentage;

                // 更新窗口标题显示字数进度（所有模式）
                UpdateWindowTitle(typedWords, totalWords);

                // 贪吃蛇模式（除打单器外）或 赛文API强制使用贪吃蛇
                if ((Config.GetBool("贪吃蛇模式") && StateManager.txtSource != TxtSource.trainer) ||
                    StateManager.txtSource == TxtSource.raceApi)
                {
                    SnakeModeUpdate(nextToType);
                    return;
                }

                // 非贪吃蛇模式下，打完最后一个字时不需要翻页和滚动，只需更新背景色
                if (nextToType >= TextInfo.Words.Count)
                    nextToType = TextInfo.Words.Count - 1;

                /*
                if (nextToType == -1)
                {
                    nextToType = TextInfo.Words.Count - 1;
                }
                */
                //         int newPageNum = Paginator.GetPageNum(nextToType);

                //   if (newPageNum == -1)
                //     return;

                //  if (newPageNum != TextInfo.PageNum)
                if (TextInfo.PageNum == -1 || nextToType >= Paginator.Pages[TextInfo.PageNum].BodyEnd ||
                    nextToType < Paginator.Pages[TextInfo.PageNum].BodyStart)
                {

                    //清空显示
                    TbDispay.Children.Clear();
                    ScDisplay.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
                    TextInfo.Blocks.Clear();



                    int pn = Paginator.GetPageNum(nextToType);
                    if (pn < 0)
                        return;
                    if (Paginator.Pages.Count < pn)
                        return;
                    CorePage p = Paginator.Pages[pn];



                    var fm = GetCurrentFontFamily(); // new FontFamily(Config.GetString("字体"));


                    double fs = DisplayFontSize;
                    double height = fs * (1.0 + Config.GetDouble("行距"));
                    double availablePad = Math.Max(0, height - fs * fm.LineSpacing);
                    double padTop = (availablePad / 2 + Math.Min((height - fs) / 2, availablePad)) / 2;
                    double padBottom = availablePad - padTop;
                    double MinWidth = fs * 0.9;

                    ScDisplay.FontFamily = fm;
                    ScDisplay.Foreground = Colors.DisplayForeground;
                    ScDisplay.FontSize = fs;
                    TbAcc.FontSize = fs / 3;
                    TbxInput.FontFamily = fm;
                    if (p.HeadEnd >= 0)
                        TextInfo.PageStartIndex = p.HeadStart;
                    else
                        TextInfo.PageStartIndex = p.BodyStart;


                    //添加头
                    if (p.HeadEnd >= 0)
                    {

                        for (int i = p.HeadStart; i <= p.HeadEnd; i++)
                        {


                            TextBlock tb = new TextBlock();
                            tb.Text = TextInfo.Words[i];

                            tb.Height = height;
                            tb.Padding = new Thickness(0, padTop, 0, padBottom);
                            if (tb.Text == "“" || tb.Text == "‘")
                            {
                                tb.MinWidth = MinWidth;
                                tb.TextAlignment = TextAlignment.Right;
                            }
                            else if (tb.Text == "”" || tb.Text == "’")
                            {
                                tb.MinWidth = MinWidth;
                                tb.TextAlignment = TextAlignment.Left;
                            }

                            TextInfo.Blocks.Add(tb);

                        }
                    }


                    if (p.BodyEnd >= 0)
                    {
                        for (int i = p.BodyStart; i <= p.BodyEnd; i++)
                        {

                            TextBlock tb = new TextBlock();
                            tb.Text = TextInfo.Words[i];
                            tb.Height = height;
                            tb.Padding = new Thickness(0, padTop, 0, padBottom);
                            if (tb.Text == "“" || tb.Text == "‘")
                            {
                                tb.MinWidth = MinWidth;
                                tb.TextAlignment = TextAlignment.Right;
                            }
                            else if (tb.Text == "”" || tb.Text == "’")
                            {
                                tb.MinWidth = MinWidth;
                                tb.TextAlignment = TextAlignment.Left;
                            }

                            TextInfo.Blocks.Add(tb);

                        }
                    }

                    if (p.FootEnd >= 0)
                    {
                        for (int i = p.FootStart; i <= p.FootEnd; i++)
                        {
                            TextBlock tb = new TextBlock();
                            tb.Text = TextInfo.Words[i];

                            tb.Height = height;
                            tb.Padding = new Thickness(0, padTop, 0, padBottom);
                            if (tb.Text == "“" || tb.Text == "‘")
                            {
                                tb.MinWidth = MinWidth;
                                tb.TextAlignment = TextAlignment.Right;
                            }
                            else if (tb.Text == "”" || tb.Text == "’")
                            {
                                tb.MinWidth = MinWidth;
                                tb.TextAlignment = TextAlignment.Left;
                            }

                            tb.TextDecorations = TextDecorations.Underline;
                            TextInfo.Blocks.Add(tb);


                        }
                    }


                    if (TextInfo.Blocks.Count >= 3) //标点打包，不在行首显示
                    {
                        int total = TextInfo.Blocks.Count;

                        //字符序列是否不允许出现在首尾
                        bool[] nohead = new bool[TextInfo.Blocks.Count];
                        bool[] notail = new bool[TextInfo.Blocks.Count];
                        for (int i = 0; i < TextInfo.Blocks.Count; i++)
                        {
                            nohead[i] = NoneHeadPuncts.Contains(TextInfo.Blocks[i].Text);
                            notail[i] = NoneTailPuncts.Contains(TextInfo.Blocks[i].Text);
                        }

                        for (int i = 1; i < TextInfo.Blocks.Count - 1; i++)
                        {
                            string c2 = TextInfo.Blocks[i].Text;
                            string c1 = TextInfo.Blocks[i - 1].Text;
                            string c3 = TextInfo.Blocks[i + 1].Text;

                            if (AZ.Contains(c2))
                            {
                                if (AZ.Contains(c1))
                                    nohead[i] = true;

                                if (AZ.Contains(c3))
                                    notail[i] = true;

                            }

                        }


                        bool[] inpack = new bool[TextInfo.Blocks.Count]; //是否打包


                        bool[] isPackHead = new bool[TextInfo.Blocks.Count]; //是否是包头
                        bool[] isPackTail = new bool[TextInfo.Blocks.Count]; // 是否是包尾,





                        inpack[0] = notail[0] || nohead[1];
                        isPackHead[0] = inpack[0];
                        isPackTail[0] = false;

                        inpack[total - 1] = nohead[total - 1] || notail[total - 2];
                        isPackHead[total - 1] = false;
                        isPackTail[total - 1] = inpack[total - 1];

                        for (int i = 1; i < TextInfo.Blocks.Count - 1; i++)
                        {
                            inpack[i] = nohead[i] || notail[i] || notail[i - 1] || nohead[i + 1];

                            isPackHead[i] = !nohead[i] && inpack[i] && !notail[i - 1];

                            isPackTail[i] = !notail[i] && inpack[i] && !nohead[i + 1];

                        }


                        StackPanel lstk = new StackPanel();
                        for (int i = 0; i < TextInfo.Blocks.Count; i++)
                        {
                            // 在换行位置前插入占满整行的空元素，强制 WrapPanel 换行
                            int globalIdx = TextInfo.PageStartIndex + i;
                            if (TextInfo.LineBreaks.Contains(globalIdx))
                            {
                                var lineBreak = new FrameworkElement();
                                lineBreak.Width = TbDispay.ActualWidth > 0 ? TbDispay.ActualWidth : 9999;
                                lineBreak.Height = 0;
                                TbDispay.Children.Add(lineBreak);
                            }

                            if (isPackHead[i])
                            {
                                lstk = new StackPanel();
                                lstk.Orientation = Orientation.Horizontal;
                                lstk.Width = double.NaN;
                                lstk.Height = double.NaN;



                                lstk.Children.Add(TextInfo.Blocks[i]);
                            }
                            else if (isPackTail[i])
                            {
                                lstk.Children.Add(TextInfo.Blocks[i]);
                                TbDispay.Children.Add(lstk);
                            }
                            else if (inpack[i])
                            {
                                lstk.Children.Add(TextInfo.Blocks[i]);
                            }
                            else
                            {
                                TbDispay.Children.Add(TextInfo.Blocks[i]);
                            }

                        }


                    }
                    else
                    {
                        for (int i = 0; i < TextInfo.Blocks.Count; i++)
                        {
                            int globalIdx = TextInfo.PageStartIndex + i;
                            if (TextInfo.LineBreaks.Contains(globalIdx))
                            {
                                var lineBreak = new FrameworkElement();
                                lineBreak.Width = TbDispay.ActualWidth > 0 ? TbDispay.ActualWidth : 9999;
                                lineBreak.Height = 0;
                                TbDispay.Children.Add(lineBreak);
                            }
                            TbDispay.Children.Add(TextInfo.Blocks[i]);
                        }
                    }


                    TextInfo.PageNum = pn;

                    TextInfo.BlocksStates.Clear();
                    TextInfo.Blocks.ForEach(t => TextInfo.BlocksStates.Add(WordStates.NO_TYPE));
                }



                //设置背景色
                if (!IsLookingType || IsBlindType)
                    for (int i = 0; i < TextInfo.Blocks.Count; i++)
                    {
                        if (TextInfo.BlocksStates[i] != TextInfo.wordStates[TextInfo.PageStartIndex + i])
                        {
                            switch (TextInfo.wordStates[TextInfo.PageStartIndex + i])
                            {
                                case WordStates.WRONG:
                                    // 盲打模式：不显示任何提示
                                    // 跟打模式：错字显示红色
                                    TextInfo.Blocks[i].Background = IsBlindType ? null : Colors.IncorrectBackground;
                                    break;
                                case WordStates.RIGHT:
                                    // 盲打模式：不显示任何提示
                                    // 跟打模式：对的字显示绿色
                                    TextInfo.Blocks[i].Background = IsBlindType ? null : Colors.CorrectBackground;
                                    break;
                                case WordStates.NO_TYPE:
                                    TextInfo.Blocks[i].Background = null;
                                    break;
                                default:
                                    break;

                            }

                            TextInfo.BlocksStates[i] = TextInfo.wordStates[TextInfo.PageStartIndex + i];

                        }
                    }



                // 滚动逻辑（统一使用 paindutch-main 方案）
                if (TextInfo.Blocks.Count > 0)
                {
                    int NextBlockIndex = (nextToType - TextInfo.PageStartIndex);

                    // 确保索引在有效范围内
                    if (NextBlockIndex >= 0 && NextBlockIndex < TextInfo.Blocks.Count)
                    {
                        // 强制更新布局，确保 ActualHeight 已计算
                        TextInfo.Blocks[NextBlockIndex].UpdateLayout();
                        TextInfo.Blocks[0].UpdateLayout();

                        // 计算当前字符的Y坐标（相对于第一个Block）
                        double currentPosY = TextInfo.Blocks[NextBlockIndex]
                                                 .TranslatePoint(new Point(0, 0), TextInfo.Blocks[0]).Y
                                             + TextInfo.Blocks[NextBlockIndex].ActualHeight / 2;

                        // 使用统一方法计算目标滚动位置（始终居中显示）
                        double targetOffset = CalculateScrollOffset(currentPosY);

                        // 执行滚动（起始位置强制滚动，其他时候由 SmoothScrollTo 自动判断）
                        SmoothScrollTo(targetOffset, forceScroll: (nextToType == 0));
                    }
                }

                // 普通模式滚动和速度跟随提示（每次更新都执行，不仅在翻页时）
                if (TextInfo.Blocks.Count > 0)
                {
                    try
                    {
                        // 计算当前字符在当前页中的索引
                        int NextBlockIndex = (nextToType - TextInfo.PageStartIndex);

                        // 确保索引在有效范围内
                        if (NextBlockIndex >= 0 && NextBlockIndex < TextInfo.Blocks.Count)
                        {
                            // 强制更新布局，确保 ActualHeight 已计算
                            TextInfo.Blocks[NextBlockIndex].UpdateLayout();
                            TextInfo.Blocks[0].UpdateLayout();

                            // 计算当前字符的Y坐标（相对于第一个Block）
                            double currentPosY = TextInfo.Blocks[NextBlockIndex]
                                                     .TranslatePoint(new Point(0, 0), TextInfo.Blocks[0]).Y
                                                 + TextInfo.Blocks[NextBlockIndex].ActualHeight / 2;

                            // 使用统一方法计算目标滚动位置（始终居中显示）
                            double targetOffset = CalculateScrollOffset(currentPosY);

                            // 执行滚动（起始位置强制滚动，其他时候由 SmoothScrollTo 自动判断）
                            SmoothScrollTo(targetOffset, forceScroll: (nextToType == 0));

                            // 跟随显示速度（统一走 UpdateSpeedFollowHint）
                            UpdateSpeedFollowHint(NextBlockIndex);
                        }
                    }
                    catch
                    {
                        // 忽略异常（布局未完成等情况）
                    }
                }







            }




            void BlindDiff()
            {


                TbDispay.Children.Clear();
                ScDisplay.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                TextInfo.Blocks.Clear(); // 修复BUG：确保Blocks和显示区域同步清空
                TextBlock tb = new TextBlock();
                tb.FontSize = DisplayFontSize;
                tb.FontFamily = GetCurrentFontFamily(); // new FontFamily(Config.GetString("字体"));
                tb.Background = BdDisplay.Background;
                tb.TextWrapping = TextWrapping.Wrap;
                tb.HorizontalAlignment = HorizontalAlignment.Stretch;
                tb.VerticalAlignment = VerticalAlignment.Top;
                tb.Foreground = Colors.DisplayForeground;


                string currentMatchText = string.Concat(TextInfo.Words);



                string t1 = currentMatchText.Replace('”', '\"').Replace('“', '\"').Replace('‘', '\'')
                    .Replace('’', '\'');
                string t2 = TbxInput.Text.Replace('”', '\"').Replace('“', '\"').Replace('‘', '\'').Replace('’', '\'');
                List<DiffRes> diffs = DiffTool.Diff(t1, t2);
                int counter = 0;
                foreach (var df in diffs)
                {
                    Run r = new Run();

                    switch (df.Type)
                    {
                        case DiffType.None:
                            r.Text = currentMatchText.Substring(df.OrigIndex, 1);
                            r.Background = Colors.CorrectBackground;
                            break;
                        case DiffType.Delete:

                            r.Text = currentMatchText.Substring(df.OrigIndex - 1, 1);
                            counter--;
                            //   r.Background = Colors.CorrectBackground;
                            break;
                        case DiffType.Add:

                            r.Text = TbxInput.Text.Substring(df.RevIndex + counter, 1);
                            counter++;
                            r.Background = Colors.IncorrectBackground;
                            break;

                    }

                    tb.Inlines.Add(r);
                }

                TbDispay.Children.Add(tb);
                TextInfo.Blocks.Add(tb); // 修复BUG：确保Blocks和显示区域同步

            }







        }

        /// <summary>
        /// 统一的滚动位置计算方法（参考 paindutch-main）
        /// 始终将当前字符保持在屏幕中心位置
        /// </summary>
        /// <param name="currentPosY">当前字符的Y坐标（相对于内容顶部）</param>
        /// <param name="nextToType">当前输入位置的字符索引</param>
        /// <param name="totalCount">总字符数</param>
        /// <returns>目标滚动偏移量</returns>
        internal double CalculateScrollOffset(double currentPosY, int nextToType = -1, int totalCount = -1)
        {
            double fs = DisplayFontSize;

            // 将当前字符保持在屏幕中心位置（48%位置）
            double center = ScDisplay.ViewportHeight * 0.48;
            double offset = currentPosY - center;

            // 限制滚动范围
            if (offset < 0)
                offset = 0;

            // 最大滚动位置加上底部额外空间
            double bottomPadding = fs * 0.8; // 底部额外空间为0.8个字体大小
            double maxOffset = ScDisplay.ScrollableHeight + bottomPadding;
            if (offset > maxOffset)
                offset = maxOffset;

            return offset;
        }

        /// <summary>
        /// 执行滚动（带条件判断，避免频繁滚动）
        /// </summary>
        /// <param name="targetOffset">目标滚动偏移量</param>
        /// <param name="forceScroll">是否强制滚动（例如：换行、起始位置）</param>
        internal void SmoothScrollTo(double targetOffset, bool forceScroll = false)
        {
            double fs = DisplayFontSize;
            double fh = fs * (1.0 + Config.GetDouble("行距"));

            // 只有当偏移量变化较大时才滚动，避免频繁滚动
            if (forceScroll || Math.Abs(ScDisplay.VerticalOffset - targetOffset) > fh * 0.8)
            {
                ScDisplay.ScrollToVerticalOffset(targetOffset);
            }
        }

        /// <summary>
        /// 更新字提显示
        /// </summary>
        internal void UpdateZiTi()
        {
            // 检查是否启用字提功能
            if (!Config.GetBool("启用字提"))
            {
                TbkZiTi.Text = "";
                return;
            }

            // 极速杯模式不显示字提
            if (StateManager.txtSource == TxtSource.jisucup)
            {
                TbkZiTi.Text = "";
                return;
            }

            // 没有载文时清空字提
            if (TextInfo.Words.Count == 0)
            {
                TbkZiTi.Text = "";
                return;
            }

            // 赛文模式：5秒没有字上屏时才显示字提
            if (StateManager.txtSource == TxtSource.raceApi)
            {
                var timeSinceLastInput = DateTime.Now - StateManager.LastInputTime;
                if (timeSinceLastInput.TotalSeconds < 5)
                {
                    TbkZiTi.Text = "";
                    return;
                }
            }

            // 获取下一个需要打的字
            int nextIndex;
            if (_copybookMode != null && _copybookMode.IsActive)
                nextIndex = _copybookMode.CurrentIndex;
            else if (_tracingMode != null && _tracingMode.IsActive)
                nextIndex = _tracingMode.CurrentIndex;
            else
            {
                System.Globalization.StringInfo si = new System.Globalization.StringInfo(TbxInput.Text);
                nextIndex = si.LengthInTextElements;
            }

            if (nextIndex >= TextInfo.Words.Count)
            {
                TbkZiTi.Text = "";
                return;
            }

            string nextChar = TextInfo.Words[nextIndex];
            string hint = ZiTiHelper.GetZiTi(nextChar);

            if (!string.IsNullOrEmpty(hint))
            {
                TbkZiTi.Text = hint;
            }
            else
            {
                TbkZiTi.Text = "";
            }
        }

        /// <summary>
        /// 贪吃蛇模式更新 - 委托给 SnakeMode
        /// </summary>
        private void SnakeModeUpdate(int nextToType)
        {
            _snakeMode.Update(nextToType);
        }

        /// <summary>
        /// 字帖模式下触发贪吃蛇更新（字符显隐+滚动）
        /// </summary>
        internal void SnakeModeUpdateFromCopybook(int nextToType)
        {
            _snakeMode.Update(nextToType);
        }

        /// <summary>
        /// 更新速度跟随提示位置
        /// </summary>
        internal void UpdateSpeedFollowHint(int nextToType)
        {
            bool showSpeed = Config.GetBool("速度跟随提示") && !Config.GetBool("盲打模式") &&
                             !double.IsNaN(Score.GetValidSpeed()) && Score.GetValidSpeed() > 0;

            if (!showSpeed)
            {
                if (TbAcc.Visibility == Visibility.Visible)
                    TbAcc.Visibility = Visibility.Hidden;
            }
            else if (nextToType < TextInfo.Blocks.Count)
            {
                if (TbAcc.Visibility == Visibility.Hidden)
                    TbAcc.Visibility = Visibility.Visible;

                double AccLeft;
                double blockLeft = TextInfo.Blocks[nextToType].TranslatePoint(new Point(0, 0), TextInfo.Blocks[0]).X;
                double blockTop = TextInfo.Blocks[nextToType].TranslatePoint(new Point(0, 0), TextInfo.Blocks[0]).Y;
                double blockHeight = TextInfo.Blocks[nextToType].ActualHeight;
                double AccTop;

                if (_copybookMode != null && _copybookMode.IsActive)
                {
                    // 字帖模式：速度提示放在当前字顶部与上一行之间的行间距区域
                    double lineSpacing = DisplayFontSize * Config.GetDouble("行距");
                    AccLeft = blockLeft;
                    AccTop = blockTop - lineSpacing * 1.1 + 0.5 * DisplayFontSize - ScDisplay.VerticalOffset;
                }
                else if (_tracingMode != null && _tracingMode.IsActive)
                {
                    // 临摹模式：速度提示放在镜像行当前字的上方
                    if (_tracingMode.TryGetMirrorBlockPosition(nextToType, out double mLeft, out double mTop, out double mHeight))
                    {
                        double lineSpacing = DisplayFontSize * Config.GetDouble("行距");
                        AccLeft = mLeft;
                        AccTop = mTop - lineSpacing * 1.1 + 0.5 * DisplayFontSize - ScDisplay.VerticalOffset;
                    }
                    else
                    {
                        AccLeft = blockLeft;
                        AccTop = blockTop + blockHeight - ScDisplay.VerticalOffset;
                    }
                }
                else
                {
                    AccTop = blockTop + blockHeight - ScDisplay.VerticalOffset;
                    AccLeft = blockLeft + TextInfo.Blocks[nextToType].ActualWidth / 3;
                }
                Canvas.SetTop(TbAcc, AccTop);
                Canvas.SetLeft(TbAcc, AccLeft);

                TbAcc.Text = Score.GetValidSpeed().ToString("F2");
                TbAcc.Foreground = Colors.GetSpeedColor(Score.GetValidSpeed());
            }
        }

        Timer Tdisplay = null;

        private void DispatchUpdateDisplay(object obj)
        {
            UpdateLevel ul = (UpdateLevel)obj;
            Dispatcher.Invoke(new Action(() => { UpdateDisplay(ul); }));

            if (Tdisplay != null)
            {
                Tdisplay.Dispose();
                Tdisplay = null;
            }

        }

        private void DelayUpdateDisplay(int delay, UpdateLevel updateLevel)
        {
            if (delay == 0)
            {
                if (Tdisplay != null)
                {
                    Tdisplay.Dispose();
                    Tdisplay = null;
                }




            }
            else if (delay > 0)
            {
                if (Tdisplay == null)
                {
                    Tdisplay = new Timer(DispatchUpdateDisplay, updateLevel, delay, Timeout.Infinite);
                }
                else
                {
                    Tdisplay.Dispose();
                    Tdisplay = new Timer(DispatchUpdateDisplay, updateLevel, delay, Timeout.Infinite);


                }
            }
        }

        private void RetypeThisGroup()
        {



            UpdateTypingStat();

            if (StateManager.txtSource == TxtSource.changeSheng || StateManager.txtSource == TxtSource.jbs ||
                StateManager.txtSource == TxtSource.jisucup || StateManager.txtSource == TxtSource.raceApi ||
                Config.GetBool("禁止F3重打"))
                return;





            LoadText(TextInfo.MatchText, RetypeType.retype, TxtSource.unchange);
            //         TbkStatusTop.Text = "重打";
            return;






        }


        private async void InternalHotkeyF2(object sender, ExecutedRoutedEventArgs e)
        {
            await SendArticle();
        }


        private void InternalHotkeyF3(object sender, ExecutedRoutedEventArgs e)
        {
            HotkeyF3();
        }


        private void HotkeyF3()
        {
            if (StateManager.txtSource == TxtSource.trainer && winTrainer != null)
            {
                // 如果正在打字，先记录当前进度
                if (StateManager.typingState == TypingState.typing && sw.IsRunning)
                {
                    // 计算当前输入的字数
                    int inputWordCount = new System.Globalization.StringInfo(TbxInput.Text).LengthInTextElements;

                    // 计算已用时间（秒）
                    double timeSeconds = sw.Elapsed.TotalSeconds;

                    // 计算准确率（简单比对已输入的部分）
                    double accuracy = 1.0;
                    if (inputWordCount > 0)
                    {
                        int correctCount = 0;
                        for (int i = 0; i < Math.Min(inputWordCount, TextInfo.wordStates.Count); i++)
                        {
                            if (TextInfo.wordStates[i] == WordStates.RIGHT)
                                correctCount++;
                        }

                        accuracy = (double)correctCount / inputWordCount;
                    }

                    // 记录部分进度
                    winTrainer.RecordPartialProgress(inputWordCount, timeSeconds, accuracy);
                }

                winTrainer.F3();
            }
            else
                RetypeThisGroup();
        }

        public MainWindow()
        {
            // InitCfg();

            InitializeComponent();

            _snakeMode = new SnakeMode(this);
            _copybookMode = new CopybookMode(this);
            _tracingMode = new TracingMode(this);
            double left = Config.GetDouble("窗口坐标X");
            double top = Config.GetDouble("窗口坐标Y");
            double width = Config.GetDouble("窗口宽度");
            double height = Config.GetDouble("窗口高度");

            // 获取屏幕工作区域
            var workArea = SystemParameters.WorkArea;

            // 设置合理的尺寸范围，避免窗口过大
            if (width <= 0 || width > 3000)
                width = 966;
            if (height <= 0 || height > 2000)
                height = 750;

            this.Width = width;
            this.Height = height;

            // 检查保存的坐标是否合理
            // 默认值0或很小的值、负值、超出屏幕范围的值都视为无效
            bool positionValid = true;
            if (left <= 10 || left < 0 || left >= workArea.Width - 100)
                positionValid = false;
            if (top <= 10 || top < 0 || top >= workArea.Height - 100)
                positionValid = false;

            // 如果位置无效，使用屏幕中心（不设置手动坐标）
            if (!positionValid)
            {
                this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
            else
            {
                this.WindowStartupLocation = WindowStartupLocation.Manual;
                this.Left = left;
                this.Top = top;
            }

            // 延迟初始化赛文菜单到窗口加载完成后
            this.Loaded += (s, e) =>
            {
                try
                {
                    // 初始化赛文菜单（动态生成）
                    InitializeRaceMenu();

                    // 初始化文来菜单（动态生成）
                    InitializeWenlaiMenu();

                    // 菜单创建完成后，更新Helper中的菜单项引用
                    if (jbsHelper != null)
                    {
                        jbsHelper.SetMenuItems(
                            raceMenuItems.ContainsKey("jbs_login") ? raceMenuItems["jbs_login"] : null,
                            raceMenuItems.ContainsKey("jbs_loadArticle") ? raceMenuItems["jbs_loadArticle"] : null
                        );
                    }

                    if (jiSuCupHelper != null)
                    {
                        jiSuCupHelper.SetMenuItems(
                            raceMenuItems.ContainsKey("jisucup_login") ? raceMenuItems["jisucup_login"] : null,
                            raceMenuItems.ContainsKey("jisucup_loadArticle")
                                ? raceMenuItems["jisucup_loadArticle"]
                                : null
                        );
                    }

                    // 菜单项引用更新后，再更新登录状态
                    // 初始化锦标赛菜单显示状态
                    if (jbsHelper != null)
                    {
                        jbsHelper.UpdateLoginStatus();
                        jbsHelper.UpdateArticleButtonStatus();
                    }

                    // 初始化极速杯菜单显示状态
                    if (jiSuCupHelper != null)
                    {
                        jiSuCupHelper.UpdateLoginStatus();
                    }

                    // 初始化赛文API菜单显示状态
                    if (raceHelper != null)
                    {
                        raceHelper.UpdateLoginStatus();
                        raceHelper.UpdateArticleButtonStatus();
                    }

                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"延迟初始化菜单失败: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                }
            };

            // 初始化Helper类 - 使用动态生成的MenuItem
            // 注意：此时raceMenuItems可能还是空的，所以全部传null
            jbsHelper = new JbsHelper(
                raceMenuItems.ContainsKey("jbs_login") ? raceMenuItems["jbs_login"] : null,
                raceMenuItems.ContainsKey("jbs_loadArticle") ? raceMenuItems["jbs_loadArticle"] : null
            );

            jiSuCupHelper = new JiSuCupHelper(
                raceMenuItems.ContainsKey("jisucup_login") ? raceMenuItems["jisucup_login"] : null,
                raceMenuItems.ContainsKey("jisucup_loadArticle") ? raceMenuItems["jisucup_loadArticle"] : null
            );

            // 建立锦标赛和极速杯的关联（账号一体）
            jbsHelper.SetJiSuCupHelper(jiSuCupHelper);
            jiSuCupHelper.SetJbsHelper(jbsHelper);

            // 初始化赛文API Helper（新版，支持多服务器）
            raceHelperV2 = new TypeSunny.Net.RaceHelperV2();

            // 初始化旧版Helper（保留兼容）
            raceHelper = new TypeSunny.Net.RaceHelper(
                raceMenuItems.ContainsKey("race_login") ? raceMenuItems["race_login"] : null,
                raceMenuItems.ContainsKey("race_loadArticle") ? raceMenuItems["race_loadArticle"] : null,
                raceMenuItems.ContainsKey("race_register") ? raceMenuItems["race_register"] : null,
                raceMenuItems.ContainsKey("race_history") ? raceMenuItems["race_history"] : null,
                raceMenuItems.ContainsKey("race_leaderboard") ? raceMenuItems["race_leaderboard"] : null
            );

            // 注意：UpdateLoginStatus 和 UpdateArticleButtonStatus 已移到 Loaded 事件中调用
            // 因为此时菜单项还未创建完成
        }

        private void BtnF3_Click(object sender, RoutedEventArgs e)
        {
            HotkeyF3();
        }














        private void expanded(object sender, RoutedEventArgs e)
        {
            if (StateManager.ConfigLoaded)
            {
                LoadResultsPanelHeight();
                Config.Set("成绩面板展开", true);
            }
        }

        private void expd_Collapsed(object sender, RoutedEventArgs e)
        {
            // 新布局中收起/展开逻辑已简化，此方法保留以兼容
            if (StateManager.ConfigLoaded)
            {
                Config.Set("成绩面板展开", false);
            }
        }


        private void InitFontFamilySelector()
        {

            DirectoryInfo dr = new DirectoryInfo("字体");
            if (!dr.Exists)
                dr.Create();

            System.Globalization.CultureInfo cn = System.Globalization.CultureInfo.GetCultureInfo("zh-CN");
            System.Globalization.CultureInfo en = System.Globalization.CultureInfo.GetCultureInfo("en-US");

            foreach (var f in dr.GetFiles("*.ttf"))
            {
                try
                {
                    var fullname = f.FullName;

                    GlyphTypeface gf = new GlyphTypeface(new Uri(fullname));
                    var s = gf.FamilyNames;
                    //       var b =  gf.FontUri.ToString();

                    string fontname = "";


                    if (s.ContainsKey(cn))
                        fontname = s[cn];
                    else if (s.ContainsKey(en))
                        fontname = s[en];


                    if (fontname != "")
                    {




                        ComboBoxItem cbi = new ComboBoxItem();



                        string currentPath = System.AppDomain.CurrentDomain.BaseDirectory;
                        Uri uri = new Uri(currentPath + "字体\\");
                        FontFamily fm = new FontFamily(uri, "./#" + fontname);
                        cbi.FontFamily = fm;
                        cbi.FontSize = 40.0;
                        cbi.Content = "#" + fontname;

                        // 字体选择已移到设置窗口
                    }
                }
                catch (Exception ex)
                {
                    // 忽略无法加载的字体文件
                    System.Diagnostics.Debug.WriteLine($"加载字体文件失败: {f.FullName}, 错误: {ex.Message}");
                }

            }





            foreach (FontFamily fontfamily in Fonts.SystemFontFamilies)
            {
                LanguageSpecificStringDictionary lsd = fontfamily.FamilyNames;
                if (lsd.ContainsKey(XmlLanguage.GetLanguage("zh-cn")))
                {
                    // 字体选择已移到设置窗口
                }
                else
                {
                    // 字体选择已移到设置窗口
                }
            }
        }

        // 字体缓存
        private FontFamily cachedFontFamily = null;
        private string cachedFontName = "";

        // 字提字体缓存
        private FontFamily cachedZiTiFontFamily = null;
        private string cachedZiTiFontName = "";

        public FontFamily GetCurrentFontFamily()
        {
            string fontName = Config.GetString("字体");

            if (fontName == null || fontName.Length == 0)
                return null;

            // 如果字体名称没变，返回缓存的字体
            if (cachedFontFamily != null && cachedFontName == fontName)
            {
                return cachedFontFamily;
            }

            // 创建新的字体并缓存
            FontFamily fm;
            if (fontName.Substring(0, 1) == "#")
            {
                string currentPath = System.AppDomain.CurrentDomain.BaseDirectory;
                Uri uri = new Uri(currentPath + "字体\\");
                fm = new FontFamily(uri, "./" + fontName);
            }
            else
            {
                fm = new FontFamily(fontName);
            }

            cachedFontFamily = fm;
            cachedFontName = fontName;
            return fm;
        }

        public FontFamily GetZiTiFontFamily()
        {
            string fontName = Config.GetString("字提字体");

            if (fontName == null || fontName.Length == 0)
                return null;

            // 如果字体名称没变，返回缓存的字体
            if (cachedZiTiFontFamily != null && cachedZiTiFontName == fontName)
            {
                return cachedZiTiFontFamily;
            }

            // 创建新的字体并缓存
            FontFamily fm;
            if (fontName.Substring(0, 1) == "#")
            {
                string currentPath = System.AppDomain.CurrentDomain.BaseDirectory;
                Uri uri = new Uri(currentPath + "字体\\");
                fm = new FontFamily(uri, "./" + fontName);
            }
            else
            {
                fm = new FontFamily(fontName);
            }

            cachedZiTiFontFamily = fm;
            cachedZiTiFontName = fontName;
            return fm;
        }

        private void InitDisplay()
        {


            MainBorder.Background = Colors.FromString(Config.GetString("窗体背景色"));
            this.Foreground = Colors.FromString(Config.GetString("窗体字体色"));
            BdDisplay.Background = Colors.FromString(Config.GetString("跟打区背景色"));
            Colors.DisplayForeground = Colors.FromString(Config.GetString("发文区字体色"));
            Colors.CorrectBackground = Colors.FromString(Config.GetString("打对色"));
            Colors.IncorrectBackground = Colors.FromString(Config.GetString("打错色"));

            // 设置跟打区（TbxInput）字体颜色
            TbxInput.Foreground = Colors.FromString(Config.GetString("跟打区字体色"));

            // 应用按钮、菜单和顶部栏颜色
            ApplyButtonMenuColors();

            // 应用字体大小
            TbxInput.FontSize = Config.GetDouble("跟打区字体大小") > 0 ? Config.GetDouble("跟打区字体大小") : 40.0;
            TbxResults.FontSize = Config.GetDouble("成绩区字体大小") > 0 ? Config.GetDouble("成绩区字体大小") : 15.0;

            // 应用发文框和跟打框的比例
            ApplyDisplayInputRatio();

            if (winTrainer != null)
            {
                WinTrainer.Current.DisplayGrid.Background = BdDisplay.Background;
                winTrainer.Background = this.Background;
            }

            ReadBlindType();




            this.Height = Config.GetDouble("窗口高度");
            this.Width = Config.GetDouble("窗口宽度");

            // 加载成绩面板展开状态
            var grid_a = this.FindName("grid_a") as Grid;
            if (grid_a != null)
            {
                if (Config.GetBool("成绩面板展开"))
                {
                    _isResultsExpanded = true;
                    // 成绩区高度已在ApplyDisplayInputRatio中设置
                    resultsTextBoxGrid.Visibility = Visibility.Visible;
                    gridSplitterResults.Visibility = Visibility.Visible;
                    BtnToggleResults.Content = "▼";

                    // 启用所有 GridSplitter
                    gridSplitterArticleTyping.IsEnabled = true;
                    gridSplitterResults.IsEnabled = true;

                    // 保存展开时的窗口高度
                    _expandedWindowHeight = this.ActualHeight;
                }
                else
                {
                    _isResultsExpanded = false;
                    // 收起状态
                    this.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        // 获取发文区和跟打区的当前实际高度
                        double articleHeight = grid_a.RowDefinitions[2].ActualHeight;
                        double typingHeight = grid_a.RowDefinitions[4].ActualHeight;

                        // 先将发文区和跟打区设置为固定像素高度
                        grid_a.RowDefinitions[2].Height = new GridLength(articleHeight, GridUnitType.Pixel);
                        grid_a.RowDefinitions[4].Height = new GridLength(typingHeight, GridUnitType.Pixel);

                        // 只禁用成绩区上方的 GridSplitter
                        gridSplitterArticleTyping.IsEnabled = true;
                        gridSplitterResults.IsEnabled = false;

                        // 设置成绩区 Border 的 margin 为 0
                        resultsTextBoxGrid.Margin = new Thickness(0);

                        // 设置成绩区行高度为0
                        grid_a.RowDefinitions[6].Height = new GridLength(0, GridUnitType.Pixel);
                        resultsTextBoxGrid.Visibility = Visibility.Collapsed;
                        gridSplitterResults.Visibility = Visibility.Collapsed;
                        BtnToggleResults.Content = "▲";

                        // 计算收起后的窗口高度：当前窗口高度 - 成绩区实际高度 - GridSplitter高度
                        double currentWindowHeight = this.ActualHeight;
                        double resultsAreaHeight = grid_a.RowDefinitions[6].ActualHeight;
                        double gridSplitterHeight = 5; // GridSplitter 的高度

                        // 收起后的高度 = 当前高度 - 成绩区高度 - GridSplitter高度
                        double collapsedHeight = currentWindowHeight - resultsAreaHeight - gridSplitterHeight;

                        this.Height = collapsedHeight;

                        // 延迟将发文区和跟打区改为 *，让它们可以自适应
                        this.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            grid_a.RowDefinitions[2].Height = new GridLength(1, GridUnitType.Star);
                            if ((_copybookMode == null || !_copybookMode.IsActive) &&
                                (_tracingMode == null || !_tracingMode.IsActive))
                            {
                                grid_a.RowDefinitions[4].Height = new GridLength(1, GridUnitType.Star);
                            }
                            if (_copybookMode != null && _copybookMode.IsActive)
                                _copybookMode.ScheduleUpdatePosition();
                            if (_tracingMode != null && _tracingMode.IsActive)
                                _tracingMode.ScheduleInsertMirrorBlocks();
                        }), System.Windows.Threading.DispatcherPriority.Loaded);

                        // 保存展开时的窗口高度
                        _expandedWindowHeight = Config.GetDouble("窗口高度");
                    }), System.Windows.Threading.DispatcherPriority.Loaded);
                }
            }

            TbxInput.FontFamily = GetCurrentFontFamily(); // new FontFamily(Config.GetString("字体")); ;
            SldZoom.Value = 40; // 默认字体大小

            // 设置字提控件字体
            TbkZiTi.FontFamily = GetZiTiFontFamily();
            TbkZiTi.FontSize = Config.GetDouble("字提字体大小");
            TbkZiTi.Foreground = Colors.FromString(Config.GetString("发文区字体色"));

            InitFontFamilySelector();


            // 字体选择已移到设置窗口

            UpdateButtonProgress();

            /*
                        if (Config.GetBool("新版QQ"))
                        {
                            BtnF5.Visibility = Visibility.Hidden;

                        }
                        else
                            BtnF5.Visibility = Visibility.Visible;
            */

            if (Config.GetBool("鼠标中键载文"))
                StartMouseHook();
            /*
                        if (Config.GetBool("回放功能"))
                            StartHook();
                        else
                            StopHook();

                        Tbk.IsEnabled = Config.GetBool("回放功能");
            */

            BtnF3.IsEnabled = !Config.GetBool("禁止F3重打");

            IntStringDict.Load();

            StateManager.ConfigLoaded = true;

            // 强制刷新标题栏和顶部栏背景色
            Dispatcher.BeginInvoke(new Action(() =>
            {
                // 应用自定义标题栏背景色
                if (TitleBarGrid != null)
                {
                    TitleBarGrid.Background = Colors.FromString(Config.GetString("窗体背景色"));
                    TitleBarGrid.UpdateLayout();
                    System.Diagnostics.Debug.WriteLine($"强制刷新TitleBarGrid背景色: {Config.GetString("窗体背景色")}");
                }

                if (TopBarGrid != null)
                {
                    TopBarGrid.Background = Colors.FromString(Config.GetString("窗体背景色"));
                    TopBarGrid.UpdateLayout();
                    System.Diagnostics.Debug.WriteLine($"强制刷新TopBarGrid背景色: {Config.GetString("窗体背景色")}");
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);

            // 启动时检查字帖模式（此时可能还没有文章，Enable 会先隐藏跟打区）
            if (Config.GetBool("字帖模式") && !_copybookMode.IsActive)
                _copybookMode.Enable();
        }

        private void ReadBlindType()
        {
            // 打字模式控件已移到设置窗口
            /*
            if (SldBlind.Value < 1.01 ) //盲打
            {
                Config.Set("盲打模式", true);
                Config.Set("看打模式", false);
            }
            else if (SldBlind.Value > 2.99) //看打
            {
                Config.Set("盲打模式", true);
                Config.Set("看打模式", true);
            }
            else
            {
                Config.Set("盲打模式", false);
                Config.Set("看打模式", false);
            }

            */

            //      ChkBlindType.IsChecked = Config.GetBool("盲打模式");

            //      ChkLookType.IsChecked = Config.GetBool("看打模式") && !Config.GetBool("盲打模式");
        }

        private void ReloadCfg()
        {
            // 重新加载字提数据（方案可能已更改）
            string scheme = Config.GetString("字提方案");
            if (!string.IsNullOrEmpty(scheme))
            {
                ZiTiHelper.Reload(scheme);
            }
            else
            {
                ZiTiHelper.Reload();
            }

            StateManager.ConfigLoaded = false;

            MainBorder.Background = Colors.FromString(Config.GetString("窗体背景色"));
            this.Foreground = Colors.FromString(Config.GetString("窗体字体色"));
            BdDisplay.Background = Colors.FromString(Config.GetString("跟打区背景色"));
            Colors.DisplayForeground = Colors.FromString(Config.GetString("发文区字体色"));
            Colors.CorrectBackground = Colors.FromString(Config.GetString("打对色"));
            Colors.IncorrectBackground = Colors.FromString(Config.GetString("打错色"));

            // 设置跟打区（TbxInput）字体颜色
            TbxInput.Foreground = Colors.FromString(Config.GetString("跟打区字体色"));

            // 应用按钮、菜单和顶部栏颜色
            ApplyButtonMenuColors();

            // 应用字体大小
            TbxInput.FontSize = Config.GetDouble("跟打区字体大小") > 0 ? Config.GetDouble("跟打区字体大小") : 40.0;
            TbxResults.FontSize = Config.GetDouble("成绩区字体大小") > 0 ? Config.GetDouble("成绩区字体大小") : 15.0;

            if (winTrainer != null)
            {
                WinTrainer.Current.DisplayGrid.Background = BdDisplay.Background;
                winTrainer.Background = this.Background;
            }

            this.Height = Config.GetDouble("窗口高度");
            this.Width = Config.GetDouble("窗口宽度");

            // 加载成绩面板展开状态
            var grid_a = this.FindName("grid_a") as Grid;
            if (grid_a != null)
            {
                if (Config.GetBool("成绩面板展开"))
                {
                    _isResultsExpanded = true;
                    // 成绩区高度已在ApplyDisplayInputRatio中设置
                    resultsTextBoxGrid.Visibility = Visibility.Visible;
                    gridSplitterResults.Visibility = Visibility.Visible;
                    BtnToggleResults.Content = "▼";

                    // 启用所有 GridSplitter
                    gridSplitterArticleTyping.IsEnabled = true;
                    gridSplitterResults.IsEnabled = true;

                    // 保存展开时的窗口高度
                    _expandedWindowHeight = this.ActualHeight;
                }
                else
                {
                    // 收起状态 — 只有从展开切换到收起时才调整窗口高度
                    if (_isResultsExpanded)
                    {
                        _isResultsExpanded = false;
                        this.Dispatcher.BeginInvoke(new Action(() =>
                        {
                        // 获取发文区和跟打区的当前实际高度
                        double articleHeight = grid_a.RowDefinitions[2].ActualHeight;
                        double typingHeight = grid_a.RowDefinitions[4].ActualHeight;

                        // 先将发文区和跟打区设置为固定像素高度
                        grid_a.RowDefinitions[2].Height = new GridLength(articleHeight, GridUnitType.Pixel);
                        grid_a.RowDefinitions[4].Height = new GridLength(typingHeight, GridUnitType.Pixel);

                        // 只禁用成绩区上方的 GridSplitter
                        gridSplitterArticleTyping.IsEnabled = true;
                        gridSplitterResults.IsEnabled = false;

                        // 设置成绩区 Border 的 margin 为 0
                        resultsTextBoxGrid.Margin = new Thickness(0);

                        // 设置成绩区行高度为0
                        grid_a.RowDefinitions[6].Height = new GridLength(0, GridUnitType.Pixel);
                        resultsTextBoxGrid.Visibility = Visibility.Collapsed;
                        gridSplitterResults.Visibility = Visibility.Collapsed;
                        BtnToggleResults.Content = "▲";

                        // 计算收起后的窗口高度：当前窗口高度 - 成绩区实际高度 - GridSplitter高度
                        double currentWindowHeight = this.ActualHeight;
                        double resultsAreaHeight = grid_a.RowDefinitions[6].ActualHeight;
                        double gridSplitterHeight = 5; // GridSplitter 的高度

                        // 收起后的高度 = 当前高度 - 成绩区高度 - GridSplitter高度
                        double collapsedHeight = currentWindowHeight - resultsAreaHeight - gridSplitterHeight;

                        this.Height = collapsedHeight;

                        // 延迟将发文区和跟打区改为 *，让它们可以自适应
                        this.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            grid_a.RowDefinitions[2].Height = new GridLength(1, GridUnitType.Star);
                            if ((_copybookMode == null || !_copybookMode.IsActive) &&
                                (_tracingMode == null || !_tracingMode.IsActive))
                            {
                                grid_a.RowDefinitions[4].Height = new GridLength(1, GridUnitType.Star);
                            }
                            if (_copybookMode != null && _copybookMode.IsActive)
                                _copybookMode.ScheduleUpdatePosition();
                            if (_tracingMode != null && _tracingMode.IsActive)
                                _tracingMode.ScheduleInsertMirrorBlocks();
                        }), System.Windows.Threading.DispatcherPriority.Loaded);

                        // 保存展开时的窗口高度
                        _expandedWindowHeight = Config.GetDouble("窗口高度");
                    }), System.Windows.Threading.DispatcherPriority.Loaded);
                    }
                    else
                    {
                        // 已经是收起状态，只确保 UI 状态一致
                        resultsTextBoxGrid.Visibility = Visibility.Collapsed;
                        gridSplitterResults.Visibility = Visibility.Collapsed;
                        BtnToggleResults.Content = "▲";
                    }
                }
            }

            TbxInput.FontFamily = GetCurrentFontFamily(); // new FontFamily(Config.GetString("字体")); ;

            // 设置字提控件字体
            TbkZiTi.FontFamily = GetZiTiFontFamily();
            TbkZiTi.FontSize = Config.GetDouble("字提字体大小");
            TbkZiTi.Foreground = Colors.FromString(Config.GetString("发文区字体色"));

            ReadBlindType();

            InitFontFamilySelector();


            // 字体选择已移到设置窗口


            UpdateButtonProgress();
            /*
            if (Config.GetBool("新版QQ"))
            {
                BtnF5.Visibility = Visibility.Hidden;

            }
            else
                BtnF5.Visibility = Visibility.Visible;
            */
            if (Config.GetBool("鼠标中键载文"))
                StartMouseHook();
            /*
                        if (Config.GetBool("回放功能"))
                            StartHook();
                        else
                            StopHook();

                        Tbk.IsEnabled = Config.GetBool("回放功能");
            */
            BtnF3.IsEnabled = !Config.GetBool("禁止F3重打");
            StateManager.ConfigLoaded = true;
            IntStringDict.Load();

            UpdateDisplay(UpdateLevel.PageArrange);

            // 主题切换后刷新字帖/临摹模式的光标颜色和位置
            if (_copybookMode != null && _copybookMode.IsActive)
                _copybookMode.RefreshTheme();
            if (_tracingMode != null && _tracingMode.IsActive)
                _tracingMode.RefreshTheme();

            // 强制刷新标题栏和顶部栏背景色
            Dispatcher.BeginInvoke(new Action(() =>
            {
                // 应用自定义标题栏背景色
                if (TitleBarGrid != null)
                {
                    TitleBarGrid.Background = Colors.FromString(Config.GetString("窗体背景色"));
                    TitleBarGrid.UpdateLayout();
                    System.Diagnostics.Debug.WriteLine($"[ReloadCfg]强制刷新TitleBarGrid背景色: {Config.GetString("窗体背景色")}");
                }

                if (TopBarGrid != null)
                {
                    TopBarGrid.Background = Colors.FromString(Config.GetString("窗体背景色"));
                    TopBarGrid.UpdateLayout();
                    System.Diagnostics.Debug.WriteLine($"[ReloadCfg]强制刷新TopBarGrid背景色: {Config.GetString("窗体背景色")}");
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        /// <summary>
        /// 应用按钮、菜单和顶部栏的颜色配置
        /// </summary>
        private void ApplyButtonMenuColors()
        {
            try
            {
                string buttonBgStr = Config.GetString("按钮背景色");
                string buttonFgStr = Config.GetString("按钮字体色");
                string windowBgStr = Config.GetString("窗体背景色");
                string menuBgStr = Config.GetString("菜单背景色");
                string menuFgStr = Config.GetString("菜单字体色");

                System.Diagnostics.Debug.WriteLine($"按钮背景色: {buttonBgStr}");
                System.Diagnostics.Debug.WriteLine($"按钮字体色: {buttonFgStr}");
                System.Diagnostics.Debug.WriteLine($"窗体背景色: {windowBgStr}");
                System.Diagnostics.Debug.WriteLine($"菜单背景色: {menuBgStr}");
                System.Diagnostics.Debug.WriteLine($"菜单字体色: {menuFgStr}");

                var buttonBg = Colors.FromString(buttonBgStr);
                var buttonFg = Colors.FromString(buttonFgStr);
                var topBarBg = Colors.FromString(windowBgStr);
                var menuBg = Colors.FromString(menuBgStr);
                var menuFg = Colors.FromString(menuFgStr);

                // 应用所有按钮的背景和前景色
                BtnConfig.Background = buttonBg;
                BtnConfig.Foreground = buttonFg;
                BtnF3.Background = buttonBg;
                BtnF3.Foreground = buttonFg;
                BtnCtrlE.Background = buttonBg;
                BtnCtrlE.Foreground = buttonFg;
                BtnF4.Background = buttonBg;
                BtnF4.Foreground = buttonFg;
                BtnF5.Background = buttonBg;
                BtnF5.Foreground = buttonFg;
                BtnRandomArticle.Background = buttonBg;
                BtnRandomArticle.Foreground = buttonFg;
                BtnTrainer.Background = buttonBg;
                BtnTrainer.Foreground = buttonFg;
                BtnNext.Background = buttonBg;
                BtnNext.Foreground = buttonFg;
                BtnSendArticle.Background = buttonBg;
                BtnSendArticle.Foreground = buttonFg;
                BtnArticleManager.Background = buttonBg;
                BtnArticleManager.Foreground = buttonFg;
                BtnPrev.Background = buttonBg;
                BtnPrev.Foreground = buttonFg;

                // 应用标题栏背景色和字体色（自定义标题栏）
                if (TitleBarGrid != null)
                {
                    TitleBarGrid.Background = Colors.FromString(Config.GetString("窗体背景色"));
                    TitleBarGrid.UpdateLayout();
                    System.Diagnostics.Debug.WriteLine($"TitleBarGrid背景色已设置为: {Config.GetString("窗体背景色")}");
                }

                // 设置标题栏文字颜色
                if (TbkTitle != null)
                {
                    TbkTitle.Foreground = Colors.FromString(Config.GetString("窗体字体色"));
                }

                // 设置标题栏按钮前景色
                var titleBarForeground = Colors.FromString(Config.GetString("窗体字体色"));
                if (BtnMinimize != null)
                    BtnMinimize.Foreground = titleBarForeground;
                if (BtnMaximize != null)
                    BtnMaximize.Foreground = titleBarForeground;
                if (BtnClose != null)
                    BtnClose.Foreground = titleBarForeground;

                // 应用工具栏背景色（使用窗体背景色）
                if (TopBarGrid != null)
                {
                    TopBarGrid.Background = topBarBg;
                    System.Diagnostics.Debug.WriteLine($"TopBarGrid背景色已设置为: {windowBgStr}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("警告: TopBarGrid为null!");
                }

                // 应用菜单背景色和字体色
                MenuItemRace.Background = menuBg;
                MenuItemRace.Foreground = menuFg;
                MenuWenlai.Background = menuBg;
                MenuWenlai.Foreground = menuFg;

                // 重新初始化两个菜单以应用新主题色（包括悬停效果）
                InitializeRaceMenu();
                InitializeWenlaiMenu();

                // 更新标题进度条颜色（仅在启用进度条时）
                if (Config.GetBool("显示进度条"))
                {
                    string progressColor = Config.GetString("标题栏进度条颜色");
                    if (!string.IsNullOrEmpty(progressColor))
                    {
                        TitleProgressBar.Fill = Colors.FromString(progressColor);
                    }
                }

                System.Diagnostics.Debug.WriteLine("ApplyButtonMenuColors completed successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"应用颜色配置失败: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// 应用赛文菜单的颜色配置
        /// </summary>
        /// <param name="menuBg">菜单背景色</param>
        /// <param name="menuFg">菜单前景色</param>
        private void ApplyRaceMenuColors(System.Windows.Media.Brush menuBg, System.Windows.Media.Brush menuFg)
        {
            try
            {
                // 遍历赛文菜单的所有子项
                foreach (var item in MenuItemRace.Items)
                {
                    if (item is MenuItem parentMenu)
                    {
                        // 应用一级菜单颜色
                        parentMenu.Background = menuBg;
                        parentMenu.Foreground = menuFg;

                        // 应用二级子菜单颜色
                        foreach (var subItem in parentMenu.Items)
                        {
                            if (subItem is MenuItem subMenu)
                            {
                                subMenu.Background = menuBg;
                                subMenu.Foreground = menuFg;
                            }
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine("ApplyRaceMenuColors completed successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"应用赛文菜单颜色失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 应用文来菜单的颜色配置
        /// </summary>
        /// <param name="menuBg">菜单背景色</param>
        /// <param name="menuFg">菜单前景色</param>
        private void ApplyWenlaiMenuColors(System.Windows.Media.Brush menuBg, System.Windows.Media.Brush menuFg)
        {
            try
            {
                // 遍历文来菜单的所有子项
                foreach (var item in MenuWenlai.Items)
                {
                    if (item is MenuItem menuItem)
                    {
                        menuItem.Background = menuBg;
                        menuItem.Foreground = menuFg;
                    }
                }

                System.Diagnostics.Debug.WriteLine("ApplyWenlaiMenuColors completed successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"应用文来菜单颜色失败: {ex.Message}");
            }
        }


        /// <summary>
        /// 窗口级别的鼠标滚轮事件处理
        /// </summary>
        private void Window_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            // 只处理Ctrl+滚轮
            if (Keyboard.Modifiers != ModifierKeys.Control)
            {
                return; // 不设置e.Handled，让事件继续传递
            }

            // 强制处理Ctrl+滚轮调整字体大小（无论在哪个区域）
            e.Handled = true;
            double delta = e.Delta > 0 ? 2 : -2;
            double currentSize = DisplayFontSize;
            double newSize = Math.Max(10, Math.Min(100, currentSize + delta));

            // 保存新的字体大小到配置
            Config.Set("发文区字体大小", newSize, 1);

            // 获取鼠标相对于窗口的位置，判断要调整哪个区域的字体
            Point mousePos = e.GetPosition(this);

            // 检查是否在输入区或成绩区
            bool inInputArea = false;
            bool inResultsArea = false;

            try
            {
                Point inputPos = TbxInput.TransformToAncestor(this).Transform(new Point(0, 0));
                Rect inputRect = new Rect(inputPos.X, inputPos.Y, TbxInput.ActualWidth, TbxInput.ActualHeight);
                inInputArea = inputRect.Contains(mousePos);
            }
            catch
            {
            }

            try
            {
                if (TbxResults.IsVisible)
                {
                    Point resultsPos = TbxResults.TransformToAncestor(this).Transform(new Point(0, 0));
                    Rect resultsRect = new Rect(resultsPos.X, resultsPos.Y, TbxResults.ActualWidth,
                        TbxResults.ActualHeight);
                    inResultsArea = resultsRect.Contains(mousePos);
                }
            }
            catch
            {
            }

            if (inInputArea)
            {
                // 调整输入区字体
                TbxInput.FontSize = newSize;
                Config.Set("跟打区字体大小", newSize, 1);
                System.Diagnostics.Debug.WriteLine($"输入区字体大小调整: {currentSize} -> {newSize}");
                return;
            }

            if (inResultsArea)
            {
                // 调整成绩区字体
                TbxResults.FontSize = newSize;
                Config.Set("成绩区字体大小", newSize, 1);
                System.Diagnostics.Debug.WriteLine($"成绩区字体大小调整: {currentSize} -> {newSize}");
                return;
            }

            // 默认调整发文区字体（强制清空缓存重新渲染）
            TextInfo.Blocks.Clear();
            TextInfo.PageNum = -1;
            TbDispay.Children.Clear();
            UpdateDisplay(UpdateLevel.Progress);

            // 刷新字帖模式错字框的大小和位置
            if (_copybookMode != null && _copybookMode.IsActive)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    _copybookMode.RefreshWrongCharHints();
                    _copybookMode.ScheduleUpdatePosition();
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }

            // 刷新临摹模式镜像行
            if (_tracingMode != null && _tracingMode.IsActive)
                _tracingMode.ScheduleInsertMirrorBlocks();

            System.Diagnostics.Debug.WriteLine($"发文区字体大小调整: {currentSize} -> {newSize}");
        }

        /// <summary>
        /// Ctrl+鼠标滚轮调整字体大小
        /// </summary>
        private void Control_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            // 检查是否按下Ctrl键，如果没有按下则不处理，让默认行为继续
            if (Keyboard.Modifiers != ModifierKeys.Control)
            {
                e.Handled = false;
                return;
            }

            // 阻止默认的滚动行为
            e.Handled = true;

            // 根据滚轮方向调整字体大小
            double delta = e.Delta > 0 ? 2 : -2; // 向上滚增大，向下滚减小

            // 新布局中发文区和跟打区已分离，直接判断sender
            System.Windows.Controls.Control targetControl = null;

            // 检查是否在发文区（BdDisplay）
            if (sender == BdDisplay || sender == ScDisplay || sender == TbDispay)
            {
                // 在发文区 - 更新字体大小配置并刷新显示
                double currentSize = DisplayFontSize;
                double newSize = Math.Max(10, Math.Min(100, currentSize + delta));
                Config.Set("发文区字体大小", newSize, 1);
                UpdateDisplay(UpdateLevel.PageArrange);
                System.Diagnostics.Debug.WriteLine($"发文区字体大小调整: {currentSize} -> {newSize}");
                return;
            }
            // 检查是否在跟打区（TbxInput）
            else if (sender == TbxInput)
            {
                targetControl = TbxInput;
                System.Diagnostics.Debug.WriteLine("TbxInput Ctrl+滚轮触发");
            }
            else if (sender == TbxResults)
            {
                targetControl = TbxResults;
                System.Diagnostics.Debug.WriteLine("TbxResults Ctrl+滚轮触发");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"未知sender: {sender?.GetType().Name}");
                return; // 未知的sender
            }

            if (targetControl != null)
            {
                // 从配置读取当前字体大小，而不是从控件读取（避免控件值与配置不一致）
                double currentSize;
                if (targetControl == TbxInput)
                    currentSize = Config.GetDouble("跟打区字体大小") > 0 ? Config.GetDouble("跟打区字体大小") : 40.0;
                else if (targetControl == TbxResults)
                    currentSize = Config.GetDouble("成绩区字体大小") > 0 ? Config.GetDouble("成绩区字体大小") : 15.0;
                else
                    currentSize = targetControl.FontSize;

                double newSize = Math.Max(10, Math.Min(100, currentSize + delta));

                // 更新字体大小
                targetControl.FontSize = newSize;

                // 保存到配置
                if (targetControl == TbxInput)
                    Config.Set("跟打区字体大小", newSize, 1);
                else if (targetControl == TbxResults)
                    Config.Set("成绩区字体大小", newSize, 1);

                System.Diagnostics.Debug.WriteLine($"字体大小调整: {currentSize} -> {newSize}");
            }
        }








        private void win_size_change(object sender, SizeChangedEventArgs e)
        {


            if (StateManager.ConfigLoaded)
            {
                Config.Set("窗口宽度", this.Width, 0);
                Config.Set("窗口高度", this.Height, 0);
                // UpdateDisplay(UpdateLevel.PageArrange);
                DelayUpdateDisplay(500, UpdateLevel.PageArrange);
            }
        }


        private void NextArticle()
        {
            try
            {
                WriteDebugLog($"[NextArticle] 开始调用 NextSection");
                ArticleManager.NextSection();
                WriteDebugLog($"[NextArticle] NextSection 完成");
            }
            catch (Exception ex)
            {
                WriteDebugLog($"[NextArticle] 异常: {ex.Message}");
                throw;
            }
        }


        async void DelayStop(object obj)
        {
            await StopHelper();
        }


        private string TxtResult = "";
        private string trainerStatText = ""; // 练单器统计文本

        /// <summary>
        /// 更新练单器统计显示（由WinTrainer调用）
        /// </summary>
        public void UpdateTrainerStat(string statText)
        {
            trainerStatText = statText;
            UpdateTypingStat();
        }

        public void UpdateTypingStat(string newReport = "")
        {

            CounterLog.Add("字数", CounterLog.Buffer[0]);
            CounterLog.Buffer[0] = 0;
            CounterLog.Add("击键数", CounterLog.Buffer[1]);
            CounterLog.Buffer[1] = 0;
            CounterLog.Write();

            StringBuilder sb = new StringBuilder();

            // 第一行：今日字数统计 + 练单器统计（如果有）
            sb.Append("今日字数：");
            sb.Append(CounterLog.GetCurrent("字数") + CounterLog.Buffer[0]);
            sb.Append("   ");
            sb.Append("总字数：");
            sb.Append(CounterLog.GetSum("字数") + CounterLog.Buffer[0]);

            // 追加练单器统计到第一行末尾
            if (!string.IsNullOrEmpty(trainerStatText))
            {
                sb.Append("   [练单] ");
                sb.Append(trainerStatText);
            }



            // 第二行：当日详细统计（读取文章日志获取）
            var todayRecords = ArticleLog.ReadRecords(DateTime.Today);
            System.Diagnostics.Debug.WriteLine($"[成绩区] 今日记录数: {todayRecords.Count}");
            if (todayRecords.Count > 0)
            {
                sb.AppendLine();

                // 计算当日平均值
                double avgSpeed = todayRecords.Average(r => r.Speed);
                double avgHitRate = todayRecords.Average(r => r.HitRate);
                double avgAccuracy = todayRecords.Average(r => r.Accuracy);
                int totalWords = todayRecords.Sum(r => r.TotalWords);
                int totalHits = todayRecords.Sum(r => r.TotalHit);
                double totalTime = todayRecords.Sum(r => r.TotalSeconds);

                // 根据配置顺序显示
                var dayStats = new List<string>();

                foreach (string item in Core.Score.GetScoreOrder())
                {
                    switch (item)
                    {
                        case "字数":
                            if (Config.GetBool("显示_字数"))
                                dayStats.Add($"今日{totalWords}字");
                            break;
                        case "速度":
                            if (Config.GetBool("显示_速度"))
                                dayStats.Add($"均速{avgSpeed:F1}");
                            break;
                        case "击键":
                            if (Config.GetBool("显示_击键"))
                                dayStats.Add($"均击{avgHitRate:F1}");
                            break;
                        case "键准":
                            if (Config.GetBool("显示_键准"))
                                dayStats.Add($"均键准{avgAccuracy:P1}");
                            break;
                        case "总键数":
                            if (Config.GetBool("显示_总键数"))
                                dayStats.Add($"总键{totalHits}");
                            break;
                        case "用时":
                            if (Config.GetBool("显示_用时"))
                                dayStats.Add($"用时{Score.FormatTime(totalTime)}");
                            break;
                    }
                }
                dayStats.Add($"打文{todayRecords.Count}篇");

                sb.Append(string.Join("  ", dayStats));
            }

            sb.AppendLine();

            if (newReport != "")
            {
                TxtResult = newReport + "\n" + TxtResult;
                // 保存当日成绩记录
                CounterLog.AddDailyResult(newReport);
            }

            sb.Append(TxtResult);



            TbxResults.Text = sb.ToString();
        }


        public bool IsLookingType
        {
            get { return Config.GetBool("看打模式") && StateManager.retypeType != RetypeType.wrongRetype; }
        }


        public bool IsBlindType
        {
            get { return Config.GetBool("盲打模式") && StateManager.retypeType != RetypeType.wrongRetype; }
        }

        async Task StopHelper()
        {
            // 在非UI线程中，提前获取QQGroupName等UI相关属性
            string qqGroupName = "";
            await Dispatcher.InvokeAsync(() =>
            {
                qqGroupName = QQGroupName;
            });

            // 保存文章日志数据（在 Score 被重置之前）
            TxtSource savedTxtSource = StateManager.txtSource; // 保存文本来源
            RetypeType savedRetypeType = StateManager.retypeType; // 保存重打类型

            // 调试：输出当前状态
            WriteDebugLog($"[StopHelper] 开始 - txtSource={savedTxtSource}, retypeType={savedRetypeType}, TotalWords={Score.TotalWordCount}");
            System.Diagnostics.Debug.WriteLine(
                $"[StopHelper] 开始 - txtSource={savedTxtSource}, retypeType={savedRetypeType}, TotalWords={Score.TotalWordCount}");

            try
            {
                int savedTotalWords = Score.TotalWordCount;
                int savedInputWords = Score.InputWordCount;
                double savedSpeed = Score.Speed;
                double savedHitRate = Score.HitRate;
                double savedAccuracy = Score.GetAccuracy() * 100; // 键准（转换为百分制）
                int savedWrong = Score.Wrong;
                int savedBacks = (int)Score.GetBacks();
                double savedCorrection = Score.GetCorrection();
                double savedKPW = Score.KPW;
                double savedLRRatio = Score.LRRatio;
                int savedTotalHit = (int)Score.GetHit();
                double savedTotalSeconds = Score.Time.TotalSeconds;
                int savedWasteCodes = Score.WasteCodes;
                double savedCiRatio = Score.GetCiRatio() * 100; // 打词率（转换为百分制）
                int savedChoose = Score.GetChoose();
                int savedBiaoDing = Score.GetBiaoDing();
                string savedArticleName = "";
                string savedArticleMark = "";
                string savedDifficultyName = "";

                // 根据来源保存文章名和 mark
                if (savedTxtSource == TxtSource.book)
                {
                    savedArticleName = ArticleManager.Title ?? "未知文章";
                    savedArticleMark = ""; // 本地文章没有 mark
                    savedDifficultyName = ""; // 本地文章没有难度名称
                }
                else if (savedTxtSource == TxtSource.articlesender)
                {
                    savedArticleName = articleCache.GetCurrentTitle() ?? "文来文章";
                    savedArticleMark = articleCache.GetCurrentMark() ?? "";
                    savedDifficultyName = articleCache.GetCurrentDifficultyName();
                }
                else
                {
                    savedArticleName = "其他来源";
                    savedArticleMark = "";
                    savedDifficultyName = "";
                }

                await Dispatcher.InvokeAsync(() => { TbxInput.IsReadOnly = true; });
                StateManager.typingState = TypingState.end;
                sw.Stop();
                // 停止字提定时器
                StopZiTiTimer();

                Score.HitRate = Score.GetHit() / Score.Time.TotalSeconds;
                Score.KPW = Score.GetHit() / (double)Score.TotalWordCount;
                Score.Speed = (double)Score.TotalWordCount / Score.Time.TotalMinutes;

                string tbxInputText = "";
                await Dispatcher.InvokeAsync(() => { tbxInputText = TbxInput.Text; });
                Score.InputWordCount = new System.Globalization.StringInfo(tbxInputText).LengthInTextElements;
                savedInputWords = Score.InputWordCount; // 更新保存的输入字数

                //计算错字

                if (IsLookingType)
                {
                    string currentMatchText = string.Concat(TextInfo.Words);
                    Score.Less = 0;
                    Score.More = 0;

                    string t1 = currentMatchText.Replace('”', '\"').Replace('“', '\"').Replace('‘', '\'')
                        .Replace('’', '\'');

                    string t2 = "";
                    await Dispatcher.InvokeAsync(() =>
                    {
                        t2 = TbxInput.Text.Replace('”', '\"').Replace('“', '\"').Replace('‘', '\'')
                        .Replace('’', '\'');
                    });
                    List<DiffRes> diffs = DiffTool.Diff(t1, t2);


                    int counter = 0;
                    foreach (var df in diffs)
                    {


                        switch (df.Type)
                        {
                            case DiffType.None:


                                break;
                            case DiffType.Delete:
                                Score.Less++;
                                string w = currentMatchText.Substring(df.OrigIndex - 1, 1);


                                LogWrong(df.OrigIndex - 1, w);


                                counter--;

                                break;
                            case DiffType.Add:


                                counter++;
                                Score.More++;
                                break;

                        }


                    }



                }
                else
                {
                    Score.Wrong = 0;


                    for (int i = 0; i < TextInfo.wordStates.Count; i++)
                    {
                        if (TextInfo.wordStates[i] == WordStates.WRONG)
                        {
                            Score.Wrong++;
                            string w = TextInfo.Words[i];
                            LogWrong(i, w);
                        }
                    }
                }

                await Dispatcher.InvokeAsync(() => { TbkStatusTop.Text = Score.Progress(); });

                string typingStatReport = Score.Report(); // 提前计算，避免异步竞争
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (savedRetypeType != RetypeType.wrongRetype && savedRetypeType != RetypeType.slowRetype)
                        UpdateTypingStat(typingStatReport);
                    else
                        UpdateTypingStat();
                }));

                string result = Score.Report(); // + " " + Config.GetString("成绩签名");






                //自动发送成绩&&加载下一段
                bool articleSenderResultSent = false;




                if (StateManager.txtSource == TxtSource.jbs) //锦标赛
                {
                    string inputMethod = Config.GetString("赛文输入法");
                    System.Diagnostics.Debug.WriteLine(
                        $"[锦标赛] 读取到的输入法: [{inputMethod}], 是否为空: {string.IsNullOrWhiteSpace(inputMethod)}");
                    if (string.IsNullOrWhiteSpace(inputMethod))
                    {
                        MessageBox.Show("请先填写赛文输入法名称\n（设置 → 赛文 → 赛文输入法）", "提示", MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return;
                    }

                    string sendResult = jbsHelper.SubmitScore(
                        Score.GetValidSpeed(),
                        Score.HitRate,
                        Score.KPW,
                        Score.Time,
                        (int)Score.GetCorrection(),
                        0,
                        (int)Score.GetHit(),
                        Score.GetAccuracy(),
                        Score.GetCiRatio(),
                        Score.Wrong,
                        inputMethod);

                    // 显示提交结果
                    if (!string.IsNullOrWhiteSpace(sendResult))
                    {
                        MessageBox.Show(sendResult, "锦标赛成绩提交", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }

                if (StateManager.txtSource == TxtSource.jisucup) //极速杯
                {
                    string inputMethod = Config.GetString("赛文输入法");
                    System.Diagnostics.Debug.WriteLine(
                        $"[极速杯] 读取到的输入法: [{inputMethod}], 是否为空: {string.IsNullOrWhiteSpace(inputMethod)}");
                    if (string.IsNullOrWhiteSpace(inputMethod))
                    {
                        MessageBox.Show("请先填写赛文输入法名称\n（设置 → 赛文 → 赛文输入法）", "提示", MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return;
                    }

                    string sendResult = jiSuCupHelper.SubmitScore(
                        Score.GetValidSpeed(),
                        Score.HitRate,
                        Score.KPW,
                        Score.Time,
                        (int)Score.GetCorrection(),
                        0,
                        (int)Score.GetHit(),
                        Score.GetAccuracy(),
                        Score.GetCiRatio(),
                        Score.Wrong,
                        inputMethod);

                    // 显示提交结果
                    if (!string.IsNullOrWhiteSpace(sendResult))
                    {
                        MessageBox.Show(sendResult, "极速杯成绩提交", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }

                if (StateManager.txtSource == TxtSource.raceApi) //赛文API
                {
                    // 从配置读取输入法名称
                    string inputMethod = Config.GetString("赛文输入法");
                    System.Diagnostics.Debug.WriteLine(
                        $"[赛文API] 读取到的输入法: [{inputMethod}], 是否为空: {string.IsNullOrWhiteSpace(inputMethod)}");
                    if (string.IsNullOrWhiteSpace(inputMethod))
                    {
                        MessageBox.Show("请先填写赛文输入法名称\n（设置 → 赛文 → 赛文输入法）", "提示", MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return;
                    }

                    // 检查是否有保存的服务器和赛文ID
                    if (string.IsNullOrEmpty(StateManager.CurrentRaceServerId) || StateManager.CurrentRaceId <= 0)
                    {
                        MessageBox.Show("无法提交成绩：赛文信息丢失", "赛文API成绩提交", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // ========== DEBUG: 打印Score所有字段 ==========
                    System.Diagnostics.Debug.WriteLine("=== Score所有字段 ===");
                    System.Diagnostics.Debug.WriteLine($"Score.Hit = {Score.Hit}");
                    System.Diagnostics.Debug.WriteLine($"Score.HitRate = {Score.HitRate}");
                    System.Diagnostics.Debug.WriteLine($"Score.TotalWordCount = {Score.TotalWordCount}");
                    System.Diagnostics.Debug.WriteLine($"Score.InputWordCount = {Score.InputWordCount}");
                    System.Diagnostics.Debug.WriteLine($"Score.Speed = {Score.Speed}");
                    System.Diagnostics.Debug.WriteLine($"Score.Backs = {Score.Backs}");
                    System.Diagnostics.Debug.WriteLine($"Score.KPW = {Score.KPW}");
                    System.Diagnostics.Debug.WriteLine($"Score.Wrong = {Score.Wrong}");
                    System.Diagnostics.Debug.WriteLine($"Score.Time = {Score.Time}");
                    System.Diagnostics.Debug.WriteLine($"Score.Correction = {Score.Correction}");
                    System.Diagnostics.Debug.WriteLine($"Score.BimeHit = {Score.BimeHit}");
                    System.Diagnostics.Debug.WriteLine($"Score.BimeBacks = {Score.BimeBacks}");
                    System.Diagnostics.Debug.WriteLine($"Score.BimeCorrection = {Score.BimeCorrection}");
                    System.Diagnostics.Debug.WriteLine($"Score.LeftCount = {Score.LeftCount}");
                    System.Diagnostics.Debug.WriteLine($"Score.RightCount = {Score.RightCount}");
                    System.Diagnostics.Debug.WriteLine($"Score.SpaceCount = {Score.SpaceCount}");
                    System.Diagnostics.Debug.WriteLine($"Score.LRRatio = {Score.LRRatio}");
                    System.Diagnostics.Debug.WriteLine($"Score.WasteCodes = {Score.WasteCodes}");
                    System.Diagnostics.Debug.WriteLine("=== Score方法返回值 ===");
                    System.Diagnostics.Debug.WriteLine($"Score.GetValidSpeed() = {Score.GetValidSpeed()}");
                    System.Diagnostics.Debug.WriteLine($"Score.GetHit() = {Score.GetHit()}");
                    System.Diagnostics.Debug.WriteLine($"Score.GetBacks() = {Score.GetBacks()}");
                    System.Diagnostics.Debug.WriteLine($"Score.GetCorrection() = {Score.GetCorrection()}");
                    System.Diagnostics.Debug.WriteLine($"Score.GetAccuracy() = {Score.GetAccuracy()}");
                    System.Diagnostics.Debug.WriteLine($"Score.GetCiRatio() = {Score.GetCiRatio()}");
                    System.Diagnostics.Debug.WriteLine("========================");

                    // 提交前先保存成绩数据，避免异步任务中Score被重置
                    // 按照正确的字段映射
                    double speed = Score.GetValidSpeed(); // 速度
                    double hitRate = Score.HitRate; // 击键
                    double codeLength = Score.KPW; // 码长
                    TimeSpan timeCost = Score.Time; // 时间
                    int correction = (int)Score.GetCorrection(); // 回改
                    int backspaceCount = (int)Score.GetBacks(); // 退格
                    int keyCount = (int)Score.GetHit(); // 键数
                    double keyAccuracy = Score.GetAccuracy() * 100; // 键准（转换为百分制0-100）
                    double wordRate = Score.GetCiRatio() * 100; // 打词率（转换为百分制0-100）
                    int charCount = Score.TotalWordCount; // 字数

                    // 异步提交成绩
                    Task.Run(async () =>
                    {
                        string sendResult = await raceHelperV2.SubmitScore(
                            StateManager.CurrentRaceServerId,
                            StateManager.CurrentRaceId,
                            speed,
                            timeCost,
                            charCount,
                            hitRate, // 击键
                            codeLength, // 码长
                            backspaceCount, // 退格
                            keyCount, // 键数
                            keyAccuracy, // 键准（百分制）
                            wordRate, // 打词率（百分制）
                            inputMethod
                        );

                        // 在UI线程显示结果
                        Dispatcher.Invoke(() =>
                        {
                            if (sendResult == "提交成功")
                            {
                                // 显示提交成功信息，按照用户指定的顺序和字段
                                string successMsg =
                                    $"提交成功！\n\n速度: {speed:F2}\n击键: {hitRate:F2}\n码长: {codeLength:F2}\n键准: {keyAccuracy:F2}%\n打词率: {wordRate:F2}%";
                                MessageBox.Show(successMsg, "赛文API成绩提交", MessageBoxButton.OK,
                                    MessageBoxImage.Information);
                            }
                            else
                            {
                                MessageBox.Show(sendResult, "赛文API成绩提交", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        });
                    });
                }


                if (StateManager.txtSource == TxtSource.book) //书籍
                {
                    WriteDebugLog($"[本地文章] 进入分支，错字重打配置={Config.GetBool("错字重打")}, retypeType={StateManager.retypeType}, 错字数={TextInfo.WrongRec.Count}");

                    if (!Config.GetBool("错字重打")) //没有错字，或没有错字重打
                    {
                        WriteDebugLog($"[本地文章] 执行: NextAndSendArticle(result)");
                        await NextAndSendArticle(result);
                        WriteDebugLog($"[本地文章] 完成: NextAndSendArticle(result)");
                    }
                    else //(Config.GetBool("错字重打"))
                    {
                        if (StateManager.retypeType == RetypeType.wrongRetype) //错字重打
                        {
                            if (TextInfo.WrongRec.Count == 0) //错字重打后无错字
                            {
                                WriteDebugLog($"[本地文章] 执行: NextAndSendArticle() (错字重打完成)");
                                await NextAndSendArticle();
                                WriteDebugLog($"[本地文章] 完成: NextAndSendArticle()");
                            }
                            else
                            {
                                WriteDebugLog($"[本地文章] 跳过: 错字重打中仍有错字");
                            }
                        }
                        else // (StateManager.retypeType != RetypeType.wrongRetype)//非错字重打，正文或普通重打
                        {
                            if (TextInfo.WrongRec.Count == 0) //一次打对无错字
                            {
                                WriteDebugLog($"[本地文章] 执行: NextAndSendArticle(result) (无错字)");
                                await NextAndSendArticle(result);
                                WriteDebugLog($"[本地文章] 完成: NextAndSendArticle(result)");
                            }
                            else //有错字，发送成绩（后续通用逻辑会处理错字重打）
                            {
                                WriteDebugLog($"[本地文章] 有错字，发送成绩");
                                if (Config.GetBool("自动发送成绩"))
                                {
                                    QQHelper.SendQQMessage(qqGroupName, result, 250, this);
                                }
                                // 不进入错字重打，让后续的通用错字重打逻辑（line 2940-2977）处理
                                WriteDebugLog($"[本地文章] 让通用逻辑处理错字重打");
                            }
                        }
                    }
                }
                else if (StateManager.txtSource == TxtSource.articlesender) //文来
                {
                    // 独立的 if：处理错字/慢字重打前的成绩发送（首打时）
                    if ((Config.GetBool("错字重打") || Config.GetBool("慢字重打")) &&
                        StateManager.retypeType != RetypeType.slowRetype &&
                        StateManager.retypeType != RetypeType.wrongRetype &&
                        (Config.GetBool("错字重打") && TextInfo.WrongRec.Count > 0))
                    {
                        if (Config.GetBool("自动发送成绩"))
                        {
                            // 使用保存的 qqGroupName 避免跨线程访问UI
                            if (qqGroupName != "")
                            {
                                QQHelper.SendQQMessage(qqGroupName, result, 250, this);
                            }
                            else
                            {
                                Win32SetText(result);
                            }
                            articleSenderResultSent = true;
                        }
                        // 发送成绩后继续执行，让后续的错字重打逻辑处理
                    }

                    // 处理自动换段（需要关闭错字重打和慢字重打）
                    if (!Config.GetBool("错字重打") && !Config.GetBool("慢字重打"))
                    {
                        // 检查是否是手动换段模式
                        bool manualMode = Config.GetString("文来换段模式") == "手动";

                        if (manualMode)
                        {
                            // 手动模式：只发送成绩，不自动发下一段
                            if (Config.GetBool("自动发送成绩"))
                            {
                                // 使用保存的 qqGroupName 避免跨线程访问UI
                                if (qqGroupName != "")
                                {
                                    QQHelper.SendQQMessage(qqGroupName, result, 0, this);
                                }
                                else
                                {
                                    Win32SetText(result);
                                }
                            }
                        }
                        else
                        {
                            // 自动模式：直接发送成绩+下一段
                            await NextAndSendArticleSender(result);
                            // 注意：这里 return 会导致后面的成绩记录代码不执行
                            // 所以需要在这里先记录成绩
                            goto RecordScore; // 跳转到成绩记录逻辑
                        }
                    }
                    // 其他情况（开启了错字重打或慢字重打），继续执行后续慢字检测逻辑
                }
                else if (StateManager.txtSource == TxtSource.trainer) //练单器
                {

                    WriteDebugLog($"[练单器] 进入分支，accuracy={Score.GetAccuracy():F4}, hitRate={Score.HitRate:F2}, wrong={Score.Wrong}");

                    WriteDebugLog($"[练单器] winTrainer 是否为null: {winTrainer == null}");
                    if (winTrainer != null)
                    {
                        WriteDebugLog($"[练单器] 准备调用 GetNextRound");
                        winTrainer.GetNextRound(Score.GetAccuracy(), Score.HitRate, Score.Wrong, result);
                        WriteDebugLog($"[练单器] GetNextRound 返回");
                    }

                    WriteDebugLog($"[练单器] 完成 GetNextRound 调用");





                }
                else // 其他模式（群载文等）
                {
                    // 群载文模式：只有开启"自动发送成绩"且不是重打模式时才发送成绩
                    if (StateManager.retypeType != RetypeType.wrongRetype && StateManager.retypeType != RetypeType.slowRetype && Config.GetBool("自动发送成绩"))
                    {
                        QQHelper.SendQQMessage(qqGroupName, result, 0, this);
                    }
                }










                // 慢字检测逻辑（排除打单器和慢字重打本身）
                if (Config.GetBool("慢字重打") && StateManager.txtSource != TxtSource.trainer &&
                    StateManager.retypeType != RetypeType.slowRetype)
                {
                    TextInfo.SlowRec.Clear();
                    double slowThreshold = Config.GetDouble("慢字标准(单位:秒)") * 1000; // 转换为毫秒

                    // === 新的检测逻辑 ===
                    int textPos = 0; // 当前在 TextInfo.Words 中的位置

                    for (int i = 0; i < Score.CommitTime.Count; i++)
                    {
                        if (i >= Score.CommitCharCount.Count || i >= Score.CommitText.Count)
                            break;

                        long timeDiff = i > 0 ? (Score.CommitTime[i] - Score.CommitTime[i - 1]) : Score.CommitTime[i];
                        int charCount = Score.CommitCharCount[i];
                        string groupText = Score.CommitText[i];

                        // 计算有效字符数（排除符号）
                        int validCharCount = 0;
                        System.Globalization.StringInfo groupSi = new System.Globalization.StringInfo(groupText);
                        for (int j = 0; j < groupSi.LengthInTextElements; j++)
                        {
                            string ch = groupSi.SubstringByTextElements(j, 1);
                            if (!Score.ExcludePuncts.Contains(ch))
                            {
                                validCharCount++;
                            }
                        }

                        // 用有效字符数计算平均速度
                        double avgTimePerChar = validCharCount > 0 ? (double)timeDiff / validCharCount : 0;

                        if (avgTimePerChar > slowThreshold && validCharCount > 0)
                        {
                            // 把这一组的所有字（包括符号）加入慢字队列
                            for (int j = 0; j < groupSi.LengthInTextElements; j++)
                            {
                                if (textPos < TextInfo.Words.Count)
                                {
                                    string word = TextInfo.Words[textPos];
                                    if (!TextInfo.WrongExclude.Contains(word))
                                    {
                                        TextInfo.SlowRec[textPos] = word;
                                    }

                                    textPos++;
                                }
                            }
                        }
                        else
                        {
                            // 不慢，跳过这一组
                            textPos += charCount;
                        }
                    }
                }

                // 晴发文模式：慢字检测完成后，如果有慢字且首打成绩未发过，补发成绩
                if (savedTxtSource == TxtSource.articlesender && !articleSenderResultSent &&
                    StateManager.retypeType != RetypeType.slowRetype &&
                    StateManager.retypeType != RetypeType.wrongRetype &&
                    Config.GetBool("慢字重打") && TextInfo.SlowRec.Count > 0)
                {
                    if (Config.GetBool("自动发送成绩"))
                    {
                        if (qqGroupName != "")
                        {
                            QQHelper.SendQQMessage(qqGroupName, result, 250, this);
                        }
                        else
                        {
                            Win32SetText(result);
                        }
                        articleSenderResultSent = true;
                    }
                }

                // 合并错字和慢字重打逻辑
                if (StateManager.txtSource != TxtSource.trainer)
                {
                    bool hasWrong = Config.GetBool("错字重打") && TextInfo.WrongRec.Count > 0;
                    bool hasSlow = Config.GetBool("慢字重打") && TextInfo.SlowRec.Count > 0;

                    if (hasWrong || hasSlow)
                    {
                        StringBuilder sb = new StringBuilder();

                        // 添加错字（如果有）
                        if (hasWrong)
                        {
                            for (int i = 0; i < Config.GetInt("错字重复次数"); i++)
                            {
                                foreach (var s in TextInfo.WrongRec.Values)
                                    sb.Append(s);
                            }
                        }

                        // 添加慢字（如果有）
                        if (hasSlow)
                        {
                            for (int i = 0; i < Config.GetInt("慢字重复次数"); i++)
                            {
                                foreach (var s in TextInfo.SlowRec.Values)
                                    sb.Append(s);
                            }
                        }

                        // 确定重打类型（优先错字重打）
                        RetypeType retypeType = hasWrong ? RetypeType.wrongRetype : RetypeType.slowRetype;
                        try
                        {
                            LoadText(sb.ToString(), retypeType, TxtSource.unchange, true, true);
                        }
                        catch (Exception ex)
                        {
                            throw ex;
                        }
                    }
                    else if (savedTxtSource == TxtSource.articlesender)
                    {
                        // 文来模式：无错字且无慢字，进入下一段
                        // 检查是否是手动换段模式
                        bool manualMode = Config.GetString("文来换段模式") == "手动";

                        if (manualMode)
                        {
                            // 手动模式：只发送成绩，不自动发下一段
                            if (StateManager.retypeType != RetypeType.wrongRetype &&
                                StateManager.retypeType != RetypeType.slowRetype && Config.GetBool("自动发送成绩"))
                            {
                                if (qqGroupName != "")
                                {
                                    QQHelper.SendQQMessage(qqGroupName, result, 0, this);
                                }
                                else
                                {
                                    Win32SetText(result);
                                }
                            }
                        }
                        else
                        {
                            // 自动模式：继续发下一段
                            // 如果是重打模式，不发送成绩；如果是正文，发送成绩
                            if (StateManager.retypeType == RetypeType.wrongRetype ||
                                StateManager.retypeType == RetypeType.slowRetype)
                            {
                                await NextAndSendArticleSender(); // 重打完成，不发送成绩
                            }
                            else
                            {
                                await NextAndSendArticleSender(result); // 正文完成，发送成绩
                            }
                        }
                    }
                }
                else
                {
                }


                RecordScore:
                // 记录文章日志（不包括打单器）
                // 对于错字重打和慢字重打，只在开启了对应功能时才不记录
                if (savedTxtSource != TxtSource.trainer)
                {
                    // 只在开启功能且是重打模式时才不记录
                    bool shouldRecord = true;
                    if (savedRetypeType == RetypeType.wrongRetype && Config.GetBool("错字重打"))
                        shouldRecord = false;
                    if (savedRetypeType == RetypeType.slowRetype && Config.GetBool("慢字重打"))
                        shouldRecord = false;

                    if (shouldRecord)
                    {
                        try
                        {
                            // 直接使用保存的值（已经在 StopHelper 开始时根据来源设置好了）
                            string articleName = savedArticleName;
                            string articleMark = savedArticleMark;

                            var record = new ArticleLog.ArticleRecord
                            {
                                Time = DateTime.Now,
                                ArticleName = articleName,
                                TotalWords = savedTotalWords,
                                InputWords = savedInputWords,
                                Speed = savedSpeed,
                                HitRate = savedHitRate,
                                Accuracy = savedAccuracy,
                                Wrong = savedWrong,
                                Backs = savedBacks,
                                Correction = savedCorrection,
                                KPW = savedKPW,
                                LRRatio = savedLRRatio,
                                TotalHit = savedTotalHit,
                                TotalSeconds = savedTotalSeconds,
                                ArticleMark = savedArticleMark,
                                WasteCodes = savedWasteCodes,
                                CiRatio = savedCiRatio,
                                Choose = savedChoose,
                                BiaoDing = savedBiaoDing,
                                DifficultyName = savedDifficultyName
                            };

                            // 根据来源记录到不同的日志（使用保存的 txtSource）
                            if (savedTxtSource == TxtSource.articlesender)
                            {
                                // 文来文章记录到文来日志
                                WenlaiLog.WriteRecord(record);
                            }
                            else
                            {
                                // 其他文章记录到文章日志
                                ArticleLog.WriteRecord(record);
                            }
                        }
                        catch (Exception ex)
                        {
                            // 记录文章日志失败
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // 异常已被上层处理
            }




        }



        Timer tm1;
        int lastInputLength = 0; // 记录上次输入框长度，用于判断是否真正上屏

        void ProcInput()
        {


            if (TextInfo.Words.Count == 0)
                return;

            TextInfo.Check(TbxInput.Text);

            CalScore();



            if (IsInputEnd())
                StopTyping();


            UpdateDisplay(UpdateLevel.Progress);

            // 更新字提显示
            UpdateZiTi();
        }

        /// <summary>
        /// 停止跟打，记录成绩
        /// </summary>
        internal void StopTyping()
        {
            Trace.WriteLine("stop");

            StateManager.LastType = true;
            Score.TotalWordCount = TextInfo.Words.Count;
            Score.Time = sw.Elapsed;
            timerProgress.Dispose();
            tm1 = new Timer(DelayStop, null, 150, Timeout.Infinite);
        }

        private bool IsInputEnd()
        {
            if (!IsLookingType || TextInfo.Words.Count <= 3)
            {
                System.Globalization.StringInfo sb = new System.Globalization.StringInfo(TbxInput.Text);

                int lenA = TextInfo.Words.Count;
                int lenB = sb.LengthInTextElements;

                return lenA <= lenB && lenA >= 1 &&
                       TextInfo.Words.Last() == sb.SubstringByTextElements(lenA - 1, 1);
            }
            else
            {
                System.Globalization.StringInfo sb = new System.Globalization.StringInfo(TbxInput.Text);

                int lenA = TextInfo.Words.Count;
                int lenB = sb.LengthInTextElements;

                int LengthError = lenA / 10 + 1;

                string la = "";

                for (int i = lenA - 3; i <= lenA - 1; i++)
                    la += TextInfo.Words[i];

                bool LastMatch = lenB > 3 && sb.SubstringByTextElements(lenB - 3, 3).Replace("\u201c", "\u201d") ==
                    la.Replace("\u201c", "\u201d");

                bool LengthMatch = Math.Abs(lenB - lenA) <= LengthError;

                return LastMatch && LengthMatch;
            }
        }

        internal void CalScore()
        {
            Score.TotalWordCount = TextInfo.Words.Count;
            Score.InputWordCount = new System.Globalization.StringInfo(TbxInput.Text).LengthInTextElements;

            Score.Wrong = 0;

            if (!IsLookingType)
            {
                for (int i = 0; i < TextInfo.wordStates.Count; i++)
                {
                    if (TextInfo.wordStates[i] == WordStates.WRONG)
                    {
                        Score.Wrong++;
                    }
                }
            }
        }

        CUIAutomation root = new CUIAutomation();

        const int KEY_DELAY = 25;

        public async Task NextAndSendArticle(string lastResult)
        {
            WriteDebugLog($"[NextAndSendArticle] 开始，lastResult长度={lastResult?.Length ?? 0}");

            // 在非UI线程中，提前获取QQGroupName等UI相关属性
            string qqGroupName = "";
            bool autoSendScore = false;
            Dispatcher.Invoke(() =>
            {
                qqGroupName = QQGroupName;
                autoSendScore = Config.GetBool("自动发送成绩");
            });

            try
            {
                // 使用 Task.Run + Dispatcher.Invoke 模式（参考文来模式 line 7526-7535）
                await Task.Run(() =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        WriteDebugLog($"[NextAndSendArticle] Task.Run内 调用 NextArticle");
                        NextArticle();
                        WriteDebugLog($"[NextAndSendArticle] Task.Run内 NextArticle完成");
                    });
                });

                WriteDebugLog($"[NextAndSendArticle] 获取下一段内容");
                string content2 = await ArticleManager.GetFormattedCurrentSection();
                WriteDebugLog($"[NextAndSendArticle] 下一段内容长度={content2?.Length ?? 0}");

                // 检查是否到达文章末尾
                if (string.IsNullOrWhiteSpace(content2))
                {
                    WriteDebugLog($"[NextAndSendArticle] 已到达文章末尾，只发送成绩");
                    // 只发送成绩，不加载下一段
                    if (qqGroupName != "")
                    {
                        WriteDebugLog($"[NextAndSendArticle] 发送成绩到QQ群");
                        QQHelper.SendQQMessage(qqGroupName, lastResult, 150, this);
                    }
                    else
                    {
                        WriteDebugLog($"[NextAndSendArticle] 复制成绩到剪切板");
                        Win32SetText(lastResult);
                        FocusInput();
                    }
                    return;
                }

                // 使用 Task.Run + Dispatcher.Invoke 模式，不等待（fire-and-forget）
                Task.Run(() =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        WriteDebugLog($"[NextAndSendArticle] Task.Run内 调用 LoadText");
                        LoadText(content2, RetypeType.first, TxtSource.book, false, true);
                        WriteDebugLog($"[NextAndSendArticle] Task.Run内 LoadText完成");
                    });
                });

                // 发送成绩和下一段内容
                if (qqGroupName != "")
                {
                    WriteDebugLog($"[NextAndSendArticle] 发送到QQ群");
                    // 本地文章模式：始终发送成绩和下一段
                    QQHelper.SendQQMessageD(qqGroupName, lastResult, content2, 150, this);
                }
                else
                {
                    WriteDebugLog($"[NextAndSendArticle] 复制到剪切板");
                    // 复制成绩和下一段内容到剪切板
                    string messageToSend = lastResult + "\n" + content2;
                    Win32SetText(messageToSend);
                    FocusInput();
                }
            }
            catch (Exception ex)
            {
                WriteDebugLog($"[NextAndSendArticle] 异常: {ex.Message}");
                // 发生异常时，至少发送成绩
                if (qqGroupName != "")
                {
                    QQHelper.SendQQMessage(qqGroupName, lastResult, 150, this);
                }
                else
                {
                    Win32SetText(lastResult);
                    FocusInput();
                }
                throw;  // 重新抛出异常，让上层处理
            }
            WriteDebugLog($"[NextAndSendArticle] 完成");
        }

        public async Task NextAndSendArticle()
        {
            WriteDebugLog($"[NextAndSendArticle无参] 开始");

            // 在非UI线程中，提前获取QQGroupName等UI相关属性
            string qqGroupName = "";
            Dispatcher.Invoke(() =>
            {
                qqGroupName = QQGroupName;
            });

            try
            {
                // 使用 Task.Run + Dispatcher.Invoke 模式（参考文来模式 line 7526-7535）
                await Task.Run(() =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        WriteDebugLog($"[NextAndSendArticle无参] Task.Run内 调用 NextArticle");
                        NextArticle();
                        WriteDebugLog($"[NextAndSendArticle无参] Task.Run内 NextArticle完成");
                    });
                });

                WriteDebugLog($"[NextAndSendArticle无参] 获取下一段内容");
                string content2 = await ArticleManager.GetFormattedCurrentSection();
                WriteDebugLog($"[NextAndSendArticle无参] 下一段内容长度={content2?.Length ?? 0}");

                // 检查是否到达文章末尾
                if (string.IsNullOrWhiteSpace(content2))
                {
                    WriteDebugLog($"[NextAndSendArticle无参] 已到达文章末尾");
                    return;
                }

                // 使用 Task.Run + Dispatcher.Invoke 模式，不等待（fire-and-forget）
                Task.Run(() =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        WriteDebugLog($"[NextAndSendArticle无参] Task.Run内 调用 LoadText");
                        LoadText(content2, RetypeType.first, TxtSource.book, false, true);
                        WriteDebugLog($"[NextAndSendArticle无参] Task.Run内 LoadText完成");
                    });
                });

                // 直接发送到QQ或剪切板
                if (qqGroupName != "")
                {
                    WriteDebugLog($"[NextAndSendArticle无参] 发送到QQ群");
                    QQHelper.SendQQMessage(qqGroupName, content2, 250, this);
                    FocusInput(); // 发送QQ后确保窗口激活
                }
                else
                {
                    WriteDebugLog($"[NextAndSendArticle无参] 复制到剪切板");
                    Win32SetText(content2);
                }
            }
            catch (Exception ex)
            {
                WriteDebugLog($"[NextAndSendArticle无参] 异常: {ex.Message}");
                throw;
            }
            WriteDebugLog($"[NextAndSendArticle无参] 完成");
        }

        /// <summary>
        /// 计算字符串的SHA1值
        /// </summary>
        private string CalculateSHA1(string text)
        {
            using (SHA1 sha1 = SHA1.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(text);
                byte[] hashBytes = sha1.ComputeHash(bytes);

                // 只取前4个字节转换为16进制
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < Math.Min(4, hashBytes.Length); i++)
                {
                    sb.Append(hashBytes[i].ToString("x2"));
                }

                return sb.ToString();
            }
        }

        /// <summary>
        /// 格式化文来内容
        /// </summary>
        private string FormatArticleSenderContent(string title, string content, string mark, string difficulty = "")
        {
            StringBuilder sb = new StringBuilder();

            // 如果没有传入难度，使用全局变量（兼容非文来模式）
            string diffText = string.IsNullOrEmpty(difficulty) ? currentDifficultyText : difficulty;

            // [难度xx]标题xx [字数xx]
            sb.AppendLine($"[{diffText}]{title} [字数{content.Length}]");

            // 内容
            sb.AppendLine(content);

            // -----第xxx段-sha1(xxxxxxxx)-晴发文
            // 使用文来接口返回的mark（如"2-277101"）替代本地段号
            //string sha1 = CalculateSHA1(content);
            //sb.Append($"-----第{mark}段-sha1({sha1})-晴发文");

            sb.Append($"-----第{mark}段-晴发文");

            return sb.ToString();
        }

        /// <summary>
        /// 文来模式：格式化文章内容并发送到QQ群或剪切板
        /// </summary>
        private void SendFormattedArticle(string content)
        {
            string title = articleCache.GetCurrentTitle();
            string mark = articleCache.GetCurrentMark();
            string difficultyText = articleCache.GetCurrentDifficulty();
            string formattedContent = FormatArticleSenderContent(title, content, mark, difficultyText);
            SendContentToClipboardOrQQ(formattedContent);
        }

        /// <summary>
        /// 通用发送方法（发送到剪切板或QQ群）
        /// </summary>
        private void SendContentToClipboardOrQQ(string content, bool focus = false)
        {
            if (QQGroupName != "")
            {
                QQHelper.SendQQMessage(QQGroupName, content, 250, this);
                FocusInput(); // 发送QQ后确保窗口激活
            }
            else
            {
                Win32SetText(content);
                if (focus)
                    FocusInput();
            }
        }

        /// <summary>
        /// 文来模式：打完后自动加载新的随机文章并发送成绩
        /// </summary>
        private async Task NextAndSendArticleSender(string lastResult = "")
        {
            // 直接加载新的随机文章并发送成绩
            await LoadRandomArticleAsync(true, lastResult);
        }
    
    




public async Task SendArticle()
        {
            string content = await ArticleManager.GetFormattedCurrentSection();

            if (winArticle != null)
            {
                await winArticle.UpdateDisplay();
            }

            if (content == null || content.Length == 0)
                return;

            LoadText(content, RetypeType.first, TxtSource.book, false);

            SendContentToClipboardOrQQ(content);






        }




        /// <summary>
        /// 文字上屏统计（从 InputBox_TextInput 抽取，字帖模式复用）
        /// </summary>
        internal void HandleTextInputStats(TextCompositionEventArgs e)
        {
            if (e.Text == "")
            {
                // 编码被取消（ESC/空码空格等），没有字上屏
                if (Score.IsComposing)
                {
                    int wasteHits = Score.Hit - Score.CompositionStartHit;
                    Score.Backs += wasteHits;
                    Score.WasteCodes++;
                    Score.IsComposing = false;
                }
                LogBack();
                return;
            }

            // 成功上屏，清除 composing 标记
            if (Score.IsComposing)
                Score.IsComposing = false;

            if (e.Text != "" && e.Text != "\r")
            {
                // 启动 - TextInput 事件中也需触发计时开始（兼容某些输入法）
                if (StateManager.typingState == TypingState.pause || StateManager.typingState == TypingState.ready)
                {
                    if (StateManager.typingState == TypingState.ready && StateManager.retypeType != RetypeType.wrongRetype)
                        RetypeCounter.Add(TextInfo.TextMD5, 1);

                    sw.Start();
                    StateManager.typingState = TypingState.typing;
                    timerProgress = new Timer(timerUpdateProgress, null, 0, 250);
                }

                // 分析打词率
                Score.AddInputStack(e.Text);

                // 记录选重提交时间、字符
                System.Globalization.StringInfo si = new System.Globalization.StringInfo(e.Text);
                string last = si.SubstringByTextElements(si.LengthInTextElements - 1, 1);

                Score.CommitTime.Add(sw.ElapsedMilliseconds);
                Score.CommitStr.Add(last);
                Score.CommitCharCount.Add(si.LengthInTextElements);
                Score.CommitText.Add(e.Text);

                // 标顶提交记录
                if (si.LengthInTextElements >= 2)
                {
                    if (last == "…" || last == "—")
                    {
                        if (si.LengthInTextElements >= 3 && si.SubstringByTextElements(si.LengthInTextElements - 2, 1) == last)
                        {
                            Score.BiaoDingCommitTime.Add(sw.ElapsedMilliseconds);
                            Score.BiaoDingCommitStr.Add(last);
                        }
                    }
                    else
                    {
                        Score.BiaoDingCommitTime.Add(sw.ElapsedMilliseconds);
                        Score.BiaoDingCommitStr.Add(last);
                    }
                }

                StateManager.TextInput = true;
            }
        }

        private void InputBox_TextInput(object sender, TextCompositionEventArgs e)
        {
            HandleTextInputStats(e);
        }

        private void DisplayProgress()
        {
            Score.Time = sw.Elapsed;
            Score.HitRate = Score.GetHit() / sw.Elapsed.TotalSeconds;

            Score.KPW = Score.GetHit() / (double)Score.InputWordCount;
            Score.Speed = (double)Score.InputWordCount / Score.Time.TotalMinutes;



            TbkStatusTop.Text = Score.Progress();

            // 字帖模式下高频刷新速度跟随提示（复用已有的 250ms 定时器）
            if (_copybookMode != null && _copybookMode.IsActive)
            {
                UpdateSpeedFollowHint(_copybookMode.CurrentIndex);
            }
            // 临摹模式下高频刷新速度跟随提示
            if (_tracingMode != null && _tracingMode.IsActive)
            {
                UpdateSpeedFollowHint(_tracingMode.CurrentIndex);
            }
        }

        internal Timer timerProgress;
        internal void timerUpdateProgress(object obj)
        {
            Dispatcher.BeginInvoke(new Action(DisplayProgress));
        }

        // 字提定时器（用于赛文5秒限制）
        private System.Windows.Threading.DispatcherTimer _ziTiTimer;

        // 启动字提定时器
        private void StartZiTiTimer()
        {
            if (_ziTiTimer != null)
            {
                _ziTiTimer.Stop();
                _ziTiTimer = null;
            }

            // 只有赛文模式才需要定时器
            if (StateManager.txtSource == TxtSource.raceApi && Config.GetBool("启用字提"))
            {
                _ziTiTimer = new System.Windows.Threading.DispatcherTimer();
                _ziTiTimer.Interval = TimeSpan.FromSeconds(1); // 每秒检查一次
                _ziTiTimer.Tick += (s, e) =>
                {
                    // 检查是否超过5秒没有输入
                    var timeSinceLastInput = DateTime.Now - StateManager.LastInputTime;
                    if (timeSinceLastInput.TotalSeconds >= 5)
                    {
                        UpdateZiTi();
                    }
                };
                _ziTiTimer.Start();
            }
        }

        // 停止字提定时器
        private void StopZiTiTimer()
        {
            if (_ziTiTimer != null)
            {
                _ziTiTimer.Stop();
                _ziTiTimer = null;
            }
        }

        // 判断按键是否是有效的输入按键(字母、数字、符号、空格、退格等)
        internal bool IsValidInputKey(Key key)
        {
            // 字母键 A-Z
            if (key >= Key.A && key <= Key.Z)
                return true;

            // 主键盘区数字键 0-9
            if (key >= Key.D0 && key <= Key.D9)
                return true;

            // 小键盘区数字键
            if (key >= Key.NumPad0 && key <= Key.NumPad9)
                return true;

            // 空格、退格、回车
            if (key == Key.Space || key == Key.Back || key == Key.Enter)
                return true;

            // 小键盘运算符
            if (key == Key.Add || key == Key.Subtract || key == Key.Multiply || key == Key.Divide || key == Key.Decimal)
                return true;

            // 常用符号键
            if (key == Key.OemPlus || key == Key.OemMinus || key == Key.OemPeriod || key == Key.OemComma ||
                key == Key.OemQuestion || key == Key.OemSemicolon || key == Key.OemQuotes || key == Key.OemTilde ||
                key == Key.OemOpenBrackets || key == Key.OemCloseBrackets || key == Key.OemPipe || key == Key.OemBackslash ||
                key == Key.Oem1 || key == Key.Oem2 || key == Key.Oem3 || key == Key.Oem4 || key == Key.Oem5 ||
                key == Key.Oem6 || key == Key.Oem7 || key == Key.Oem8 || key == Key.Oem102)
                return true;

            // IME输入
            if (key == Key.ImeProcessed)
                return true;

            return false;
        }

        /// <summary>
        /// 按键统计（从 InputBox_PreviewKeyDown 抽取，字帖模式复用）
        /// </summary>
        internal void HandleKeyDownStats(KeyEventArgs e)
        {
            // 回车暂停
            if (e.Key == Key.Enter && StateManager.txtSource != TxtSource.changeSheng && StateManager.txtSource != TxtSource.jbs && StateManager.txtSource != TxtSource.jisucup && StateManager.txtSource != TxtSource.raceApi)
            {
                if (StateManager.typingState == TypingState.typing)
                {
                    StateManager.typingState = TypingState.pause;
                    TbkStatusTop.Text = "暂停\t" + TbkStatusTop.Text;
                    sw.Stop();
                    if (timerProgress != null)
                        timerProgress.Dispose();
                }
                return;
            }

            // 统计键法
            if (e.Key == Key.ImeProcessed)
                Score.SetJianFa(e.ImeProcessedKey);
            else
                Score.SetJianFa(e.Key);

            // 打字击键总数记录计数
            CounterLog.Buffer[1]++;

            // 启动 - 只有按下有效输入键才开始计时
            if ((StateManager.typingState == TypingState.pause || StateManager.typingState == TypingState.ready) && IsValidInputKey(e.Key))
            {
                if (StateManager.typingState == TypingState.ready && StateManager.retypeType != RetypeType.wrongRetype)
                    RetypeCounter.Add(TextInfo.TextMD5, 1);

                sw.Start();
                StateManager.typingState = TypingState.typing;
                timerProgress = new Timer(timerUpdateProgress, null, 0, 250);
            }

            Score.Hit++;

            switch (e.Key)
            {
                case Key.Space:
                    StateManager.TextInput = true;
                    Score.AddInputStack(" ");
                    break;
                case Key.Back:
                    LogCorrection();
                    Score.Correction++;
                    if (Score.ZiciStack.Count > 0)
                    {
                        Score.ZiciStack.Pop();
                        StateManager.TextInput = true;
                    }
                    break;

                // bime hit
                case Key.F14:
                    Score.BimeHit++;
                    break;
                case Key.F15:
                    Score.BimeCorrection++;
                    LogCorrection();
                    break;
                case Key.F16:
                    Score.BimeBacks++;
                    LogBack();
                    break;

                case Key.ImeProcessed:
                    {
                        // 统计选重
                        int vkey = KeyInterop.VirtualKeyFromKey(e.ImeProcessedKey);
                        if (IntStringDict.Selection.ContainsKey(vkey))
                        {
                            Score.ImeKeyTime.Add(sw.ElapsedMilliseconds);
                            Score.ImeKeyValue.Add(vkey);
                        }

                        if (IntStringDict.BiaoDing.ContainsKey(vkey))
                        {
                            Score.BiaoDingImeKeyTime.Add(sw.ElapsedMilliseconds);
                            Score.BiaoDingImeKeyValue.Add(vkey);
                        }
                    }

                    switch (e.ImeProcessedKey)
                    {
                        case Key.Back:
                            LogBack();
                            Score.Backs++;
                            break;
                        default:
                            if (Win32.GetKeyState(Win32.VK_BACK) < 0)
                            {
                                LogBack();
                                Score.Backs++;
                            }
                            else
                            {
                                // 非退格的IME按键，标记进入编码状态
                                if (!Score.IsComposing)
                                {
                                    Score.IsComposing = true;
                                    Score.CompositionStartHit = Score.Hit;
                                }
                            }
                            break;
                    }
                    break;
                default:
                    break;
            }
        }

        private void InputBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // 禁用Tab键，防止用户用Tab清屏影响键准
            if (e.Key == Key.Tab)
            {
                e.Handled = true;
                return;
            }

            // 禁用回改模式：拦截退格、Esc、Ctrl+Z
            if (Config.GetBool("禁用回改") && StateManager.typingState == TypingState.typing)
            {
                if (e.Key == Key.Back || e.Key == Key.Escape ||
                    (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control))
                {
                    e.Handled = true;
                    return;
                }
            }

            if (IsLookingType && StateManager.LastType && cacheLoadInfo != null && TbxInput.IsReadOnly && !detectKeyup)
            {


                detectKeyup = true;

                return;
            }


            if (TbxInput.IsReadOnly)
                return;
            //过滤热键

            if (e.Key == Key.F3 || e.Key == Key.F4 || e.Key == Key.F5)
                return;
            if (StateManager.typingState == TypingState.ready)
            {
                if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
                    return;


            }


            //回车暂停及统计
            HandleKeyDownStats(e);


        }


        private void HotKeyCtrlE()
        {
            LoadTextFromClipBoard();

        }
        private void HotKeyF4()
        {

            GetQQText();
        }


        public static bool Delay1(int delayTime) //延时函数
        {
            /*
            DateTime now = DateTime.Now;
            int s;
            do
            {
                TimeSpan spand = DateTime.Now - now;
                s = (int)spand.TotalMilliseconds;
                //Application.DoEvents();
            }
            while (s < delayTime);
            */
            System.Threading.Thread.Sleep(delayTime);

            return true;

        }
        [DllImport("User32")]
        internal static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("User32")]
        internal static extern bool CloseClipboard();

        [DllImport("User32")]
        internal static extern bool EmptyClipboard();

        [DllImport("User32")]
        internal static extern bool IsClipboardFormatAvailable(int format);

        [DllImport("User32")]
        internal static extern IntPtr GetClipboardData(int uFormat);

        [DllImport("User32", CharSet = CharSet.Unicode)]
        internal static extern IntPtr SetClipboardData(int uFormat, IntPtr hMem);

        internal static void Win32SetText(string text)
        {

            try
            {
                if (!OpenClipboard(IntPtr.Zero)) { Win32SetText(text); return; }
                EmptyClipboard();
                SetClipboardData(13, Marshal.StringToHGlobalUni(text));
                CloseClipboard();
            }
            catch (Exception)
            {

    
            }

        }

        internal static string Win32GetText(int format)
        {
            string value = string.Empty;
            //         OpenClipboard(IntPtr.Zero);
            if (OpenClipboard(IntPtr.Zero))
            {
                if (IsClipboardFormatAvailable(format))
                {
                    IntPtr ptr = GetClipboardData(format);
                    if (ptr != IntPtr.Zero)
                    {
                        value = Marshal.PtrToStringUni(ptr);
                    }
                    else
                    {
                        value = string.Empty;
                    }
                }
                CloseClipboard();
            }
            else
            {
                value = string.Empty;
            }
            return value;
        }
        private void GetQQText()
        {
            // 禁用按钮，防止重复点击
            BtnF4.IsEnabled = false;

            try
            {
                string groupName = QQGroupName;

                if (groupName == "")
                {
                    return;
                }

                // 步骤1: 查找QQ主窗口
                string MainTitle = "QQ";
                var q = root.GetRootElement().FindFirst(TreeScope.TreeScope_Children, root.CreatePropertyCondition(UIA_PropertyIds.UIA_NamePropertyId, MainTitle));
                if (q == null)
                {
                    return;
                }

                // 步骤2: 查找会话列表
                var grouplist = q.FindFirst(TreeScope.TreeScope_Descendants, root.CreatePropertyCondition(UIA_PropertyIds.UIA_NamePropertyId, "会话列表"));
                if (grouplist == null)
                {
                    return;
                }

                // 步骤3: 检查是否已在目标群
                IUIAutomationElement edits = null;
                bool alreadyInTargetGroup = false;

                var allButtons = q.FindAll(TreeScope.TreeScope_Descendants, root.CreatePropertyCondition(UIA_PropertyIds.UIA_ControlTypePropertyId, UIA_ControlTypeIds.UIA_ButtonControlTypeId));

                if (allButtons != null && allButtons.Length > 0)
                {
                    for (int i = 0; i < allButtons.Length; i++)
                    {
                        var btn = allButtons.GetElement(i);
                        string btnName = btn.CurrentName;
                        if (!string.IsNullOrWhiteSpace(btnName) && btnName == groupName)
                        {
                            alreadyInTargetGroup = true;
                            edits = q.FindFirst(TreeScope.TreeScope_Descendants, root.CreatePropertyCondition(UIA_PropertyIds.UIA_NamePropertyId, groupName));
                            if (edits == null)
                            {
                                edits = q.FindFirst(TreeScope.TreeScope_Descendants, root.CreatePropertyCondition(UIA_PropertyIds.UIA_ControlTypePropertyId, UIA_ControlTypeIds.UIA_EditControlTypeId));
                            }
                            break;
                        }
                    }
                }

                // 步骤4: 如果不在目标群，点击目标群
                if (edits == null && !alreadyInTargetGroup)
                {
                    var allChildren = grouplist.FindAll(TreeScope.TreeScope_Children, root.CreateTrueCondition());

                    if (allChildren.Length > 0)
                    {
                        for (int i = 0; i < allChildren.Length; i++)
                        {
                            var elem = allChildren.GetElement(i);
                            string itemName = elem.CurrentName;

                            bool quickMatch = false;
                            if (!string.IsNullOrWhiteSpace(itemName))
                            {
                                string quickName = itemName.Trim('\'', '"', '\u201c', '\u201d', '\u2018', '\u2019', ' ', '\t', '\r', '\n');
                                int timeIndex = quickName.IndexOf(' ');
                                if (timeIndex > 0)
                                {
                                    quickName = quickName.Substring(0, timeIndex);
                                }

                                if (quickName == groupName || quickName.StartsWith(groupName))
                                {
                                    quickMatch = true;
                                }
                            }

                            string extractedName = "";
                            if (!quickMatch && string.IsNullOrWhiteSpace(itemName))
                            {
                                var descendants = elem.FindAll(TreeScope.TreeScope_Descendants, root.CreateTrueCondition());
                                if (descendants != null && descendants.Length > 0)
                                {
                                    System.Text.StringBuilder nameBuilder = new System.Text.StringBuilder();
                                    for (int j = 0; j < descendants.Length; j++)
                                    {
                                        var desc = descendants.GetElement(j);
                                        string descName = desc.CurrentName;
                                        int descControlType = desc.CurrentControlType;

                                        if (string.IsNullOrWhiteSpace(descName))
                                            continue;

                                        if (QQHelper.IsTimeMarker(descName))
                                            break;

                                        if (descControlType == UIA_ControlTypeIds.UIA_TextControlTypeId)
                                        {
                                            nameBuilder.Append(descName);
                                        }
                                    }
                                    extractedName = nameBuilder.ToString().Trim('\'', '"', '\u201c', '\u201d', '\u2018', '\u2019', ' ', '\t', '\r', '\n');
                                }
                            }
                            else if (!quickMatch)
                            {
                                extractedName = itemName.Trim('\'', '"', '\u201c', '\u201d', '\u2018', '\u2019', ' ', '\t', '\r', '\n');
                                int timeIndex = -1;
                                for (int j = 1; j < extractedName.Length - 3; j++)
                                {
                                    if (extractedName[j - 1] == ' ' && char.IsDigit(extractedName[j]) && extractedName[j + 1] == ':')
                                    {
                                        if (j + 2 < extractedName.Length && char.IsDigit(extractedName[j + 2]))
                                        {
                                            timeIndex = j;
                                            break;
                                        }
                                    }
                                }
                                if (timeIndex > 0)
                                {
                                    extractedName = extractedName.Substring(0, timeIndex).Trim();
                                }
                            }

                            string targetName = quickMatch ? itemName : extractedName;
                            if (!string.IsNullOrWhiteSpace(targetName) && targetName == groupName)
                            {
                                var sp = elem.GetCurrentPattern(UIA_PatternIds.UIA_InvokePatternId) as IUIAutomationInvokePattern;
                                if (sp != null)
                                {
                                    sp.Invoke();
                                    Win32.Delay(200);

                                    edits = q.FindFirst(TreeScope.TreeScope_Descendants, root.CreatePropertyCondition(UIA_PropertyIds.UIA_NamePropertyId, groupName));
                                    if (edits == null)
                                    {
                                        edits = q.FindFirst(TreeScope.TreeScope_Descendants, root.CreatePropertyCondition(UIA_PropertyIds.UIA_ControlTypePropertyId, UIA_ControlTypeIds.UIA_EditControlTypeId));
                                    }
                                }
                                break;
                            }
                        }
                    }
                }

                // 步骤5: 从当前群的消息区域查找赛文格式消息
                var messageArea = q.FindFirst(TreeScope.TreeScope_Descendants, root.CreatePropertyCondition(UIA_PropertyIds.UIA_ControlTypePropertyId, UIA_ControlTypeIds.UIA_DocumentControlTypeId));
                if (messageArea != null)
                {
                    // 从消息区域内倒着查找所有Text控件
                    var allTexts = messageArea.FindAll(TreeScope.TreeScope_Descendants, root.CreatePropertyCondition(UIA_PropertyIds.UIA_ControlTypePropertyId, UIA_ControlTypeIds.UIA_TextControlTypeId));

                    if (allTexts != null && allTexts.Length > 0)
                    {
                        // 倒着查找赛文格式
                        for (int i = allTexts.Length - 1; i >= 0; i--)
                        {
                            string text = allTexts.GetElement(i).CurrentName;

                            // 检查：非空、长度>=5、包含赛文格式标记
                            if (!string.IsNullOrWhiteSpace(text) && text.Length >= 5 && text.Contains("-----第"))
                            {
                                LoadText(text, RetypeType.first, TxtSource.qq);
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // 忽略异常
            }
            finally
            {
                // 恢复按钮
                BtnF4.IsEnabled = true;
            }
        }

        private void WriteQQDebugLog(string log)
        {
            try
            {
                // 日志已禁用
            }
            catch
            {
            }
        }

        public class CacheLoadInfo
        {
            public  string rawTxt;
           public  RetypeType retypeType;
           public TxtSource source;
            public bool switchBack;
            public bool isAuto;
            public CacheLoadInfo(string rawTxt, RetypeType retypeType, TxtSource source, bool switchBack = true, bool isAuto = false)
            {
                this .rawTxt = rawTxt;
                this .retypeType = retypeType;
                this .source = source;
                this .switchBack = switchBack;
                this .isAuto = isAuto;
            }
        }

        CacheLoadInfo cacheLoadInfo = null;


        public void UpdateTopStatusText (string text)
        {
            TbkStatusTop.Text = text;
        }

        /// <summary>
        /// 更新窗口标题，显示字数进度和难度
        /// 难度显示在标题后面，格式：晴跟打 普(1.84) 0/200 三国演义[15/2974段]
        /// </summary>
        private void UpdateWindowTitle(int typedWords, int totalWords)
        {
            if (totalWords == 0)
            {
                // 没有加载文本时，只显示基本标题
                TbkTitle.Text = "晴跟打";
                return;
            }

            // 从 currentDifficultyText 中提取难度部分（去掉"难度："前缀）
            string difficulty = "";
            if (!string.IsNullOrEmpty(currentDifficultyText))
            {
                // 尝试去掉"难度："或"难度:"前缀
                if (currentDifficultyText.StartsWith("难度："))
                {
                    difficulty = " " + currentDifficultyText.Substring(3); // " 水(0.23)"
                }
                else if (currentDifficultyText.StartsWith("难度:"))
                {
                    difficulty = " " + currentDifficultyText.Substring(3); // " 水(0.23)"
                }
            }

            if (StateManager.txtSource == TxtSource.articlesender && articleCache.HasArticle())
            {
                string progress = articleCache.GetProgress();
                string title = articleCache.GetCurrentTitle();
                TbkTitle.Text = $"晴跟打{difficulty} {typedWords}/{totalWords} {title}[{progress}]";
            }
            else if (StateManager.txtSource == TxtSource.book && ArticleManager.Title != "")
            {
                // 文章管理模式，显示书名和段落进度
                string bookTitle = ArticleManager.Title.Replace(".txt", "").Replace(".Txt", "").Replace(".TXT", "").Replace(".epub", "").Replace(".Epub", "").Replace(".EPUB", "");
                string progress = $"{ArticleManager.Index}/{ArticleManager.MaxIndex}段";
                TbkTitle.Text = $"晴跟打{difficulty} {typedWords}/{totalWords} {bookTitle}[{progress}]";
            }
            else
            {
                TbkTitle.Text = $"晴跟打{difficulty} {typedWords}/{totalWords}";
            }
        }

        public void LoadText (CacheLoadInfo cli)
        {
            LoadText (cli.rawTxt, cli.retypeType, cli.source, cli.switchBack, cli.isAuto);
        }
        public void LoadText(string rawTxt, RetypeType retypeType, TxtSource source, bool switchBack = true, bool isAuto = false) //原文、来源、重打类型
        {
            if (Config.GetBool("禁止F3重打") && (retypeType == RetypeType.shuffle || retypeType == RetypeType.retype))
            {
                return;
            }

            var rt = ExtractRawTxt(rawTxt);

            if (string.IsNullOrWhiteSpace(rt.Item1))
            {
                return;
            }

            if (isAuto && IsLookingType && StateManager.LastType) //看打模式的话，先缓存起来
            {
                cacheLoadInfo = new CacheLoadInfo(rawTxt, retypeType, source, switchBack, isAuto);
                return;
            }
        


            //设置states公共变量
            if (source != TxtSource.unchange)
            {
                StateManager.txtSource = source;
            }

            StateManager.retypeType = retypeType;

            // 调试：输出LoadText的参数和状态
            System.Diagnostics.Debug.WriteLine($"[LoadText] source={source}, retypeType={retypeType}, StateManager.txtSource={StateManager.txtSource}");
            // 同时写入日志文件

            //设置段号
            if (retypeType == RetypeType.wrongRetype)
            {

                Score.Paragraph = 0;
                Score.ParagraphString = "";
                Score.ArticleMark = "";  // 清空文来标记
                Score.DifficultyText = "";
            }
            else if (retypeType == RetypeType.shuffle || retypeType == RetypeType.retype)
            {
                Score.Paragraph = TextInfo.Paragraph;
                // 重打时保持原有的ArticleMark和ParagraphString
            }
            else //(retypeType == RetypeType.first)
            {
                Score.Paragraph = rt.Item2;
                TextInfo.Paragraph = rt.Item2;
                Score.ParagraphString = rt.Item3;
            }


            //设置md5
            if (retypeType == RetypeType.first)
            {

                TextInfo.TextMD5 = TextInfo.CalMD5(rt.Item1);
            }

            //设置赛文
            if (retypeType == RetypeType.first)
            {

                TextInfo.MatchText = rt.Item1;
            }

            //设置textinfo

            TextInfo.Words.Clear();
            System.Globalization.StringInfo si = new System.Globalization.StringInfo(rt.Item1);

            for (int i = 0; i < si.LengthInTextElements; i++)
            {
                string s = si.SubstringByTextElements(i, 1);



                TextInfo.Words.Add(s);


            }

            TextInfo.wordStates.Clear();
            TextInfo.WrongRec.Clear();
            TextInfo.SlowRec.Clear();


            TextInfo.Words.ForEach(o => TextInfo.wordStates.Add(WordStates.NO_TYPE));

            StateManager.TextInput = false;

            // 强制清空Blocks，确保重新渲染（修复本地发文翻页时显示不更新、索引越界的bug）
            Dispatcher.Invoke(() => TbDispay.Children.Clear());
            TextInfo.Blocks.Clear();





            //界面
            if (TextInfo.Words.Count > 0)
            {
                StateManager.typingState = TypingState.ready;
                StateManager.LastType = false;
                // 重置最后输入时间（用于赛文字提5秒限制）
                StateManager.LastInputTime = DateTime.Now;
                // 启动字提定时器
                StartZiTiTimer();

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    switch (retypeType)
                    {
                        case RetypeType.shuffle:
                            TbkStatusTop.Text = "乱序";
                            break;
                        case RetypeType.retype:
                            TbkStatusTop.Text = "重打";
                            break;
                        case RetypeType.wrongRetype:
                            TbkStatusTop.Text = "错字重打";
                            break;
                        case RetypeType.slowRetype:
                            TbkStatusTop.Text = "慢字重打";
                            break;
                        default:
                            TbkStatusTop.Text = "准备";
                            break;
                    }
                }));
       //         if (retypeType != RetypeType.first)
        //            TbkStatusTop.Text = "准备";

                sw.Reset();
                if (timerProgress != null)
                    timerProgress.Dispose();
                Score.Reset();

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    // 字帖模式重置（必须在UI线程，因为操作了_overlay.Children）
                    if (_copybookMode != null && _copybookMode.IsActive)
                        _copybookMode.Reset();
                    // 临摹模式重置
                    if (_tracingMode != null && _tracingMode.IsActive)
                        _tracingMode.Reset();
                }));
                if (retypeType == RetypeType.first)
                {
                    if (source == TxtSource.articlesender && articleCache.HasArticle())
                    {
                        Score.ArticleMark = articleCache.GetCurrentMark();
                        Score.DifficultyText = articleCache.GetCurrentDifficulty();
                        Score.ParagraphString = "";  // 文来模式使用ArticleMark，清空ParagraphString
                    }
                    else
                    {
                        Score.ArticleMark = "";
                        // Score.DifficultyText 已在 Score.Reset() 中清空
                        // currentDifficultyText 保留给标题显示，异步回调会更新它和 Score.DifficultyText
                    }
                }


                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (!(IsLookingType && StateManager.LastType))
                        TbxInput.IsReadOnly = false;
                    TbxInput.Clear();
                    UpdateDisplay(UpdateLevel.PageArrange);

                    // 字帖模式：布局完成后重新定位光标
                    if (_copybookMode != null && _copybookMode.IsActive)
                        _copybookMode.ScheduleUpdatePosition();

                    // 临摹模式：布局完成后插入镜像行
                    if (_tracingMode != null && _tracingMode.IsActive)
                        _tracingMode.ScheduleInsertMirrorBlocks();
                }));

                // 重置滚动位置到顶部（避免乱序/重打时停留在中间位置）
                // 使用BeginInvoke确保在UI布局完成后再设置
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    ScDisplay.ScrollToVerticalOffset(0);
                }), System.Windows.Threading.DispatcherPriority.Loaded);

                // 计算并显示难度（文来模式优先使用接口返回的难度）
                // 更新字提和标题（需要在UI线程中执行）
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    System.Diagnostics.Trace.WriteLine($"[难度] BeginInvoke进入, source={source}, articleCache.HasArticle={articleCache.HasArticle()}");
                    if (source == TxtSource.articlesender && articleCache.HasArticle())
                    {
                        string wenlaiDifficulty = articleCache.GetCurrentDifficulty();
                        if (!string.IsNullOrEmpty(wenlaiDifficulty))
                        {
                            // 使用文来返回的难度
                            currentDifficultyText = "难度：" + wenlaiDifficulty;
                            Score.DifficultyText = wenlaiDifficulty;
                            UpdateWindowTitle(0, TextInfo.Words.Count);  // 重新更新标题
                        }
                        else
                        {
                            // 文来没有返回难度，调用接口获取（异步）
                            string currentText = String.Join("", TextInfo.Words);
                            Task.Run(async () =>
                            {
                                string difficulty = await ArticleFetcher.CalcDifficultyFromApiAsync(currentText);
                                Dispatcher.Invoke(() =>
                                {
                                    if (!string.IsNullOrEmpty(difficulty))
                                    {
                                        currentDifficultyText = "难度：" + difficulty;
                                        Score.DifficultyText = difficulty;
                                        UpdateWindowTitle(0, TextInfo.Words.Count);  // 重新更新标题
                                    }
                                });
                            });
                        }
                    }
                    else
                    {
                        // 其他模式，调用接口获取难度（异步）
                        string currentText = String.Join("", TextInfo.Words);
                        Task.Run(async () =>
                        {
                            string difficulty = await ArticleFetcher.CalcDifficultyFromApiAsync(currentText);
                            Dispatcher.Invoke(() =>
                            {
                                if (!string.IsNullOrEmpty(difficulty))
                                {
                                    currentDifficultyText = "难度：" + difficulty;
                                    Score.DifficultyText = difficulty;
                                    UpdateWindowTitle(0, TextInfo.Words.Count);  // 重新更新标题
                                }
                            });
                        });
                    }

                    // 更新字提显示
                    UpdateZiTi();

                    // 更新标题显示（所有模式，初始字数为0）
                    UpdateWindowTitle(0, TextInfo.Words.Count);
                }));


                if (switchBack)
                {
                    FocusInput();
                }



            }



        }

        private Tuple<string, int, string> ExtractRawTxt(string rawTxt)
        {
            string[] lines = rawTxt.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            Regex r = new Regex("-----第\\w+段");

            int paragraph = 0;
            string paragraphString = "";
            string head = "";
            string content = "";
            string tail = "";

            if (rawTxt == "")
                return new Tuple<string, int, string>(content, paragraph, paragraphString);

            //开始检测
            int index = -1;
            for (int i = 0; i< lines.Length; i++)
           // for (int i = lines.Length - 1; i > 0; i--)
            {
                if (r.Match(lines[i]).Success)
                {
                    index = i;
                    break;
                }
            }

            if (index >= 0) //找到了赛文尾行 -----第X段
            {
                tail = lines[index];

                var m = Regex.Match(tail, "第(\\w+)段");
                paragraphString = m.Groups[1].Value;
                if (int.TryParse(paragraphString, out int para))
                    paragraph = para;
                else
                    paragraph = 0;

                if (index >= 2)
                {
                    head = lines[0];
                    // 将 head 和 tail 之间的所有行拼接为 content，并记录换行位置
                    TextInfo.LineBreaks.Clear();
                    StringBuilder sb = new StringBuilder();
                    for (int i = 1; i < index; i++)
                    {
                        if (i > 1 && lines[i].Length > 0)
                        {
                            var sbSi = new System.Globalization.StringInfo(sb.ToString());
                            TextInfo.LineBreaks.Add(sbSi.LengthInTextElements);
                        }
                        sb.Append(lines[i]);
                    }
                    content = sb.ToString();

                    if (head.Length >= 3 && head.Substring(0, 3) == "皇叔 ")
                        content = UnicodeBias(content);
                }
                else if (index == 1)
                {
                    TextInfo.LineBreaks.Clear();
                    head = lines[0];
                    content = "";
                }
                else
                {
                    TextInfo.LineBreaks.Clear();
                    content = "";
                }
            }
            else //非赛文格式
            {
                TextInfo.LineBreaks.Clear();
                content = rawTxt.Replace("\n", "").Replace("\r", "").Replace("\t", "");
            }

            // 清理零宽字符（U+200B~U+200F, U+FEFF, U+2060, U+00AD 等不可见字符）
            content = Regex.Replace(content, "[\u200B-\u200F\uFEFF\u2060\u00AD]", "");

            return new Tuple<string, int, string>(content, paragraph, paragraphString);


        }

        private void HotKeyMButton()
        {
            mouse_event(MouseEventFlag.LeftDown, 0, 0, 0, IntPtr.Zero);
            mouse_event(MouseEventFlag.LeftUp, 0, 0, 0, IntPtr.Zero);
            Win32.Delay(10);
//            Win32.CtrlA();
//            Win32.Delay(10);
            Win32.CtrlC();
            Win32.Delay(10);
            LoadTextFromClipBoard();

        }

        /*
        private void HotKeyF5()
        {


            ChangQu();

        }
        */
        /*
        private void ChangQu()
        {
            string QuName = QQHelper.GetQuName();

            if (QuName.Length > 0)
            {
                BtnF5.Content = "换群F5-" + QuName;
            }
            else
            {
                BtnF5.Content = "换群F5";
            }

            this.Activate();
            this.Topmost = true;  // important
            this.Topmost = false; // important
            this.Focus();         // important

            TbxInput.Focus();
        }
        */
        private void LoadTextFromClipBoard()
        {
            string cTxt = Clipboard.GetText();
            LoadText(cTxt, RetypeType.first, TxtSource.clipboard);
        }

        private string GetContentFromMatchText(string cTxt)
        {
            if (cTxt == "")
                return "";



            char[] sp = { '\n', '\r' };
            string[] SubTxt = cTxt.Split(sp, StringSplitOptions.RemoveEmptyEntries);




            string raw_txt = "";
            if (SubTxt.Length >= 3 && SubTxt[SubTxt.Length - 1].Contains("-----"))
            {

                for (int i = 1; i < SubTxt.Length - 1; i++)
                {
                    raw_txt += SubTxt[i];
                }

          //      var m = Regex.Match(SubTxt[SubTxt.Length - 1], "第[0-9]+段");


            }
            else if (SubTxt.Length > 0)
            {
                raw_txt = cTxt.Replace("\n", "").Replace("\r", "").Replace("\t", "");
            }
            else
            {
                return "";
            }

            if (cTxt.Length >= 3 && cTxt.Substring(0, 3) == "皇叔 ")
                raw_txt = UnicodeBias(raw_txt);

            return raw_txt;
        }

        private string UnicodeBias(string input)
        {
            StringBuilder sb = new StringBuilder();
            System.Globalization.StringInfo si = new System.Globalization.StringInfo(input);

            for (int i = 0; i < si.LengthInTextElements; i++)
            {
                string s = si.SubstringByTextElements(i, 1);


                string sout;
                int unicode;
                if (s.Length == 1)
                    unicode = (int)s[0];
                else
                    unicode = char.ConvertToUtf32(s[0], s[1]);


                unicode--;
                sout = char.ConvertFromUtf32(unicode);

                sb.Append(sout);
            }


            return sb.ToString();
        }





        private void BtnCtrlE_Click(object sender, RoutedEventArgs e)
        {

            HotKeyCtrlE();



        }

        private void auto()
        {

        }

        private void TbxInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            //启动 - TextChanged 事件中也需触发计时开始（兼容 TSF 输入法）
            if ((StateManager.typingState == TypingState.pause || StateManager.typingState == TypingState.ready)
                && e.Changes.Count > 0 && e.Changes.First().AddedLength > 0)
            {
                if (StateManager.typingState == TypingState.ready && StateManager.retypeType != RetypeType.wrongRetype)
                    RetypeCounter.Add(TextInfo.TextMD5, 1);

                sw.Start();
                StateManager.typingState = TypingState.typing;
                timerProgress = new Timer(timerUpdateProgress, null, 0, 250);
            }

            // 更新最后输入时间（用于赛文字提5秒限制）
            if (e.Changes.Count > 0 && e.Changes.First().AddedLength > 0)
            {
                StateManager.LastInputTime = DateTime.Now;
                // 立即更新字提（赛文模式下会隐藏字提，因为还没到5秒）
                if (StateManager.txtSource == TxtSource.raceApi)
                {
                    UpdateZiTi();
                }
            }

            if (StateManager.TextInput || Score.BimeHit > 0)
            {



                if (e.Changes.Count > 0)// && Recorder.State != Recorder.RecorderState.Playing)
                    CounterLog.Buffer[0] += e.Changes.First().AddedLength;



                StateManager.TextInput = false;
                ProcInput();
            }

        }

        //     AutomationElement aeInput;

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 应用当前选中的 Logo
            ApplyCurrentLogo();

            // 初始化字提功能（使用配置中的方案）
            string scheme = Config.GetString("字提方案");
            if (!string.IsNullOrEmpty(scheme))
            {
                ZiTiHelper.Initialize(scheme);
            }
            else
            {
                ZiTiHelper.Initialize();
            }

            InitDisplay();

            // 初始化选群按钮文字
            string currentGroup = Config.GetString("当前选群");
            if (string.IsNullOrEmpty(currentGroup))
            {
                BtnF5.Content = "选群F5";
            }
            else
            {
                BtnF5.Content = "当前-" + currentGroup;
            }

            // 加载文来Cookie（登录后下次启动自动登录）
            try
            {
                var accountManager = new TypeSunny.Net.AccountSystemManager();
                var account = accountManager.GetAccount("文来");
                if (account != null && !string.IsNullOrWhiteSpace(account.Cookies))
                {
                    string apiUrl = Config.GetString("文来接口地址");
                    if (!string.IsNullOrWhiteSpace(apiUrl))
                    {
                        ArticleFetcher.LoadCookiesFromString(apiUrl, account.Cookies);
                        System.Diagnostics.Debug.WriteLine("✓ 文来Cookie已自动加载");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载文来Cookie失败: {ex.Message}");
            }

            IntStringDict.Load();

            // 加载当日成绩记录
            CounterLog.LoadDailyResults();
            TxtResult = CounterLog.GetDailyResults();

            // 强制刷新窗体背景色和字体色（确保主题生效）
            Dispatcher.BeginInvoke(new Action(() =>
            {
                MainBorder.Background = Colors.FromString(Config.GetString("窗体背景色"));
                this.Foreground = Colors.FromString(Config.GetString("窗体字体色"));

                // 应用自定义标题栏背景色
                if (TitleBarGrid != null)
                {
                    TitleBarGrid.Background = Colors.FromString(Config.GetString("窗体背景色"));
                    System.Diagnostics.Debug.WriteLine($"[Window_Loaded]强制刷新TitleBarGrid背景色: {Config.GetString("窗体背景色")}");
                }

                this.UpdateLayout();
                System.Diagnostics.Debug.WriteLine($"[Window_Loaded]强制刷新窗体背景色: {Config.GetString("窗体背景色")}");
                System.Diagnostics.Debug.WriteLine($"[Window_Loaded]强制刷新窗体字体色: {Config.GetString("窗体字体色")}");
            }), System.Windows.Threading.DispatcherPriority.Loaded);

            // 后台异步预加载字体和UI组件，避免首次发文卡顿
            Task.Run(() =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine("[预加载]开始异步预加载字体和UI组件");

                    // 预加载程序集（避免首次发文时加载DLL卡顿）
                    System.Diagnostics.Debug.WriteLine("[预加载]开始预加载程序集");

                    // 预加载System.Net.Http
                    var httpClientType = typeof(System.Net.Http.HttpClient);
                    System.Diagnostics.Debug.WriteLine($"[预加载]System.Net.Http已加载: {httpClientType.Name}");

                    // 预加载Newtonsoft.Json
                    var jsonType = typeof(Newtonsoft.Json.JsonConvert);
                    System.Diagnostics.Debug.WriteLine($"[预加载]Newtonsoft.Json已加载: {jsonType.Name}");

                    // 预加载System.Numerics（尝试加载程序集）
                    try
                    {
                        System.Reflection.Assembly.Load("System.Numerics, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
                        System.Diagnostics.Debug.WriteLine("[预加载]System.Numerics已加载");
                    }
                    catch
                    {
                        System.Diagnostics.Debug.WriteLine("[预加载]System.Numerics加载跳过（可选）");
                    }

                    System.Diagnostics.Debug.WriteLine("[预加载]程序集预加载完成");

                    // 预加载字体（触发字体文件加载）
                    Dispatcher.Invoke(() =>
                    {
                        var fm = GetCurrentFontFamily();
                        if (fm != null)
                        {
                            // 创建一个临时TextBlock来触发字体加载
                            var tempTb = new TextBlock();
                            tempTb.FontFamily = fm;
                            tempTb.FontSize = 40.0;
                            tempTb.Text = "预加载";
                            // 不需要添加到界面，只是触发字体加载
                        }
                        System.Diagnostics.Debug.WriteLine("[预加载]字体预加载完成");
                    }, System.Windows.Threading.DispatcherPriority.Background);

                    // 预加载文来难度数据（避免首次载文时等待）
                    System.Diagnostics.Debug.WriteLine("[预加载]开始预加载文来难度数据");
                    try
                    {
                        var difficulties = Task.Run(async () => await ArticleFetcher.GetDifficultiesAsync()).GetAwaiter().GetResult();
                        System.Diagnostics.Debug.WriteLine($"[预加载]文来难度数据预加载完成，共{difficulties.Count}个难度");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[预加载]文来难度数据预加载失败: {ex.Message}");
                    }

                    // 预加载Filter数据（避免首次载文时Filter.ProcFilter慢）
                    System.Diagnostics.Debug.WriteLine("[预加载]开始预加载Filter数据");
                    try
                    {
                        // 调用一次ProcFilter来触发Read()加载过滤规则
                        Filter.ProcFilter("测试");
                        System.Diagnostics.Debug.WriteLine("[预加载]Filter数据预加载完成");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[预加载]Filter数据预加载失败: {ex.Message}");
                    }

                    System.Diagnostics.Debug.WriteLine("[预加载]所有预加载任务完成");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[预加载]预加载失败: {ex.Message}");
                }
            });

            // 加载标题栏进度条颜色配置
            // try
            // {
            //     LoadTitleProgressBarColor();
            // }
            // catch { }


            //      AutomationElement dis = AutomationElement.FromHandle(new WindowInteropHelper(this).Handle);
            //     aeInput = dis.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.AutomationIdProperty, "input"));   //找到XXXX交流群的聊天窗口
            //     if (aeInput != null)
            //      {
            //          return;
            //      }

            // 启动版本检测
            StartVersionCheck();
        }

        // 版本检测定时器
        private System.Windows.Threading.DispatcherTimer _versionCheckTimer;
        // 标记用户是否已关闭更新提醒
        private bool _updateReminderDismissed = false;

        /// <summary>
        /// 启动版本检测（启动时检查 + 定时器每小时检查一次）
        /// </summary>
        private void StartVersionCheck()
        {
            // 启动时立即检查一次（如果距离上次检查超过24小时）
            Task.Run(async () =>
            {
                try
                {
                    bool hasUpdate = await VersionManager.CheckUpdateAsync();
                    if (hasUpdate)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            // 只有在用户没有关闭过提醒时才显示
                            if (!_updateReminderDismissed)
                            {
                                ShowUpdateReminder();
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"版本检测失败: {ex.Message}");
                }
            });

            // 启动定时器：每小时检查一次是否超过24小时
            _versionCheckTimer = new System.Windows.Threading.DispatcherTimer();
            _versionCheckTimer.Interval = TimeSpan.FromHours(1); // 每小时检查一次
            _versionCheckTimer.Tick += async (s, e) =>
            {
                try
                {
                    bool hasUpdate = await VersionManager.CheckUpdateAsync();
                    if (hasUpdate)
                    {
                        // 真正检查到有更新时，重置标志，允许显示提醒
                        _updateReminderDismissed = false;
                        ShowUpdateReminder();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"定时版本检测失败: {ex.Message}");
                }
            };
            _versionCheckTimer.Start();
        }

        /// <summary>
        /// 显示更新提醒
        /// </summary>
        private void ShowUpdateReminder()
        {
            // 检查窗口是否已关闭或正在关闭
            if (!IsLoaded)
                return;

            // 如果用户已经关闭过提醒，不重复显示（除非24小时后重新检查）
            if (_updateReminderDismissed)
                return;

            // 查找按钮
            var btn = this.FindName("BtnUpdateReminder") as System.Windows.Controls.Button;
            if (btn == null)
                return;

            // 如果提醒已经可见，不需要重复显示
            if (btn.Visibility == Visibility.Visible)
                return;

            try
            {
                btn.Content = $"[有新版本 {VersionManager.LatestVersion}]";
                btn.Visibility = Visibility.Visible;
            }
            catch
            {
                // 窗口已关闭，忽略错误
            }
        }

        /// <summary>
        /// 更新提醒被点击
        /// </summary>
        private void BtnUpdateReminder_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as System.Windows.Controls.Button;
            if (btn != null)
            {
                btn.Visibility = Visibility.Collapsed;
                _updateReminderDismissed = true;  // 标记用户已关闭
                VersionManager.DismissUpdateReminder();
            }
        }

        private void LoadTitleProgressBarColor()
        {
            // 如果未启用进度条，不设置颜色
            if (!Config.GetBool("显示进度条"))
                return;

            try
            {
                string colorStr = Config.GetString("标题栏进度条颜色");
                if (!string.IsNullOrEmpty(colorStr))
                {
                    // 如果没有#前缀，添加上
                    if (!colorStr.StartsWith("#"))
                        colorStr = "#" + colorStr;

                    var converter = new System.Windows.Media.BrushConverter();
                    var brush = (System.Windows.Media.Brush)converter.ConvertFromString(colorStr);

                    // 直接设置Rectangle的颜色
                    TitleProgressBar.Fill = brush;
                }
            }
            catch
            {
                // 如果颜色格式错误，使用默认颜色
                TitleProgressBar.Fill = new SolidColorBrush(Color.FromRgb(0x00, 0x7A, 0xCC));
            }
        }

        private void SldZoom_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // SldZoom已隐藏，字体大小现在通过Ctrl+鼠标滚轮独立调节
            /*
            if (StateManager.ConfigLoaded)
            {

                Config.Set("发文区字体大小", e.NewValue, 1);

                // UpdateDisplay(UpdateLevel.PageArrange);

                DelayUpdateDisplay(100, UpdateLevel.PageArrange);
            }
            */
        }

        private void MenuQQ_Click(object sender, RoutedEventArgs e)
        {

            var mi = (MenuItem)sender;
            foreach (var mb in MenuQQGroup.Items)
            {
                var mbt = (MenuItem)mb;
                if (mbt.Header.ToString() != mi.Header.ToString())
                    mbt.IsChecked = false;
                else
                    mbt.IsChecked = true;
            }

            // 提取纯群名（去掉表情前缀）
            string header = mi.Header.ToString();
            string groupName = header.Replace("🌊 -潜水-", "-潜水-").Replace("👥 ", "");

            SelectQQGroup(groupName);



        }

        private void SelectQQGroup(string groupName)
        {

            if (groupName == "-潜水-")
            {
                BtnF5.Content = "选群F5";
                Config.Set("当前选群", "");
                Config.WriteConfig(0); // 立即保存
            }
            else if (groupName != null && groupName.Length > 0)
            {
                BtnF5.Content = "当前-" + groupName;
                Config.Set("当前选群", groupName);
                Config.WriteConfig(0); // 立即保存
            }
            else
            {
                BtnF5.Content = "选群F5";
                Config.Set("当前选群", "");
                Config.WriteConfig(0); // 立即保存
            }

            FocusInput();
        }

        private async void BtnF5_Click(object sender, RoutedEventArgs e)
        {
            // 禁用按钮，防止重复点击
            BtnF5.IsEnabled = false;

            // 异步获取群列表，避免阻塞UI线程
            List<string> groupList = null;
            await System.Threading.Tasks.Task.Run(() =>
            {
                groupList = QQHelper.GetQunList();
            });

            // 调试信息（如需查看，取消注释下面3行）
            //if (!string.IsNullOrEmpty(QQHelper.LastDebugInfo))
            //{
            //    MessageBox.Show(QQHelper.LastDebugInfo, "QQ群列表获取调试信息", MessageBoxButton.OK, MessageBoxImage.Information);
            //}

            MenuQQGroup.Items.Clear();

            FocusInput();

            // 从配置读取当前选中的群
            string currentGroup = Config.GetString("当前选群");

            {
                MenuItem mi = new MenuItem();
                mi.Header = "🌊 -潜水-";
                mi.Click += MenuQQ_Click;
                mi.IsCheckable = true;

                // 如果配置为空或未选择，默认选中潜水
                if (string.IsNullOrEmpty(currentGroup))
                    mi.IsChecked = true;

                MenuQQGroup.Items.Add(mi);
            }
            foreach (string groupName in groupList)
            {
                MenuItem mi = new MenuItem();

                mi.Header = "👥 " + groupName;
                mi.Click += MenuQQ_Click;
                mi.IsCheckable = true;

                // 如果配置中的群名匹配，设置选中
                if (groupName == currentGroup)
                    mi.IsChecked = true;
                else if (!string.IsNullOrEmpty(currentGroup) && currentGroup == "-潜水-")
                    // 如果当前选择的是潜水，确保普通群不被选中
                    mi.IsChecked = false;

                MenuQQGroup.Items.Add(mi);

            }

            // 设置菜单位置在按钮下方
            MenuQQGroup.PlacementTarget = BtnF5;
            MenuQQGroup.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;

            MenuQQGroup.IsOpen = true;

            // 恢复按钮
            BtnF5.IsEnabled = true;

            return;
        }

        private void BtnF4_Click(object sender, RoutedEventArgs e)
        {
            GetQQText();
        }




        /*
        public void CtrlTab()
        {

            System.Windows.Forms.SendKeys.SendWait("^{TAB}");


            //         Sleep(50);
        }

        public void CtrlA()
        {

            System.Windows.Forms.SendKeys.SendWait("^a");
        }

        public void CtrlV()
        {

            System.Windows.Forms.SendKeys.SendWait("^v");
        }


        public void CtrlC()
        {

            System.Windows.Forms.SendKeys.SendWait("^c");
        }


        public void AltS()
        {
            System.Windows.Forms.SendKeys.SendWait("%s");

        }
        public void Tab()
        {

            System.Windows.Forms.SendKeys.SendWait("{TAB}");


        }

        */
        // CbFonts控件已移到设置窗口

        private void TbClip_TextChanged(object sender, TextChangedEventArgs e)
        {

            if (TbClip.Text != "")
            {

                LoadText(TbClip.Text, RetypeType.first, TxtSource.qq);
            }


            return;// get_;
        }


        private static Random rng = new Random();
        private void HotKeyCtrlL()
        {
            if (TextInfo.MatchText == "")
                return;
            List<string> ls = new List<string>();

            string sl = GetContentFromMatchText(TextInfo.MatchText);
            System.Globalization.StringInfo si = new System.Globalization.StringInfo(sl);

            for (int i = 0; i < si.LengthInTextElements; i++)
                ls.Add(si.SubstringByTextElements(i, 1));





            Sf(ls);

            string s = string.Join("", ls);

            LoadText(s, RetypeType.shuffle, TxtSource.unchange);

            void Sf<T>(IList<T> list)
            {
                int n = list.Count;
                while (n > 1)
                {
                    n--;
                    int k = rng.Next(n + 1);
                    T value = list[k];
                    list[k] = list[n];
                    list[n] = value;
                }
            }



            //    HotkeyF3();
   //         TbkStatusTop.Text = "乱序";
        }

        public static IEnumerable<T> Randomize<T>(IEnumerable<T> source)
        {
            Random rnd = new Random();
            return source.OrderBy((item) => rnd.Next());
        }



        private void InternalHotkeyF4(object sender, ExecutedRoutedEventArgs e)
        {
            HotKeyF4();
        }

        private void InternalHotkeyF5(object sender, ExecutedRoutedEventArgs e)
        {
            BtnF5_Click(null, null);
        }
        private void InternalHotkeyCtrlE(object sender, ExecutedRoutedEventArgs e)
        {
            HotKeyCtrlE();
        }

        private void InternalHotkeyCtrlL(object sender, ExecutedRoutedEventArgs e)
        {

            if (StateManager.txtSource == TxtSource.trainer && winTrainer != null)
            {
                // 如果正在打字，先记录当前进度
                if (StateManager.typingState == TypingState.typing && sw.IsRunning)
                {
                    // 计算当前输入的字数
                    int inputWordCount = new System.Globalization.StringInfo(TbxInput.Text).LengthInTextElements;

                    // 计算已用时间（秒）
                    double timeSeconds = sw.Elapsed.TotalSeconds;

                    // 计算准确率（简单比对已输入的部分）
                    double accuracy = 1.0;
                    if (inputWordCount > 0)
                    {
                        int correctCount = 0;
                        for (int i = 0; i < Math.Min(inputWordCount, TextInfo.wordStates.Count); i++)
                        {
                            if (TextInfo.wordStates[i] == WordStates.RIGHT)
                                correctCount++;
                        }
                        accuracy = (double)correctCount / inputWordCount;
                    }

                    // 记录部分进度
                    winTrainer.RecordPartialProgress(inputWordCount, timeSeconds, accuracy);
                }

                winTrainer.CtrlL();
            }
            else
                HotKeyCtrlL();


        }

        private void InternalHotkeyCtrlR(object sender, ExecutedRoutedEventArgs e)
        {
            LoadRandomArticle(true);
        }

        private async void InternalHotkeyCtrlP(object sender, ExecutedRoutedEventArgs e)
        {
            // 判断当前是文来模式还是本地文章模式
            if (StateManager.txtSource == TxtSource.articlesender && articleCache.HasArticle())
            {
                // 文来模式：调用API获取下一段
                LoadNextSegment();
            }
            else
            {
                // 本地文章模式：翻到下一页
                ArticleManager.NextSection();
                await SendArticle();
            }
        }

        private async void InternalHotkeyCtrlO(object sender, ExecutedRoutedEventArgs e)
        {
            // 判断当前是文来模式还是本地文章模式
            if (StateManager.txtSource == TxtSource.articlesender && articleCache.HasArticle())
            {
                // 文来模式：调用API获取上一段
                LoadPreviousSegment();
            }
            else
            {
                // 本地文章模式：翻到上一页
                ArticleManager.PrevSection();
                await SendArticle();
            }
        }



        /*
        private void Tbk_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {


            PlayRecord();
        }

        */


        /*
                private void Child2()
                {
                    Recorder.State = Recorder.RecorderState.Playing;
                    Stopwatch st = new Stopwatch();
                    INPUT[] input = new INPUT[1];
                    st.Start();

                    for (int i = 0; i < Recorder.RecItems.Count; i++)
                    {
                        var rec = Recorder.RecItems[i];
                        while (true)
                        {
                            if (st.ElapsedTicks >= rec.time + 10000000)
                            {

                                input[0].type = 1;//模拟键盘
                                                  //         input[0].ki.wVk = (short)KeyInterop.VirtualKeyFromKey(rec.key);
                                input[0].ki.wVk = (short)rec.key;
                                input[0].ki.dwFlags = rec.keystate;

                                SendInput((uint)1, input, Marshal.SizeOf((object)default(INPUT)));

                                break;
                            }
                        }
                    }

                    Recorder.State = Recorder.RecorderState.Stopped;


                }

        */


        /*
        private void PlayRecord()
        {
            if (Recorder.RecItems.Count == 0)
                return;
            StateManager.PlayRecord = true;
            Recorder.State = Recorder.RecorderState.Playing;
            HotkeyF3();
            TbxInput.Focus();
            Delay(500);

                 Thread th = new Thread(Child2);
                th.Start();


       


        }
        */
        
        bool detectKeyup = false;
        private void TbxInput_PreviewKeyUp(object sender, KeyEventArgs e)
        {

            if (IsLookingType && StateManager.LastType && cacheLoadInfo != null && TbxInput.IsReadOnly && detectKeyup)
            {
                cacheLoadInfo.isAuto = false;
                LoadText(cacheLoadInfo);
                cacheLoadInfo = null;

                detectKeyup = false;
                TbxInput.IsReadOnly = false;
                return;
            }


        }
        
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // 先关闭字帖模式，避免OnLostFocus抢焦点导致关闭卡死
            if (_copybookMode != null && _copybookMode.IsActive)
                _copybookMode.Disable();

            // 确保文来日志队列被清空
            WenlaiLog.Shutdown();

            // 确保文章日志队列被清空
            ArticleLog.Shutdown();

            // 停止版本检测定时器
            if (_versionCheckTimer != null)
            {
                _versionCheckTimer.Stop();
                _versionCheckTimer = null;
            }

            CounterLog.Add("字数", CounterLog.Buffer[0]);
            CounterLog.Buffer[0] = 0;
            CounterLog.Add("击键数", CounterLog.Buffer[1]);
            CounterLog.Buffer[1] = 0;
            CounterLog.Write();

            // 保存当日成绩记录
            CounterLog.SaveDailyResults();

            // 确保打单器日志队列被清空
            CounterLog.Shutdown();

            Config.Set("窗口坐标X", this.Left, 0);
            Config.Set("窗口坐标Y", this.Top, 0);
            SaveDisplayInputRatio();
            Config.WriteConfig(0);

            //         StopHook();
            StopMouseHook();
            TextInfo.Exit = true;
            foreach (Window a in Application.Current.Windows)
            {
                //if (a.Title != "" && a.Title != this.Title)
                if (!( a is MainWindow))
                    a.Close();
            }

            //   Window[] childArray = Application.Current.Windows.

        }
        WinConfig winConfig;
        private void Tbk_PreviewMouseUp_1(object sender, MouseButtonEventArgs e)
        {
            foreach (Window item in Application.Current.Windows)
            {
                if (item is WinConfig)
                {
                    item.Focus();
                    item.Activate();
                    return;
                }

            }

            winConfig = new WinConfig();
            winConfig.Owner = this;
            winConfig.ConfigSaved += new WinConfig.DelegateConfigSaved(ReloadCfg);
            winConfig.Show();
        }



        #region win32
        public delegate int HookProc(int nCode, Int32 wParam, IntPtr lParam);
        //    static int hKeyboardHook = 0; //声明键盘钩子处理的初始值
        public const int WH_KEYBOARD_LL = 13;   //线程键盘钩子监听鼠标消息设为2，全局键盘监听鼠标消息设为13
        public const int WH_KEYBOARD = 20;   //线程键盘钩子监听鼠标消息设为2，全局键盘监听鼠标消息设为13
        public const int WH_MOUSE_LL = 14;   //线程键盘钩子监听鼠标消息设为2，全局键盘监听鼠标消息设为13
                                             //      HookProc KeyboardHookProcedure; //声明KeyboardHookProcedure作为HookProc类型
        HookProc MouseHookProcedure; //声明KeyboardHookProcedure作为HookProc类型



        //键盘结构
        [StructLayout(LayoutKind.Sequential)]
        public class KeyboardHookStruct
        {
            public int vkCode;  //定一个虚拟键码。该代码必须有一个价值的范围1至254
            public int scanCode; // 指定的硬件扫描码的关键
            public int flags;  // 键标志
            public int time; // 指定的时间戳记的这个讯息
            public int dwExtraInfo; // 指定额外信息相关的信息
        }


        //使用此功能，安装了一个钩子
        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        public static extern int SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hInstance, int threadId);


        //调用此函数卸载钩子
        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        public static extern bool UnhookWindowsHookEx(int idHook);




        /*
        public void StartHook()
        {
            // 安装键盘钩子
            if (hKeyboardHook == 0)
            {
                KeyboardHookProcedure = new HookProc(KeyboardHookProc);

                hKeyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, KeyboardHookProcedure, GetModuleHandle(Process.GetCurrentProcess().MainModule.ModuleName), 0);

                //如果SetWindowsHookEx失败
                if (hKeyboardHook == 0)
                {
                    StopHook();
                    throw new Exception("安装键盘钩子失败");
                }
            }
        }
        public void StopHook()
        {
            bool retKeyboard = true;


            if (hKeyboardHook != 0)
            {
                retKeyboard = UnhookWindowsHookEx(hKeyboardHook);
                hKeyboardHook = 0;
            }

            if (!(retKeyboard))
                throw new Exception("卸载钩子失败！");
        }
        */

        //使用WINDOWS API函数代替获取当前实例的函数,防止钩子失效
        [DllImport("kernel32.dll")]
        public static extern IntPtr GetModuleHandle(string name);
        private const int WM_KEYDOWN = 0x100;//KEYDOWN
        private const int WM_KEYUP = 0x101;//KEYUP
        private const int WM_SYSKEYDOWN = 0x104;//SYSKEYDOWN
        private const int WM_SYSKEYUP = 0x105;//SYSKEYUP

        /*
        private int KeyboardHookProc(int nCode, Int32 wParam, IntPtr lParam)
        {

            if (!Config.GetBool("回放功能"))
                return 0;

            int rt = 0;

            //key down事件处理

            if (nCode < 0)
                return 0;

            KeyboardHookStruct InputKey = (KeyboardHookStruct)Marshal.PtrToStructure(lParam, typeof(KeyboardHookStruct));


            if (Recorder.State == Recorder.RecorderState.Recording)
            {

                Recorder.RecItem r = new Recorder.RecItem();



                r.time = sw.ElapsedTicks;
                r.key = InputKey.vkCode;

                uint testbit = 1 << 7;
                uint testinject = 1 << 4;

                if ((InputKey.flags & testbit) == 0)
                    r.keystate = 0;
                else if ((InputKey.flags & testbit) != 0)
                    r.keystate = 2;
                r.modifier = 0;

                //被注入不记录
                if ((InputKey.flags ^ testinject) != 0)
                    Recorder.RecItems.Add(r);


            }





            return rt;
        }

*/
        #endregion


        static private int MouseHooked = 0;
        public void StartMouseHook()
        {

            IntPtr pInstance = Marshal.GetHINSTANCE(Assembly.GetExecutingAssembly().ManifestModule);
            //pInstance = (IntPtr)4194304;
            // IntPtr pInstanc2 = Marshal.GetHINSTANCE(Assembly.GetExecutingAssembly());
            // Assembly.GetExecutingAssembly().GetModules()[0]
            // 假如没有安装鼠标钩子
            if (MouseHooked == 0)
            {
                MouseHookProcedure = new HookProc(MouseHookProc);
                MouseHooked = SetWindowsHookEx(WH_MOUSE_LL, MouseHookProcedure, pInstance, 0);
                if (MouseHooked == 0)
                {
                    StopMouseHook();
                    throw new Exception("安装鼠标钩子失败");
                }
            }

        }


        public void StopMouseHook()
        {
            bool retKeyboard = true;


            if (MouseHooked != 0)
            {
                retKeyboard = UnhookWindowsHookEx(MouseHooked);
                MouseHooked = 0;
            }

            if (!(retKeyboard))
                throw new Exception("卸载钩子失败！");
        }


        private int MouseHookProc(int nCode, Int32 wParam, IntPtr lParam)
        {

            if (wParam == 0x207)
            {
                HotKeyMButton();
                return 1;
            }

            return 0;
        }

        JBS jbs = null;
        JiSuCup jiSuCup = null;
        JbsHelper jbsHelper;
        JiSuCupHelper jiSuCupHelper;
        TypeSunny.Net.RaceHelper raceHelper;  // 保留旧版Helper用于兼容
        TypeSunny.Net.RaceHelperV2 raceHelperV2;  // 新版Helper支持多服务器
        ArticleCache articleCache = new ArticleCache();
        WenlaiHelper wenlaiHelper = new WenlaiHelper();  // 文来登录助手

        // 赛文菜单项字典：存储动态创建的MenuItem
        private Dictionary<string, MenuItem> raceMenuItems = new Dictionary<string, MenuItem>();

        /// <summary>
        /// 从URL提取域名（协议+主机+端口）
        /// </summary>
        private string ExtractDomainFromUrl(string url)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(url))
                    return "";

                Uri uri = new Uri(url.TrimEnd('/'));
                return uri.GetLeftPart(UriPartial.Authority);
            }
            catch
            {
                return url;
            }
        }

        /// <summary>
        /// 创建MenuItem样式以支持暗色主题
        /// </summary>
        private Style CreateMenuItemStyle(System.Windows.Media.SolidColorBrush menuBg, System.Windows.Media.SolidColorBrush menuFg)
        {
            var style = new Style(typeof(MenuItem));

            // 获取背景色
            var bgColor = menuBg.Color;
            // 判断是否为暗色主题（亮度低于0.5为暗色）
            double brightness = (bgColor.R * 0.299 + bgColor.G * 0.587 + bgColor.B * 0.114) / 255.0;
            bool isDarkTheme = brightness < 0.5;

            // 根据主题计算鼠标悬停颜色 - 只使用边框高亮效果
            System.Windows.Media.Color hoverBorderColor;
            System.Windows.Media.Color hoverFgColor;

            if (isDarkTheme)
            {
                // 暗色主题：悬停时边框变亮
                hoverBorderColor = System.Windows.Media.Color.FromRgb(
                    (byte)Math.Min(255, bgColor.R + 80),
                    (byte)Math.Min(255, bgColor.G + 80),
                    (byte)Math.Min(255, bgColor.B + 80)
                );
                hoverFgColor = menuFg.Color;
            }
            else
            {
                // 亮色主题：悬停时边框变深
                hoverBorderColor = System.Windows.Media.Color.FromRgb(
                    (byte)Math.Max(0, bgColor.R - 50),
                    (byte)Math.Max(0, bgColor.G - 50),
                    (byte)Math.Max(0, bgColor.B - 50)
                );
                hoverFgColor = menuFg.Color;
            }

            var hoverBorderBrush = new System.Windows.Media.SolidColorBrush(hoverBorderColor);
            var hoverFgBrush = new System.Windows.Media.SolidColorBrush(hoverFgColor);
            hoverBorderBrush.Freeze();
            hoverFgBrush.Freeze();

            // 设置默认背景和前景色
            style.Setters.Add(new Setter(MenuItem.BackgroundProperty, menuBg));
            style.Setters.Add(new Setter(MenuItem.ForegroundProperty, menuFg));
            style.Setters.Add(new Setter(MenuItem.BorderThicknessProperty, new System.Windows.Thickness(0)));
            style.Setters.Add(new Setter(MenuItem.PaddingProperty, new System.Windows.Thickness(8, 6, 8, 6)));

            // 鼠标悬停触发器 - 只添加边框效果，不改变背景色
            var highlightTrigger = new Trigger
            {
                Property = MenuItem.IsHighlightedProperty,
                Value = true
            };
            highlightTrigger.Setters.Add(new Setter(MenuItem.BorderBrushProperty, hoverBorderBrush));
            highlightTrigger.Setters.Add(new Setter(MenuItem.BorderThicknessProperty, new System.Windows.Thickness(1)));
            highlightTrigger.Setters.Add(new Setter(Control.ForegroundProperty, hoverFgBrush));
            style.Triggers.Add(highlightTrigger);

            // 禁用状态触发器
            var disabledTrigger = new Trigger
            {
                Property = UIElement.IsEnabledProperty,
                Value = false
            };
            disabledTrigger.Setters.Add(new Setter(UIElement.OpacityProperty, 0.5));
            style.Triggers.Add(disabledTrigger);

            return style;
        }

        /// <summary>
        /// 创建带背景色的Separator
        /// </summary>
        private Separator CreateStyledSeparator(System.Windows.Media.SolidColorBrush menuBg)
        {
            var separator = new Separator();
            var style = new Style(typeof(Separator));

            // 根据主题计算分隔线颜色
            var bgColor = menuBg.Color;
            double brightness = (bgColor.R * 0.299 + bgColor.G * 0.587 + bgColor.B * 0.114) / 255.0;
            bool isDarkTheme = brightness < 0.5;

            System.Windows.Media.Color separatorColor;
            if (isDarkTheme)
            {
                // 暗色主题：分隔线变亮
                separatorColor = System.Windows.Media.Color.FromRgb(
                    (byte)Math.Min(255, bgColor.R + 50),
                    (byte)Math.Min(255, bgColor.G + 50),
                    (byte)Math.Min(255, bgColor.B + 50)
                );
            }
            else
            {
                // 亮色主题：分隔线变暗
                separatorColor = System.Windows.Media.Color.FromRgb(
                    (byte)Math.Max(0, bgColor.R - 40),
                    (byte)Math.Max(0, bgColor.G - 40),
                    (byte)Math.Max(0, bgColor.B - 40)
                );
            }

            // 设置分隔线背景色和边距
            style.Setters.Add(new Setter(Separator.BackgroundProperty, menuBg));
            style.Setters.Add(new Setter(Separator.ForegroundProperty, new System.Windows.Media.SolidColorBrush(separatorColor)));
            style.Setters.Add(new Setter(Separator.MarginProperty, new Thickness(0, 2, 0, 2)));
            separator.Style = style;

            return separator;
        }

        /// <summary>
        /// 根据背景色计算合适的次要文字颜色（用于提示信息等）
        /// </summary>
        private System.Windows.Media.Color GetSecondaryTextColor(System.Windows.Media.SolidColorBrush background)
        {
            var bgColor = background.Color;
            double brightness = (bgColor.R * 0.299 + bgColor.G * 0.587 + bgColor.B * 0.114) / 255.0;
            bool isDarkTheme = brightness < 0.5;

            if (isDarkTheme)
            {
                // 暗色主题：使用浅灰色
                return System.Windows.Media.Color.FromRgb(170, 170, 170);
            }
            else
            {
                // 亮色主题：使用深灰色
                return System.Windows.Media.Color.FromRgb(100, 100, 100);
            }
        }

        /// <summary>
        /// 根据背景色计算合适的成功提示颜色
        /// </summary>
        private System.Windows.Media.Color GetSuccessColor(System.Windows.Media.SolidColorBrush background)
        {
            var bgColor = background.Color;
            double brightness = (bgColor.R * 0.299 + bgColor.G * 0.587 + bgColor.B * 0.114) / 255.0;
            bool isDarkTheme = brightness < 0.5;

            if (isDarkTheme)
            {
                // 暗色主题：使用亮绿色
                return System.Windows.Media.Color.FromRgb(100, 220, 100);
            }
            else
            {
                // 亮色主题：使用深绿色
                return System.Windows.Media.Color.FromRgb(34, 139, 34);
            }
        }

        /// <summary>
        /// 初始化赛文菜单（扁平菜单）
        /// </summary>
        private void InitializeRaceMenu()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== InitializeRaceMenu 开始 ===");

                MenuItemRace.Items.Clear();

                // 获取菜单颜色配置
                var menuBg = Colors.FromString(Config.GetString("菜单背景色"));
                var menuFg = Colors.FromString(Config.GetString("菜单字体色"));

                MenuRace.Background = menuBg;
                MenuRace.Foreground = menuFg;
                MenuItemRace.Background = menuBg;
                MenuItemRace.Foreground = menuFg;

                // 菜单打开时触发刷新
                MenuItemRace.SubmenuOpened -= MenuItemRace_SubmenuOpened;
                MenuItemRace.SubmenuOpened += MenuItemRace_SubmenuOpened;
                MenuItemRace.SubmenuClosed -= MenuItemRace_SubmenuClosed;
                MenuItemRace.SubmenuClosed += MenuItemRace_SubmenuClosed;

                var menuItemStyle = CreateMenuItemStyle(menuBg, menuFg);

                // ========== 登录状态区 ==========
                bool isLoggedIn = wenlaiHelper.IsLoggedIn();
                string username = wenlaiHelper.GetCurrentUsername();

                if (isLoggedIn && !string.IsNullOrWhiteSpace(username))
                {
                    MenuItem loginStatusItem = new MenuItem
                    {
                        Header = $"已登录: {username}",
                        Background = menuBg, Foreground = menuFg,
                        IsEnabled = false, Style = menuItemStyle
                    };
                    MenuItemRace.Items.Add(loginStatusItem);

                    // 退出登录
                    MenuItem logoutItem = new MenuItem
                    {
                        Header = "    退出登录",
                        Background = menuBg, Foreground = menuFg,
                        Style = menuItemStyle
                    };
                    logoutItem.Click += MenuItemRaceLogout_Click;
                    MenuItemRace.Items.Add(logoutItem);
                }
                else
                {
                    MenuItem loginItem = new MenuItem
                    {
                        Header = "登录",
                        Background = menuBg, Foreground = menuFg,
                        Style = menuItemStyle
                    };
                    loginItem.Click += MenuItemRaceLogin_Click;
                    MenuItemRace.Items.Add(loginItem);

                    MenuItem registerItem = new MenuItem
                    {
                        Header = "注册",
                        Background = menuBg, Foreground = menuFg,
                        Style = menuItemStyle
                    };
                    registerItem.Click += MenuItemRaceRegister_Click;
                    MenuItemRace.Items.Add(registerItem);
                }

                // 排行榜（不依赖登录状态，始终显示）
                {
                    var serverManager = raceHelperV2.GetServerManager();
                    var servers = serverManager.GetAllServers() ?? new List<TypeSunny.Net.RaceServer>();
                    if (servers.Count > 0 && !string.IsNullOrWhiteSpace(servers[0].Url))
                    {
                        MenuItem leaderboardItem = new MenuItem
                        {
                            Header = "    🏆 排行榜",
                            Background = menuBg, Foreground = menuFg,
                            Tag = $"server_{servers[0].Id}_leaderboard",
                            Style = menuItemStyle
                        };
                        leaderboardItem.Click += MenuItemRaceLeaderboard_Click;
                        MenuItemRace.Items.Add(leaderboardItem);
                    }
                }

                MenuItemRace.Items.Add(CreateStyledSeparator(menuBg));

                // ========== 赛文列表 ==========
                {
                    var serverManager = raceHelperV2.GetServerManager();
                    var servers = serverManager.GetAllServers() ?? new List<TypeSunny.Net.RaceServer>();

                    // 同步账号信息
                    var accountManager = new TypeSunny.Net.AccountSystemManager();
                    foreach (var server in servers)
                    {
                        var wenlaiAccount = accountManager.GetAccount("文来");
                        if (wenlaiAccount != null && !string.IsNullOrWhiteSpace(wenlaiAccount.Username))
                        {
                            string serverDomain = ExtractDomainFromUrl(server.Url);
                            string accountDomain = ExtractDomainFromUrl(wenlaiAccount.Domain);
                            if (serverDomain == accountDomain)
                            {
                                server.UserId = wenlaiAccount.UserId;
                                server.Username = wenlaiAccount.Username;
                                server.DisplayName = wenlaiAccount.DisplayName;
                                if (!string.IsNullOrWhiteSpace(wenlaiAccount.ClientKeyXml))
                                    server.ClientKeyXml = wenlaiAccount.ClientKeyXml;
                            }
                        }
                    }

                    bool hasRaces = false;
                    foreach (var server in servers)
                    {
                        if (server.Races == null || server.Races.Count == 0) continue;
                        foreach (var race in server.Races)
                        {
                            hasRaces = true;

                            string countStr = race.CharCount > 0 ? $"{race.CharCount}字" : "";
                            string diffStr = $"难度{race.DifficultyGroup}";
                            string submitStr = race.AllowResubmit ? "可重复" : "每日一次";
                            string infoText = !string.IsNullOrWhiteSpace(countStr) ? $"{countStr} · {diffStr} · {submitStr}" : $"{diffStr} · {submitStr}";

                            // 赛文名称
                            MenuItem raceItem = new MenuItem
                            {
                                Header = $"{race.Name}  ({infoText})",
                                Background = menuBg, Foreground = menuFg,
                                IsEnabled = false,
                                FontWeight = System.Windows.FontWeights.SemiBold,
                                Style = menuItemStyle
                            };
                            MenuItemRace.Items.Add(raceItem);

                            // 发文
                            MenuItem loadArticleItem = new MenuItem
                            {
                                Background = menuBg, Foreground = menuFg,
                                Tag = $"server_{server.Id}_race_{race.Id}_load",
                                Style = menuItemStyle
                            };

                            if (!race.AllowResubmit && server.IsTodaySubmitted(race.Id))
                            {
                                loadArticleItem.Header = "    今日已完成";
                                var successColor = GetSuccessColor(menuBg);
                                loadArticleItem.Foreground = new System.Windows.Media.SolidColorBrush(successColor);
                                loadArticleItem.IsEnabled = false;
                            }
                            else
                            {
                                loadArticleItem.Header = "    发文";
                                loadArticleItem.Click += MenuItemRaceServerLoadArticle_Click;
                            }
                            MenuItemRace.Items.Add(loadArticleItem);

                            MenuItemRace.Items.Add(CreateStyledSeparator(menuBg));
                        }
                    }

                    if (!hasRaces)
                    {
                        MenuItem noRaceItem = new MenuItem
                        {
                            Header = "暂无赛文",
                            Background = menuBg, Foreground = menuFg,
                            IsEnabled = false, Style = menuItemStyle
                        };
                        MenuItemRace.Items.Add(noRaceItem);
                        MenuItemRace.Items.Add(CreateStyledSeparator(menuBg));
                    }
                }

                // ========== 刷新赛文列表 ==========
                MenuItem refreshItem = new MenuItem
                {
                    Header = "🔄 刷新赛文列表",
                    Background = menuBg, Foreground = menuFg,
                    Style = menuItemStyle
                };
                refreshItem.Click += MenuItemRefreshRaceList_Click;
                MenuItemRace.Items.Add(refreshItem);

                // ========== 分隔符 + 传统赛文源（锦标赛、极速杯） ==========
                MenuItemRace.Items.Add(CreateStyledSeparator(menuBg));

                var raceConfigs = RaceConfig.GetRaceConfigs();

                foreach (var config in raceConfigs)
                {
                    if (!config.Enabled)
                        continue;

                    // 跳过新赛文API（已经在上面处理了）
                    if (config.Type == "race")
                        continue;

                    // 创建一级菜单项（赛文源名称，如"锦标赛"、"极速杯"）
                    MenuItem parentMenu = new MenuItem();
                    string parentIcon = config.Name.Contains("锦标赛") ? "🏅 " : (config.Name.Contains("极速") ? "⚡ " : "🏁 ");
                    parentMenu.Header = parentIcon + config.Name;

                    // 应用菜单颜色和样式
                    parentMenu.Background = menuBg;
                    parentMenu.Foreground = menuFg;
                    parentMenu.Style = menuItemStyle;

                    // 为每个功能创建子菜单项
                    foreach (var feature in config.Features)
                    {
                        MenuItem subMenu = new MenuItem();
                        string featureIcon = feature switch
                        {
                            "载文" => "📄 ",
                            "登录" => "🔑 ",
                            "排行榜" => "🏆 ",
                            _ => ""
                        };
                        subMenu.Header = featureIcon + feature;
                        subMenu.Tag = config.Type + "_" + feature;

                        subMenu.Background = menuBg;
                        subMenu.Foreground = menuFg;
                        subMenu.Style = menuItemStyle;

                        switch (feature)
                        {
                            case "载文":
                                if (config.Type == "jbs")
                                    subMenu.Click += MenuItemLoadArticle_Click;
                                else if (config.Type == "jisucup")
                                    subMenu.Click += MenuItemJiSuLoadArticle_Click;
                                raceMenuItems[config.Type + "_loadArticle"] = subMenu;
                                break;
                            case "登录":
                                if (config.Type == "jbs")
                                    subMenu.Click += MenuItemLogin_Click;
                                else if (config.Type == "jisucup")
                                    subMenu.Click += MenuItemJiSuLogin_Click;
                                raceMenuItems[config.Type + "_login"] = subMenu;
                                break;
                            case "排行榜":
                                if (config.Type == "jbs")
                                    subMenu.Click += MenuItemRanking_Click;
                                else if (config.Type == "jisucup")
                                    subMenu.Click += MenuItemJiSuRanking_Click;
                                raceMenuItems[config.Type + "_leaderboard"] = subMenu;
                                break;
                        }

                        parentMenu.Items.Add(subMenu);
                    }

                    MenuItemRace.Items.Add(parentMenu);
                }

                // 菜单创建完成后，更新Helper中的菜单项引用
                if (jbsHelper != null)
                {
                    jbsHelper.SetMenuItems(
                        raceMenuItems.ContainsKey("jbs_login") ? raceMenuItems["jbs_login"] : null,
                        raceMenuItems.ContainsKey("jbs_loadArticle") ? raceMenuItems["jbs_loadArticle"] : null
                    );
                    jbsHelper.UpdateLoginStatus();
                    jbsHelper.UpdateArticleButtonStatus();
                }

                if (jiSuCupHelper != null)
                {
                    jiSuCupHelper.SetMenuItems(
                        raceMenuItems.ContainsKey("jisucup_login") ? raceMenuItems["jisucup_login"] : null,
                        raceMenuItems.ContainsKey("jisucup_loadArticle") ? raceMenuItems["jisucup_loadArticle"] : null
                    );
                    jiSuCupHelper.UpdateLoginStatus();
                }

                System.Diagnostics.Debug.WriteLine("=== InitializeRaceMenu 结束 ===");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ 初始化赛文菜单失败: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");

                try
                {
                    MenuItem errorItem = new MenuItem
                    {
                        Header = "⚠️ 菜单加载失败，请检查配置",
                        IsEnabled = false
                    };
                    MenuItemRace.Items.Add(errorItem);
                }
                catch { }
            }
        }

        /// <summary>
        /// 初始化文来右键菜单
        /// </summary>
        private void InitializeWenlaiMenu()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== InitializeWenlaiMenu 开始 ===");

                // 获取菜单颜色配置
                var menuBg = Colors.FromString(Config.GetString("菜单背景色"));
                var menuFg = Colors.FromString(Config.GetString("菜单字体色"));

                // 为ContextMenu设置背景色和前景色
                MenuWenlai.Background = menuBg;
                MenuWenlai.Foreground = menuFg;


                // 创建MenuItem样式以支持暗色主题
                var menuItemStyle = CreateMenuItemStyle(menuBg, menuFg);

                // 创建全局Menu样式，确保子菜单Popup也使用正确的背景色
                var menuStyle = new Style(typeof(Menu));
                menuStyle.Setters.Add(new Setter(Menu.BackgroundProperty, menuBg));
                menuStyle.Setters.Add(new Setter(Menu.ForegroundProperty, menuFg));

                // 创建全局ContextMenu样式
                var contextMenuStyle = new Style(typeof(ContextMenu));
                contextMenuStyle.Setters.Add(new Setter(ContextMenu.BackgroundProperty, menuBg));
                contextMenuStyle.Setters.Add(new Setter(ContextMenu.ForegroundProperty, menuFg));

                // 将样式添加到Window资源中
                this.Resources["MenuStyle"] = menuStyle;
                this.Resources["ContextMenuStyle"] = contextMenuStyle;

                // 清空旧菜单
                MenuWenlai.Items.Clear();

                // 检查是否已登录
                System.Diagnostics.Debug.WriteLine("  调用 IsLoggedIn()...");
                bool isLoggedIn = wenlaiHelper.IsLoggedIn();
                System.Diagnostics.Debug.WriteLine($"  IsLoggedIn() 返回: {isLoggedIn}");

                System.Diagnostics.Debug.WriteLine("  调用 GetCurrentUsername()...");
                string username = wenlaiHelper.GetCurrentUsername();
                System.Diagnostics.Debug.WriteLine($"  GetCurrentUsername() 返回: '{username}'");
                System.Diagnostics.Debug.WriteLine($"  string.IsNullOrWhiteSpace(username): {string.IsNullOrWhiteSpace(username)}");
                System.Diagnostics.Debug.WriteLine($"  最终条件判断: isLoggedIn={isLoggedIn}, !IsNullOrWhiteSpace={!string.IsNullOrWhiteSpace(username)}, 结果={(isLoggedIn && !string.IsNullOrWhiteSpace(username))}");

                if (isLoggedIn && !string.IsNullOrWhiteSpace(username))
                {
                    System.Diagnostics.Debug.WriteLine("  ✓ 进入已登录分支，显示用户名菜单");
                    // 已登录：显示用户名
                    MenuItem loginStatusItem = new MenuItem
                    {
                        Header = $"✅ {username}",
                        Background = menuBg,
                        Foreground = menuFg,
                        IsEnabled = false,
                        Style = menuItemStyle
                    };
                    MenuWenlai.Items.Add(loginStatusItem);
                    MenuWenlai.Items.Add(CreateStyledSeparator(menuBg));

                    // 退出登录
                    MenuItem logoutItem = new MenuItem
                    {
                        Header = "🚪 退出登录",
                        Background = menuBg,
                        Foreground = menuFg,
                        Style = menuItemStyle
                    };
                    logoutItem.Click += MenuItemWenlaiLogout_Click;
                    MenuWenlai.Items.Add(logoutItem);

                    MenuWenlai.Items.Add(CreateStyledSeparator(menuBg));
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("  ⚠ 进入未登录分支，显示登录/注册菜单");
                    // 未登录：显示登录和注册
                    MenuItem loginItem = new MenuItem
                    {
                        Header = "🔑 登录",
                        Background = menuBg,
                        Foreground = menuFg,
                        Style = menuItemStyle
                    };
                    loginItem.Click += MenuItemWenlaiLogin_Click;
                    MenuWenlai.Items.Add(loginItem);

                    MenuItem registerItem = new MenuItem
                    {
                        Header = "📝 注册",
                        Background = menuBg,
                        Foreground = menuFg,
                        Style = menuItemStyle
                    };
                    registerItem.Click += MenuItemWenlaiRegister_Click;
                    MenuWenlai.Items.Add(registerItem);

                    MenuWenlai.Items.Add(CreateStyledSeparator(menuBg));
                }

                // 服务器设置（始终显示）
                MenuItem serverSettingsItem = new MenuItem
                {
                    Header = "⚙️ 服务器设置",
                    Background = menuBg,
                    Foreground = menuFg,
                    Style = menuItemStyle
                };
                serverSettingsItem.Click += MenuItemWenlaiServerSettings_Click;
                MenuWenlai.Items.Add(serverSettingsItem);

                // 选择难度（需要登录，作为子菜单）
                MenuItem difficultyItem = new MenuItem
                {
                    Header = "🎯 选择难度",
                    Background = menuBg,
                    Foreground = menuFg,
                    Style = menuItemStyle,
                    IsEnabled = isLoggedIn && !string.IsNullOrWhiteSpace(username)  // 只有登录时才能点击
                };

                if (isLoggedIn && !string.IsNullOrWhiteSpace(username))
                {
                    // 同步加载难度列表（使用缓存，没有缓存则返回空列表）
                    var difficulties = ArticleFetcher.GetDifficulties();

                    // 获取当前选中的难度
                    string currentDifficulty = Config.GetString("文来难度") ?? "";
                    int currentDifficultyId = 0;
                    if (!string.IsNullOrEmpty(currentDifficulty))
                    {
                        int.TryParse(currentDifficulty, out currentDifficultyId);
                    }

                    // 查找当前难度名称
                    string currentDifficultyName = "随机";
                    if (currentDifficultyId > 0 && difficulties.Count > 0)
                    {
                        var currentDiff = difficulties.FirstOrDefault(d => d.Id == currentDifficultyId);
                        if (currentDiff != null)
                        {
                            currentDifficultyName = currentDiff.Name;
                        }
                    }

                    // 更新主菜单项，显示当前选择的难度
                    difficultyItem.Header = $"🎯 选择难度 [{currentDifficultyName}]";

                    // 始终添加"刷新"选项（最顶部，无论是否有缓存数据）
                    MenuItem refreshItem = new MenuItem
                    {
                        Header = "🔄 刷新难度列表",
                        Background = menuBg,
                        Foreground = menuFg,
                        Style = menuItemStyle
                    };
                    refreshItem.Click += async (s, args) =>
                    {
                        ArticleFetcher.ClearDifficultyCache();
                        // 异步加载最新数据
                        var newDifficulties = await ArticleFetcher.GetDifficultiesAsync();
                        InitializeWenlaiMenu();  // 重新加载菜单
                    };
                    difficultyItem.Items.Add(refreshItem);

                    // 如果有难度列表，添加难度选项
                    if (difficulties.Count > 0)
                    {
                        // 添加分隔线
                        difficultyItem.Items.Add(CreateStyledSeparator(menuBg));

                        // 计算总段数
                        int totalCount = difficulties.Sum(d => d.Count);

                        // 添加"随机"选项
                        MenuItem randomItem = new MenuItem
                        {
                            Header = $"随机 ({totalCount}段){(currentDifficultyId == 0 ? " ✓" : "")}",
                            Background = menuBg,
                            Foreground = menuFg,
                            Style = menuItemStyle,
                            Tag = 0
                        };
                        randomItem.Click += (s, args) =>
                        {
                            Config.Set("文来难度", "");
                            InitializeWenlaiMenu();
                        };
                        difficultyItem.Items.Add(randomItem);

                        // 按难度ID排序并添加
                        foreach (var diff in difficulties.OrderBy(d => d.Id))
                        {
                            // 跳过文章数为0的难度
                            if (diff.Count == 0)
                                continue;

                            MenuItem diffMenuItem = new MenuItem
                            {
                                Header = $"{diff.Name} ({diff.Count}段){(diff.Id == currentDifficultyId ? " ✓" : "")}",
                                Background = menuBg,
                                Foreground = menuFg,
                                Style = menuItemStyle,
                                Tag = diff.Id
                            };
                            diffMenuItem.Click += (s, args) =>
                            {
                                Config.Set("文来难度", diff.Id.ToString());
                                InitializeWenlaiMenu();
                            };
                            difficultyItem.Items.Add(diffMenuItem);
                        }
                    }
                    else
                    {
                        // 没有缓存数据，显示加载提示
                        difficultyItem.Items.Add(CreateStyledSeparator(menuBg));
                        MenuItem loadingItem = new MenuItem
                        {
                            Header = "⏳ 加载难度数据...",
                            Background = menuBg,
                            Foreground = menuFg,
                            Style = menuItemStyle,
                            IsEnabled = false
                        };
                        difficultyItem.Items.Add(loadingItem);

                        // 后台异步加载
                        System.Threading.Tasks.Task.Run(async () =>
                        {
                            var loadedDifficulties = await ArticleFetcher.GetDifficultiesAsync();
                            if (loadedDifficulties != null && loadedDifficulties.Count > 0)
                            {
                                this.Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    InitializeWenlaiMenu();  // 重新加载菜单
                                }));
                            }
                        });
                    }
                }
                MenuWenlai.Items.Add(difficultyItem);

                // 选择分类（需要登录，作为子菜单）
                MenuItem categoryItem = new MenuItem
                {
                    Header = "📂 选择分类",
                    Background = menuBg,
                    Foreground = menuFg,
                    Style = menuItemStyle,
                    IsEnabled = isLoggedIn && !string.IsNullOrWhiteSpace(username)
                };

                if (isLoggedIn && !string.IsNullOrWhiteSpace(username))
                {
                    var categories = ArticleFetcher.GetCategories();
                    string currentCategory = Config.GetString("文来分类") ?? "";
                    string currentCategoryName = "全部";
                    if (!string.IsNullOrEmpty(currentCategory) && categories.Count > 0)
                    {
                        var cur = categories.FirstOrDefault(c => c.Code == currentCategory);
                        if (cur != null) currentCategoryName = cur.Name;
                    }
                    categoryItem.Header = $"📂 选择分类 [{currentCategoryName}]";

                    // 刷新
                    MenuItem refreshCatItem = new MenuItem
                    {
                        Header = "🔄 刷新分类列表",
                        Background = menuBg,
                        Foreground = menuFg,
                        Style = menuItemStyle
                    };
                    refreshCatItem.Click += async (s, args) =>
                    {
                        ArticleFetcher.ClearCategoryCache();
                        await ArticleFetcher.GetCategoriesAsync();
                        InitializeWenlaiMenu();
                    };
                    categoryItem.Items.Add(refreshCatItem);

                    if (categories.Count > 0)
                    {
                        categoryItem.Items.Add(CreateStyledSeparator(menuBg));

                        // "全部"选项
                        MenuItem allItem = new MenuItem
                        {
                            Header = $"全部{(string.IsNullOrEmpty(currentCategory) ? " ✓" : "")}",
                            Background = menuBg,
                            Foreground = menuFg,
                            Style = menuItemStyle
                        };
                        allItem.Click += (s, args) =>
                        {
                            Config.Set("文来分类", "");
                            InitializeWenlaiMenu();
                        };
                        categoryItem.Items.Add(allItem);

                        foreach (var cat in categories)
                        {
                            MenuItem catMenuItem = new MenuItem
                            {
                                Header = $"{cat.Name}{(cat.Code == currentCategory ? " ✓" : "")}",
                                Background = menuBg,
                                Foreground = menuFg,
                                Style = menuItemStyle,
                                Tag = cat.Code
                            };
                            catMenuItem.Click += (s, args) =>
                            {
                                Config.Set("文来分类", cat.Code);
                                InitializeWenlaiMenu();
                            };
                            categoryItem.Items.Add(catMenuItem);
                        }
                    }
                    else
                    {
                        categoryItem.Items.Add(CreateStyledSeparator(menuBg));
                        MenuItem loadingCatItem = new MenuItem
                        {
                            Header = "⏳ 加载分类数据...",
                            Background = menuBg,
                            Foreground = menuFg,
                            Style = menuItemStyle,
                            IsEnabled = false
                        };
                        categoryItem.Items.Add(loadingCatItem);

                        System.Threading.Tasks.Task.Run(async () =>
                        {
                            var loaded = await ArticleFetcher.GetCategoriesAsync();
                            if (loaded != null && loaded.Count > 0)
                            {
                                this.Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    InitializeWenlaiMenu();
                                }));
                            }
                        });
                    }
                }
                MenuWenlai.Items.Add(categoryItem);

                // 成绩统计（始终显示，放在最下面）
                MenuWenlai.Items.Add(CreateStyledSeparator(menuBg));
                MenuItem statisticsItem = new MenuItem
                {
                    Header = "📊 成绩统计",
                    Background = menuBg,
                    Foreground = menuFg,
                    Style = menuItemStyle
                };
                statisticsItem.Click += MenuItemWenlaiStatistics_Click;
                MenuWenlai.Items.Add(statisticsItem);

                // 输出菜单项列表，验证菜单是否正确构建
                System.Diagnostics.Debug.WriteLine($"  菜单项数量: {MenuWenlai.Items.Count}");
                for (int i = 0; i < MenuWenlai.Items.Count; i++)
                {
                    var item = MenuWenlai.Items[i];
                    if (item is MenuItem menuItem)
                    {
                        System.Diagnostics.Debug.WriteLine($"    [{i}] MenuItem: Header='{menuItem.Header}', IsEnabled={menuItem.IsEnabled}");
                    }
                    else if (item is Separator)
                    {
                        System.Diagnostics.Debug.WriteLine($"    [{i}] Separator");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"    [{i}] {item.GetType().Name}");
                    }
                }

                System.Diagnostics.Debug.WriteLine("=== InitializeWenlaiMenu 结束 ===");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ 初始化文来菜单失败: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// 文来菜单 - 登录
        /// </summary>
        private void MenuItemWenlaiLogin_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine(">>> MenuItemWenlaiLogin_Click 开始");
            System.Diagnostics.Debug.WriteLine("  调用 ShowLoginDialog...");
            wenlaiHelper.ShowLoginDialog(this);
            System.Diagnostics.Debug.WriteLine("  ShowLoginDialog 返回");

            // 重新初始化文来菜单以更新登录状态
            System.Diagnostics.Debug.WriteLine("  调用 InitializeWenlaiMenu...");
            InitializeWenlaiMenu();
            System.Diagnostics.Debug.WriteLine("  InitializeWenlaiMenu 完成");

            // 如果登录成功，且文来和赛文同域名，赛文也会自动同步登录状态
            // 需要刷新赛文菜单以显示最新状态
            System.Diagnostics.Debug.WriteLine("  检查是否已登录...");
            if (wenlaiHelper.IsLoggedIn())
            {
                System.Diagnostics.Debug.WriteLine("  已登录，同步等待难度数据加载...");
                // 登录成功后，同步等待难度数据加载完成
                Task.Run(async () =>
                {
                    await ArticleFetcher.GetDifficultiesAsync();
                }).GetAwaiter().GetResult();

                System.Diagnostics.Debug.WriteLine("  调用 InitializeRaceMenu...");
                InitializeRaceMenu();
                System.Diagnostics.Debug.WriteLine("  InitializeRaceMenu 完成");

                // 通知所有打开的设置窗口刷新文来难度数据
                NotifyConfigWindowsRefreshWenlai();
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("  未登录，跳过 InitializeRaceMenu");
            }
            System.Diagnostics.Debug.WriteLine("<<< MenuItemWenlaiLogin_Click 结束");
        }

        /// <summary>
        /// 通知所有打开的设置窗口刷新文来难度数据
        /// </summary>
        private void NotifyConfigWindowsRefreshWenlai()
        {
            try
            {
                // 使用 Task.Run 避免阻塞 UI 线程
                Task.Run(async () =>
                {
                    foreach (Window window in Application.Current.Windows)
                    {
                        if (window is WinConfig configWindow)
                        {
                            System.Diagnostics.Debug.WriteLine($"[MainWindow] 通知设置窗口刷新文来数据");

                            // 在窗口的 Dispatcher 上执行
                            await configWindow.Dispatcher.InvokeAsync(async () =>
                            {
                                try
                                {
                                    await configWindow.ReloadWenlaiDifficultyConfig();
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[MainWindow] 刷新设置窗口文来数据失败: {ex.Message}");
                                }
                            }, System.Windows.Threading.DispatcherPriority.Normal);
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] 通知设置窗口刷新失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 文来菜单 - 注册
        /// </summary>
        private void MenuItemWenlaiRegister_Click(object sender, RoutedEventArgs e)
        {
            wenlaiHelper.ShowRegisterDialog(this);

            // 重新初始化文来菜单以更新登录状态
            InitializeWenlaiMenu();

            // 如果注册并登录成功，且文来和赛文同域名，赛文也会自动同步登录状态
            // 需要刷新赛文菜单以显示最新状态
            if (wenlaiHelper.IsLoggedIn())
            {
                // 注册成功后，同步等待难度数据加载完成
                Task.Run(async () =>
                {
                    await ArticleFetcher.GetDifficultiesAsync();
                }).GetAwaiter().GetResult();

                InitializeRaceMenu();

                // 通知所有打开的设置窗口刷新文来难度数据
                NotifyConfigWindowsRefreshWenlai();
            }
        }

        /// <summary>
        /// 文来菜单 - 退出登录（同时退出赛文）
        /// </summary>
        private void MenuItemWenlaiLogout_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("退出登录将同时退出文来和赛文。\n确定要退出吗？", "确认", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                wenlaiHelper.Logout();

                // 同时清除赛文账号
                var accountManager = new TypeSunny.Net.AccountSystemManager();
                accountManager.ClearAccount("赛文");

                // 清除赛文服务器登录信息
                var serverManager = raceHelperV2.GetServerManager();
                foreach (var server in serverManager.GetAllServers())
                {
                    server.UserId = -1;
                    server.Username = "";
                    server.DisplayName = "";
                    server.ServerPublicKey = "";
                    server.KeyId = "";
                    server.SessionNonce = "";
                }
                serverManager.SaveToConfig();

                // 刷新两个菜单
                InitializeWenlaiMenu();
                InitializeRaceMenu();

                // 通知所有打开的设置窗口刷新文来难度数据
                NotifyConfigWindowsRefreshWenlai();

                MessageBox.Show("已退出登录", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// 文来菜单 - 服务器设置
        /// </summary>
        private void MenuItemWenlaiServerSettings_Click(object sender, RoutedEventArgs e)
        {
            wenlaiHelper.ShowServerSettingsDialog(this);
            // 服务器地址改变后，清除旧缓存并重新加载
            ArticleFetcher.ClearDifficultyCache();
            if (wenlaiHelper.IsLoggedIn())
            {
                Task.Run(async () =>
                {
                    await ArticleFetcher.GetDifficultiesAsync();
                });
            }
            // 重新初始化菜单
            InitializeWenlaiMenu();
        }

        private void MenuItemWenlaiStatistics_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var winStats = new WinStatistics(this);
                winStats.Show();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"打开成绩统计窗口失败: {ex.Message}\n堆栈: {ex.StackTrace}");
                MessageBox.Show($"打开成绩统计窗口失败: {ex.Message}\n\n堆栈: {ex.StackTrace}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MenuItemLoadArticle_Click(object sender, RoutedEventArgs e) // 锦标赛载文
        {
            // 检查是否已登录
            string displayName = Config.GetString("极速显示名称");
            if (string.IsNullOrWhiteSpace(displayName))
            {
                MessageBox.Show("请先登录锦标赛账号", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 检查输入法是否已设置
            string inputMethod = Config.GetString("赛文输入法");
            if (string.IsNullOrWhiteSpace(inputMethod))
            {
                MessageBox.Show("请先填写赛文输入法名称\n（设置 → 赛文 → 赛文输入法）", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            jbs = jbsHelper.GetInstance();
            string article = jbs.GetArticle();

            // 检查是否今日已获取
            if (article.StartsWith("TODAY_LIMIT:"))
            {
                jbsHelper.MarkArticleLoaded();
                string message = article.Substring("TODAY_LIMIT:".Length);
                MessageBox.Show(message, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (article != null && article.Length > 0)
            {
                // 打字模式控件已移到设置窗口
                LoadText(article, RetypeType.first, TxtSource.jbs);

                // 标记今日已载文
                jbsHelper.MarkArticleLoaded();
            }
            else
            {
                MessageBox.Show("获取赛文失败，请检查网络连接或稍后重试", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MenuItemLogin_Click(object sender, RoutedEventArgs e) // 锦标赛登录
        {
            jbsHelper.ShowLoginDialog(this);
            jbs = jbsHelper.GetInstance();
        }

        private void MenuItemRanking_Click(object sender, RoutedEventArgs e) // 锦标赛排行榜
        {
            jbsHelper.OpenRanking();
        }

        // 极速杯 - 载文
        private void MenuItemJiSuLoadArticle_Click(object sender, RoutedEventArgs e)
        {
            // 检查是否已登录
            string displayName = Config.GetString("极速杯显示名称");
            if (string.IsNullOrWhiteSpace(displayName))
            {
                MessageBox.Show("请先登录极速杯账号", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 检查输入法是否已设置
            string inputMethod = Config.GetString("赛文输入法");
            if (string.IsNullOrWhiteSpace(inputMethod))
            {
                MessageBox.Show("请先填写赛文输入法名称\n（设置 → 赛文 → 赛文输入法）", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            jiSuCup = jiSuCupHelper.GetInstance();
            string article = jiSuCup.GetArticle();

            if (!string.IsNullOrWhiteSpace(article))
            {
                // 打字模式控件已移到设置窗口
                LoadText(article, RetypeType.first, TxtSource.jisucup);
            }
            else
            {
                MessageBox.Show("获取赛文失败，请检查网络连接或稍后重试", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 极速杯 - 登录
        private void MenuItemJiSuLogin_Click(object sender, RoutedEventArgs e)
        {
            jiSuCupHelper.ShowLoginDialog(this);
            jiSuCup = jiSuCupHelper.GetInstance();
        }

        // 极速杯 - 排行榜
        private void MenuItemJiSuRanking_Click(object sender, RoutedEventArgs e)
        {
            jiSuCupHelper.OpenRanking();
        }

        // 赛文API - 登录（调用文来登录）
        private void MenuItemRaceLogin_Click(object sender, RoutedEventArgs e)
        {
            wenlaiHelper.ShowLoginDialog(this);

            // 登录后刷新两个菜单
            InitializeWenlaiMenu();
            if (wenlaiHelper.IsLoggedIn())
            {
                Task.Run(async () =>
                {
                    await ArticleFetcher.GetDifficultiesAsync();
                }).GetAwaiter().GetResult();

                InitializeRaceMenu();
                raceMenuLoaded = false;
                NotifyConfigWindowsRefreshWenlai();
            }
        }

        // 赛文API - 注册（调用文来注册）
        private void MenuItemRaceRegister_Click(object sender, RoutedEventArgs e)
        {
            wenlaiHelper.ShowRegisterDialog(this);

            // 注册后刷新两个菜单
            InitializeWenlaiMenu();
            if (wenlaiHelper.IsLoggedIn())
            {
                Task.Run(async () =>
                {
                    await ArticleFetcher.GetDifficultiesAsync();
                }).GetAwaiter().GetResult();

                InitializeRaceMenu();
                raceMenuLoaded = false;
                NotifyConfigWindowsRefreshWenlai();
            }
        }

        // 赛文API - 退出登录（同时退出文来和赛文）
        private void MenuItemRaceLogout_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("退出登录将同时退出文来和赛文。\n确定要退出吗？", "确认", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
                return;

            // 退出文来
            wenlaiHelper.Logout();

            // 清除赛文相关账号
            var accountManager = new TypeSunny.Net.AccountSystemManager();
            accountManager.ClearAccount("赛文");

            // 清除赛文服务器登录信息
            var serverManager = raceHelperV2.GetServerManager();
            foreach (var server in serverManager.GetAllServers())
            {
                server.UserId = -1;
                server.Username = "";
                server.DisplayName = "";
                server.ServerPublicKey = "";
                server.KeyId = "";
                server.SessionNonce = "";
            }
            serverManager.SaveToConfig();

            // 刷新两个菜单
            InitializeWenlaiMenu();
            raceMenuLoaded = false;
            InitializeRaceMenu();
            NotifyConfigWindowsRefreshWenlai();

            MessageBox.Show("已退出登录", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // 赛文API - 载文
        private async void MenuItemRaceLoadArticle_Click(object sender, RoutedEventArgs e)
        {
            string article = await raceHelper.LoadDailyArticle();

            if (article != null && article.Length > 0 && !article.StartsWith("载文失败") && !article.StartsWith("未登录"))
            {
                LoadText(article, RetypeType.first, TxtSource.raceApi);
            }
            else
            {
                MessageBox.Show(article, "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // ========== 新版赛文API事件处理（支持多服务器） ==========

        // 赛文服务器 - 登录（直接调用文来登录）
        private void MenuItemRaceServerLogin_Click(object sender, RoutedEventArgs e)
        {
            wenlaiHelper.ShowLoginDialog(this);

            InitializeWenlaiMenu();
            if (wenlaiHelper.IsLoggedIn())
            {
                Task.Run(async () =>
                {
                    await ArticleFetcher.GetDifficultiesAsync();
                }).GetAwaiter().GetResult();

                InitializeRaceMenu();
                NotifyConfigWindowsRefreshWenlai();
            }
        }

        // 赛文服务器 - 注册（直接调用文来注册）
        private void MenuItemRaceServerRegister_Click(object sender, RoutedEventArgs e)
        {
            wenlaiHelper.ShowRegisterDialog(this);

            InitializeWenlaiMenu();
            if (wenlaiHelper.IsLoggedIn())
            {
                Task.Run(async () =>
                {
                    await ArticleFetcher.GetDifficultiesAsync();
                }).GetAwaiter().GetResult();

                InitializeRaceMenu();
                NotifyConfigWindowsRefreshWenlai();
            }
        }

        // 赛文服务器 - 退出登录（同时退出文来和赛文）
        private void MenuItemRaceServerLogout_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("退出登录将同时退出文来和赛文。\n确定要退出吗？", "确认", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
                return;

            wenlaiHelper.Logout();

            var accountManager = new TypeSunny.Net.AccountSystemManager();
            accountManager.ClearAccount("赛文");

            var serverManager = raceHelperV2.GetServerManager();
            foreach (var server in serverManager.GetAllServers())
            {
                server.UserId = -1;
                server.Username = "";
                server.DisplayName = "";
                server.ServerPublicKey = "";
                server.KeyId = "";
                server.SessionNonce = "";
            }
            serverManager.SaveToConfig();

            InitializeWenlaiMenu();
            InitializeRaceMenu();
            NotifyConfigWindowsRefreshWenlai();

            MessageBox.Show("已退出登录", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // 赛文服务器 - 载文
        private async void MenuItemRaceServerLoadArticle_Click(object sender, RoutedEventArgs e)
        {
            // 检查输入法是否已设置
            string inputMethod = Config.GetString("赛文输入法");
            if (string.IsNullOrWhiteSpace(inputMethod))
            {
                MessageBox.Show("请先填写赛文输入法名称\n（设置 → 赛文 → 赛文输入法）", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MenuItem menuItem = sender as MenuItem;
            if (menuItem == null || menuItem.Tag == null)
                return;

            string tag = menuItem.Tag.ToString();
            // Tag格式: "server_{serverId}_race_{raceId}_load"
            var parts = tag.Split('_');
            if (parts.Length < 5)
                return;

            string serverId = parts[1];
            int raceId = int.Parse(parts[3]);

            string article = await raceHelperV2.LoadDailyArticle(serverId, raceId);

            // 先检查是否是错误消息（以"载文失败"或"请先登录"开头）
            if (!string.IsNullOrWhiteSpace(article) && (article.StartsWith("载文失败") || article.StartsWith("请先登录")))
            {
                MessageBox.Show(article, "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 检查文章内容是否有效
            if (string.IsNullOrWhiteSpace(article) || article.Length < 10)
            {
                MessageBox.Show($"文章内容异常: {article ?? "空"}", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 文章内容有效，加载到打字区
            // 保存当前服务器和赛文ID，用于提交成绩
            StateManager.CurrentRaceServerId = serverId;
            StateManager.CurrentRaceId = raceId;

            // 标记今天已载文（用于不可重复提交的赛文）
            var serverManager = raceHelperV2.GetServerManager();
            var server = serverManager.GetAllServers().Find(s => s.Id == serverId);
            if (server != null)
            {
                server.MarkTodaySubmitted(raceId);
                // 刷新菜单以更新"今日已完成"状态
                RefreshRaceMenu();
            }

            LoadText(article, RetypeType.first, TxtSource.raceApi);
        }

        // 赛文服务器 - 排行榜
        private void MenuItemRaceLeaderboard_Click(object sender, RoutedEventArgs e)
        {
            MenuItem menuItem = sender as MenuItem;
            if (menuItem == null || menuItem.Tag == null)
                return;

            string tag = menuItem.Tag.ToString();
            // Tag格式: "server_{serverId}_leaderboard"
            var parts = tag.Split('_');
            if (parts.Length < 3)
                return;

            string serverId = parts[1];

            // 获取服务器信息
            var serverManager = raceHelperV2.GetServerManager();
            var server = serverManager.GetAllServers().Find(s => s.Id == serverId);
            if (server == null || string.IsNullOrWhiteSpace(server.Url))
            {
                MessageBox.Show("服务器不存在", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 直接用浏览器打开赛文服务器地址
            try
            {
                System.Diagnostics.Process.Start(server.Url.TrimEnd('/') + "/");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开排行榜失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 添加赛文服务器
        private async void MenuItemAddRaceServer_Click(object sender, RoutedEventArgs e)
        {
            // 创建简单的输入对话框
            var dialog = new Window
            {
                Title = "添加赛文服务器",
                Width = 400,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            var grid = new Grid { Margin = new Thickness(20) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var lblName = new Label { Content = "服务器名称:" };
            Grid.SetRow(lblName, 0);
            grid.Children.Add(lblName);

            var txtName = new TextBox
            {
                Text = "赛文服务器",
                Padding = new Thickness(5),
                Margin = new Thickness(90, 0, 0, 0)
            };
            Grid.SetRow(txtName, 0);
            grid.Children.Add(txtName);

            var lblUrl = new Label { Content = "服务器地址:" };
            Grid.SetRow(lblUrl, 2);
            grid.Children.Add(lblUrl);

            var txtUrl = new TextBox
            {
                Text = Config.GetString("赛文服务器地址") ?? "https://qingfawen.fcxxz.com/",
                Padding = new Thickness(5),
                Margin = new Thickness(90, 0, 0, 0)
            };
            Grid.SetRow(txtUrl, 2);
            grid.Children.Add(txtUrl);

            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetRow(btnPanel, 4);

            var btnAdd = new Button { Content = "添加", Width = 80, Height = 30, Margin = new Thickness(0, 0, 10, 0) };
            var btnCancel = new Button { Content = "取消", Width = 80, Height = 30 };

            btnAdd.Click += async (s, args) =>
            {
                if (string.IsNullOrWhiteSpace(txtName.Text) || string.IsNullOrWhiteSpace(txtUrl.Text))
                {
                    MessageBox.Show("请填写完整信息", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                btnAdd.IsEnabled = false;
                btnAdd.Content = "添加中...";

                try
                {
                    var serverManager = raceHelperV2.GetServerManager();
                    var server = serverManager.AddServer(txtName.Text, txtUrl.Text);

                    // 刷新该服务器的赛文列表
                    bool success = await serverManager.RefreshServerRaces(server.Id);

                    if (success)
                    {
                        MessageBox.Show($"服务器添加成功！共找到 {server.Races.Count} 个赛文", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                        dialog.DialogResult = true;
                        dialog.Close();

                        // 刷新菜单
                        RefreshRaceMenu();
                    }
                    else
                    {
                        MessageBox.Show("无法连接到服务器或获取赛文列表失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        btnAdd.IsEnabled = true;
                        btnAdd.Content = "添加";
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"添加失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    btnAdd.IsEnabled = true;
                    btnAdd.Content = "添加";
                }
            };

            btnCancel.Click += (s, args) =>
            {
                dialog.DialogResult = false;
                dialog.Close();
            };

            btnPanel.Children.Add(btnAdd);
            btnPanel.Children.Add(btnCancel);
            grid.Children.Add(btnPanel);

            dialog.Content = grid;
            dialog.ShowDialog();
        }

        // 刷新赛文列表（全局）
        private async void MenuItemRefreshRaceList_Click(object sender, RoutedEventArgs e)
        {
            var serverManager = raceHelperV2.GetServerManager();
            var servers = serverManager.GetAllServers();

            if (servers == null || servers.Count == 0)
            {
                MessageBox.Show("没有赛文服务器", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                bool anySuccess = false;
                foreach (var server in servers)
                {
                    bool success = await serverManager.RefreshServerRaces(server.Id);
                    if (success) anySuccess = true;
                }

                if (anySuccess)
                {
                    RefreshRaceMenu();
                }
                else
                {
                    MessageBox.Show("刷新失败，请检查网络连接或服务器地址", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"刷新失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 刷新单个赛文服务器（保留兼容）
        private async void MenuItemRefreshServer_Click(object sender, RoutedEventArgs e)
        {
            MenuItem menuItem = sender as MenuItem;
            if (menuItem == null)
                return;

            // 从Tag中提取serverId（格式：server_{serverId}）
            string tag = menuItem.Tag as string;
            if (string.IsNullOrEmpty(tag) || !tag.StartsWith("server_"))
                return;

            string serverId = tag.Substring("server_".Length);
            var serverManager = raceHelperV2.GetServerManager();
            var server = serverManager.GetAllServers().Find(s => s.Id == serverId);

            if (server == null)
            {
                MessageBox.Show("服务器不存在", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 显示进度提示
            var progressDialog = new Window
            {
                Title = "刷新中...",
                Width = 300,
                Height = 100,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.ToolWindow
            };

            var label = new Label
            {
                Content = $"正在刷新 {server.Name} 的赛文列表...",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            progressDialog.Content = label;

            progressDialog.Show();

            try
            {
                bool success = await serverManager.RefreshServerRaces(serverId);

                if (success)
                {
                    MessageBox.Show("刷新完成！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);

                    // 刷新菜单
                    RefreshRaceMenu();
                }
                else
                {
                    MessageBox.Show("刷新失败，请检查网络连接或服务器地址", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"刷新失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                progressDialog.Close();
            }
        }

        /// <summary>
        /// 刷新赛文菜单
        /// </summary>
        private void RefreshRaceMenu()
        {
            // 清空现有菜单
            MenuItemRace.Items.Clear();

            // 重新初始化菜单
            InitializeRaceMenu();
        }

        private bool isRefreshingRaceMenu = false;
        private bool needsRaceMenuRefresh = false;
        private static bool hasCheckedRaceMenuLogin = false;
        private bool isRebuildingFromRefresh = false;
        private object raceMenuHeaderBackup = null;
        private bool raceMenuLoaded = false;

        /// <summary>
        /// 赛文菜单关闭时检查是否需要刷新（避免打断菜单打开）
        /// </summary>
        private void MenuItemRace_SubmenuClosed(object sender, RoutedEventArgs e)
        {
            // 检查是否是整个赛文菜单关闭（而不是子菜单关闭）
            if (sender != MenuItemRace)
                return;

            // 如果后台刷新了赛文列表，菜单关闭后再重建
            if (needsRaceMenuRefresh)
            {
                needsRaceMenuRefresh = false;
                InitializeRaceMenu();
                return;
            }

            // 避免重复检查
            if (hasCheckedRaceMenuLogin || isRefreshingRaceMenu)
                return;

            try
            {
                // 检查赛文是否已登录
                var accountManager = new TypeSunny.Net.AccountSystemManager();
                var raceAccount = accountManager.GetAccount("赛文");

                // 如果赛文已登录，检查菜单是否显示登录状态
                if (raceAccount != null && !string.IsNullOrWhiteSpace(raceAccount.Username))
                {
                    // 检查菜单中是否有"登录"项
                    if (MenuItemRace.Items.Count > 0 && MenuItemRace.Items[0] is MenuItem firstItem)
                    {
                        bool needsRefresh = HasLoginMenuItem(firstItem);

                        if (needsRefresh)
                        {
                            System.Diagnostics.Debug.WriteLine($"[赛文菜单] 菜单关闭后检测到需要刷新，刷新菜单");
                            hasCheckedRaceMenuLogin = true;
                            isRefreshingRaceMenu = true;
                            InitializeRaceMenu();
                            isRefreshingRaceMenu = false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[赛文菜单] 检查登录状态失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 赛文菜单打开时自动刷新赛文列表
        /// </summary>
        private void MenuItemRace_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            // 如果是刷新后重建触发的打开，跳过
            if (isRebuildingFromRefresh) return;

            hasCheckedRaceMenuLogin = false;

            // 已经加载过，直接用缓存
            if (raceMenuLoaded) return;

            if (!isRefreshingRaceMenu)
            {
                isRefreshingRaceMenu = true;

                // 关闭菜单，按钮显示加载中
                MenuItemRace.IsSubmenuOpen = false;
                raceMenuHeaderBackup = MenuItemRace.Header;
                MenuItemRace.Header = "⏳ 加载中...";
                MenuItemRace.IsEnabled = false;

                // 后台刷新，10秒超时
                Task.Run(async () =>
                {
                    try
                    {
                        var refreshTask = Task.Run(async () =>
                        {
                            var serverManager = raceHelperV2.GetServerManager();
                            var servers = serverManager.GetAllServers();
                            if (servers != null)
                            {
                                foreach (var server in servers)
                                    await serverManager.RefreshServerRaces(server.Id);
                            }
                        });

                        await Task.WhenAny(refreshTask, Task.Delay(10000));
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[赛文菜单] 自动刷新失败: {ex.Message}");
                    }
                    finally
                    {
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            isRebuildingFromRefresh = true;
                            try
                            {
                                MenuItemRace.Header = raceMenuHeaderBackup ?? "🏆 赛文";
                                MenuItemRace.IsEnabled = true;
                                InitializeRaceMenu();
                                raceMenuLoaded = true;
                                MenuItemRace.IsSubmenuOpen = true;
                            }
                            finally
                            {
                                isRefreshingRaceMenu = false;
                                Dispatcher.BeginInvoke(new Action(() => { isRebuildingFromRefresh = false; }),
                                    System.Windows.Threading.DispatcherPriority.Background);
                            }
                        }));
                    }
                });
            }
        }

        /// <summary>
        /// 递归检查菜单中是否包含"登录"菜单项
        /// </summary>
        private bool HasLoginMenuItem(MenuItem menu)
        {
            foreach (var item in menu.Items)
            {
                if (item is MenuItem menuItem)
                {
                    if (menuItem.Header != null && menuItem.Header.ToString().Contains("登录"))
                        return true;
                    if (menuItem.Items.Count > 0 && HasLoginMenuItem(menuItem))
                        return true;
                }
            }
            return false;
        }


        private void BtnJbs_Click(object sender, RoutedEventArgs e) //锦标赛
        {
            jbs = new JBS(Config.GetString("极速用户名"), Config.GetPassword("极速密码"));
            string article = jbs.GetArticle();
            if (article != null && article.Length > 0)
            {


     
                // 打字模式控件已移到设置窗口

                LoadText(article, RetypeType.first, TxtSource.jbs);
            }
        }

        public WinArticle winArticle;// = new WinArticle();
        private void BtnArticle_Click(object sender, RoutedEventArgs e)
        {
            foreach (Window item in Application.Current.Windows)
            {
                if (item is WinArticle)
                {
                    item.Focus();
                    item.Activate();
                    return;
                }

            }

            winArticle = new WinArticle();
 
            winArticle.Show();
        }

        private async void BtnSendArticle_Click(object sender, RoutedEventArgs e)
        {
            await SendArticle();
        }

        private async void BtnRandomArticle_Click(object sender, RoutedEventArgs e)
        {
            await LoadRandomArticleAsync(true);
        }

        private async Task LoadRandomArticleAsync(bool autoSend = false, string lastResult = "")
        {
            // 在非UI线程中，提前获取QQGroupName等UI相关属性
            string qqGroupName = "";
            Dispatcher.Invoke(() =>
            {
                qqGroupName = QQGroupName;
            });

            // 禁用按钮，防止重复点击
            Dispatcher.Invoke(() =>
            {
                BtnRandomArticle.IsEnabled = false;
                BtnRandomArticle.Content = "加载中...";
            });

            try
            {
                // 先检查登录状态，未登录直接弹登录窗口（底部有注册入口）
                if (!wenlaiHelper.IsLoggedIn())
                {
                    wenlaiHelper.ShowLoginDialog(this);
                    bool shouldRetry = wenlaiHelper.IsLoggedIn();
                    if (shouldRetry) { InitializeWenlaiMenu(); InitializeRaceMenu(); }

                    if (shouldRetry)
                    {
                        await LoadRandomArticleAsync(autoSend, lastResult);
                    }
                    return;
                }

                // ✅ 关键修复：在载文前加载登录Cookie
                string wenlaiServerUrl = Config.GetString("文来接口地址");
                var wenlaiAccountManager = new TypeSunny.Net.AccountSystemManager();
                var wenlaiAccount = wenlaiAccountManager.GetAccount("文来");
                if (wenlaiAccount != null && !string.IsNullOrWhiteSpace(wenlaiAccount.Cookies))
                {
                    System.Diagnostics.Debug.WriteLine($"[载文] 加载文来登录Cookie: {wenlaiAccount.Cookies.Substring(0, Math.Min(50, wenlaiAccount.Cookies.Length))}...");
                    ArticleFetcher.LoadCookiesFromString(wenlaiServerUrl, wenlaiAccount.Cookies);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[载文] 警告：未找到文来登录Cookie，可能需要先登录");
                }

                // 在获取文章前先加载难度列表（确保难度名称缓存已加载）
                await ArticleFetcher.GetDifficultiesAsync();

                int difficulty = Config.GetInt("文来难度");
                int maxLength = Config.GetInt("文来字数");

                // difficulty=0 表示随机难度，不传递 difficulty 参数让服务端随机选择
                // 字数未填写时不传参数（保持0或负数）

                // 异步获取文章
                ArticleData article = await ArticleFetcher.FetchArticleAsync(difficulty, maxLength);

                // 检查是否是错误消息
                if (string.IsNullOrEmpty(article.Content) ||
                    article.Title == "配置错误" ||
                    article.Title == "接口错误" ||
                    article.Title == "数据错误" ||
                    article.Title == "获取失败")
                {
                    // 检查是否是"请先登录"错误
                    if (article.Title == "接口错误" && article.Content.Contains("登录"))
                    {
                        // 直接弹出登录对话框（登录页底部有注册入口）
                        wenlaiHelper.ShowLoginDialog(this);
                        bool shouldRetry = wenlaiHelper.IsLoggedIn();

                        if (shouldRetry)
                        {
                            InitializeWenlaiMenu();
                            InitializeRaceMenu();
                        }

                        if (shouldRetry)
                        {
                            // 加载Cookie到ArticleFetcher
                            var accountManager = new TypeSunny.Net.AccountSystemManager();
                            var account = accountManager.GetAccount("文来");
                            if (account != null && !string.IsNullOrWhiteSpace(account.Cookies))
                            {
                                string apiUrl = Config.GetString("文来接口地址");
                                ArticleFetcher.LoadCookiesFromString(apiUrl, account.Cookies);
                            }

                            // 重新调用载文（递归调用）
                            await LoadRandomArticleAsync(autoSend, lastResult);
                            return;
                        }
                    }
                    else
                    {
                        MessageBox.Show($"{article.Title}: {article.Content}", "文来加载失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    return;
                }

                // 加载到缓存
                articleCache.LoadArticle(article);

                // 加载第一段
                string segment = articleCache.GetCurrentSegment();

                // 如果是自动发送模式，先渲染再发送
                if (autoSend)
                {
                    string title = articleCache.GetCurrentTitle();
                    string mark = articleCache.GetCurrentMark();  // 使用文来接口返回的mark
                    string difficultyText = articleCache.GetCurrentDifficulty();  // 使用文来接口返回的难度
                    string formattedContent = FormatArticleSenderContent(title, segment, mark, difficultyText);

                // 先发送QQ，再异步渲染（提升响应速度）
                if (qqGroupName != "")
                {
                    if (!string.IsNullOrEmpty(lastResult))
                    {
                        // 有成绩：根据"自动发送成绩"开关决定
                        if (Config.GetBool("自动发送成绩"))
                        {
                            // 开启自动发送成绩：先发成绩，再发下一段
                            QQHelper.SendQQMessageD(qqGroupName, lastResult, formattedContent, 0, this);
                        }
                        else
                        {
                            // 未开启自动发送成绩：只发下一段文章
                            QQHelper.SendQQMessage(qqGroupName, formattedContent, 0, this);
                        }
                    }
                    else
                    {
                        // 无成绩：只发下一段
                        QQHelper.SendQQMessage(qqGroupName, formattedContent, 0, this);
                    }
                }
                else
                {
                    // 没有选群：复制到剪切板
                    // 根据"自动发送成绩"开关决定是否复制成绩
                    string messageToSend = formattedContent;
                    if (!string.IsNullOrEmpty(lastResult) && Config.GetBool("自动发送成绩"))
                    {
                        messageToSend = lastResult + "\n" + formattedContent;
                    }
                    Win32SetText(messageToSend);
                }

                // 异步渲染文本，不等待渲染完成（fire-and-forget）
                System.Threading.Tasks.Task.Run(() =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        LoadText(segment, RetypeType.first, TxtSource.articlesender, switchBack: false);
                        // 重置进度条
                        if (Config.GetBool("显示进度条"))
                            TitleProgressBar.Width = 0;
                    });
                });
                }
                else
                {
                    // 非自动发送模式，正常渲染
                    LoadText(segment, RetypeType.first, TxtSource.articlesender, switchBack: false);
                    // 重置进度条
                    if (Config.GetBool("显示进度条"))
                        TitleProgressBar.Width = 0;
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"加载文章时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
            finally
            {
                // 恢复按钮状态
                Dispatcher.Invoke(() =>
                {
                    BtnRandomArticle.IsEnabled = true;
                    BtnRandomArticle.Content = "文来Ctrl+R";
                });
            }
        }

        private void LoadRandomArticle(bool autoSend = false, string lastResult = "")
        {
            // 调用异步版本，不等待结果
            _ = LoadRandomArticleAsync(autoSend, lastResult);
        }

        private async void LoadNextSegment()
        {
            if (!articleCache.HasArticle())
            {
                MessageBox.Show("请先加载文章（Ctrl+R）", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 获取当前书籍信息
            int bookId = articleCache.GetBookId();
            int sortNum = articleCache.GetSortNum();
            string category = articleCache.GetCategory();
            int endSortNum = articleCache.GetEndSortNum();
            string endChars = articleCache.GetEndChars();

            // 调用API获取下一段（使用异步方法避免死锁）
            ArticleData segmentData = await ArticleFetcher.FetchSegmentAsync(bookId, sortNum, 1, category, endSortNum, endChars);

            if (string.IsNullOrEmpty(segmentData.Content) || segmentData.Title == "获取失败" || segmentData.Title == "接口错误")
            {
                MessageBox.Show("获取下一段失败: " + segmentData.Content, "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 更新articleCache中的参数（bookId、sortNum、difficultyId等），以便下次翻页使用正确的参数
            articleCache.LoadArticle(segmentData);

            // 加载下一段
            LoadText(segmentData.Content, RetypeType.first, TxtSource.articlesender, switchBack: false);

            // 格式化后发送到QQ群
            SendFormattedArticle(segmentData.Content);

            // 重置进度条
            if (Config.GetBool("显示进度条"))
                TitleProgressBar.Width = 0;
        }

        private async void LoadPreviousSegment()
        {
            if (!articleCache.HasArticle())
            {
                MessageBox.Show("请先加载文章（Ctrl+R）", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 获取当前书籍信息
            int bookId = articleCache.GetBookId();
            int sortNum = articleCache.GetSortNum();
            string category = articleCache.GetCategory();
            string startChars = articleCache.GetStartChars();

            // 上一段：传 startChars 用于前翻精确定位
            ArticleData segmentData = await ArticleFetcher.FetchSegmentAsync(bookId, sortNum, 0, category, 0, null, startChars);

            if (string.IsNullOrEmpty(segmentData.Content) || segmentData.Title == "获取失败" || segmentData.Title == "接口错误")
            {
                MessageBox.Show("获取上一段失败: " + segmentData.Content, "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 更新articleCache中的参数（bookId、sortNum、difficultyId等），以便下次翻页使用正确的参数
            articleCache.LoadArticle(segmentData);

            // 加载上一段
            LoadText(segmentData.Content, RetypeType.first, TxtSource.articlesender, switchBack: false);

            // 格式化后发送到QQ群
            SendFormattedArticle(segmentData.Content);

            // 重置进度条
            if (Config.GetBool("显示进度条"))
                TitleProgressBar.Width = 0;
        }



        private void MainWin_Deactivated(object sender, EventArgs e)
        {
            if (StateManager.txtSource != TxtSource.changeSheng && StateManager.txtSource != TxtSource.jbs && StateManager.txtSource != TxtSource.jisucup && StateManager.txtSource != TxtSource.raceApi)
            {
                if (StateManager.typingState == TypingState.typing)
                {
                    StateManager.typingState = TypingState.pause;
                    TbkStatusTop.Text = "暂停\t" + TbkStatusTop.Text;
                    sw.Stop();
                    //              Recorder.Stop();
                    if (timerProgress != null)
                        timerProgress.Dispose();
                }

            }
        }


        /*
        private void ChkLookType_Click(object sender, RoutedEventArgs e)
        {
            if (StateManager.ConfigLoaded)
            {
                if (ChkLookType.IsChecked == true)
                    ChkBlindType.IsChecked = false;

                Config.Set("盲打模式", ChkBlindType.IsChecked == true ? "是" : "否");
                Config.Set("看打模式", ChkLookType.IsChecked == true || ChkBlindType.IsChecked == true ? "是" : "否");
                UpdateDisplay(UpdateLevel.PageArrange);
                TbxInput.Focus();
            }
        }


        private void ChkBlindType_Click(object sender, RoutedEventArgs e)
        {
            if (StateManager.ConfigLoaded)
            {
                if (ChkBlindType.IsChecked == true)
                    ChkLookType.IsChecked = false;

                Config.Set("盲打模式", ChkBlindType.IsChecked == true ? "是" : "否");
                Config.Set("看打模式", ChkLookType.IsChecked == true || ChkBlindType.IsChecked == true ? "是" : "否");
                UpdateDisplay(UpdateLevel.PageArrange);
                TbxInput.Focus();
            }
        }
        */

        private async void BtnPrev_Click(object sender, RoutedEventArgs e)
        {
            ArticleManager.PrevSection();
            string content = await ArticleManager.GetFormattedCurrentSection();
            LoadText(content, RetypeType.first, TxtSource.book, false);
            SendContentToClipboardOrQQ(content);
        }

        private async void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            ArticleManager.NextSection();
            string content = await ArticleManager.GetFormattedCurrentSection();
            LoadText(content, RetypeType.first, TxtSource.book, false);
            SendContentToClipboardOrQQ(content);
        }



        public void UpdateButtonProgress()
        {
            if (ArticleManager.Title == "")
            {
                BtnSendArticle.IsEnabled = false;
                BtnNext.IsEnabled = false;
                BtnPrev.IsEnabled = false;
                BtnSendArticle.Content = "发文F2";

            }

            else
            {
                BtnSendArticle.IsEnabled = true;
                BtnNext.IsEnabled = true;
                BtnPrev.IsEnabled = true;
                BtnSendArticle.Content = "发文-" + ArticleManager.Title.Replace(".txt", "").Replace(".TXT", "").Replace(".Txt", "") + "-" + ArticleManager.Progress + "/" + ArticleManager.TotalSize;
            }
        }



        private int GetLookTyping() //获取正在盲打的字
        {

            string currentMatchText = string.Join("", TextInfo.Words);


            string t1 = currentMatchText.Replace('”', '\"').Replace('“', '\"').Replace('‘', '\'').Replace('’', '\'');
            string t2 = TbxInput.Text.Replace('”', '\"').Replace('“', '\"').Replace('‘', '\'').Replace('’', '\'');
            List<DiffRes> diffs = DiffTool.Diff(t1, t2);

            int pos = 0;
            int counter = 0;
            foreach (var df in diffs)
            {
                Run r = new Run();

                switch (df.Type)
                {
                    case DiffType.None:
                        r.Text = currentMatchText.Substring(df.OrigIndex, 1);
                        pos = df.OrigIndex + 1;
                        break;
                    case DiffType.Delete:

                        r.Text = currentMatchText.Substring(df.OrigIndex - 1, 1);
                        counter--;
                        r.Background = Colors.CorrectBackground;
                        break;
                    case DiffType.Add:

                        r.Text = TbxInput.Text.Substring(df.RevIndex + counter, 1);
                        counter++;
                        r.Background = Colors.IncorrectBackground;
                        break;

                }




            }

            if (pos >= currentMatchText.Length)
                pos = currentMatchText.Length - 1;


            return pos;
        }

        internal void LogBack() //记录回改的字
        {
            if (TextInfo.Words.Count == 0)
                return;
            string currentMatchText = string.Concat(TextInfo.Words);
            if (!Config.GetBool("错字重打"))
                return;

            int pos;
            string w;
            if (!IsLookingType)
            {
                pos = TextInfo.wordStates.IndexOf(WordStates.NO_TYPE);
                if (pos == -1)
                    pos = TextInfo.Words.Count - 1;
                w = TextInfo.Words[pos];
            }
            else
            {
                pos = GetLookTyping();
                w = currentMatchText.Substring(pos, 1);
            }

            if (pos >= 0)
            {

                //             if (!TextInfo.BackCounter.ContainsKey(pos))
                //           {
                //        TxtBack.Set(w, TxtBack.GetInt(w) + 1);
                //             TextInfo.BackCounter[pos] = w;
                //        }

                if (!TextInfo.WrongExclude.Contains(w))
                    TextInfo.WrongRec[pos] = w;
            }
        }

        internal void LogCorrection()
        {
            string currentMatchText = string.Concat(TextInfo.Words);
            if (!Config.GetBool("错字重打"))
                return;
            int pos;
            string w;
            if (IsLookingType)
            {
                pos = GetLookTyping();
                if (pos < 0)
                    return;

                w = currentMatchText.Substring(pos, 1);
            }
            else
            {
                pos = TextInfo.wordStates.IndexOf(WordStates.NO_TYPE);
                if (pos == -1)
                    pos = TextInfo.Words.Count - 1;

                pos -= 1;
                if (pos < 0)
                    return;

                w = TextInfo.Words[pos];
            }


            /*
            if (!TextInfo.CorrectionCounter.ContainsKey(pos))
            {
                TxtCorrection.Set(w, TxtCorrection.GetInt(w) + 1);
                TextInfo.CorrectionCounter[pos] = w;
            }
            */
            if (!TextInfo.WrongExclude.Contains(w))
                TextInfo.WrongRec[pos] = w;

        }


        private void LogWrong(int pos, string w)
        {
            if (!Config.GetBool("错字重打"))
                return;

            /*
            if (!TextInfo.WrongCounter.ContainsKey(pos))
            {
                TxtWrong.Set(w, TxtWrong.GetInt(w) + 1);
                TextInfo.WrongCounter[pos] = w;
            }
            */
            if (!TextInfo.WrongExclude.Contains(w))
                TextInfo.WrongRec[pos] = w;
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {

            BtnF5.Content = "选群F5";


            FocusInput();
        }

        private void TbxResults_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateTypingStat();
        }

        private void BtnTrainer_Click(object sender, RoutedEventArgs e)
        {
            ShowWinTrainer();
        }

        // 成绩面板展开/收起状态
        private bool _isResultsExpanded = true;

        // 保存展开状态时的主窗口高度
        private double _expandedWindowHeight = 0;
        // 保存收起时Row 2、Row 4和Row 6的像素高度
        private double _collapsedArticleHeight = 0;
        private double _collapsedTypingHeight = 0;
        private double _collapsedResultsHeight = 0;

        // 展开/收起按钮点击事件
        private void BtnToggleResults_Click(object sender, RoutedEventArgs e)
        {
            _isResultsExpanded = !_isResultsExpanded;

            // 获取主Grid（通过FindName）
            var grid_a = this.FindName("grid_a") as Grid;
            if (grid_a == null) return;

            if (_isResultsExpanded)
            {
                // 展开：恢复成绩区，所有区域都使用 * 自动分配空间
                BtnToggleResults.Content = "▼";
                Config.Set("成绩面板展开", true);

                // 隐藏底边框
                var bottomBorder = this.FindName("bottomBorder") as Border;
                if (bottomBorder != null)
                {
                    grid_a.RowDefinitions[7].Height = new GridLength(0, GridUnitType.Pixel);
                    bottomBorder.Visibility = Visibility.Collapsed;
                }

                // 恢复Row 5和Row 6的MinHeight（清除MinHeight设置，使用默认值）
                grid_a.RowDefinitions[5].ClearValue(RowDefinition.MinHeightProperty);
                grid_a.RowDefinitions[6].ClearValue(RowDefinition.MinHeightProperty);

                // 恢复成绩区 Border 的 margin
                resultsTextBoxGrid.Margin = new Thickness(15, 5, 15, 10);
                resultsTextBoxGrid.ClearValue(FrameworkElement.MinHeightProperty);
                resultsTextBoxGrid.ClearValue(FrameworkElement.HeightProperty);

                // 恢复TbxResults的属性
                var tbxResults = this.FindName("TbxResults") as TextBox;
                if (tbxResults != null)
                {
                    tbxResults.ClearValue(FrameworkElement.MinHeightProperty);
                    tbxResults.Margin = new Thickness(0);  // 保持0
                    tbxResults.ClearValue(FrameworkElement.HeightProperty);
                    tbxResults.Padding = new Thickness(10, 5, 10, 10);
                    tbxResults.Visibility = Visibility.Visible;
                }

                // 先把Row 2、Row 4和Row 6从像素转回Star，恢复它们的自适应能力
                // 使用保存的高度比例来设置Star值，保持原来的比例关系
                if (_collapsedArticleHeight > 0 && _collapsedTypingHeight > 0 && _collapsedResultsHeight > 0)
                {
                    // 找到最大的值作为基准，保持所有三个区域的比例关系
                    double max = Math.Max(_collapsedArticleHeight, Math.Max(_collapsedTypingHeight, _collapsedResultsHeight));
                    if (max > 0)
                    {
                        // 归一化到1-10的范围，保持比例
                        double scaleFactor = 10.0 / max;
                        grid_a.RowDefinitions[2].Height = new GridLength(_collapsedArticleHeight * scaleFactor, GridUnitType.Star);
                        grid_a.RowDefinitions[4].Height = new GridLength(_collapsedTypingHeight * scaleFactor, GridUnitType.Star);
                        grid_a.RowDefinitions[6].Height = new GridLength(_collapsedResultsHeight * scaleFactor, GridUnitType.Star);
                    }
                    else
                    {
                        // 使用配置的默认比例
                        ApplyDisplayInputRatio();
                    }
                }
                else
                {
                    // 使用配置的默认比例
                    ApplyDisplayInputRatio();
                }
                grid_a.RowDefinitions[5].Height = new GridLength(5, GridUnitType.Pixel);
                grid_a.RowDefinitions[5].ClearValue(RowDefinition.MinHeightProperty);
                resultsTextBoxGrid.Visibility = Visibility.Visible;
                gridSplitterResults.Visibility = Visibility.Visible;

                // 启用所有 GridSplitter
                gridSplitterArticleTyping.IsEnabled = true;
                gridSplitterResults.IsEnabled = true;

                // 恢复窗口高度
                if (_expandedWindowHeight > 300)
                {
                    this.Height = _expandedWindowHeight;
                }
            }
            else
            {
                // 收起：先获取各区域的比例，然后隐藏成绩区，最后调整窗口高度
                _expandedWindowHeight = this.ActualHeight;

                // 计算grid_a的内容高度
                double gridContentHeight = 0;
                for (int i = 0; i < 7; i++)
                {
                    gridContentHeight += grid_a.RowDefinitions[i].ActualHeight;
                }

                // 计算成绩区高度
                double resultsAreaHeight = grid_a.RowDefinitions[6].ActualHeight;

                // 关键：在收起前，先把Row 2和Row 4固定为当前高度
                // 这样收起后它们的大小不会改变
                double articleHeight = grid_a.RowDefinitions[2].ActualHeight;
                double typingHeight = grid_a.RowDefinitions[4].ActualHeight;
                grid_a.RowDefinitions[2].Height = new GridLength(articleHeight, GridUnitType.Pixel);
                grid_a.RowDefinitions[4].Height = new GridLength(typingHeight, GridUnitType.Pixel);

                // 保存收起时的高度，用于展开时恢复
                _collapsedArticleHeight = articleHeight;
                _collapsedTypingHeight = typingHeight;

                // 计算收起前Row 6的实际高度并保存
                double row6ActualHeightBefore = grid_a.RowDefinitions[6].ActualHeight;
                _collapsedResultsHeight = row6ActualHeightBefore;

                // 获取内部的TbxResults TextBox
                var tbxResults = this.FindName("TbxResults") as TextBox;
                var bottomBorder = this.FindName("bottomBorder") as Border;

                // 然后设置成绩区高度为0并隐藏
                resultsTextBoxGrid.Margin = new Thickness(0);
                // 方案1：设置Row 6的Height=0和MinHeight=0（关键修复！）
                grid_a.RowDefinitions[6].Height = new GridLength(0, GridUnitType.Pixel);
                grid_a.RowDefinitions[6].MinHeight = 0;  // 关键！必须设置MinHeight=0
                grid_a.RowDefinitions[5].Height = new GridLength(0, GridUnitType.Pixel);
                grid_a.RowDefinitions[5].MinHeight = 0;  // 也设置Row 5的MinHeight=0

                // 显示底边框：设置Row 7的高度为10px
                if (bottomBorder != null)
                {
                    grid_a.RowDefinitions[7].Height = new GridLength(10, GridUnitType.Pixel);
                    bottomBorder.Visibility = Visibility.Visible;
                }

                resultsTextBoxGrid.Visibility = Visibility.Collapsed;
                gridSplitterResults.Visibility = Visibility.Collapsed;
                gridSplitterResults.IsEnabled = false;

                // 方案2：设置TextBox的属性，确保不占用空间
                if (tbxResults != null)
                {
                    tbxResults.MinHeight = 0;
                    tbxResults.Margin = new Thickness(0);  // 方案2：设置Margin=0
                    tbxResults.Height = 0;
                    // 方案3：设置VerticalContentAlignment和Padding
                    tbxResults.VerticalContentAlignment = System.Windows.VerticalAlignment.Stretch;
                    tbxResults.Padding = new Thickness(0);
                    tbxResults.Visibility = Visibility.Collapsed;
                }
                resultsTextBoxGrid.MinHeight = 0;
                resultsTextBoxGrid.Height = 0;

                // 强制更新布局，获取Row 6收起后的实际高度
                this.UpdateLayout();
                double row6ActualHeightAfter = grid_a.RowDefinitions[6].ActualHeight;

                // 计算新的窗口高度
                // 收起后grid_a的高度 = 原grid_a高度 - 成绩区高度 - 分隔条2高度(5)
                double collapsedGridHeight = gridContentHeight - resultsAreaHeight - 5;
                // 收起后的窗口高度 = collapsedGridHeight + 窗口与grid_a的高度差
                double windowOffset = _expandedWindowHeight - gridContentHeight;
                double collapsedHeight = collapsedGridHeight + windowOffset;

                BtnToggleResults.Content = "▲";
                Config.Set("成绩面板展开", false);

                this.Height = collapsedHeight;

                // 延迟将发文区和跟打区改为Star，让它们可以自适应窗口调整
                this.Dispatcher.BeginInvoke(new Action(() =>
                {
                    grid_a.RowDefinitions[2].Height = new GridLength(1, GridUnitType.Star);
                    // 字帖模式下跟打区已折叠，不要给它Star，否则会分走空间导致底部留白
                    if ((_copybookMode == null || !_copybookMode.IsActive) &&
                        (_tracingMode == null || !_tracingMode.IsActive))
                    {
                        grid_a.RowDefinitions[4].Height = new GridLength(1, GridUnitType.Star);
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);

            }
        }

        // GridSplitter拖动完成事件：保存发文区和跟打区的比例
        private void GridSplitterArticleTyping_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            if (StateManager.ConfigLoaded)
            {
                // 延迟保存，确保布局已经更新
                this.Dispatcher.BeginInvoke(new Action(() =>
                {
                    SaveDisplayInputRatio();
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        // 辅助方法：获取Row名称
        private string GetRowName(int index)
        {
            switch (index)
            {
                case 0: return "标题栏";
                case 1: return "按纽区1";
                case 2: return "发文区";
                case 3: return "分隔条1";
                case 4: return "跟打区+按纽2";
                case 5: return "分隔条2";
                case 6: return "成绩区";
                default: return "";
            }
        }

        // 获取所有可用的 Logo
        public static string[] GetAvailableLogos()
        {
            try
            {
                string icoFolder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "ico");
                if (System.IO.Directory.Exists(icoFolder))
                {
                    var logos = new List<string>();
                    foreach (string file in System.IO.Directory.GetFiles(icoFolder, "*.ico"))
                    {
                        // 获取文件名（不含扩展名）
                        string name = System.IO.Path.GetFileNameWithoutExtension(file);
                        logos.Add(name);
                    }
                    if (logos.Count > 0)
                        return logos.ToArray();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取Logo列表失败: {ex.Message}");
            }

            // 如果读取失败，返回默认值
            return new string[] { "sunny" };
        }

        // 应用当前选中的 Logo
        public void ApplyCurrentLogo()
        {
            try
            {
                string currentLogo = Config.GetString("当前Logo");
                // 图标文件是 Content 类型，需要使用相对于程序目录的路径
                string iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "ico", $"{currentLogo}.ico");
                if (System.IO.File.Exists(iconPath))
                {
                    var iconUri = new Uri(iconPath, UriKind.Absolute);
                    // 更新窗口图标（任务栏、Alt+Tab等）
                    this.Icon = new BitmapImage(iconUri);
                    // 更新标题栏图标（窗口左上角显示的图标）
                    TitleBarIcon.Source = new BitmapImage(iconUri);
                    System.Diagnostics.Debug.WriteLine($"应用Logo成功: {iconPath}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Logo文件不存在: {iconPath}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"应用Logo失败: {ex.Message}");
            }
        }

        WinTrainer winTrainer;
        private void ShowWinTrainer()
        {

            if (WinTrainer.Current != null)
            {
                WinTrainer.Current.RefreshFileList();
                WinTrainer.Current.Show();
                WinTrainer.Current.Focus();
                WinTrainer.Current.Activate();
            }
            else
            {
                winTrainer = new WinTrainer();
                winTrainer.Show();
                winTrainer.Activate();
            }

        }


        public string QQGroupName
        {
            get
            {
                string content = BtnF5.Content.ToString();
                // 检查是否以"当前-"开头，表示已经选择了群
                if (content.StartsWith("当前-"))
                {
                    return content.Substring(3); // 返回"当前-"后面的群名
                }
                else
                    return "";
            }

        }


        public void FocusInput()
        {
            Dispatcher.Invoke(() =>
            {
                this.Activate();
                this.Topmost = true;  // important
                this.Topmost = false; // important
                this.Focus();
                TbxInput.Focus();
            });
        }





        // SldBindLookUpdate 已移除 - 打字模式控件已移到设置窗口

        // CbBlindType控件已移到设置窗口

        private void ApplyDisplayInputRatio()
        {
            // 恢复发文区和跟打区的比例
            var grid_a = this.FindName("grid_a") as Grid;
            if (grid_a != null)
            {
                // 默认比例：发文区45%，跟打区35%，成绩区20%
                double resultsRatio = Config.GetDouble("成绩区高度比例");
                if (resultsRatio <= 0 || resultsRatio >= 1)
                    resultsRatio = 0.2; // 默认成绩区占20%

                double articleTypingRatio = Config.GetDouble("发文区跟打区比例");
                if (articleTypingRatio <= 0 || articleTypingRatio >= 1)
                    articleTypingRatio = 0.56; // 默认发文区占(发文+跟打)的56%，即总高度的45%左右

                // 发文区比例 = 总剩余比例 × 发文区跟打区比例
                double articleRatio = (1 - resultsRatio) * articleTypingRatio;
                // 跟打区比例 = 总剩余比例 × (1 - 发文区跟打区比例)
                double typingRatio = (1 - resultsRatio) * (1 - articleTypingRatio);

                grid_a.RowDefinitions[2].Height = new GridLength(articleRatio, GridUnitType.Star);
                grid_a.RowDefinitions[4].Height = new GridLength(typingRatio, GridUnitType.Star);
                grid_a.RowDefinitions[6].Height = new GridLength(resultsRatio, GridUnitType.Star);
            }
        }

        private void SaveDisplayInputRatio()
        {
            // 保存所有三个区域的比例
            var grid_a = this.FindName("grid_a") as Grid;
            if (grid_a != null)
            {
                double articleHeight = grid_a.RowDefinitions[2].ActualHeight;
                double typingHeight = grid_a.RowDefinitions[4].ActualHeight;
                double resultsHeight = grid_a.RowDefinitions[6].ActualHeight;
                double total = articleHeight + typingHeight + resultsHeight;

                if (total > 0)
                {
                    // 保存发文区占(发文+跟打)的比例（字帖模式下跟打区高度为0，不保存）
                    double articleTypingTotal = articleHeight + typingHeight;
                    if (articleTypingTotal > 0)
                    {
                        double articleTypingRatio = articleHeight / articleTypingTotal;
                        Config.dicts["发文区跟打区比例"] = articleTypingRatio.ToString("F6");
                    }

                    // 成绩面板收起时 resultsHeight 为0，不保存，避免写入错误的0值
                    if (_isResultsExpanded && resultsHeight > 0)
                    {
                        double resultsRatio = resultsHeight / total;
                        Config.dicts["成绩区高度比例"] = resultsRatio.ToString("F6");
                    }

                    // 立即写入配置文件
                    Config.WriteConfig(0);
                }
            }
        }

        private void GridSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            // 新布局中发文区和跟打区已分离，不再需要保存比例
        }

        // GridSplitter拖动完成事件：保存成绩区高度
        private void GridSplitterResults_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            if (StateManager.ConfigLoaded)
            {
                // 延迟保存，确保布局已经更新
                this.Dispatcher.BeginInvoke(new Action(() =>
                {
                    SaveDisplayInputRatio();
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        private void SaveResultsPanelHeight()
        {
            // 已整合到SaveDisplayInputRatio中
            SaveDisplayInputRatio();
        }

        private void LoadResultsPanelHeight()
        {
            // 委托给ApplyDisplayInputRatio处理，保持逻辑统一
            ApplyDisplayInputRatio();
        }

        // 新布局使用标准GridSplitter，不再需要手动拖动处理

        private void SldBlind_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            // 已废弃，使用ComboBox替代
        }

        // 自定义标题栏事件处理
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                // 双击切换最大化/还原
                if (this.WindowState == WindowState.Maximized)
                    this.WindowState = WindowState.Normal;
                else
                    this.WindowState = WindowState.Maximized;
            }
            else
            {
                // 单击拖动窗口
                this.DragMove();
            }
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

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
            ReleaseCapture();

            int direction = 0;
            string borderName = border.Name;

            switch (borderName)
            {
                case "ResizeTop": direction = HT_TOP; break;
                case "ResizeBottom": direction = HT_BOTTOM; break;
                case "ResizeLeft": direction = HT_LEFT; break;
                case "ResizeRight": direction = HT_RIGHT; break;
                case "ResizeTopLeft": direction = HT_TOPLEFT; break;
                case "ResizeTopRight": direction = HT_TOPRIGHT; break;
                case "ResizeBottomLeft": direction = HT_BOTTOMLEFT; break;
                case "ResizeBottomRight": direction = HT_BOTTOMRIGHT; break;
            }

            if (direction != 0)
            {
                SendMessage(windowHandle, WM_NCLBUTTONDOWN, (IntPtr)direction, IntPtr.Zero);
            }
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
                case "ResizeTopLeft":
                case "ResizeBottomRight":
                    this.Cursor = Cursors.SizeNWSE;
                    break;
                case "ResizeTopRight":
                case "ResizeBottomLeft":
                    this.Cursor = Cursors.SizeNESW;
                    break;
            }
        }

        private void ResizeBorder_MouseLeave(object sender, MouseEventArgs e)
        {
            this.Cursor = Cursors.Arrow;
        }
    }
}

