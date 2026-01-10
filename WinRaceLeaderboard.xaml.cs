using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json.Linq;
using TypeSunny.Net;

namespace TypeSunny
{
    public partial class WinRaceLeaderboard : Window
    {
        private string serverId;
        private int raceId;
        private RaceAPI raceAPI;
        private List<LeaderboardEntry> allEntries;
        private int currentPage = 1;
        private int pageSize = 50;
        private int totalPages = 1;

        public WinRaceLeaderboard(string serverId, int raceId, string serverUrl, string raceName, string clientKeyXml = null)
        {
            InitializeComponent();

            this.serverId = serverId;
            this.raceId = raceId;
            this.allEntries = new List<LeaderboardEntry>();

            // 初始化RaceAPI
            raceAPI = new RaceAPI(serverUrl, clientKeyXml);

            // 设置标题
            txtTitle.Text = $"{raceName} - 排行榜";
            Title = $"{raceName} - 排行榜";

            // 设置日期选择器为今天
            datePicker.SelectedDate = DateTime.Now;

            // 应用主题颜色
            ApplyThemeColors();

            // 加载数据
            LoadLeaderboard();
        }

        /// <summary>
        /// 应用主题颜色
        /// </summary>
        private void ApplyThemeColors()
        {
            try
            {
                // 读取主题颜色配置
                var windowBgStr = Config.GetString("窗体背景色");
                var windowFgStr = Config.GetString("窗体字体色");
                var btnBgStr = Config.GetString("按钮背景色");
                var btnFgStr = Config.GetString("按钮字体色");

                System.Diagnostics.Debug.WriteLine($"应用主题颜色: windowBg={windowBgStr}, windowFg={windowFgStr}");

                var windowBg = System.Windows.Media.ColorConverter.ConvertFromString(windowBgStr);
                var windowFg = System.Windows.Media.ColorConverter.ConvertFromString(windowFgStr);
                var btnBg = System.Windows.Media.ColorConverter.ConvertFromString(btnBgStr);
                var btnFg = System.Windows.Media.ColorConverter.ConvertFromString(btnFgStr);

                if (windowBg != null)
                {
                    var bgColor = (System.Windows.Media.Color)windowBg;
                    var bgBrush = new System.Windows.Media.SolidColorBrush(bgColor);

                    mainBorder.Background = bgBrush;

                    // 判断是否为暗色主题（亮度低于0.5为暗色）
                    double brightness = (bgColor.R * 0.299 + bgColor.G * 0.587 + bgColor.B * 0.114) / 255.0;
                    bool isDarkTheme = brightness < 0.5;

                    // 边框颜色根据主题调整
                    System.Windows.Media.Color borderColor;
                    if (isDarkTheme)
                    {
                        // 暗色主题：边框稍微变亮
                        borderColor = System.Windows.Media.Color.FromRgb(
                            (byte)Math.Min(255, bgColor.R + 50),
                            (byte)Math.Min(255, bgColor.G + 50),
                            (byte)Math.Min(255, bgColor.B + 50)
                        );
                    }
                    else
                    {
                        // 亮色主题：边框稍微变暗
                        borderColor = System.Windows.Media.Color.FromRgb(
                            (byte)Math.Max(0, bgColor.R - 40),
                            (byte)Math.Max(0, bgColor.G - 40),
                            (byte)Math.Max(0, bgColor.B - 40)
                        );
                    }
                    mainBorder.BorderBrush = new System.Windows.Media.SolidColorBrush(borderColor);

                    // DataGrid背景色
                    dgLeaderboard.Background = bgBrush;
                    dgLeaderboard.RowBackground = bgBrush;

                    // 交替行颜色
                    var alternateBg = bgColor;
                    if (isDarkTheme)
                    {
                        // 暗色主题：交替行稍微变亮
                        alternateBg.R = (byte)Math.Min(255, alternateBg.R + 20);
                        alternateBg.G = (byte)Math.Min(255, alternateBg.G + 20);
                        alternateBg.B = (byte)Math.Min(255, alternateBg.B + 20);
                    }
                    else
                    {
                        // 亮色主题：交替行稍微变暗
                        alternateBg.R = (byte)Math.Max(0, alternateBg.R - 15);
                        alternateBg.G = (byte)Math.Max(0, alternateBg.G - 15);
                        alternateBg.B = (byte)Math.Max(0, alternateBg.B - 15);
                    }
                    dgLeaderboard.AlternatingRowBackground = new System.Windows.Media.SolidColorBrush(alternateBg);

                    // DataGrid边框颜色
                    dgLeaderboard.BorderBrush = new System.Windows.Media.SolidColorBrush(borderColor);
                }

                if (windowFg != null)
                {
                    var fgBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)windowFg);
                    txtTitle.Foreground = fgBrush;
                    lblDate.Foreground = fgBrush;
                    txtStatus.Foreground = fgBrush;
                    txtPageInfo.Foreground = fgBrush;
                    dgLeaderboard.Foreground = fgBrush;

                    // 表头样式
                    var headerStyle = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
                    headerStyle.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, fgBrush));
                    headerStyle.Setters.Add(new Setter(System.Windows.Controls.Control.FontWeightProperty, System.Windows.FontWeights.Bold));
                    dgLeaderboard.ColumnHeaderStyle = headerStyle;
                }

                if (btnBg != null && btnFg != null)
                {
                    var btnBgBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)btnBg);
                    var btnFgBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)btnFg);

                    // 应用到所有按钮
                    btnPrevDay.Background = btnBgBrush;
                    btnPrevDay.Foreground = btnFgBrush;
                    btnNextDay.Background = btnBgBrush;
                    btnNextDay.Foreground = btnFgBrush;
                    btnRefresh.Background = btnBgBrush;
                    btnRefresh.Foreground = btnFgBrush;
                    btnFirst.Background = btnBgBrush;
                    btnFirst.Foreground = btnFgBrush;
                    btnPrev.Background = btnBgBrush;
                    btnPrev.Foreground = btnFgBrush;
                    btnNext.Background = btnBgBrush;
                    btnNext.Foreground = btnFgBrush;
                    btnLast.Background = btnBgBrush;
                    btnLast.Foreground = btnFgBrush;
                    btnClose.Background = btnBgBrush;
                    btnClose.Foreground = btnFgBrush;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"应用主题颜色失败: {ex.Message}");
                // 如果读取主题失败，使用默认浅色样式
                mainBorder.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 245, 245));
                mainBorder.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(204, 204, 204));
            }
        }

        private void Border_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // 拖动窗口
            this.DragMove();
        }

        private bool isResizing = false;
        private System.Windows.Point resizeStartPoint;

        private void ResizeGrip_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            isResizing = true;
            resizeStartPoint = e.GetPosition(this);
            resizeGrip.CaptureMouse();
            e.Handled = true;
        }

        protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (isResizing && e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
            {
                var currentPoint = e.GetPosition(this);
                var deltaX = currentPoint.X - resizeStartPoint.X;
                var deltaY = currentPoint.Y - resizeStartPoint.Y;

                double newWidth = this.Width + deltaX;
                double newHeight = this.Height + deltaY;

                if (newWidth >= this.MinWidth)
                {
                    this.Width = newWidth;
                    resizeStartPoint = new System.Windows.Point(currentPoint.X, resizeStartPoint.Y);
                }

                if (newHeight >= this.MinHeight)
                {
                    this.Height = newHeight;
                    resizeStartPoint = new System.Windows.Point(resizeStartPoint.X, currentPoint.Y);
                }
            }
        }

        protected override void OnMouseUp(System.Windows.Input.MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);

            if (isResizing)
            {
                isResizing = false;
                resizeGrip.ReleaseMouseCapture();
            }
        }

        private async void LoadLeaderboard()
        {
            try
            {
                txtStatus.Text = "加载中...";
                btnRefresh.IsEnabled = false;

                // 初始化API
                await raceAPI.InitializeAsync();

                // 获取选择的日期
                string dateStr = null;
                if (datePicker.SelectedDate.HasValue)
                {
                    dateStr = datePicker.SelectedDate.Value.ToString("yyyy-MM-dd");
                }

                // 获取排行榜数据
                var result = await raceAPI.GetLeaderboardAsync(raceId, dateStr, 1000);

                System.Diagnostics.Debug.WriteLine($"=== 排行榜API返回 ===");
                System.Diagnostics.Debug.WriteLine($"Success: {result.Success}");
                System.Diagnostics.Debug.WriteLine($"Message: {result.Message}");
                System.Diagnostics.Debug.WriteLine($"Data: {result.Data}");

                if (result.Success && result.Data != null)
                {
                    allEntries.Clear();

                    System.Diagnostics.Debug.WriteLine($"Data字段: {string.Join(", ", result.Data.Properties().Select(p => p.Name))}");

                    // 解析数据 - 字段名是 leaderboard
                    var records = result.Data["leaderboard"] as JArray;
                    System.Diagnostics.Debug.WriteLine($"records类型: {records?.GetType().Name}, 长度: {records?.Count}");

                    if (records != null)
                    {
                        int rank = 1;
                        foreach (var record in records)
                        {
                            System.Diagnostics.Debug.WriteLine($"记录{rank}: {record}");

                            // 按照用户指定的顺序：排名、用户名、速度、击键、码长、时间、回改、键数、键准、打词率、输入法、提交时间
                            var entry = new LeaderboardEntry
                            {
                                Rank = rank++,
                                Username = record["username"]?.ToString() ?? "",
                                Speed = FormatDouble(record["speed"]?.ToObject<double>() ?? 0),
                                HitRate = FormatDouble(record["keystroke"]?.ToObject<double>() ?? 0),  // 击键
                                CodeLength = FormatDouble(record["code_length"]?.ToObject<double>() ?? 0),
                                Time = FormatTime(record["time_cost"]?.ToObject<int>() ?? 0),  // 时间（毫秒转时分秒）
                                Correction = record["backspace_count"]?.ToObject<int>().ToString() ?? "0",  // 回改（暂用退格数）
                                KeyCount = record["key_count"]?.ToObject<int>().ToString() ?? "0",  // 键数
                                KeyAccuracy = FormatDouble(record["key_accuracy"]?.ToObject<double>() ?? 0) + "%",
                                WordRate = FormatDouble(record["word_rate"]?.ToObject<double>() ?? 0) + "%",
                                InputMethod = record["input_method"]?.ToString() ?? "",
                                SubmitTime = record["submit_time"]?.ToString() ?? ""
                            };
                            allEntries.Add(entry);
                        }
                    }

                    System.Diagnostics.Debug.WriteLine($"总共解析了 {allEntries.Count} 条记录");

                    // 计算总页数
                    totalPages = (int)Math.Ceiling((double)allEntries.Count / pageSize);
                    if (totalPages == 0) totalPages = 1;

                    currentPage = 1;
                    UpdatePage();

                    txtStatus.Text = $"共 {allEntries.Count} 条记录";
                }
                else
                {
                    txtStatus.Text = result.Message ?? "加载失败";
                    MessageBox.Show(result.Message ?? "加载排行榜失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                txtStatus.Text = "加载失败";
                MessageBox.Show($"加载排行榜失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnRefresh.IsEnabled = true;
            }
        }

        private void UpdatePage()
        {
            System.Diagnostics.Debug.WriteLine($"=== UpdatePage ===");
            System.Diagnostics.Debug.WriteLine($"当前页: {currentPage}, 总页数: {totalPages}");
            System.Diagnostics.Debug.WriteLine($"总记录数: {allEntries.Count}");

            // 计算当前页的数据范围
            int startIndex = (currentPage - 1) * pageSize;
            int endIndex = Math.Min(startIndex + pageSize, allEntries.Count);

            System.Diagnostics.Debug.WriteLine($"显示范围: {startIndex} - {endIndex}");

            var pageEntries = new List<LeaderboardEntry>();
            for (int i = startIndex; i < endIndex; i++)
            {
                pageEntries.Add(allEntries[i]);
            }

            System.Diagnostics.Debug.WriteLine($"当前页记录数: {pageEntries.Count}");
            if (pageEntries.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"第一条: {pageEntries[0].Rank} - {pageEntries[0].Username} - 速度{pageEntries[0].Speed} 击键{pageEntries[0].HitRate}");
            }

            // 强制刷新绑定
            dgLeaderboard.ItemsSource = null;
            dgLeaderboard.ItemsSource = pageEntries;

            System.Diagnostics.Debug.WriteLine($"DataGrid.ItemsSource已设置，当前项数: {dgLeaderboard.Items.Count}");

            // 更新页码信息
            txtPageInfo.Text = $"第 {currentPage}/{totalPages} 页";

            // 更新按钮状态
            btnFirst.IsEnabled = currentPage > 1;
            btnPrev.IsEnabled = currentPage > 1;
            btnNext.IsEnabled = currentPage < totalPages;
            btnLast.IsEnabled = currentPage < totalPages;
        }

        private string FormatDouble(double value)
        {
            return value.ToString("F2");
        }

        private string FormatTime(int milliseconds)
        {
            TimeSpan time = TimeSpan.FromMilliseconds(milliseconds);
            if (time.TotalHours >= 1)
            {
                return time.ToString(@"hh\:mm\:ss");
            }
            else
            {
                return time.ToString(@"mm\:ss");
            }
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadLeaderboard();
        }

        private void BtnFirst_Click(object sender, RoutedEventArgs e)
        {
            currentPage = 1;
            UpdatePage();
        }

        private void BtnPrev_Click(object sender, RoutedEventArgs e)
        {
            if (currentPage > 1)
            {
                currentPage--;
                UpdatePage();
            }
        }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            if (currentPage < totalPages)
            {
                currentPage++;
                UpdatePage();
            }
        }

        private void BtnLast_Click(object sender, RoutedEventArgs e)
        {
            currentPage = totalPages;
            UpdatePage();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BtnPrevDay_Click(object sender, RoutedEventArgs e)
        {
            if (datePicker.SelectedDate.HasValue)
            {
                datePicker.SelectedDate = datePicker.SelectedDate.Value.AddDays(-1);
                LoadLeaderboard();
            }
        }

        private void BtnNextDay_Click(object sender, RoutedEventArgs e)
        {
            if (datePicker.SelectedDate.HasValue)
            {
                datePicker.SelectedDate = datePicker.SelectedDate.Value.AddDays(1);
                LoadLeaderboard();
            }
        }
    }

    /// <summary>
    /// 排行榜条目
    /// </summary>
    public class LeaderboardEntry
    {
        public int Rank { get; set; }           // 排名
        public string Username { get; set; }     // 用户名
        public string Speed { get; set; }        // 速度
        public string HitRate { get; set; }      // 击键
        public string CodeLength { get; set; }   // 码长
        public string Time { get; set; }         // 时间
        public string Correction { get; set; }   // 回改
        public string KeyCount { get; set; }     // 键数
        public string KeyAccuracy { get; set; }  // 键准
        public string WordRate { get; set; }     // 打词率
        public string InputMethod { get; set; }  // 输入法
        public string SubmitTime { get; set; }   // 提交时间
    }
}
