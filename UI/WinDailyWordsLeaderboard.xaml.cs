using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TypeSunny.Net;
using TypeSunny.Utils;
using UiColors = TypeSunny.Utils.Colors;

namespace TypeSunny.UI
{
    public partial class WinDailyWordsLeaderboard : Window
    {
        private const int DefaultLimit = 100;
        private readonly IDailyWordsService dailyWordsService;
        private int loadVersion;
        private bool isCustomMaximized;
        private Rect restoreBounds;

        public WinDailyWordsLeaderboard(IDailyWordsService dailyWordsService)
        {
            InitializeComponent();
            this.dailyWordsService = dailyWordsService ?? throw new ArgumentNullException(nameof(dailyWordsService));
            this.EnableEscapeToClose();
            ApplyTheme();
            Loaded += async (s, e) => await LoadLeaderboardAsync();
        }

        private async Task LoadLeaderboardAsync()
        {
            int version = Interlocked.Increment(ref loadVersion);
            DailyWordsLeaderboardType type = RbTotal.IsChecked == true
                ? DailyWordsLeaderboardType.Total
                : DailyWordsLeaderboardType.Daily;

            TxtStatus.Text = "加载中...";
            BtnRefresh.IsEnabled = false;

            try
            {
                DateTime? date = type == DailyWordsLeaderboardType.Daily ? DateTime.Now.Date : (DateTime?)null;
                DailyWordsLeaderboardResult result = await dailyWordsService.GetLeaderboardAsync(
                    type,
                    date,
                    DefaultLimit,
                    CancellationToken.None);

                if (version != loadVersion)
                    return;

                if (result == null || !result.IsSuccess)
                {
                    LeaderboardGrid.ItemsSource = new List<Row>();
                    TxtStatus.Text = result == null || string.IsNullOrWhiteSpace(result.Message)
                        ? "榜单加载失败"
                        : result.Message;
                    return;
                }

                var rows = new List<Row>();
                foreach (DailyWordsLeaderboardEntry entry in result.Entries)
                    rows.Add(new Row(entry));

                LeaderboardGrid.ItemsSource = rows;
                TxtStatus.Text = BuildStatusText(result);
            }
            catch (Exception ex)
            {
                if (version == loadVersion)
                {
                    LeaderboardGrid.ItemsSource = new List<Row>();
                    TxtStatus.Text = "榜单加载失败: " + ex.Message;
                }
            }
            finally
            {
                BtnRefresh.IsEnabled = true;
            }
        }

        private static string BuildStatusText(DailyWordsLeaderboardResult result)
        {
            string prefix;
            if (result.Type == DailyWordsLeaderboardType.Total)
            {
                prefix = "总榜";
            }
            else
            {
                DateTime date = result.Date ?? DateTime.Now.Date;
                prefix = "日榜 " + date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }

            return result.Entries.Count == 0
                ? prefix + "，暂无数据"
                : prefix + "，共 " + result.Entries.Count.ToString(CultureInfo.InvariantCulture) + " 人";
        }

        private void ApplyTheme()
        {
            Brush bg = UiColors.FromString(Config.GetString("窗体背景色"));
            Brush fg = UiColors.FromString(Config.GetString("窗体字体色"));
            Brush panelBg = UiColors.FromString(Config.GetString("跟打区背景色"));
            Brush border = UiColors.FromString(Config.GetString("按钮背景色"));

            mainBorder.Background = bg;
            mainBorder.BorderBrush = border;
            titleBarGrid.Background = bg;
            txtTitle.Foreground = fg;
            TxtStatus.Foreground = fg;
            LeaderboardGrid.Background = panelBg;
            LeaderboardGrid.Foreground = fg;
            LeaderboardGrid.BorderBrush = border;
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            await LoadLeaderboardAsync();
        }

        private async void LeaderboardType_Checked(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded)
                return;

            await LoadLeaderboardAsync();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
        {
            if (isCustomMaximized)
            {
                Left = restoreBounds.X;
                Top = restoreBounds.Y;
                Width = restoreBounds.Width;
                Height = restoreBounds.Height;
                isCustomMaximized = false;
            }
            else
            {
                restoreBounds = new Rect(Left, Top, Width, Height);
                Rect workArea = SystemParameters.WorkArea;
                Left = workArea.Left;
                Top = workArea.Top;
                Width = workArea.Width;
                Height = workArea.Height;
                isCustomMaximized = true;
            }

            TitleBarButtonIcons.SetMaximizeButtonState(BtnMaximize, isCustomMaximized);
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DependencyObject source = e.OriginalSource as DependencyObject;
            while (source != null && source != this)
            {
                if (source is Button)
                    return;
                source = VisualTreeHelper.GetParent(source);
            }

            if (e.ClickCount == 2)
                BtnMaximize_Click(sender, e);
            else
                DragMove();
        }

        public sealed class Row
        {
            public Row(DailyWordsLeaderboardEntry entry)
            {
                RankText = entry.Rank.ToString(CultureInfo.InvariantCulture);
                Username = string.IsNullOrWhiteSpace(entry.Username)
                    ? "用户" + entry.UserId.ToString(CultureInfo.InvariantCulture)
                    : entry.Username;
                WordCountText = entry.WordCount.ToString(CultureInfo.InvariantCulture);
                SingleWordCountText = entry.SingleWordCount.ToString(CultureInfo.InvariantCulture);
                ArticleWordCountText = entry.ArticleWordCount.ToString(CultureInfo.InvariantCulture);
                ArticleAvgSpeedText = entry.ArticleAvgSpeed.ToString("F2", CultureInfo.InvariantCulture);
                SingleAvgKeystrokeText = entry.SingleAvgKeystroke.ToString("F2", CultureInfo.InvariantCulture);
            }

            public string RankText { get; private set; }
            public string Username { get; private set; }
            public string WordCountText { get; private set; }
            public string SingleWordCountText { get; private set; }
            public string ArticleWordCountText { get; private set; }
            public string ArticleAvgSpeedText { get; private set; }
            public string SingleAvgKeystrokeText { get; private set; }
        }
    }
}
