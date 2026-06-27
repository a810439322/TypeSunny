using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TypeSunny.Logs;
using TypeSunny.UI;
using TypeSunny.Utils;

namespace TypeSunny
{
    public class WinTrainerHistoryWindow : Window
    {
        // 手动 resize 的 Win32 互操作
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

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

        private const string AllTitles = "全部标题";

        // 持久化键
        private const string CfgWidth = "练单历史窗口宽度";
        private const string CfgHeight = "练单历史窗口高度";
        private const string CfgLeft = "练单历史窗口左边";
        private const string CfgTop = "练单历史窗口顶边";
        private const string CfgMaximized = "练单历史最大化状态";
        private const string CfgColWidthPrefix = "练单历史列宽_";

        private readonly string initialTitle;

        // 内容控件
        private readonly DockPanel root;
        private readonly StackPanel toolbar;
        private readonly TextBlock titleLabel;
        private readonly ComboBox titleSelector;
        private readonly Button refreshButton;
        private readonly DataGrid historyGrid;
        private readonly TextBlock statusText;

        // 自定义 chrome 控件
        private readonly Border mainBorder;
        private readonly Border titleBarBorder;
        private readonly TextBlock titleBarTitle;
        private readonly TextBlock titleBarStats;
        private readonly Button btnMinimize;
        private readonly Button btnMaximize;
        private readonly Button btnClose;

        private List<ArticleLog.ArticleRecord> allRecords = new List<ArticleLog.ArticleRecord>();

        public WinTrainerHistoryWindow(string currentTitle)
        {
            initialTitle = currentTitle;

            this.EnableEscapeToClose();

            Title = "练单历史";
            Width = 820;
            Height = 460;
            MinWidth = 700;
            MinHeight = 340;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            ResizeMode = ResizeMode.CanResize;
            Background = Brushes.Transparent;

            // ===== 内容 =====
            root = new DockPanel { Margin = new Thickness(10) };

            toolbar = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 8)
            };
            DockPanel.SetDock(toolbar, Dock.Top);

            titleLabel = new TextBlock
            {
                Text = "标题:",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            toolbar.Children.Add(titleLabel);

            titleSelector = new ComboBox
            {
                Width = 220,
                Height = 26,
                VerticalAlignment = VerticalAlignment.Center
            };
            titleSelector.SelectionChanged += (_, _) => ApplyTitleFilter();
            toolbar.Children.Add(titleSelector);

            refreshButton = new Button
            {
                Content = "刷新",
                Width = 70,
                Height = 26,
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Template = GetThemedButtonTemplate()
            };
            refreshButton.Click += (_, _) =>
            {
                LoadHistory();
                UpdateTitleBarStats();
            };
            toolbar.Children.Add(refreshButton);

            statusText = new TextBlock
            {
                Margin = new Thickness(0, 8, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(statusText, Dock.Bottom);

            historyGrid = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                SelectionMode = DataGridSelectionMode.Single,
                CanUserSortColumns = false,
                CanUserResizeColumns = true,
                RowHeight = 28,
                ColumnHeaderHeight = 32,
                FontSize = 13,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            AddColumns();

            root.Children.Add(toolbar);
            root.Children.Add(statusText);
            root.Children.Add(historyGrid);

            // ===== 自定义 chrome =====
            mainBorder = new Border
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                ClipToBounds = true,
                Margin = new Thickness(5)
            };

            var chromeGrid = new Grid();
            chromeGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
            chromeGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            titleBarBorder = new Border
            {
                CornerRadius = new CornerRadius(5, 5, 0, 0),
                ClipToBounds = true
            };
            titleBarBorder.MouseLeftButtonDown += TitleBar_MouseLeftButtonDown;
            Grid.SetRow(titleBarBorder, 0);
            chromeGrid.Children.Add(titleBarBorder);

            var titleBarGrid = new Grid();
            titleBarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            titleBarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            titleBarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            titleBarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            titleBarBorder.Child = titleBarGrid;

            try
            {
                var titleIcon = new Image
                {
                    Source = new BitmapImage(new Uri("pack://application:,,,/Resources/ico/sunny.ico", UriKind.Absolute)),
                    Width = 14,
                    Height = 14,
                    Margin = new Thickness(10, 0, 6, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(titleIcon, 0);
                titleBarGrid.Children.Add(titleIcon);
            }
            catch
            {
                // 资源加载失败忽略
            }

            titleBarTitle = new TextBlock
            {
                Text = Title,
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(titleBarTitle, 1);
            titleBarGrid.Children.Add(titleBarTitle);

            titleBarStats = new TextBlock
            {
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0),
                Opacity = 0.85
            };
            Grid.SetColumn(titleBarStats, 2);
            titleBarGrid.Children.Add(titleBarStats);

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal };
            Grid.SetColumn(btnPanel, 3);
            titleBarGrid.Children.Add(btnPanel);

            btnMinimize = new Button { Focusable = false };
            TitleBarButtonIcons.ApplyMinimizeButtonStyle(btnMinimize);
            btnMinimize.Click += (_, _) => WindowState = WindowState.Minimized;
            btnPanel.Children.Add(btnMinimize);

            btnMaximize = new Button { Focusable = false };
            TitleBarButtonIcons.SetMaximizeButtonState(btnMaximize, WindowState == WindowState.Maximized);
            btnMaximize.Click += (_, _) =>
            {
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            };
            btnPanel.Children.Add(btnMaximize);

            btnClose = new Button { Focusable = false };
            TitleBarButtonIcons.ApplyCloseButtonStyle(btnClose);
            btnClose.Click += (_, _) => Close();
            btnPanel.Children.Add(btnClose);

            StateChanged += (_, _) => TitleBarButtonIcons.SetMaximizeButtonState(btnMaximize, WindowState == WindowState.Maximized);

            Grid.SetRow(root, 1);
            chromeGrid.Children.Add(root);

            mainBorder.Child = chromeGrid;

            // 外层用 3x3 Grid 把 resize 边和角各占一格，避免叠加 z-order 导致的命中冲突
            var outerGrid = new Grid();
            outerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
            outerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            outerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
            outerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            outerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            outerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });

            // mainBorder 占中间 + 跨格，去掉 Margin 自己（外层 Grid 控制定位）
            mainBorder.Margin = new Thickness(0);
            Grid.SetRow(mainBorder, 0);
            Grid.SetColumn(mainBorder, 0);
            Grid.SetRowSpan(mainBorder, 3);
            Grid.SetColumnSpan(mainBorder, 3);
            // mainBorder 自己留 5px 视觉边距（在 8px 总格内）
            mainBorder.Margin = new Thickness(5);
            outerGrid.Children.Add(mainBorder);

            // 4 个角（固定在四角格子里）
            outerGrid.Children.Add(MakeResizeCell(0, 0, Cursors.SizeNWSE, HT_TOPLEFT));
            outerGrid.Children.Add(MakeResizeCell(0, 2, Cursors.SizeNESW, HT_TOPRIGHT));
            outerGrid.Children.Add(MakeResizeCell(2, 0, Cursors.SizeNESW, HT_BOTTOMLEFT));
            outerGrid.Children.Add(MakeResizeCell(2, 2, Cursors.SizeNWSE, HT_BOTTOMRIGHT));

            // 4 条边（占中间格，竖向/横向 stretch 在边内）
            outerGrid.Children.Add(MakeResizeCell(0, 1, Cursors.SizeNS, HT_TOP));
            outerGrid.Children.Add(MakeResizeCell(2, 1, Cursors.SizeNS, HT_BOTTOM));
            outerGrid.Children.Add(MakeResizeCell(1, 0, Cursors.SizeWE, HT_LEFT));
            outerGrid.Children.Add(MakeResizeCell(1, 2, Cursors.SizeWE, HT_RIGHT));

            Content = outerGrid;

            ApplyThemeColors();
            RestoreWindowState();
            RestoreColumnWidths();

            Loaded += (_, _) =>
            {
                LoadHistory();
                UpdateTitleBarStats();
            };

            Closing += (_, _) => SaveWindowState();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;

            if (e.ClickCount == 2)
            {
                // 双击切换最大化
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
                return;
            }

            try { DragMove(); }
            catch { /* 状态不允许时忽略 */ }
        }

        private Border MakeResizeCell(int row, int col, Cursor cursor, int hitTest)
        {
            var b = new Border { Background = Brushes.Transparent, Cursor = cursor };
            Grid.SetRow(b, row);
            Grid.SetColumn(b, col);
            b.MouseLeftButtonDown += (s, e) => ResizeDrag(hitTest);
            return b;
        }

        private void ResizeDrag(int hitTest)
        {
            if (ResizeMode != ResizeMode.CanResize && ResizeMode != ResizeMode.CanResizeWithGrip) return;
            if (WindowState != WindowState.Normal) return;

            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;

            ReleaseCapture();
            SendMessage(hwnd, WM_NCLBUTTONDOWN, (IntPtr)hitTest, IntPtr.Zero);
        }

        private static ControlTemplate _cachedThemedButtonTemplate;

        private static ControlTemplate GetThemedButtonTemplate()
        {
            if (_cachedThemedButtonTemplate != null)
                return _cachedThemedButtonTemplate;

            const string xaml = @"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Button'>
    <Border x:Name='b'
            Background='{TemplateBinding Background}'
            BorderBrush='{TemplateBinding BorderBrush}'
            BorderThickness='{TemplateBinding BorderThickness}'
            CornerRadius='3'
            SnapsToDevicePixels='True'>
        <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center' TextBlock.Foreground='{TemplateBinding Foreground}'/>
    </Border>
    <ControlTemplate.Triggers>
        <Trigger Property='IsMouseOver' Value='True'>
            <Setter TargetName='b' Property='Background' Value='#33888888'/>
        </Trigger>
        <Trigger Property='IsPressed' Value='True'>
            <Setter TargetName='b' Property='Background' Value='#55888888'/>
        </Trigger>
    </ControlTemplate.Triggers>
</ControlTemplate>";

            _cachedThemedButtonTemplate = (ControlTemplate)System.Windows.Markup.XamlReader.Parse(xaml);
            _cachedThemedButtonTemplate.Seal();
            return _cachedThemedButtonTemplate;
        }

        private void AddColumns()
        {
            historyGrid.Columns.Add(CreateColumn("时间", "TimeText", 140));
            historyGrid.Columns.Add(CreateStarColumn("标题", "Title"));
            historyGrid.Columns.Add(CreateColumn("均速", "AvgSpeed", 70, "{0:F2}"));
            historyGrid.Columns.Add(CreateColumn("均击", "AvgHitRate", 70, "{0:F2}"));
            historyGrid.Columns.Add(CreateColumn("目标击键", "TargetHitText", 80));
            historyGrid.Columns.Add(CreateColumn("键准", "AvgAccuracy", 70, "{0:F2}%"));
            historyGrid.Columns.Add(CreateColumn("字数", "TotalWords", 70));
            historyGrid.Columns.Add(CreateColumn("实际", "InputWords", 70));
            historyGrid.Columns.Add(CreateColumn("用时", "DurationText", 80));
        }

        private static DataGridTextColumn CreateColumn(string header, string bindingPath, double width, string stringFormat = null)
        {
            var binding = new Binding(bindingPath);
            if (!string.IsNullOrEmpty(stringFormat))
                binding.StringFormat = stringFormat;

            return new DataGridTextColumn
            {
                Header = header,
                Binding = binding,
                Width = new DataGridLength(width),
                MinWidth = Math.Min(width, 60)
            };
        }

        private static DataGridTextColumn CreateStarColumn(string header, string bindingPath)
        {
            return new DataGridTextColumn
            {
                Header = header,
                Binding = new Binding(bindingPath),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                MinWidth = 120
            };
        }

        private void ApplyThemeColors()
        {
            try
            {
                var windowBgColor = (Color)ColorConverter.ConvertFromString("#" + Config.GetString("窗体背景色"));
                var windowFgColor = (Color)ColorConverter.ConvertFromString("#" + Config.GetString("窗体字体色"));
                var buttonBgColor = (Color)ColorConverter.ConvertFromString("#" + Config.GetString("按钮背景色"));
                var buttonFgColor = (Color)ColorConverter.ConvertFromString("#" + Config.GetString("按钮字体色"));
                var menuBgColor = (Color)ColorConverter.ConvertFromString("#" + Config.GetString("菜单背景色"));
                var menuFgColor = (Color)ColorConverter.ConvertFromString("#" + Config.GetString("菜单字体色"));
                var borderColor = ThemeColorHelper.GetSubtleBorderColor(windowBgColor);

                var windowBgBrush = new SolidColorBrush(windowBgColor);
                var windowFgBrush = new SolidColorBrush(windowFgColor);
                var buttonBgBrush = new SolidColorBrush(buttonBgColor);
                var buttonFgBrush = new SolidColorBrush(buttonFgColor);
                var menuBgBrush = new SolidColorBrush(menuBgColor);
                var menuFgBrush = new SolidColorBrush(menuFgColor);
                var borderBrush = new SolidColorBrush(borderColor);

                Foreground = windowFgBrush;

                mainBorder.Background = windowBgBrush;
                mainBorder.BorderBrush = borderBrush;

                titleBarBorder.Background = menuBgBrush;
                titleBarTitle.Foreground = menuFgBrush;
                titleBarStats.Foreground = menuFgBrush;
                btnMinimize.Foreground = menuFgBrush;
                btnMaximize.Foreground = menuFgBrush;
                btnClose.Foreground = menuFgBrush;

                root.Background = windowBgBrush;
                toolbar.Background = menuBgBrush;
                titleLabel.Foreground = windowFgBrush;
                statusText.Foreground = windowFgBrush;

                titleSelector.Background = menuBgBrush;
                titleSelector.Foreground = windowFgBrush;
                titleSelector.BorderBrush = borderBrush;

                refreshButton.Background = buttonBgBrush;
                refreshButton.Foreground = buttonFgBrush;
                refreshButton.BorderBrush = borderBrush;

                ApplyDataGridTheme(windowBgColor, menuBgColor, windowFgColor, borderColor);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"应用练单历史主题颜色失败: {ex.Message}");
            }
        }

        private void ApplyDataGridTheme(Color windowBgColor, Color menuBgColor, Color windowFgColor, Color borderColor)
        {
            var windowBgBrush = new SolidColorBrush(windowBgColor);
            var menuBgBrush = new SolidColorBrush(menuBgColor);
            var windowFgBrush = new SolidColorBrush(windowFgColor);
            var borderBrush = new SolidColorBrush(borderColor);
            var alternateBgBrush = new SolidColorBrush(ShiftColor(windowBgColor, ThemeColorHelper.IsDark(windowBgColor) ? 18 : -12));

            historyGrid.Background = windowBgBrush;
            historyGrid.Foreground = windowFgBrush;
            historyGrid.RowBackground = windowBgBrush;
            historyGrid.AlternatingRowBackground = alternateBgBrush;
            historyGrid.BorderBrush = borderBrush;
            historyGrid.HorizontalGridLinesBrush = borderBrush;

            historyGrid.ColumnHeaderStyle = new Style(typeof(DataGridColumnHeader))
            {
                Setters =
                {
                    new Setter(Control.BackgroundProperty, menuBgBrush),
                    new Setter(Control.ForegroundProperty, windowFgBrush),
                    new Setter(Control.BorderBrushProperty, borderBrush),
                    new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 1, 1)),
                    new Setter(Control.PaddingProperty, new Thickness(8, 4, 8, 4))
                }
            };

            historyGrid.RowStyle = new Style(typeof(DataGridRow))
            {
                Setters =
                {
                    new Setter(Control.BackgroundProperty, windowBgBrush),
                    new Setter(Control.ForegroundProperty, windowFgBrush),
                    new Setter(Control.BorderBrushProperty, borderBrush)
                }
            };

            historyGrid.CellStyle = new Style(typeof(DataGridCell))
            {
                Setters =
                {
                    new Setter(Control.BackgroundProperty, Brushes.Transparent),
                    new Setter(Control.ForegroundProperty, windowFgBrush),
                    new Setter(Control.BorderBrushProperty, borderBrush),
                    new Setter(Control.PaddingProperty, new Thickness(6, 0, 6, 0))
                }
            };
        }

        private static Color ShiftColor(Color color, int delta)
        {
            return Color.FromArgb(
                color.A,
                ClampToByte(color.R + delta),
                ClampToByte(color.G + delta),
                ClampToByte(color.B + delta));
        }

        private static byte ClampToByte(int value)
        {
            return (byte)Math.Max(0, Math.Min(255, value));
        }

        private void LoadHistory()
        {
            allRecords = TrainerLog.ReadRecentRecords();

            var titles = allRecords
                .Select(record => string.IsNullOrWhiteSpace(record.ArticleName) ? "未命名" : record.ArticleName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(title => title)
                .ToList();
            titles.Insert(0, AllTitles);

            titleSelector.ItemsSource = titles;

            if (!string.IsNullOrWhiteSpace(initialTitle) &&
                titles.Any(title => string.Equals(title, initialTitle, StringComparison.OrdinalIgnoreCase)))
            {
                titleSelector.SelectedItem = titles.First(title => string.Equals(title, initialTitle, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                titleSelector.SelectedIndex = 0;
            }

            ApplyTitleFilter();
        }

        private void ApplyTitleFilter()
        {
            if (historyGrid == null || titleSelector == null)
                return;

            string selectedTitle = titleSelector.SelectedItem as string;
            IEnumerable<ArticleLog.ArticleRecord> records = allRecords;

            if (!string.IsNullOrWhiteSpace(selectedTitle) && selectedTitle != AllTitles)
            {
                records = records.Where(record =>
                    string.Equals(record.ArticleName, selectedTitle, StringComparison.OrdinalIgnoreCase));
            }

            var items = records
                .OrderByDescending(record => record.Time)
                .Select(record => new TrainerHistoryItem(record))
                .ToList();

            historyGrid.ItemsSource = items;
            statusText.Text = items.Count > 0 ? $"共 {items.Count} 条记录" : "暂无记录";
        }

        private void UpdateTitleBarStats()
        {
            try
            {
                var snapshot = TrainerTitleWordStats.Read();
                titleBarStats.Text = $"今日练单 {snapshot.TodayWords:N0} 字，累计 {snapshot.TotalWords:N0} 字";
            }
            catch
            {
                // 统计读取失败保持空显示
            }
        }

        private void RestoreWindowState()
        {
            try
            {
                if (Config.dicts.TryGetValue(CfgWidth, out string ws) && double.TryParse(ws, out double w) && w >= MinWidth)
                    Width = w;
                if (Config.dicts.TryGetValue(CfgHeight, out string hs) && double.TryParse(hs, out double h) && h >= MinHeight)
                    Height = h;
                if (Config.dicts.TryGetValue(CfgLeft, out string ls) && double.TryParse(ls, out double l))
                    Left = l;
                if (Config.dicts.TryGetValue(CfgTop, out string ts) && double.TryParse(ts, out double t))
                    Top = t;
                if (Config.dicts.TryGetValue(CfgMaximized, out string ms) && bool.TryParse(ms, out bool maxed) && maxed)
                    WindowState = WindowState.Maximized;
            }
            catch
            {
                // 配置缺失或损坏时用默认值
            }
        }

        private void RestoreColumnWidths()
        {
            foreach (DataGridColumn col in historyGrid.Columns)
            {
                if (col.Width.IsStar) continue;
                string key = CfgColWidthPrefix + (col.Header?.ToString() ?? "");
                if (Config.dicts.TryGetValue(key, out string val) &&
                    !string.IsNullOrEmpty(val) &&
                    double.TryParse(val, out double cw) &&
                    cw >= 30)
                {
                    col.Width = new DataGridLength(cw);
                }
            }
        }

        private void SaveWindowState()
        {
            try
            {
                if (WindowState == WindowState.Normal)
                {
                    Config.dicts[CfgWidth] = Width.ToString();
                    Config.dicts[CfgHeight] = Height.ToString();
                    Config.dicts[CfgLeft] = Left.ToString();
                    Config.dicts[CfgTop] = Top.ToString();
                }
                Config.dicts[CfgMaximized] = (WindowState == WindowState.Maximized).ToString();

                foreach (DataGridColumn col in historyGrid.Columns)
                {
                    if (col.Width.IsStar) continue;
                    string key = CfgColWidthPrefix + (col.Header?.ToString() ?? "");
                    Config.dicts[key] = col.ActualWidth.ToString();
                }
                Config.WriteConfig(0);
            }
            catch
            {
                // 保存失败忽略
            }
        }

        private class TrainerHistoryItem
        {
            public TrainerHistoryItem(ArticleLog.ArticleRecord record)
            {
                TimeText = record.Time.ToString("yyyy-MM-dd HH:mm");
                Title = string.IsNullOrWhiteSpace(record.ArticleName) ? "未命名" : record.ArticleName;
                AvgSpeed = record.Speed;
                AvgHitRate = record.HitRate;
                TargetHitText = record.TargetHit > 0 ? record.TargetHit.ToString("F2") : "-";
                AvgAccuracy = record.Accuracy * 100;
                TotalWords = record.TotalWords;
                InputWords = record.InputWords;
                DurationText = FormatDuration(record.TotalSeconds);
            }

            public string TimeText { get; }
            public string Title { get; }
            public double AvgSpeed { get; }
            public double AvgHitRate { get; }
            public string TargetHitText { get; }
            public double AvgAccuracy { get; }
            public int TotalWords { get; }
            public int InputWords { get; }
            public string DurationText { get; }

            private static string FormatDuration(double totalSeconds)
            {
                if (totalSeconds <= 0)
                    return "00:00";

                var time = TimeSpan.FromSeconds(totalSeconds);
                if (time.TotalHours >= 1)
                    return string.Format("{0:00}:{1:00}:{2:00}", (int)time.TotalHours, time.Minutes, time.Seconds);

                return string.Format("{0:00}:{1:00}", (int)time.TotalMinutes, time.Seconds);
            }
        }
    }
}
