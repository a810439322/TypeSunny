using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Windows.Documents;
using System.Windows.Markup;


using Diff;

using System.Reflection;
using Interop.UIAutomationClient;

using Net;
using LibB;
using TypeSunny.Difficulty;
using TypeSunny.ArticleSender;



namespace TypeSunny
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 






    public partial class MainWindow : Window
    {
        public static MainWindow Current => Application.Current.MainWindow as MainWindow;


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








        Stopwatch sw = new Stopwatch();
        DifficultyDict difficultyDict = new DifficultyDict();
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
            ";" ,
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
            ":" ,
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


        static List<string> AZ = new List<string> { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z", "a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k", "l", "m", "n", "o", "p", "q", "r", "s", "t", "u", "v", "w", "x", "y", "z", "'" };

        private void UpdateDisplay(UpdateLevel updateLevel)
        {

            if (IsLookingType && StateManager.LastType)
            {
                BlindDiff();
                return;
            }



            if (updateLevel >= UpdateLevel.PageArrange)
                PageReArrange();

            if (updateLevel >= UpdateLevel.Progress)
                PageProgressUpdate();


            void PageReArrange()
            {
                // 如果从贪吃蛇模式切换到普通模式，需要触发页面重新渲染（赛文API除外，它强制使用贪吃蛇）
                if (!Config.GetBool("贪吃蛇模式") && StateManager.txtSource != TxtSource.trainer && StateManager.txtSource != TxtSource.articlesender && StateManager.txtSource != TxtSource.raceApi)
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
                if ((Config.GetBool("贪吃蛇模式") && StateManager.txtSource != TxtSource.trainer) || StateManager.txtSource == TxtSource.raceApi)
                {
                    TextInfo.PageNum = -1;
                    return;
                }

                // 文来使用简化渲染模式（无需分页，性能优化）
                if (StateManager.txtSource == TxtSource.articlesender)
                {
                    TextInfo.PageNum = -1;
                    return;
                }

                double y = (this.ActualHeight - 95.74 - 0.5 - 15) * 0.75 - (expd.IsExpanded ? 145 : 0);
                double x = (this.ActualWidth - 52);
                Paginator.ArrangePage(x, y, 40.0, TextInfo.Words.Count);

                TextInfo.PageNum = -1;
            }

            void PageProgressUpdate()
            {
                //计算页码
                // int nextToType = TextInfo.wordStates.IndexOf(WordStates.NO_TYPE);
                int nextToType = new StringInfo(TbxInput.Text).LengthInTextElements;
                if (nextToType >= TextInfo.Words.Count)
                    nextToType = TextInfo.Words.Count - 1;

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
                if ((Config.GetBool("贪吃蛇模式") && StateManager.txtSource != TxtSource.trainer) || StateManager.txtSource == TxtSource.raceApi)
                {
                    SnakeModeUpdate(nextToType);
                    return;
                }

                // 文来使用简化渲染模式（性能优化）
                if (StateManager.txtSource == TxtSource.articlesender)
                {
                    SimplifiedRenderMode(nextToType);
                    return;
                }

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
                if (TextInfo.PageNum == -1 || nextToType > Paginator.Pages[TextInfo.PageNum].BodyEnd || nextToType < Paginator.Pages[TextInfo.PageNum].BodyStart)
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
                    Page p = Paginator.Pages[pn];



                    var fm = GetCurrentFontFamily();// new FontFamily(Config.GetString("字体"));


                    double fs = 40.0;
                    double height = fs * (1.0 + Config.GetDouble("行距"));
                    double MinWidth = 40.0 * 0.9;

                    ScDisplay.FontFamily = fm;
                    ScDisplay.Foreground = Colors.DisplayForeground;
                    ScDisplay.FontSize = fs; TbAcc.FontSize = fs / 2.3;
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
                            string c1 = TextInfo.Blocks[i-1].Text;
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
                        foreach (var tb in TextInfo.Blocks)
                            TbDispay.Children.Add(tb);
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
                {
                    int NextBlockIndex = (nextToType - TextInfo.PageStartIndex);

                    // 计算当前字符的Y坐标（相对于第一个Block）
                    double currentPosY = TextInfo.Blocks[NextBlockIndex].TranslatePoint(new Point(0, 0), TextInfo.Blocks[0]).Y
                        + TextInfo.Blocks[NextBlockIndex].ActualHeight / 2;

                    // 使用统一方法计算目标滚动位置（始终居中显示）
                    double targetOffset = CalculateScrollOffset(currentPosY);

                    // 执行滚动（起始位置强制滚动，其他时候由 SmoothScrollTo 自动判断）
                    SmoothScrollTo(targetOffset, forceScroll: (nextToType == 0));


                    //跟随显示速度
                    bool showSpeed = Config.GetBool("速度跟随提示") && !Config.GetBool("盲打模式") && !double.IsNaN(Score.GetValidSpeed()) && Score.GetValidSpeed() > 0;


                    if (!showSpeed)
                    {
                        if (TbAcc.Visibility == Visibility.Visible)
                            TbAcc.Visibility = Visibility.Hidden;
                    }
                    else
                    {
                        if (TbAcc.Visibility == Visibility.Hidden)
                            TbAcc.Visibility = Visibility.Visible;

                        double AccLeft = TextInfo.Blocks[NextBlockIndex].TranslatePoint(new Point(0, 0), TextInfo.Blocks[0]).X + TextInfo.Blocks[NextBlockIndex].ActualWidth / 3;
                        double AccTop = TextInfo.Blocks[NextBlockIndex].TranslatePoint(new Point(0, 0), TextInfo.Blocks[0]).Y + TextInfo.Blocks[NextBlockIndex].ActualHeight - targetOffset;
                        Canvas.SetTop(TbAcc, AccTop);
                        Canvas.SetLeft(TbAcc, AccLeft);


                        // 只显示速度
                        TbAcc.Text = Score.GetValidSpeed().ToString("F2");
                        TbAcc.Foreground = Colors.GetSpeedColor(Score.GetValidSpeed());

                    }

                    //跟随显示提示
                    /*
                    bool showAccuracy = Config.GetBool("增强键准提示") && !double.IsNaN(Score.GetAccuracy());
                    bool showSpeed = Config.GetBool("增强速度提示") && !double.IsNaN(Score.GetValidSpeed());
                    
                    if (!showAccuracy && !showSpeed)
                    {
                        if (TbAcc.Visibility == Visibility.Visible)
                            TbAcc.Visibility = Visibility.Hidden;
                    }
                    else
                    {
                        if (TbAcc.Visibility == Visibility.Hidden)
                            TbAcc.Visibility = Visibility.Visible;
                        
                        double AccLeft = TextInfo.Blocks[NextBlockIndex].TranslatePoint(new Point(0, 0), TextInfo.Blocks[0]).X + TextInfo.Blocks[NextBlockIndex].ActualWidth / 3;
                        double AccTop = TextInfo.Blocks[NextBlockIndex].TranslatePoint(new Point(0, 0), TextInfo.Blocks[0]).Y + TextInfo.Blocks[NextBlockIndex].ActualHeight - offset;
                        Canvas.SetTop(TbAcc, AccTop);
                        Canvas.SetLeft(TbAcc, AccLeft);
                        
                        // 根据启用的功能显示不同内容
                        if (showAccuracy && showSpeed)
                        {
                            // 两个功能都启用，显示组合信息
                            TbAcc.Text = $"{Score.GetValidSpeed():F0},{(100 * Score.GetAccuracy()):F1}%";
                            // 使用速度颜色作为主色调
                            TbAcc.Foreground = Colors.GetSpeedColor(Score.GetValidSpeed());
                        }
                        else if (showSpeed)
                        {
                            // 只显示速度
                            TbAcc.Text = Score.GetValidSpeed().ToString("F2");
                            TbAcc.Foreground = Colors.GetSpeedColor(Score.GetValidSpeed());
                        }
                        else if (showAccuracy)
                        {
                            // 只显示准确率
                            TbAcc.Text = (100 * Score.GetAccuracy()).ToString("F2");
                            TbAcc.Foreground = Colors.GetAccColor(Score.GetAccuracy());
                        }
                    }

                    
                    */
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
                            // 计算当前字符的Y坐标（相对于第一个Block）
                            double currentPosY = TextInfo.Blocks[NextBlockIndex].TranslatePoint(new Point(0, 0), TextInfo.Blocks[0]).Y
                                + TextInfo.Blocks[NextBlockIndex].ActualHeight / 2;

                            // 使用统一方法计算目标滚动位置（始终居中显示）
                            double targetOffset = CalculateScrollOffset(currentPosY);

                            // 执行滚动（起始位置强制滚动，其他时候由 SmoothScrollTo 自动判断）
                            SmoothScrollTo(targetOffset, forceScroll: (nextToType == 0));

                            // 跟随显示速度
                            bool showSpeed = Config.GetBool("速度跟随提示") && !Config.GetBool("盲打模式") && !double.IsNaN(Score.GetValidSpeed()) && Score.GetValidSpeed() > 0;

                            if (!showSpeed)
                            {
                                if (TbAcc.Visibility == Visibility.Visible)
                                    TbAcc.Visibility = Visibility.Hidden;
                            }
                            else
                            {
                                if (TbAcc.Visibility == Visibility.Hidden)
                                    TbAcc.Visibility = Visibility.Visible;

                                double AccLeft = TextInfo.Blocks[NextBlockIndex].TranslatePoint(new Point(0, 0), TextInfo.Blocks[0]).X + TextInfo.Blocks[NextBlockIndex].ActualWidth / 3;
                                double AccTop = TextInfo.Blocks[NextBlockIndex].TranslatePoint(new Point(0, 0), TextInfo.Blocks[0]).Y + TextInfo.Blocks[NextBlockIndex].ActualHeight - targetOffset;
                                Canvas.SetTop(TbAcc, AccTop);
                                Canvas.SetLeft(TbAcc, AccLeft);

                                // 只显示速度
                                TbAcc.Text = Score.GetValidSpeed().ToString("F2");
                                TbAcc.Foreground = Colors.GetSpeedColor(Score.GetValidSpeed());
                            }
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
                tb.FontSize = 40.0;
                tb.FontFamily = GetCurrentFontFamily();// new FontFamily(Config.GetString("字体"));
                tb.Background = BdDisplay.Background;
                tb.TextWrapping = TextWrapping.Wrap;
                tb.HorizontalAlignment = HorizontalAlignment.Stretch;
                tb.VerticalAlignment = VerticalAlignment.Top;
                tb.Foreground = Colors.DisplayForeground;


                string currentMatchText = string.Concat(TextInfo.Words);



                string t1 = currentMatchText.Replace('”', '\"').Replace('“', '\"').Replace('‘', '\'').Replace('’', '\'');
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
        /// <returns>目标滚动偏移量</returns>
        private double CalculateScrollOffset(double currentPosY)
        {
            // 将当前字符保持在屏幕中心位置（48%位置）
            double center = ScDisplay.ViewportHeight * 0.48;
            double offset = currentPosY - center;

            // 限制滚动范围
            if (offset < 0)
                offset = 0;
            if (offset > ScDisplay.ScrollableHeight)
                offset = ScDisplay.ScrollableHeight;

            return offset;
        }

        /// <summary>
        /// 执行滚动（带条件判断，避免频繁滚动）
        /// </summary>
        /// <param name="targetOffset">目标滚动偏移量</param>
        /// <param name="forceScroll">是否强制滚动（例如：换行、起始位置）</param>
        private void SmoothScrollTo(double targetOffset, bool forceScroll = false)
        {
            double fs = 40.0;
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
        private void UpdateZiTi()
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

            // 获取下一个需要打的字
            StringInfo si = new StringInfo(TbxInput.Text);
            int nextIndex = si.LengthInTextElements;

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
        /// 贪吃蛇模式更新 - 流动式显示前后字符
        /// </summary>
        private void SnakeModeUpdate(int nextToType)
        {
            // 获取配置（赛文API强制使用前20后30）
            int preShowCount;
            int postShowCount;

            if (StateManager.txtSource == TxtSource.raceApi)
            {
                // 赛文API强制使用固定值
                preShowCount = 20;
                postShowCount = 30;
            }
            else
            {
                // 其他模式使用配置
                preShowCount = Config.GetInt("贪吃蛇前显字数");
                postShowCount = Config.GetInt("贪吃蛇后显字数");
            }

            // 如果是第一次进入贪吃蛇模式，需要创建所有TextBlock
            bool isFirstTime = (TextInfo.Blocks.Count != TextInfo.Words.Count);
            if (isFirstTime)
            {
                // 清空显示区域
                TbDispay.Children.Clear();
                ScDisplay.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
                TextInfo.Blocks.Clear();

                // 获取字体设置
                var fm = GetCurrentFontFamily();
                double fs = 40.0;
                double height = fs * (1.0 + Config.GetDouble("行距"));
                double MinWidth = 40.0 * 0.9;

                ScDisplay.FontFamily = fm;
                ScDisplay.Foreground = Colors.DisplayForeground;
                ScDisplay.FontSize = fs;
                TbxInput.FontFamily = fm;

                // 创建所有字符的TextBlock
                for (int i = 0; i < TextInfo.Words.Count; i++)
                {
                    TextBlock tb = new TextBlock();
                    tb.Text = TextInfo.Words[i];
                    tb.Height = height;

                    // 引号的特殊处理（保持原逻辑）
                    if (tb.Text == "\u201c" || tb.Text == "\u2018")  // " 和 '
                    {
                        tb.MinWidth = MinWidth;
                        tb.TextAlignment = TextAlignment.Right;
                    }
                    else if (tb.Text == "\u201d" || tb.Text == "\u2019")  // " 和 '
                    {
                        tb.MinWidth = MinWidth;
                        tb.TextAlignment = TextAlignment.Left;
                    }

                    TextInfo.Blocks.Add(tb);
                    TbDispay.Children.Add(tb);
                }
            }

            // 更新所有字符的显示状态（背景色和透明度）
            for (int i = 0; i < TextInfo.Blocks.Count; i++)
            {
                TextBlock tb = TextInfo.Blocks[i];

                // 计算透明度
                double opacity = 1.0;
                int distance = Math.Abs(i - nextToType);

                if (i < nextToType)
                {
                    // 已打字符 - 向前渐变
                    int distanceFromCurrent = nextToType - i;
                    if (distanceFromCurrent > preShowCount)
                    {
                        // 超出显示范围，完全透明
                        opacity = 0.0;
                    }
                    else if (distanceFromCurrent > preShowCount - 10)
                    {
                        // 在渐变区域（最后10个字符）
                        double fadeDistance = distanceFromCurrent - (preShowCount - 10);
                        opacity = 1.0 - (fadeDistance / 10.0);
                    }
                    else
                    {
                        // 核心显示区域
                        opacity = 1.0;
                    }

                    // 设置背景色
                    if (i < TextInfo.wordStates.Count)
                    {
                        switch (TextInfo.wordStates[i])
                        {
                            case WordStates.WRONG:
                                // 盲打模式：不显示任何提示
                                // 跟打模式：错字显示红色
                                tb.Background = IsBlindType ? null : Colors.IncorrectBackground;
                                break;
                            case WordStates.RIGHT:
                                // 盲打模式：不显示任何提示
                                // 跟打模式：对的字显示绿色
                                tb.Background = IsBlindType ? null : Colors.CorrectBackground;
                                break;
                            default:
                                tb.Background = null;
                                break;
                        }
                    }

                    tb.Foreground = Colors.DisplayForeground;
                }
                else if (i == nextToType)
                {
                    // 当前要打的字符 - 不高亮
                    opacity = 1.0;
                    tb.Foreground = Colors.DisplayForeground;
                    tb.Background = null;
                }
                else
                {
                    // 未打字符 - 向后渐变
                    int distanceFromCurrent = i - nextToType;
                    if (distanceFromCurrent > postShowCount)
                    {
                        // 超出显示范围，完全透明
                        opacity = 0.0;
                    }
                    else if (distanceFromCurrent > postShowCount - 10)
                    {
                        // 在渐变区域（最后10个字符）
                        double fadeDistance = distanceFromCurrent - (postShowCount - 10);
                        opacity = 1.0 - (fadeDistance / 10.0);
                    }
                    else
                    {
                        // 核心显示区域
                        opacity = 1.0;
                    }

                    tb.Foreground = Colors.DisplayForeground;
                    tb.Background = null;
                }

                tb.Opacity = opacity;
            }

            // 滚动到当前字符位置
            {
                if (nextToType < TextInfo.Blocks.Count && nextToType >= 0)
                {
                    try
                    {
                        // 计算当前字符的Y坐标（相对于第一个Block）
                        double currentPosY = TextInfo.Blocks[nextToType].TranslatePoint(new Point(0, 0), TextInfo.Blocks[0]).Y
                            + TextInfo.Blocks[nextToType].ActualHeight / 2;

                        // 使用统一方法计算目标滚动位置（始终居中显示）
                        double targetOffset = CalculateScrollOffset(currentPosY);

                        // 贪吃蛇模式：使用条件判断滚动（起始位置强制滚动，避免跳动）
                        SmoothScrollTo(targetOffset, forceScroll: (nextToType == 0));

                        // 更新速度跟随提示位置
                        UpdateSpeedFollowHint(nextToType);
                    }
                    catch
                    {
                        // 忽略布局异常
                    }
                }
            }
        }

        /// <summary>
        /// 更新速度跟随提示位置
        /// </summary>
        private void UpdateSpeedFollowHint(int nextToType)
        {
            bool showSpeed = Config.GetBool("速度跟随提示") && !double.IsNaN(Score.GetValidSpeed()) && Score.GetValidSpeed() > 0;

            if (!showSpeed)
            {
                if (TbAcc.Visibility == Visibility.Visible)
                    TbAcc.Visibility = Visibility.Hidden;
            }
            else
            {
                if (TbAcc.Visibility == Visibility.Hidden)
                    TbAcc.Visibility = Visibility.Visible;

                double AccLeft = TextInfo.Blocks[nextToType].TranslatePoint(new Point(0, 0), TextInfo.Blocks[0]).X + TextInfo.Blocks[nextToType].ActualWidth / 3;
                double AccTop = TextInfo.Blocks[nextToType].TranslatePoint(new Point(0, 0), TextInfo.Blocks[0]).Y + TextInfo.Blocks[nextToType].ActualHeight - ScDisplay.VerticalOffset;
                Canvas.SetTop(TbAcc, AccTop);
                Canvas.SetLeft(TbAcc, AccLeft);

                // 只显示速度
                TbAcc.Text = Score.GetValidSpeed().ToString("F2");
                TbAcc.Foreground = Colors.GetSpeedColor(Score.GetValidSpeed());
            }
        }

        /// <summary>
        /// 文来简化渲染模式 - 使用单个 TextBlock 提升性能
        /// </summary>
        private void SimplifiedRenderMode(int nextToType)
        {
            // 如果是第一次加载或文本变更，需要重新渲染
            // 或者TextBlock不在显示区域中（修复BUG：确保TextBlock始终在显示区域）
            bool needRecreate = TextInfo.Blocks.Count != 1 ||
                               (TextInfo.Blocks.Count > 0 && !TbDispay.Children.Contains(TextInfo.Blocks[0]));

            if (needRecreate)
            {
                // 清空显示区域
                TbDispay.Children.Clear();
                ScDisplay.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                TextInfo.Blocks.Clear();

                // 获取字体设置
                var fm = GetCurrentFontFamily();
                double fs = 40.0;

                // 创建单个 TextBlock 显示所有文本
                TextBlock tb = new TextBlock();
                tb.FontSize = fs;
                tb.FontFamily = fm;
                tb.Background = BdDisplay.Background;
                tb.TextWrapping = TextWrapping.Wrap;
                tb.HorizontalAlignment = HorizontalAlignment.Stretch;
                tb.VerticalAlignment = VerticalAlignment.Top;
                tb.Foreground = Colors.DisplayForeground;

                // 设置行高
                tb.LineHeight = fs * (1.0 + Config.GetDouble("行距"));

                TextInfo.Blocks.Add(tb);
                TbDispay.Children.Add(tb);
            }

            // 更新文本内容和样式
            TextBlock textBlock = TextInfo.Blocks[0];
            textBlock.Inlines.Clear();

            // 重置对象池索引，准备复用Run对象
            ResetRunPool();

            // 合并连续相同状态的字符，减少 Run 对象数量
            StringBuilder currentText = new StringBuilder();
            WordStates currentState = WordStates.NO_TYPE;
            bool isFirstChar = true;

            for (int i = 0; i < TextInfo.Words.Count; i++)
            {
                WordStates state = i < TextInfo.wordStates.Count ? TextInfo.wordStates[i] : WordStates.NO_TYPE;

                // 如果状态改变，先输出之前的 Run
                if (!isFirstChar && state != currentState)
                {
                    Run r = GetRunFromPool(); // 从对象池获取，避免重复创建
                    r.Text = currentText.ToString();
                    r.Foreground = Colors.DisplayForeground; // 显式设置前景色，避免继承失败

                    switch (currentState)
                    {
                        case WordStates.WRONG:
                            // 盲打模式：不显示任何提示
                            // 跟打模式：错字显示红色
                            r.Background = IsBlindType ? null : Colors.IncorrectBackground;
                            break;
                        case WordStates.RIGHT:
                            // 盲打模式：不显示任何提示
                            // 跟打模式：对的字显示绿色
                            r.Background = IsBlindType ? null : Colors.CorrectBackground;
                            break;
                        default:
                            r.Background = null;
                            break;
                    }

                    textBlock.Inlines.Add(r);
                    currentText.Clear();
                }

                currentText.Append(TextInfo.Words[i]);
                currentState = state;
                isFirstChar = false;
            }

            // 输出最后一段
            if (currentText.Length > 0)
            {
                Run r = GetRunFromPool(); // 从对象池获取，避免重复创建
                r.Text = currentText.ToString();
                r.Foreground = Colors.DisplayForeground; // 显式设置前景色，避免继承失败

                switch (currentState)
                {
                    case WordStates.WRONG:
                        // 盲打模式：不显示任何提示
                        // 跟打模式：错字显示红色
                        r.Background = IsBlindType ? null : Colors.IncorrectBackground;
                        break;
                    case WordStates.RIGHT:
                        // 盲打模式：不显示任何提示
                        // 跟打模式：对的字显示绿色
                        r.Background = IsBlindType ? null : Colors.CorrectBackground;
                        break;
                    default:
                        r.Background = null;
                        break;
                }

                textBlock.Inlines.Add(r);
            }

            // 自动滚动到当前输入位置
            {
                try
                {
                    // 等待UI更新完成后再计算滚动位置
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            if (textBlock.Inlines.Count == 0)
                                return;

                            // 使用 TextPointer 精确定位到第 nextToType 个字符
                            // 注意：GetPositionAtOffset 的偏移量需要考虑特殊字符
                            TextPointer startPointer = textBlock.ContentStart;

                            // 计算实际的字符偏移量（包括换行符等）
                            int actualOffset = 0;
                            int targetCharIndex = 0;

                            // 遍历所有文本内容，找到第 nextToType 个实际字符的位置
                            foreach (Inline inline in textBlock.Inlines)
                            {
                                if (inline is Run run)
                                {
                                    if (targetCharIndex + run.Text.Length > nextToType)
                                    {
                                        // 目标字符在这个Run中
                                        int offsetInRun = nextToType - targetCharIndex;
                                        actualOffset += offsetInRun;
                                        break;
                                    }
                                    else
                                    {
                                        // 累加这个Run的长度
                                        targetCharIndex += run.Text.Length;
                                        actualOffset += run.Text.Length;
                                    }
                                }
                            }

                            // 获取目标位置的 TextPointer
                            TextPointer targetPointer = startPointer.GetPositionAtOffset(actualOffset, LogicalDirection.Forward);

                            if (targetPointer == null)
                                return;

                            // 获取目标位置的矩形区域
                            Rect rect = targetPointer.GetCharacterRect(LogicalDirection.Forward);

                            // 使用统一方法计算目标滚动位置（始终居中显示）
                            double targetOffset = CalculateScrollOffset(rect.Top);

                            // 执行滚动（起始位置强制滚动，其他时候由 SmoothScrollTo 自动判断）
                            SmoothScrollTo(targetOffset, forceScroll: (nextToType == 0));

                            // 更新速度跟随提示
                            UpdateSpeedFollowHintForSimplified(nextToType, rect, targetOffset);
                        }
                        catch
                        {
                            // 忽略滚动错误
                        }
                    }), DispatcherPriority.Loaded);
                }
                catch
                {
                    // 忽略滚动错误
                }
            }
        }

        /// <summary>
        /// 更新文来模式的速度跟随提示位置
        /// </summary>
        private void UpdateSpeedFollowHintForSimplified(int nextToType, Rect targetRect, double targetOffset)
        {
            bool showSpeed = Config.GetBool("速度跟随提示") && !Config.GetBool("盲打模式") && !double.IsNaN(Score.GetValidSpeed()) && Score.GetValidSpeed() > 0;

            if (!showSpeed)
            {
                if (TbAcc.Visibility == Visibility.Visible)
                    TbAcc.Visibility = Visibility.Hidden;
            }
            else
            {
                if (TbAcc.Visibility == Visibility.Hidden)
                    TbAcc.Visibility = Visibility.Visible;

                double AccLeft = targetRect.Left + targetRect.Width / 3;
                double AccTop = targetRect.Top + targetRect.Height - ScDisplay.VerticalOffset;
                Canvas.SetTop(TbAcc, AccTop);
                Canvas.SetLeft(TbAcc, AccLeft);

                // 只显示速度
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

            if (StateManager.txtSource == TxtSource.changeSheng ||  StateManager.txtSource == TxtSource.jbs || StateManager.txtSource == TxtSource.jisucup || StateManager.txtSource == TxtSource.raceApi || Config.GetBool("禁止F3重打"))
                return;




 
            LoadText(TextInfo.MatchText, RetypeType.retype, TxtSource.unchange);
   //         TbkStatusTop.Text = "重打";
            return;
    





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
                    int inputWordCount = new StringInfo(TbxInput.Text).LengthInTextElements;

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
            this.WindowStartupLocation = WindowStartupLocation.Manual;
            // InitCfg();

            double left = Config.GetDouble("窗口坐标X");
            double top = Config.GetDouble("窗口坐标Y");
            double width = Config.GetDouble("窗口宽度");
            double height = Config.GetDouble("窗口高度");

            // 获取屏幕工作区域
            var workArea = SystemParameters.WorkArea;

            // 设置合理的尺寸范围，避免窗口过大
            if (width <= 0 || width > 1200)
                width = 700;
            if (height <= 0 || height > 800)
                height = 500;

            // 限制窗口位置在屏幕内
            if (left < 0 || left + width > workArea.Width)
                left = Math.Max(0, (workArea.Width - width) / 2);
            if (top < 0 || top + height > workArea.Height)
                top = Math.Max(0, (workArea.Height - height) / 2);

            this.Left = left;
            this.Top = top;
            this.Width = width;
            this.Height = height;

            InitializeComponent();

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
                            raceMenuItems.ContainsKey("jisucup_loadArticle") ? raceMenuItems["jisucup_loadArticle"] : null
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














        const double CollapseHeight = 141;
        private void expanded(object sender, RoutedEventArgs e)
        {
            if (StateManager.ConfigLoaded)
            {
                content.Height = new GridLength(content.ActualHeight);
                Application.Current.MainWindow.Height += CollapseHeight;

                Config.Set("成绩面板展开", true);
                Config.Set("窗口宽度", this.Width, 0);
                Config.Set("窗口高度", this.Height, 0);



                content.Height = new GridLength(1.0, GridUnitType.Star);
            }



        }

        private void expd_Collapsed(object sender, RoutedEventArgs e)
        {

            if (StateManager.ConfigLoaded)
            {
                content.Height = new GridLength(content.ActualHeight);
                Application.Current.MainWindow.Height -= CollapseHeight;
                Config.Set("成绩面板展开", false);
                Config.Set("窗口宽度", this.Width, 0);
                Config.Set("窗口高度", this.Height, 0);



                content.Height = new GridLength(1.0, GridUnitType.Star);
            }


        }


        private void InitFontFamilySelector()
        {

            DirectoryInfo dr = new DirectoryInfo("字体");
            if (!dr.Exists)
                dr.Create();

            CultureInfo cn = CultureInfo.GetCultureInfo("zh-CN");
            CultureInfo en = CultureInfo.GetCultureInfo("en-US");

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

        // Run对象池，避免每次打字都创建新对象
        private List<Run> runPool = new List<Run>();
        private int runPoolIndex = 0;

        /// <summary>
        /// 从对象池获取或创建Run对象
        /// </summary>
        private Run GetRunFromPool()
        {
            Run r;
            if (runPoolIndex < runPool.Count)
            {
                // 复用现有对象，清除旧属性
                r = runPool[runPoolIndex++];
                r.Text = "";
                r.Background = null;
                r.Foreground = null;
            }
            else
            {
                // 创建新对象并加入池
                r = new Run();
                runPool.Add(r);
                runPoolIndex++;
            }
            return r;
        }

        /// <summary>
        /// 重置对象池索引（准备下一轮复用）
        /// </summary>
        private void ResetRunPool()
        {
            runPoolIndex = 0;
        }

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
            TbxInput.FontSize = 40.0;
            TbxResults.FontSize = 15.0;

            if (winTrainer != null)
            {
                WinTrainer.Current.DisplayGrid.Background = BdDisplay.Background;
                winTrainer.Background = this.Background;
            }

            ReadBlindType();




            this.Height = Config.GetDouble("窗口高度");
            this.Width = Config.GetDouble("窗口宽度");
            expd.IsExpanded = Config.GetBool("成绩面板展开");




            TbxInput.FontFamily = GetCurrentFontFamily();// new FontFamily(Config.GetString("字体")); ;
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
        }

        private void ReadBlindType ()
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
            TbxInput.FontSize = 40; // 默认跟打区字体大小
            TbxResults.FontSize = 15; // 默认成绩区字体大小

            if (winTrainer != null)
            {
                WinTrainer.Current.DisplayGrid.Background = BdDisplay.Background;
                winTrainer.Background = this.Background;
            }


            this.Height = Config.GetDouble("窗口高度");
            this.Width = Config.GetDouble("窗口宽度");
            expd.IsExpanded = Config.GetBool("成绩面板展开");

            TbxInput.FontFamily = GetCurrentFontFamily();// new FontFamily(Config.GetString("字体")); ;

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

                // 应用赛文子菜单颜色
                ApplyRaceMenuColors(menuBg, menuFg);

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
        /// 窗口级别的鼠标滚轮事件处理
        /// </summary>
        private void Window_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            // 只处理Ctrl+滚轮
            if (Keyboard.Modifiers != ModifierKeys.Control)
            {
                return; // 不设置e.Handled，让事件继续传递
            }

            // 获取鼠标相对于窗口的位置
            Point mousePos = e.GetPosition(this);

            // 检查鼠标是否在发文区（BdDisplay）内
            try
            {
                Point displayPos = BdDisplay.TransformToAncestor(this).Transform(new Point(0, 0));
                Rect displayRect = new Rect(displayPos.X, displayPos.Y, BdDisplay.ActualWidth, BdDisplay.ActualHeight);

                if (displayRect.Contains(mousePos))
                {
                    // 在发文区
                    e.Handled = true;
                    double delta = e.Delta > 0 ? 2 : -2;
                    double currentSize = 40.0;
                    double newSize = Math.Max(10, Math.Min(100, currentSize + delta));

                    // 如果使用简化渲染模式（文来），需要强制重新渲染
                    if (StateManager.txtSource == TxtSource.articlesender)
                    {
                        // 清空Blocks，强制SimplifiedRenderMode重新创建TextBlock
                        TextInfo.Blocks.Clear();
                        UpdateDisplay(UpdateLevel.Progress);
                    }
                    else
                    {
                        // 其他模式重新排列页面
                        UpdateDisplay(UpdateLevel.PageArrange);
                    }

                    System.Diagnostics.Debug.WriteLine($"发文区字体大小调整: {currentSize} -> {newSize}");
                    return;
                }
            }
            catch { }

            // 检查鼠标是否在输入区（TbxInput）内
            try
            {
                Point inputPos = TbxInput.TransformToAncestor(this).Transform(new Point(0, 0));
                Rect inputRect = new Rect(inputPos.X, inputPos.Y, TbxInput.ActualWidth, TbxInput.ActualHeight);

                if (inputRect.Contains(mousePos))
                {
                    // 在输入区
                    e.Handled = true;
                    double delta = e.Delta > 0 ? 2 : -2;
                    double currentSize = TbxInput.FontSize;
                    double newSize = Math.Max(10, Math.Min(100, currentSize + delta));
                    TbxInput.FontSize = newSize;
                    System.Diagnostics.Debug.WriteLine($"跟打区字体大小调整: {currentSize} -> {newSize}");
                    return;
                }
            }
            catch { }

            // 检查鼠标是否在成绩区（TbxResults）内
            try
            {
                if (TbxResults.IsVisible)
                {
                    Point resultsPos = TbxResults.TransformToAncestor(this).Transform(new Point(0, 0));
                    Rect resultsRect = new Rect(resultsPos.X, resultsPos.Y, TbxResults.ActualWidth, TbxResults.ActualHeight);

                    if (resultsRect.Contains(mousePos))
                    {
                        // 在成绩区
                        e.Handled = true;
                        double delta = e.Delta > 0 ? 2 : -2;
                        double currentSize = TbxResults.FontSize;
                        double newSize = Math.Max(10, Math.Min(100, currentSize + delta));
                        TbxResults.FontSize = newSize;
                        System.Diagnostics.Debug.WriteLine($"成绩区字体大小调整: {currentSize} -> {newSize}");
                        return;
                    }
                }
            }
            catch { }
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

            // 根据sender判断是哪个控件
            System.Windows.Controls.Control targetControl = null;

            // 如果sender是grid_c，需要根据鼠标位置判断是发文区还是输入区
            if (sender == grid_c)
            {
                Point mousePos = e.GetPosition(grid_c);

                // 检查鼠标是否在发文区（BdDisplay）内
                Point displayPos = BdDisplay.TransformToAncestor(grid_c).Transform(new Point(0, 0));
                Rect displayRect = new Rect(displayPos.X, displayPos.Y, BdDisplay.ActualWidth, BdDisplay.ActualHeight);

                // 检查鼠标是否在输入区（TbxInput）内
                Point inputPos = TbxInput.TransformToAncestor(grid_c).Transform(new Point(0, 0));
                Rect inputRect = new Rect(inputPos.X, inputPos.Y, TbxInput.ActualWidth, TbxInput.ActualHeight);

                if (displayRect.Contains(mousePos))
                {
                    // 在发文区 - 更新显示但不保存配置
                    UpdateDisplay(UpdateLevel.PageArrange);
                    System.Diagnostics.Debug.WriteLine($"发文区字体大小调整");
                    return;
                }
                else if (inputRect.Contains(mousePos))
                {
                    // 在输入区
                    targetControl = TbxInput;
                }
                else
                {
                    // 不在任何有效区域
                    return;
                }
            }
            else if (sender == TbxInput)
            {
                targetControl = TbxInput;
                System.Diagnostics.Debug.WriteLine("TbxInput Ctrl+滚轮触发");
            }
            else if (sender == TbDispay || sender == ScDisplay || sender == BdDisplay)
            {
                // 更新所有发文区的TextBlock字体
                UpdateDisplay(UpdateLevel.PageArrange);
                System.Diagnostics.Debug.WriteLine($"发文区字体大小调整");
                return;
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
                double currentSize = targetControl.FontSize;
                double newSize = Math.Max(10, Math.Min(100, currentSize + delta));

                // 更新字体大小
                targetControl.FontSize = newSize;

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
            ArticleManager.NextSection();

        }


        void DelayStop(object obj)
        {

            Dispatcher.Invoke(StopHelper);

        }


        private string TxtResult = "";

        public void UpdateTypingStat(string newReport = "")
        {

            CounterLog.Add("字数", CounterLog.Buffer[0]);
            CounterLog.Buffer[0] = 0;
            CounterLog.Add("击键数", CounterLog.Buffer[1]);
            CounterLog.Buffer[1] = 0;
            CounterLog.Write();

            StringBuilder sb = new StringBuilder();
            sb.Append("今日字数：");
            sb.Append(CounterLog.GetCurrent("字数") + CounterLog.Buffer[0]);
            sb.Append("   ");
            sb.Append("总字数：");
            sb.Append(CounterLog.GetSum("字数") + CounterLog.Buffer[0]);
           


            sb.AppendLine();

            if (newReport != "")
                TxtResult = newReport + "\n" + TxtResult;
            sb.Append(TxtResult);



            TbxResults.Text = sb.ToString();
        }


        public bool IsLookingType
        {
            get
            {
                return Config.GetBool("看打模式") && StateManager.retypeType != RetypeType.wrongRetype;
            }
        }


        public bool IsBlindType
        {
            get
            {
                return Config.GetBool("盲打模式") && StateManager.retypeType != RetypeType.wrongRetype;
            }
        }

        void StopHelper()
        {
            TbxInput.IsReadOnly = true;
            StateManager.typingState = TypingState.end;
            sw.Stop();
         

            Score.HitRate = Score.GetHit() / Score.Time.TotalSeconds;
            Score.KPW = Score.GetHit() / (double)Score.TotalWordCount;
            Score.Speed = (double)Score.TotalWordCount / Score.Time.TotalMinutes;




            Score.InputWordCount = new StringInfo(TbxInput.Text).LengthInTextElements;

            //计算错字

            if (IsLookingType)
            {
                string currentMatchText = string.Concat(TextInfo.Words);
                Score.Less = 0;
                Score.More = 0;

                string t1 = currentMatchText.Replace('”', '\"').Replace('“', '\"').Replace('‘', '\'').Replace('’', '\'');

                string t2 = TbxInput.Text.Replace('”', '\"').Replace('“', '\"').Replace('‘', '\'').Replace('’', '\'');
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

            TbkStatusTop.Text = Score.Progress();
            if (StateManager.retypeType != RetypeType.wrongRetype)
                UpdateTypingStat(Score.Report());// + " " + ;
            else
                UpdateTypingStat();
            string result = Score.Report();// + " " + Config.GetString("成绩签名");






            //自动发送成绩&&加载下一段




            if (StateManager.txtSource == TxtSource.jbs) //锦标赛
            {
                string inputMethod = Config.GetString("赛文输入法");
                System.Diagnostics.Debug.WriteLine($"[锦标赛] 读取到的输入法: [{inputMethod}], 是否为空: {string.IsNullOrWhiteSpace(inputMethod)}");
                if (string.IsNullOrWhiteSpace(inputMethod))
                {
                    MessageBox.Show("请先在设置中填写赛文输入法名称\n（网络设置 → 赛文输入法）", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                System.Diagnostics.Debug.WriteLine($"[极速杯] 读取到的输入法: [{inputMethod}], 是否为空: {string.IsNullOrWhiteSpace(inputMethod)}");
                if (string.IsNullOrWhiteSpace(inputMethod))
                {
                    MessageBox.Show("请先在设置中填写赛文输入法名称\n（网络设置 → 赛文输入法）", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                System.Diagnostics.Debug.WriteLine($"[赛文API] 读取到的输入法: [{inputMethod}], 是否为空: {string.IsNullOrWhiteSpace(inputMethod)}");
                if (string.IsNullOrWhiteSpace(inputMethod))
                {
                    MessageBox.Show("请先在设置中填写赛文输入法名称\n（网络设置 → 赛文输入法）", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                double speed = Score.GetValidSpeed();        // 速度
                double hitRate = Score.HitRate;              // 击键
                double codeLength = Score.KPW;               // 码长
                TimeSpan timeCost = Score.Time;              // 时间
                int correction = (int)Score.GetCorrection(); // 回改
                int backspaceCount = (int)Score.GetBacks();  // 退格
                int keyCount = (int)Score.GetHit();          // 键数
                double keyAccuracy = Score.GetAccuracy() * 100;  // 键准（转换为百分制0-100）
                double wordRate = Score.GetCiRatio() * 100;      // 打词率（转换为百分制0-100）
                int charCount = Score.TotalWordCount;        // 字数

                // 异步提交成绩
                Task.Run(async () =>
                {
                    string sendResult = await raceHelperV2.SubmitScore(
                        StateManager.CurrentRaceServerId,
                        StateManager.CurrentRaceId,
                        speed,
                        timeCost,
                        charCount,
                        hitRate,           // 击键
                        codeLength,        // 码长
                        backspaceCount,    // 退格
                        keyCount,          // 键数
                        keyAccuracy,       // 键准（百分制）
                        wordRate,          // 打词率（百分制）
                        inputMethod
                    );

                    // 在UI线程显示结果
                    Dispatcher.Invoke(() =>
                    {
                        if (sendResult == "提交成功")
                        {
                            // 显示提交成功信息，按照用户指定的顺序和字段
                            string successMsg = $"提交成功！\n\n速度: {speed:F2}\n击键: {hitRate:F2}\n码长: {codeLength:F2}\n键准: {keyAccuracy:F2}%\n打词率: {wordRate:F2}%";
                            MessageBox.Show(successMsg, "赛文API成绩提交", MessageBoxButton.OK, MessageBoxImage.Information);
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

                if (!Config.GetBool("错字重打")) //没有错字，或没有错字重打
                {
                    NextAndSendArticle(result);
                }
                else //(Config.GetBool("错字重打"))
                {
                    if (StateManager.retypeType == RetypeType.wrongRetype ) //错字重打
                    {
                        if ( TextInfo.WrongRec.Count == 0) //错字重打后无错字
                        {
                            NextAndSendArticle();
                        }
                       else
                        { }
                    }
                    else// (StateManager.retypeType != RetypeType.wrongRetype)//非错字重打，正文或普通重打
                    {
                        if (TextInfo.WrongRec.Count == 0) //一次打对无错字
                        {
                            NextAndSendArticle(result);
                        }
                        else //有错字，只发成绩
                        {
                            if (Config.GetBool("自动发送成绩"))
                            {
                                QQHelper.SendQQMessage(QQGroupName, result, 250, this);
                            }
                        }
                    }



                }
            }
            else if (StateManager.txtSource == TxtSource.articlesender) //文来
            {
                // 错字重打逻辑
                if (Config.GetBool("错字重打") && StateManager.retypeType != RetypeType.slowRetype)
                {
                    if (StateManager.retypeType != RetypeType.wrongRetype) // 正文
                    {
                        if (TextInfo.WrongRec.Count > 0) // 有错字，根据"自动发送成绩"开关决定是否发送成绩
                        {
                            if (Config.GetBool("自动发送成绩"))
                            {
                                SendContentToClipboardOrQQ(result);
                            }
                        }
                    }
                    // 如果是错字重打或正文有错字，后续会在3088-3134行统一处理
                }
                else if (!Config.GetBool("慢字重打")) // 关闭了错字重打和慢字重打
                {
                    // 直接发送成绩+下一段
                    NextAndSendArticleSender(result);
                    return;
                }
                // 其他情况（开启了慢字重打），继续执行后续慢字检测逻辑
            }
            else if (StateManager.txtSource == TxtSource.trainer) //练单器
            {

                if (winTrainer != null)
                    winTrainer.GetNextRound(Score.GetAccuracy(), Score.HitRate, Score.Wrong, result);





            }
            else
            {
                if (StateManager.retypeType != RetypeType.wrongRetype)
                {

                    QQHelper.SendQQMessage(QQGroupName, result, 250, this);

                }
                else
                {
                   
                }
            }










            // 慢字检测逻辑（排除打单器和慢字重打本身）
            if (Config.GetBool("慢字重打") && StateManager.txtSource != TxtSource.trainer && StateManager.retypeType != RetypeType.slowRetype)
            {
                TextInfo.SlowRec.Clear();
                double slowThreshold = Config.GetDouble("慢字标准(单位:秒)") * 1000; // 转换为毫秒

                // === 调试信息收集 ===
                StringBuilder debugInfo = new StringBuilder();
                debugInfo.AppendLine("========== 慢字检测调试信息 ==========");
                debugInfo.AppendLine($"慢字标准: {slowThreshold}ms ({Config.GetDouble("慢字标准(单位:秒)")}秒)");
                debugInfo.AppendLine($"CommitTime数量: {Score.CommitTime.Count}");
                debugInfo.AppendLine($"CommitCharCount数量: {Score.CommitCharCount.Count}");
                debugInfo.AppendLine($"TextInfo.Words数量: {TextInfo.Words.Count}");

                if (Score.CommitTime.Count > 0)
                {
                    debugInfo.AppendLine("\n【上屏记录详情】:");
                    for (int i = 0; i < Score.CommitTime.Count; i++)
                    {
                        long time = Score.CommitTime[i];
                        int charCount = i < Score.CommitCharCount.Count ? Score.CommitCharCount[i] : 0;
                        string text = i < Score.CommitText.Count ? Score.CommitText[i] : "N/A";
                        long timeDiff = i > 0 ? (Score.CommitTime[i] - Score.CommitTime[i - 1]) : Score.CommitTime[i];

                        // 计算有效字符数（排除符号）
                        int validCharCount = 0;
                        if (i < Score.CommitText.Count)
                        {
                            StringInfo si = new StringInfo(Score.CommitText[i]);
                            for (int j = 0; j < si.LengthInTextElements; j++)
                            {
                                string ch = si.SubstringByTextElements(j, 1);
                                if (!Score.ExcludePuncts.Contains(ch))
                                {
                                    validCharCount++;
                                }
                            }
                        }

                        double avgTime = validCharCount > 0 ? (double)timeDiff / validCharCount : 0;

                        debugInfo.AppendLine($"  [{i}] 文本:'{text}' 总字数:{charCount}, 有效字数:{validCharCount}, 总时:{timeDiff}ms, 平均:{avgTime:F0}ms/字{(avgTime > slowThreshold && validCharCount > 0 ? " ← 慢!" : "")}");
                    }
                }

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
                    StringInfo groupSi = new StringInfo(groupText);
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

                debugInfo.AppendLine("\n【检测结果】:");
                if (TextInfo.SlowRec.Count > 0)
                {
                    foreach (var kvp in TextInfo.SlowRec)
                    {
                        debugInfo.AppendLine($"  慢字: 位置{kvp.Key}, 字符'{kvp.Value}'");
                    }
                }
                else
                {
                    debugInfo.AppendLine("  无慢字");
                }

                debugInfo.AppendLine($"\n慢字总数: {TextInfo.SlowRec.Count}");
                debugInfo.AppendLine("==========================================");

                // 输出到日志（如果开启）
                DebugLog.AppendLine(debugInfo.ToString());
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
                    LoadText(sb.ToString(), retypeType, TxtSource.unchange, true, true);
                }
                else if (StateManager.txtSource == TxtSource.articlesender)
                {
                    // 文来模式：无错字且无慢字，进入下一段
                    // 如果是重打模式，不发送成绩；如果是正文，发送成绩
                    if (StateManager.retypeType == RetypeType.wrongRetype || StateManager.retypeType == RetypeType.slowRetype)
                    {
                        NextAndSendArticleSender();  // 重打完成，不发送成绩
                    }
                    else
                    {
                        NextAndSendArticleSender(result);  // 正文完成，发送成绩
                    }
                }
            }




        }



        Timer tm1;
        int lastInputLength = 0;  // 记录上次输入框长度，用于判断是否真正上屏

        void ProcInput()
        {


            if (TextInfo.Words.Count == 0)
                return;

            TextInfo.Check(TbxInput.Text);

            CalScore();



            if (IsInputEnd())
                Stop();


            UpdateDisplay(UpdateLevel.Progress);

            // 更新字提显示
            UpdateZiTi();

            void Stop()
            {
                Trace.WriteLine("stop");
              
                StateManager.LastType = true;
                Score.TotalWordCount = TextInfo.Words.Count;
                Score.Time = sw.Elapsed;
                timerProgress.Dispose();
                tm1 = new Timer(DelayStop, null, 150, Timeout.Infinite);





            }




            bool IsInputEnd()
            {



                if (!IsLookingType || TextInfo.Words.Count <= 3)
                {
                    StringInfo sb = new StringInfo(TbxInput.Text);

                    int lenA = TextInfo.Words.Count;
                    int lenB = sb.LengthInTextElements;

                    return lenA <= lenB && lenA >= 1 && TextInfo.Words.Last() == sb.SubstringByTextElements(lenA - 1, 1);

                }
                else
                {

                    StringInfo sb = new StringInfo(TbxInput.Text);

                    int lenA = TextInfo.Words.Count;
                    int lenB = sb.LengthInTextElements;

                    int LengthError = lenA / 10 + 1;

                    string la = "";

                    for (int i = lenA - 3; i <= lenA - 1; i++)
                        la += TextInfo.Words[i];

                    bool LastMatch = lenB > 3 && sb.SubstringByTextElements(lenB - 3, 3).Replace("”", "“") == la.Replace("”", "“");

                    bool LengthMatch = Math.Abs(lenB - lenA) <= LengthError;

                    return LastMatch && LengthMatch;
                }




            }


            void CalScore()
            {


                Score.TotalWordCount = TextInfo.Words.Count;
                Score.InputWordCount = new StringInfo(TbxInput.Text).LengthInTextElements;

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



        }

        CUIAutomation root = new CUIAutomation();

        const int KEY_DELAY = 25;

        public void NextAndSendArticle( string lastResult)
        {
            NextArticle();


            if (winArticle != null)
            {
                winArticle.UpdateDisplay();
            }
            string content2 = ArticleManager.GetFormattedCurrentSection();
            LoadText(content2, RetypeType.first, TxtSource.book, false, true);

            // 发送成绩和下一段内容
            if (QQGroupName != "")
            {
                if (Config.GetBool("自动发送成绩"))
                {
                    QQHelper.SendQQMessageD(QQGroupName, lastResult, content2, 150, this);
                }
                else
                {
                    QQHelper.SendQQMessage(QQGroupName, content2, 150, this);
                }
            }
            else
            {
                // 复制下一段内容到剪切板
                // 根据"自动发送成绩"开关决定是否复制成绩
                string messageToSend = content2;
                if (Config.GetBool("自动发送成绩"))
                {
                    messageToSend = lastResult + "\n" + content2;
                }
                Win32SetText(messageToSend);
                FocusInput();
            }
        }

        public void NextAndSendArticle()
        {
            NextArticle();


            if (winArticle != null)
            {
                winArticle.UpdateDisplay();
            }
            string content2 = ArticleManager.GetFormattedCurrentSection();
            LoadText(content2, RetypeType.first, TxtSource.book, false, true);

            SendContentToClipboardOrQQ(content2);
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
        private string FormatArticleSenderContent(string title, string content, string mark)
        {
            StringBuilder sb = new StringBuilder();

            // [难度xx]标题xx [字数xx]
            sb.AppendLine($"[{currentDifficultyText}]{title} [字数{content.Length}]");

            // 内容
            sb.AppendLine(content);

            // -----第xxx段-sha1(xxxxxxxx)-晴发文
            // 使用文来接口返回的mark（如"2-277101"）替代本地段号
            string sha1 = CalculateSHA1(content);
            sb.Append($"-----第{mark}段-sha1({sha1})-晴发文");

            return sb.ToString();
        }

        /// <summary>
        /// 通用发送方法（发送到剪切板或QQ群）
        /// </summary>
        private void SendContentToClipboardOrQQ(string content)
        {
            if (QQGroupName != "")
            {
                QQHelper.SendQQMessage(QQGroupName, content, 250, this);
            }
            else
            {
                Win32SetText(content);
                FocusInput();
            }
        }

        /// <summary>
        /// 文来模式：加载下一段并发送
        /// </summary>
        private void NextAndSendArticleSender(string lastResult = "")
        {
            string segment = articleCache.GetNextSegment();

            if (string.IsNullOrEmpty(segment))
            {
                // 已经是最后一段，自动加载新文章并发送
                LoadRandomArticle(true, lastResult);
            }
            else
            {
                // 格式化发文内容
                string title = articleCache.GetCurrentTitle();
                string mark = articleCache.GetCurrentMark();  // 使用文来接口返回的mark
                string formattedContent = FormatArticleSenderContent(title, segment, mark);

                // 加载下一段
                LoadText(segment, RetypeType.first, TxtSource.articlesender);

                // 重置进度条（LoadText后已显示标题）
                if (Config.GetBool("显示进度条"))
                    TitleProgressBar.Width = 0;

                // 发送成绩和下一段内容
                if (QQGroupName != "")
                {
                    if (!string.IsNullOrEmpty(lastResult))
                    {
                        // 有成绩：根据"自动发送成绩"开关决定
                        if (Config.GetBool("自动发送成绩"))
                        {
                            // 开启自动发送成绩：先发成绩，再发下一段
                            QQHelper.SendQQMessageD(QQGroupName, lastResult, formattedContent, 150, this);
                        }
                        else
                        {
                            // 未开启自动发送成绩：只发下一段文章
                            QQHelper.SendQQMessage(QQGroupName, formattedContent, 250, this);
                        }
                    }
                    else
                    {
                        // 无成绩：只发下一段
                        QQHelper.SendQQMessage(QQGroupName, formattedContent, 250, this);
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
                    FocusInput();
                }
            }
        }



        public void SendArticle()
        {
            string content = ArticleManager.GetFormattedCurrentSection();

            if (winArticle != null)
            {
                winArticle.UpdateDisplay();
            }

            if (content == null || content.Length == 0)
                return;

            LoadText(content, RetypeType.first, TxtSource.book, false);

            SendContentToClipboardOrQQ(content);






        }




        private void InputBox_TextInput(object sender, TextCompositionEventArgs e)
        {
            // 成功上屏，清除 composing 标记
            if (Score.IsComposing)
            {
                Score.IsComposing = false;
            }

            if (e.Text == "")
            {

                LogBack();


                return;
            }

            if (e.Text != "" && e.Text != "\r")
            {
                //启动 - TextInput 事件中也需触发计时开始（兼容某些输入法）
                if (StateManager.typingState == TypingState.pause || StateManager.typingState == TypingState.ready)
                {
                    if (StateManager.typingState == TypingState.ready && StateManager.retypeType != RetypeType.wrongRetype)
                        RetypeCounter.Add(TextInfo.TextMD5, 1);

                    sw.Start();
                    StateManager.typingState = TypingState.typing;
                    timerProgress = new Timer(timerUpdateProgress, null, 0, 250);
                }

                //分析打词率
                Score.AddInputStack(e.Text);

                //记录选重提交时间、字符
                StringInfo si = new StringInfo(e.Text);
                string last = si.SubstringByTextElements(si.LengthInTextElements - 1, 1);


                Score.CommitTime.Add(sw.ElapsedMilliseconds);
                Score.CommitStr.Add(last);
                Score.CommitCharCount.Add(si.LengthInTextElements);  // 记录本次上屏的字符数
                Score.CommitText.Add(e.Text);                        // 记录本次上屏的完整文本


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

        private void DisplayProgress()
        {
            Score.Time = sw.Elapsed;
            Score.HitRate = Score.GetHit() / sw.Elapsed.TotalSeconds;

            Score.KPW = Score.GetHit() / (double)Score.InputWordCount;
            Score.Speed = (double)Score.InputWordCount / Score.Time.TotalMinutes;



            TbkStatusTop.Text = Score.Progress();

        }

        Timer timerProgress;
        private void timerUpdateProgress(object obj)
        {
            Dispatcher.Invoke(DisplayProgress);
        }

        // 判断按键是否是有效的输入按键(字母、数字、符号、空格、退格等)
        private bool IsValidInputKey(Key key)
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

        private void InputBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // 禁用Tab键，防止用户用Tab清屏影响键准
            if (e.Key == Key.Tab)
            {
                e.Handled = true;
                return;
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


            //回车暂停
            if (e.Key == Key.Enter && StateManager.txtSource != TxtSource.changeSheng && StateManager.txtSource != TxtSource.jbs && StateManager.txtSource != TxtSource.jisucup && StateManager.txtSource != TxtSource.raceApi)
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


                return;
            }

            //统计键法
            if (e.Key == Key.ImeProcessed)
                Score.SetJianFa(e.ImeProcessedKey);
            else
                Score.SetJianFa(e.Key);

            //打字击键总数记录计数
            //       if ( Recorder.State != Recorder.RecorderState.Playing)
            CounterLog.Buffer[1]++;








            //启动 - 只有按下有效输入键才开始计时
            if ((StateManager.typingState == TypingState.pause || StateManager.typingState == TypingState.ready) && IsValidInputKey(e.Key))
            {
                var oldstate = StateManager.typingState;
                if (StateManager.typingState == TypingState.ready && StateManager.retypeType != RetypeType.wrongRetype)// && Recorder.State != Recorder.RecorderState.Playing)
                    RetypeCounter.Add(TextInfo.TextMD5, 1);

                sw.Start();

                StateManager.typingState = TypingState.typing;
                timerProgress = new Timer(timerUpdateProgress, null, 0, 250);

            }


            //退格
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
                    if (TbxInput.Text.Length > 0 && Score.ZiciStack.Count > 0)
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

                    {//统计选重
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
                            break;
                    }
                    break;
                default:
                    break;
            }


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
         //   Win32.DelayCount.Clear();
            string groupName = QQGroupName;
            if (groupName == "")
            {
                DebugLog.AppendLine("无群");
                return;
            }

            DebugLog.AppendLine("GetQQText");

            string MainTitle = "QQ";
            string allTxt = "";
            var q = root.GetRootElement().FindFirst(TreeScope.TreeScope_Children, root.CreatePropertyCondition(UIA_PropertyIds.UIA_NamePropertyId, MainTitle));
            if (q == null)
                return;

            if (null == q.FindFirst(TreeScope.TreeScope_Children, root.CreatePropertyCondition(UIA_PropertyIds.UIA_ControlTypePropertyId, UIA_ControlTypeIds.UIA_DocumentControlTypeId)))
            {
                var wp = q.GetCurrentPattern(UIA_PatternIds.UIA_WindowPatternId) as IUIAutomationWindowPattern;
                wp.SetWindowVisualState(WindowVisualState.WindowVisualState_Normal);
                q.SetFocus();

                Win32.Delay(50);
            }
            var msglist = q.FindFirst(TreeScope.TreeScope_Descendants,  root.CreateOrCondition( root.CreatePropertyCondition(UIA_PropertyIds.UIA_NamePropertyId, "消息列表"), root.CreatePropertyCondition(UIA_PropertyIds.UIA_LocalizedControlTypePropertyId, "主要")));
            var grouplist = q.FindFirst(TreeScope.TreeScope_Descendants, root.CreatePropertyCondition(UIA_PropertyIds.UIA_NamePropertyId, "会话列表"));

            if (msglist == null || grouplist == null)
            {
                return;
            }

            var edits = q.FindFirst(TreeScope.TreeScope_Descendants, root.CreateAndCondition(root.CreatePropertyCondition(UIA_PropertyIds.UIA_ControlTypePropertyId, UIA_ControlTypeIds.UIA_EditControlTypeId), root.CreatePropertyCondition(UIA_PropertyIds.UIA_NamePropertyId, groupName)));


            //点击群名
            if (edits == null)
            {
                var groups = grouplist.FindAll(TreeScope.TreeScope_Children, root.CreateOrCondition(root.CreatePropertyCondition(UIA_PropertyIds.UIA_ControlTypePropertyId, UIA_ControlTypeIds.UIA_WindowControlTypeId), root.CreatePropertyCondition(UIA_PropertyIds.UIA_ControlTypePropertyId, UIA_ControlTypeIds.UIA_GroupControlTypeId)));
                if (groups.Length > 0)
                {
                    for (int i = 0; i < groups.Length; i++)
                    {
                        var group = groups.GetElement(i);
                        string s = group.CurrentName;

                        if ((s + "").IndexOf(groupName + " ") != 0)
                            continue;


                        var sp = group.GetCurrentPattern(UIA_PatternIds.UIA_InvokePatternId) as IUIAutomationInvokePattern;
                        if (sp != null)
                        {
                            sp.Invoke();

                            Win32.Delay(801);
                            edits = q.FindFirst(TreeScope.TreeScope_Descendants, root.CreateAndCondition(root.CreatePropertyCondition(UIA_PropertyIds.UIA_ControlTypePropertyId, UIA_ControlTypeIds.UIA_EditControlTypeId), root.CreatePropertyCondition(UIA_PropertyIds.UIA_NamePropertyId, groupName)));

                            break;
                        }

                        }
                    }
                }

                /*
                //遍历群
                int counter = 0;
                while (edits == null)
                {
                    counter++;
                    if (counter > 100)
                        break;
                    CtrlDown();
                    edits = q.FindFirst(TreeScope.TreeScope_Descendants, root.CreateAndCondition(root.CreatePropertyCondition(UIA_PropertyIds.UIA_ControlTypePropertyId, UIA_ControlTypeIds.UIA_EditControlTypeId), root.CreatePropertyCondition(UIA_PropertyIds.UIA_NamePropertyId, title)));

                }
                */

            if (edits != null)
            {
                var msgs = msglist.FindAll(TreeScope.TreeScope_Descendants, root.CreatePropertyCondition(UIA_PropertyIds.UIA_ControlTypePropertyId, UIA_ControlTypeIds.UIA_TextControlTypeId));


                if (msgs != null && msgs.Length > 0)
                {
                    for (int i = msgs.Length - 1; i >= 0; i--)
                    {
                        allTxt += msgs.GetElement(i).CurrentName + "\n";
                    }
                }


 

                LoadText(allTxt, RetypeType.first, TxtSource.qq);
            }


            return;
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
        /// </summary>
        private void UpdateWindowTitle(int typedWords, int totalWords)
        {
            if (totalWords == 0)
            {
                // 没有加载文本时，只显示基本标题
                TbkTitle.Text = "晴跟打";
                return;
            }

            if (StateManager.txtSource == TxtSource.articlesender && articleCache.HasArticle())
            {
                string progress = articleCache.GetProgress();
                string title = articleCache.GetCurrentTitle();
                TbkTitle.Text = $"晴跟打 - {title} [{progress}]     {typedWords}/{totalWords}     {currentDifficultyText}";
            }
            else if (StateManager.txtSource == TxtSource.book && ArticleManager.Title != "")
            {
                // 文章管理模式，显示书名和段落进度
                string bookTitle = ArticleManager.Title.Replace(".txt", "").Replace(".Txt", "").Replace(".TXT", "").Replace(".epub", "").Replace(".Epub", "").Replace(".EPUB", "");
                string progress = $"{ArticleManager.Index}/{ArticleManager.MaxIndex}段";
                TbkTitle.Text = $"晴跟打 - {bookTitle} [{progress}]     {typedWords}/{totalWords}     {currentDifficultyText}";
            }
            else
            {
                TbkTitle.Text = $"晴跟打     {typedWords}/{totalWords}     {currentDifficultyText}";
            }
        }

        public void LoadText (CacheLoadInfo cli)
        {
            LoadText (cli.rawTxt, cli.retypeType, cli.source, cli.switchBack, cli.isAuto);
        }
        public void LoadText(string rawTxt, RetypeType retypeType, TxtSource source, bool switchBack = true, bool isAuto = false) //原文、来源、重打类型
        {
            if (Config.GetBool("禁止F3重打") && (retypeType == RetypeType.shuffle || retypeType == RetypeType.retype))
                return;

            var rt = ExtractRawTxt(rawTxt);

            if (rt.Item1 == "")
                return;

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

            //设置段号
            if (retypeType == RetypeType.wrongRetype)
            {

                Score.Paragraph = 0;
                Score.ArticleMark = "";  // 清空文来标记
            }
            else if (retypeType == RetypeType.shuffle || retypeType == RetypeType.retype)
            {
                Score.Paragraph = TextInfo.Paragraph;
                // 重打时保持原有的ArticleMark
            }
            else //(retypeType == RetypeType.first)
            {
                Score.Paragraph = rt.Item2;
                TextInfo.Paragraph = rt.Item2;

                // 如果是文来模式，获取并设置ArticleMark
                if (source == TxtSource.articlesender && articleCache.HasArticle())
                {
                    Score.ArticleMark = articleCache.GetCurrentMark();
                }
                else
                {
                    Score.ArticleMark = "";  // 其他模式清空ArticleMark
                }

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
            StringInfo si = new StringInfo(rt.Item1);

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

            // 文来模式：强制清空Blocks，确保重新渲染（修复显示不更新的bug）
            if (source == TxtSource.articlesender)
            {
                TbDispay.Children.Clear();
                TextInfo.Blocks.Clear();
            }





            //界面
            if (TextInfo.Words.Count > 0)
            {
                StateManager.typingState = TypingState.ready;
                StateManager.LastType = false;

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
       //         if (retypeType != RetypeType.first)
        //            TbkStatusTop.Text = "准备";

                sw.Reset();
                if (timerProgress != null)
                    timerProgress.Dispose();
                Score.Reset();



                if (!(IsLookingType && StateManager.LastType))
                    TbxInput.IsReadOnly = false;
                TbxInput.Clear();

                UpdateDisplay(UpdateLevel.PageArrange);

                // 重置滚动位置到顶部（避免乱序/重打时停留在中间位置）
                // 使用BeginInvoke确保在UI布局完成后再设置
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    ScDisplay.ScrollToVerticalOffset(0);
                }), System.Windows.Threading.DispatcherPriority.Loaded);

                // 计算并显示难度
                string currentText = String.Join("", TextInfo.Words);
                double difficulty = difficultyDict.Calc(currentText);
                currentDifficultyText = "难度:" + difficultyDict.DiffText(difficulty);
                TbkDifficulty.Text = currentDifficultyText;

                // 更新字提显示
                UpdateZiTi();

                // 更新标题显示（所有模式，初始字数为0）
                UpdateWindowTitle(0, TextInfo.Words.Count);


                if (switchBack)
                {
                    FocusInput();
                }



            }



        }

        private Tuple<string, int> ExtractRawTxt(string rawTxt)
        {



            string[] lines = rawTxt.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            Regex r = new Regex("-----第[0-9]+段");

            int paragraph = 0;
            string head = "";
            string content = "";
            string tail = "";

            if (rawTxt == "")
                return new Tuple<string, int>(content, paragraph);


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

            if (index >= 2) //赛文格式
            {

                head = lines[index - 2];
                content = lines[index - 1];
                tail = lines[index];

                var m = Regex.Match(tail, "第[0-9]+段");

                paragraph = Convert.ToInt32(m.Value.Substring(1, m.Value.Length - 2));

                if (head.Length >= 3 && head.Substring(0, 3) == "皇叔 ")
                    content = UnicodeBias(content);


            }
            else //非赛文格式
            {
                content = rawTxt.Replace("\n", "").Replace("\r", "").Replace("\t", "");
            }

            return new Tuple<string, int>(content, paragraph);


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
            string content = GetContentFromMatchText(cTxt);  // 解析格式化文本，提取正文
            if (!string.IsNullOrEmpty(content))
            {
                LoadText(content, RetypeType.first, TxtSource.clipboard);
            }
            else
            {
                // 如果解析失败，使用原始内容
                LoadText(cTxt, RetypeType.first, TxtSource.clipboard);
            }
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
            StringInfo si = new StringInfo(input);

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
            // 调试：打印所有可用数据（已禁用，避免文件写入错误）
            /*
            if (StateManager.typingState == TypingState.typing && e.Changes.Count > 0)
            {
                var change = e.Changes.First();
                string currentWord = TextInfo.Words.Count > 0 && TbxInput.Text.Length > 0
                    ? TextInfo.Words[Math.Min(TbxInput.Text.Length - 1, TextInfo.Words.Count - 1)]
                    : "";

                System.IO.File.AppendAllText(@"E:\debug_log.txt",
                    $"=== TextChanged ===\n" +
                    $"Time: {DateTime.Now:HH:mm:ss.fff}\n" +
                    $"TbxInput.Text: '{TbxInput.Text}' (Length={TbxInput.Text.Length})\n" +
                    $"Selection: Start={TbxInput.SelectionStart}, Length={TbxInput.SelectionLength}\n" +
                    $"CaretIndex: {TbxInput.CaretIndex}\n" +
                    $"Changes: Added={change.AddedLength}, Removed={change.RemovedLength}, Offset={change.Offset}\n" +
                    $"IsComposing: {Score.IsComposing}\n" +
                    $"TextInput flag: {StateManager.TextInput}\n" +
                    $"BimeHit: {Score.BimeHit}\n" +
                    $"typingState: {StateManager.typingState}\n" +
                    $"Current expected word: '{currentWord}'\n" +
                    $"==================\n");
            }
            */

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
            // 初始化字提功能
            ZiTiHelper.Initialize();

            InitDisplay();

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

                Config.Set("字体大小", e.NewValue, 1);

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


            SelectQQGroup(mi.Header.ToString());



        }

        private void SelectQQGroup(string groupName)
        {

            if (groupName == "-潜水-")
            {
                BtnF5.Content = "选群▸";
            }
            else if (groupName != null && groupName.Length > 0)
            {
                BtnF5.Content = "当前-" + groupName;
            }
            else
            {
                BtnF5.Content = "选群▸";
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

            {
                MenuItem mi = new MenuItem();
                mi.Header = "-潜水-";
                mi.Click += MenuQQ_Click;
                mi.IsCheckable = true;


                if (BtnF5.Content.ToString().Length < 6)
                    mi.IsChecked = true;

                MenuQQGroup.Items.Add(mi);
            }
            foreach (string groupName in groupList)
            {
                MenuItem mi = new MenuItem();

                mi.Header = groupName;
                mi.Click += MenuQQ_Click;
                mi.IsCheckable = true;

                if (BtnF5.Content.ToString().Length > 3 && BtnF5.Content.ToString().Substring(3) == groupName)
                    mi.IsChecked = true;

                MenuQQGroup.Items.Add(mi);

            }

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
            StringInfo si = new StringInfo(sl);

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
        /*
        private void InternalHotkeyF5(object sender, ExecutedRoutedEventArgs e)
        {
            HotKeyF5();
        }
        */
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
                    int inputWordCount = new StringInfo(TbxInput.Text).LengthInTextElements;

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

        private void InternalHotkeyCtrlP(object sender, ExecutedRoutedEventArgs e)
        {
            LoadNextSegment();
        }

        private void InternalHotkeyCtrlO(object sender, ExecutedRoutedEventArgs e)
        {
            LoadPreviousSegment();
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
            CounterLog.Add("字数", CounterLog.Buffer[0]);
            CounterLog.Buffer[0] = 0;
            CounterLog.Add("击键数", CounterLog.Buffer[1]);
            CounterLog.Buffer[1] = 0;
            CounterLog.Write();

            Config.Set("窗口坐标X", this.Left, 0);
            Config.Set("窗口坐标Y", this.Top, 0);
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

            // 根据主题计算鼠标悬停颜色
            System.Windows.Media.Color hoverBgColor;
            System.Windows.Media.Color hoverFgColor;

            if (isDarkTheme)
            {
                // 暗色主题：悬停时背景变亮，文字保持原色
                hoverBgColor = System.Windows.Media.Color.FromRgb(
                    (byte)Math.Min(255, bgColor.R + 40),
                    (byte)Math.Min(255, bgColor.G + 40),
                    (byte)Math.Min(255, bgColor.B + 40)
                );
                hoverFgColor = menuFg.Color;
            }
            else
            {
                // 亮色主题：悬停时背景变深蓝色，文字变白
                hoverBgColor = System.Windows.Media.Color.FromRgb(51, 153, 255); // 蓝色高亮
                hoverFgColor = System.Windows.Media.Colors.White;
            }

            // 创建触发器用于鼠标悬停
            var trigger = new Trigger
            {
                Property = MenuItem.IsHighlightedProperty,
                Value = true
            };
            trigger.Setters.Add(new Setter(MenuItem.BackgroundProperty, new System.Windows.Media.SolidColorBrush(hoverBgColor)));
            trigger.Setters.Add(new Setter(MenuItem.ForegroundProperty, new System.Windows.Media.SolidColorBrush(hoverFgColor)));

            style.Triggers.Add(trigger);

            return style;
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
        /// 初始化赛文菜单（动态生成菜单项）
        /// </summary>
        private void InitializeRaceMenu()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== InitializeRaceMenu 开始 ===");

                // ✨ 清空旧菜单项，防止重复
                MenuItemRace.Items.Clear();
                System.Diagnostics.Debug.WriteLine("  已清空旧菜单");

                // 获取菜单颜色配置
                var menuBg = Colors.FromString(Config.GetString("菜单背景色"));
                var menuFg = Colors.FromString(Config.GetString("菜单字体色"));

                // 创建MenuItem样式以支持暗色主题
                var menuItemStyle = CreateMenuItemStyle(menuBg, menuFg);

                // ========== 第一部分：添加所有赛文API服务器 ==========
                var serverManager = new TypeSunny.Net.RaceServerManager();
                var servers = serverManager.GetAllServers();

                if (servers == null)
                {
                    servers = new List<TypeSunny.Net.RaceServer>();
                }

                // ✨ 关键修复：从 AccountSystemManager 同步登录信息到每个服务器
                var accountManager = new TypeSunny.Net.AccountSystemManager();
                System.Diagnostics.Debug.WriteLine("  检查是否需要同步账号登录信息到赛文服务器...");

                foreach (var server in servers)
                {
                    System.Diagnostics.Debug.WriteLine($"  处理服务器: {server.Name}, Url={server.Url}");

                    // 根据服务器名称查找对应的账号（服务名称通常为"赛文"或服务器名称）
                    // 尝试多个可能的服务名称
                    string[] possibleServiceNames = { "赛文", server.Name, $"赛文_{server.Id}" };
                    TypeSunny.Net.AccountInfo matchedAccount = null;

                    foreach (var serviceName in possibleServiceNames)
                    {
                        var account = accountManager.GetAccount(serviceName);
                        if (account != null && !string.IsNullOrWhiteSpace(account.Domain))
                        {
                            // 检查域名是否匹配
                            string serverDomain = ExtractDomainFromUrl(server.Url);
                            string accountDomain = ExtractDomainFromUrl(account.Domain);

                            System.Diagnostics.Debug.WriteLine($"    尝试服务名称: {serviceName}, 账号Domain={accountDomain}, 服务器Domain={serverDomain}");

                            if (serverDomain == accountDomain)
                            {
                                matchedAccount = account;
                                System.Diagnostics.Debug.WriteLine($"    ✓ 找到匹配的账号: {serviceName}, Username={account.Username}");
                                break;
                            }
                        }
                    }

                    // 如果找到匹配的账号，同步登录信息
                    if (matchedAccount != null && !string.IsNullOrWhiteSpace(matchedAccount.Username))
                    {
                        System.Diagnostics.Debug.WriteLine($"    ✓ 同步登录信息: UserId={matchedAccount.UserId}, Username={matchedAccount.Username}, DisplayName={matchedAccount.DisplayName}");
                        server.UserId = matchedAccount.UserId;
                        server.Username = matchedAccount.Username;
                        server.DisplayName = matchedAccount.DisplayName;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"    ⚠ 未找到匹配的账号");
                    }
                }

                foreach (var server in servers)
                {
                    // 创建服务器一级菜单
                    MenuItem serverMenu = new MenuItem();
                    serverMenu.Header = server.GetDisplayName();
                    serverMenu.Background = menuBg;
                    serverMenu.Foreground = menuFg;
                    serverMenu.Tag = $"server_{server.Id}";
                    serverMenu.Style = menuItemStyle;

                    // 登录/注册菜单
                    if (!server.IsLoggedIn())
                    {
                        // 未登录：显示登录和注册
                        MenuItem loginItem = new MenuItem { Header = "🔑 登录", Background = menuBg, Foreground = menuFg, Tag = $"server_{server.Id}", Style = menuItemStyle };
                        loginItem.Click += MenuItemRaceServerLogin_Click;
                        serverMenu.Items.Add(loginItem);

                        MenuItem registerItem = new MenuItem { Header = "📝 注册", Background = menuBg, Foreground = menuFg, Tag = $"server_{server.Id}", Style = menuItemStyle };
                        registerItem.Click += MenuItemRaceServerRegister_Click;
                        serverMenu.Items.Add(registerItem);

                        serverMenu.Items.Add(new Separator());
                    }
                    else
                    {
                        // 已登录：显示用户名
                        string displayName = string.IsNullOrWhiteSpace(server.DisplayName) ? server.Username : server.DisplayName;
                        MenuItem loginStatusItem = new MenuItem
                        {
                            Header = $"✅ {displayName ?? "未知用户"}",
                            Background = menuBg,
                            Foreground = menuFg,
                            IsEnabled = false,
                            Style = menuItemStyle
                        };
                        serverMenu.Items.Add(loginStatusItem);
                        serverMenu.Items.Add(new Separator());
                    }

                    // 添加该服务器的所有赛文（作为子菜单）
                    if (server.Races != null && server.Races.Count > 0)
                    {
                        foreach (var race in server.Races)
                        {
                            // 创建赛文菜单项 - 加强视觉效果
                            MenuItem raceMenu = new MenuItem
                            {
                                Header = $"▸ {race.Name}",
                                Background = menuBg,
                                Foreground = menuFg,
                                FontWeight = System.Windows.FontWeights.SemiBold,
                                FontSize = 13,
                                Style = menuItemStyle
                            };

                            // 添加赛文信息说明（调整颜色以适应暗色主题）
                            string diffStr = $"难度{race.DifficultyGroup}";
                            string submitStr = race.AllowResubmit ? "可重复" : "每日一次";

                            // 根据背景色计算合适的灰色文字颜色
                            var infoColor = GetSecondaryTextColor(menuBg);

                            MenuItem infoItem = new MenuItem
                            {
                                Header = $"      {diffStr} · {submitStr}",
                                Background = menuBg,
                                Foreground = new System.Windows.Media.SolidColorBrush(infoColor),
                                IsEnabled = false,
                                FontSize = 10.5,
                                Style = menuItemStyle
                            };
                            raceMenu.Items.Add(infoItem);

                            // 子菜单1：发文
                            MenuItem loadArticleItem = new MenuItem
                            {
                                Background = menuBg,
                                Foreground = menuFg,
                                Tag = $"server_{server.Id}_race_{race.Id}_load",
                                FontSize = 12,
                                Style = menuItemStyle
                            };

                            // 根据状态设置不同的显示
                            if (!race.AllowResubmit && server.IsTodaySubmitted(race.Id))
                            {
                                loadArticleItem.Header = "      ✓ 今日已完成";
                                // 根据主题使用合适的成功颜色
                                var successColor = GetSuccessColor(menuBg);
                                loadArticleItem.Foreground = new System.Windows.Media.SolidColorBrush(successColor);
                                loadArticleItem.IsEnabled = false;
                            }
                            else
                            {
                                loadArticleItem.Header = "      📝 发文";
                                loadArticleItem.Click += MenuItemRaceServerLoadArticle_Click;
                            }

                            raceMenu.Items.Add(loadArticleItem);

                            // 子菜单2：排行榜
                            MenuItem leaderboardItem = new MenuItem
                            {
                                Header = "      🏆 排行榜",
                                Background = menuBg,
                                Foreground = menuFg,
                                Tag = $"server_{server.Id}_race_{race.Id}_leaderboard",
                                FontSize = 12,
                                Style = menuItemStyle
                            };
                            leaderboardItem.Click += MenuItemRaceLeaderboard_Click;
                            raceMenu.Items.Add(leaderboardItem);

                            serverMenu.Items.Add(raceMenu);
                        }
                    }
                    else
                    {
                        // 没有赛文，显示提示
                        MenuItem noRaceItem = new MenuItem
                        {
                            Header = "⚠️ 暂无赛文（请刷新）",
                            Background = menuBg,
                            Foreground = menuFg,
                            IsEnabled = false,
                            Style = menuItemStyle
                        };
                        serverMenu.Items.Add(noRaceItem);
                    }

                    // 底部添加刷新选项
                    serverMenu.Items.Add(new Separator());
                    MenuItem refreshItem = new MenuItem { Header = "🔄 刷新赛文列表", Background = menuBg, Foreground = menuFg, Tag = $"server_{server.Id}", FontSize = 11, Style = menuItemStyle };
                    refreshItem.Click += MenuItemRefreshServer_Click;
                    serverMenu.Items.Add(refreshItem);

                    // 将服务器菜单添加到主菜单
                    MenuItemRace.Items.Add(serverMenu);
                }

                // 分隔符
                if (servers.Count > 0)
                {
                    MenuItemRace.Items.Add(new Separator());
                }

                // ========== 第二部分：添加服务器菜单（简化为直接添加服务器） ==========
                MenuItem addServerItem = new MenuItem { Header = "➕ 添加服务器", Background = menuBg, Foreground = menuFg, Style = menuItemStyle };
                addServerItem.Click += MenuItemAddRaceServer_Click;
                MenuItemRace.Items.Add(addServerItem);

                MenuItemRace.Items.Add(new Separator());

                // ========== 第三部分：添加传统赛文源（锦标赛、极速杯） ==========
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
                    parentMenu.Header = config.Name;

                    // 应用菜单颜色
                    parentMenu.Background = menuBg;
                    parentMenu.Foreground = menuFg;

                    // 为每个功能创建子菜单项
                    foreach (var feature in config.Features)
                    {
                        MenuItem subMenu = new MenuItem();
                        subMenu.Header = feature;
                        subMenu.Tag = config.Type + "_" + feature; // 用于识别事件来源

                        // 应用菜单颜色到子菜单
                        subMenu.Background = menuBg;
                        subMenu.Foreground = menuFg;

                        // 根据功能类型绑定事件
                        switch (feature)
                        {
                            case "载文":
                                if (config.Type == "jbs")
                                    subMenu.Click += MenuItemLoadArticle_Click;
                                else if (config.Type == "jisucup")
                                    subMenu.Click += MenuItemJiSuLoadArticle_Click;
                                // 保存载文菜单项引用（用于Helper）
                                raceMenuItems[config.Type + "_loadArticle"] = subMenu;
                                break;
                            case "登录":
                                if (config.Type == "jbs")
                                    subMenu.Click += MenuItemLogin_Click;
                                else if (config.Type == "jisucup")
                                    subMenu.Click += MenuItemJiSuLogin_Click;
                                // 保存登录菜单项引用（用于Helper）
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

                    // 将一级菜单添加到主菜单
                    MenuItemRace.Items.Add(parentMenu);
                }

                // 菜单创建完成后，更新Helper中的菜单项引用
                if (jbsHelper != null)
                {
                    jbsHelper.SetMenuItems(
                        raceMenuItems.ContainsKey("jbs_login") ? raceMenuItems["jbs_login"] : null,
                        raceMenuItems.ContainsKey("jbs_loadArticle") ? raceMenuItems["jbs_loadArticle"] : null
                    );
                    // 更新登录状态显示
                    jbsHelper.UpdateLoginStatus();
                    jbsHelper.UpdateArticleButtonStatus();
                }

                if (jiSuCupHelper != null)
                {
                    jiSuCupHelper.SetMenuItems(
                        raceMenuItems.ContainsKey("jisucup_login") ? raceMenuItems["jisucup_login"] : null,
                        raceMenuItems.ContainsKey("jisucup_loadArticle") ? raceMenuItems["jisucup_loadArticle"] : null
                    );
                    // 更新登录状态显示
                    jiSuCupHelper.UpdateLoginStatus();
                }

                System.Diagnostics.Debug.WriteLine("=== InitializeRaceMenu 结束 ===");
            }
            catch (Exception ex)
            {
                // 如果初始化菜单失败，记录错误并添加错误提示
                System.Diagnostics.Debug.WriteLine($"❌ 初始化赛文菜单失败: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");

                // 添加错误提示菜单项
                try
                {
                    MenuItem errorItem = new MenuItem
                    {
                        Header = "⚠️ 菜单加载失败，请检查配置",
                        IsEnabled = false
                    };
                    MenuItemRace.Items.Add(errorItem);
                }
                catch
                {
                    // 忽略二次错误
                }
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

                // 创建MenuItem样式以支持暗色主题
                var menuItemStyle = CreateMenuItemStyle(menuBg, menuFg);

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
                    MenuWenlai.Items.Add(new Separator());

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

                    MenuWenlai.Items.Add(new Separator());
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

                    MenuWenlai.Items.Add(new Separator());
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
                System.Diagnostics.Debug.WriteLine("  已登录，调用 InitializeRaceMenu...");
                InitializeRaceMenu();
                System.Diagnostics.Debug.WriteLine("  InitializeRaceMenu 完成");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("  未登录，跳过 InitializeRaceMenu");
            }
            System.Diagnostics.Debug.WriteLine("<<< MenuItemWenlaiLogin_Click 结束");
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
                InitializeRaceMenu();
            }
        }

        /// <summary>
        /// 文来菜单 - 退出登录
        /// </summary>
        private void MenuItemWenlaiLogout_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("确定要退出登录吗？", "确认", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                wenlaiHelper.Logout();
                // 重新初始化菜单以更新登录状态
                InitializeWenlaiMenu();
                MessageBox.Show("已退出登录", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// 文来菜单 - 服务器设置
        /// </summary>
        private void MenuItemWenlaiServerSettings_Click(object sender, RoutedEventArgs e)
        {
            wenlaiHelper.ShowServerSettingsDialog(this);
            // 服务器地址改变后，重新初始化菜单
            InitializeWenlaiMenu();
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
                MessageBox.Show("请先在设置中填写赛文输入法名称\n（网络设置 → 赛文输入法）", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                MessageBox.Show("请先在设置中填写赛文输入法名称\n（网络设置 → 赛文输入法）", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
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

        // 赛文API - 登录
        private void MenuItemRaceLogin_Click(object sender, RoutedEventArgs e)
        {
            raceHelper.ShowLoginDialog(this);
        }

        // 赛文API - 注册
        private void MenuItemRaceRegister_Click(object sender, RoutedEventArgs e)
        {
            raceHelper.ShowRegisterDialog(this);
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

        // 赛文服务器 - 登录
        private void MenuItemRaceServerLogin_Click(object sender, RoutedEventArgs e)
        {
            MenuItem menuItem = sender as MenuItem;
            if (menuItem == null || menuItem.Tag == null)
                return;

            string tag = menuItem.Tag.ToString();
            string serverId = tag.Replace("server_", "");

            raceHelperV2.ShowLoginDialog(this, serverId);

            // 刷新菜单
            RefreshRaceMenu();
        }

        // 赛文服务器 - 注册
        private void MenuItemRaceServerRegister_Click(object sender, RoutedEventArgs e)
        {
            MenuItem menuItem = sender as MenuItem;
            if (menuItem == null || menuItem.Tag == null)
                return;

            string tag = menuItem.Tag.ToString();
            string serverId = tag.Replace("server_", "");

            raceHelperV2.ShowRegisterDialog(this, serverId);
        }

        // 赛文服务器 - 载文
        private async void MenuItemRaceServerLoadArticle_Click(object sender, RoutedEventArgs e)
        {
            // 检查输入法是否已设置
            string inputMethod = Config.GetString("赛文输入法");
            if (string.IsNullOrWhiteSpace(inputMethod))
            {
                MessageBox.Show("请先在设置中填写赛文输入法名称\n（网络设置 → 赛文输入法）", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
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

            if (article != null && article.Length > 0 && !article.Contains("失败") && !article.Contains("不存在") && !article.Contains("未登录"))
            {
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
            else
            {
                MessageBox.Show(article, "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // 赛文服务器 - 排行榜
        private void MenuItemRaceLeaderboard_Click(object sender, RoutedEventArgs e)
        {
            MenuItem menuItem = sender as MenuItem;
            if (menuItem == null || menuItem.Tag == null)
                return;

            string tag = menuItem.Tag.ToString();
            // Tag格式: "server_{serverId}_race_{raceId}_leaderboard"
            var parts = tag.Split('_');
            if (parts.Length < 5)
                return;

            string serverId = parts[1];
            int raceId = int.Parse(parts[3]);

            // 获取服务器信息
            var serverManager = raceHelperV2.GetServerManager();
            var server = serverManager.GetAllServers().Find(s => s.Id == serverId);
            if (server == null)
            {
                MessageBox.Show("服务器不存在", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 获取赛文信息
            var race = server.Races.FirstOrDefault(r => r.Id == raceId);
            if (race == null)
            {
                MessageBox.Show("赛文不存在", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 打开排行榜窗口
            try
            {
                var leaderboardWindow = new WinRaceLeaderboard(
                    serverId,
                    raceId,
                    server.Url,
                    race.Name ?? "未命名赛文",
                    server.ClientKeyXml
                );
                leaderboardWindow.Owner = this;
                leaderboardWindow.ShowDialog();
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
                Text = Config.GetString("赛文服务器地址") ?? "https://typing.fcxxz.com/",
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

        // 刷新单个赛文服务器
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


        private void BtnJbs_Click(object sender, RoutedEventArgs e) //锦标赛
        {
            jbs = new JBS(Config.GetString("极速用户名"), Config.GetString("极速密码"));
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

        private void BtnSendArticle_Click(object sender, RoutedEventArgs e)
        {
            SendArticle();
        }

        private async void BtnRandomArticle_Click(object sender, RoutedEventArgs e)
        {
            await LoadRandomArticleAsync(true);
        }

        private async Task LoadRandomArticleAsync(bool autoSend = false, string lastResult = "")
        {
            // 禁用按钮，防止重复点击
            BtnRandomArticle.IsEnabled = false;
            BtnRandomArticle.Content = "加载中...";

            try
            {

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

                int difficulty = Config.GetInt("文来难度");
                int maxLength = Config.GetInt("文来字数");

                if (difficulty <= 0)
                    difficulty = 2; // 默认普通难度
                if (maxLength <= 0)
                    maxLength = 500; // 默认500字

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
                        // 使用自定义对话框提供"登录"和"注册"两个选项
                        var dialog = new Window
                        {
                            Title = "需要登录",
                            Width = 360,
                            Height = 180,
                            WindowStartupLocation = WindowStartupLocation.CenterOwner,
                            Owner = this,
                            ResizeMode = ResizeMode.NoResize
                        };

                        var grid = new Grid();
                        grid.Margin = new Thickness(20);
                        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) });
                        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                        var message = new TextBlock
                        {
                            Text = article.Content + "\n\n请选择操作：",
                            TextWrapping = TextWrapping.Wrap,
                            FontSize = 14
                        };
                        Grid.SetRow(message, 0);
                        grid.Children.Add(message);

                        var btnPanel = new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Center
                        };
                        Grid.SetRow(btnPanel, 2);

                        var btnLogin = new Button
                        {
                            Content = "🔑 登录",
                            Width = 90,
                            Height = 32,
                            Margin = new Thickness(0, 0, 10, 0),
                            FontSize = 14
                        };

                        var btnRegister = new Button
                        {
                            Content = "📝 注册",
                            Width = 90,
                            Height = 32,
                            Margin = new Thickness(0, 0, 10, 0),
                            FontSize = 14
                        };

                        var btnCancel = new Button
                        {
                            Content = "取消",
                            Width = 90,
                            Height = 32,
                            FontSize = 14
                        };

                        bool shouldRetry = false;

                        btnLogin.Click += (s, args) =>
                        {
                            dialog.Close();
                            wenlaiHelper.ShowLoginDialog(this);
                            shouldRetry = wenlaiHelper.IsLoggedIn();

                            // ✨ 登录成功后更新菜单（与右键菜单登录保持一致）
                            if (shouldRetry)
                            {
                                InitializeWenlaiMenu();
                                InitializeRaceMenu();  // 同域名的赛文也可能需要更新
                            }
                        };

                        btnRegister.Click += (s, args) =>
                        {
                            dialog.Close();
                            wenlaiHelper.ShowRegisterDialog(this);
                            shouldRetry = wenlaiHelper.IsLoggedIn();

                            // ✨ 注册成功后更新菜单（与右键菜单注册保持一致）
                            if (shouldRetry)
                            {
                                InitializeWenlaiMenu();
                                InitializeRaceMenu();  // 同域名的赛文也可能需要更新
                            }
                        };

                        btnCancel.Click += (s, args) =>
                        {
                            dialog.Close();
                        };

                        btnPanel.Children.Add(btnLogin);
                        btnPanel.Children.Add(btnRegister);
                        btnPanel.Children.Add(btnCancel);
                        grid.Children.Add(btnPanel);

                        dialog.Content = grid;
                        dialog.ShowDialog();

                        // 如果登录或注册成功，重新尝试加载文章
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
                LoadText(segment, RetypeType.first, TxtSource.articlesender);

                // 重置进度条（LoadText后已显示标题）
                if (Config.GetBool("显示进度条"))
                    TitleProgressBar.Width = 0;

                // 如果是自动发送（打完最后一段后自动加载新文章），需要发送格式化内容
                if (autoSend)
                {
                    string title = articleCache.GetCurrentTitle();
                    string mark = articleCache.GetCurrentMark();  // 使用文来接口返回的mark
                    string formattedContent = FormatArticleSenderContent(title, segment, mark);

                    if (QQGroupName != "")
                    {
                        if (!string.IsNullOrEmpty(lastResult))
                        {
                            // 有成绩：根据"自动发送成绩"开关决定
                            if (Config.GetBool("自动发送成绩"))
                            {
                                // 开启自动发送成绩：先发成绩，再发新文章第一段
                                QQHelper.SendQQMessageD(QQGroupName, lastResult, formattedContent, 150, this);
                            }
                            else
                            {
                                // 未开启自动发送成绩：只发新文章第一段
                                QQHelper.SendQQMessage(QQGroupName, formattedContent, 250, this);
                            }
                        }
                        else
                        {
                            // 无成绩：只发新文章第一段
                            QQHelper.SendQQMessage(QQGroupName, formattedContent, 250, this);
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
                        FocusInput();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载文章时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // 恢复按钮状态
                BtnRandomArticle.IsEnabled = true;
                BtnRandomArticle.Content = "文来Ctrl+R";
            }
        }

        private void LoadRandomArticle(bool autoSend = false, string lastResult = "")
        {
            // 调用异步版本，不等待结果
            _ = LoadRandomArticleAsync(autoSend, lastResult);
        }

        private void LoadNextSegment()
        {
            if (!articleCache.HasArticle())
            {
                MessageBox.Show("请先加载文章（Ctrl+R）", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string segment = articleCache.GetNextSegment();

            if (string.IsNullOrEmpty(segment))
            {
                // 已经是最后一段，自动加载新文章
                LoadRandomArticle();
            }
            else
            {
                // 加载下一段
                LoadText(segment, RetypeType.first, TxtSource.articlesender);

                // 重置进度条（LoadText后已显示标题）
                if (Config.GetBool("显示进度条"))
                    TitleProgressBar.Width = 0;
            }
        }

        private void LoadPreviousSegment()
        {
            if (!articleCache.HasArticle())
            {
                MessageBox.Show("请先加载文章（Ctrl+R）", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string segment = articleCache.GetPreviousSegment();

            // 加载上一段
            LoadText(segment, RetypeType.first, TxtSource.articlesender);

            // 重置进度条（LoadText后已显示标题）
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

        private void BtnPrev_Click(object sender, RoutedEventArgs e)
        {
            ArticleManager.PrevSection();
            LoadText(ArticleManager.GetFormattedCurrentSection(), RetypeType.first, TxtSource.book, false);
            TbxInput.Focus();

        }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {

            ArticleManager.NextSection();



            LoadText(ArticleManager.GetFormattedCurrentSection(), RetypeType.first, TxtSource.book, false);
            TbxInput.Focus();

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

        private void LogBack() //记录回改的字
        {
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

        private void LogCorrection()
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

            BtnF5.Content = "选群▸";


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



        WinTrainer winTrainer;
        private void ShowWinTrainer()
        {

            if (WinTrainer.Current != null)
           // if (winTrainer != null)
            {
                winTrainer.Show();
                winTrainer.Focus();
                winTrainer.Activate();
                //         winTrainer.InitText();
            }
            else
            {
                winTrainer = new WinTrainer();
                winTrainer.Show();
                winTrainer.Activate();
                //    winTrainer.InitText();
            }

        }


        public string QQGroupName
        {
            get
            {
                if (BtnF5.Content.ToString().Length > 3)
                {
                    return BtnF5.Content.ToString().Substring(3);
                }
                else
                    return "";
            }

        }


        public void FocusInput()
        {
            this.Activate();
            this.Topmost = true;  // important
            this.Topmost = false; // important
            this.Focus();
            TbxInput.Focus();
        }





        // SldBindLookUpdate 已移除 - 打字模式控件已移到设置窗口

        // CbBlindType控件已移到设置窗口

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
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
                BtnMaximize.Content = "◻";
            }
            else
            {
                this.WindowState = WindowState.Maximized;
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

