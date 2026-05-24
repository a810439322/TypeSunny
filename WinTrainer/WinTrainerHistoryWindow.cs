using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using TypeSunny.Logs;
using TypeSunny.Utils;

namespace TypeSunny
{
    public class WinTrainerHistoryWindow : Window
    {
        private const string AllTitles = "全部标题";

        private readonly string initialTitle;
        private readonly DockPanel root;
        private readonly StackPanel toolbar;
        private readonly TextBlock titleLabel;
        private readonly ComboBox titleSelector;
        private readonly Button refreshButton;
        private readonly DataGrid historyGrid;
        private readonly TextBlock statusText;
        private List<ArticleLog.ArticleRecord> allRecords = new List<ArticleLog.ArticleRecord>();

        public WinTrainerHistoryWindow(string currentTitle)
        {
            initialTitle = currentTitle;

            this.EnableEscapeToClose();

            Title = "练单历史";
            Width = 780;
            Height = 430;
            MinWidth = 680;
            MinHeight = 320;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            root = new DockPanel
            {
                Margin = new Thickness(10)
            };

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
                VerticalAlignment = VerticalAlignment.Center
            };
            refreshButton.Click += (_, _) => LoadHistory();
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
                RowHeight = 28,
                ColumnHeaderHeight = 32,
                FontSize = 13
            };

            AddColumns();

            root.Children.Add(toolbar);
            root.Children.Add(statusText);
            root.Children.Add(historyGrid);
            Content = root;
            ApplyThemeColors();

            Loaded += (_, _) => LoadHistory();
        }

        private void AddColumns()
        {
            historyGrid.Columns.Add(CreateColumn("时间", "TimeText", 140));
            historyGrid.Columns.Add(CreateColumn("标题", "Title", 160));
            historyGrid.Columns.Add(CreateColumn("均速", "AvgSpeed", 70, "{0:F2}"));
            historyGrid.Columns.Add(CreateColumn("均击", "AvgHitRate", 70, "{0:F2}"));
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
                Width = width,
                MinWidth = Math.Min(width, 70)
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
                var borderColor = ThemeColorHelper.GetSubtleBorderColor(windowBgColor);

                var windowBgBrush = new SolidColorBrush(windowBgColor);
                var windowFgBrush = new SolidColorBrush(windowFgColor);
                var buttonBgBrush = new SolidColorBrush(buttonBgColor);
                var buttonFgBrush = new SolidColorBrush(buttonFgColor);
                var menuBgBrush = new SolidColorBrush(menuBgColor);
                var borderBrush = new SolidColorBrush(borderColor);

                Background = windowBgBrush;
                Foreground = windowFgBrush;

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

        private class TrainerHistoryItem
        {
            public TrainerHistoryItem(ArticleLog.ArticleRecord record)
            {
                TimeText = record.Time.ToString("yyyy-MM-dd HH:mm");
                Title = string.IsNullOrWhiteSpace(record.ArticleName) ? "未命名" : record.ArticleName;
                AvgSpeed = record.Speed;
                AvgHitRate = record.HitRate;
                AvgAccuracy = record.Accuracy * 100;
                TotalWords = record.TotalWords;
                InputWords = record.InputWords;
                DurationText = FormatDuration(record.TotalSeconds);
            }

            public string TimeText { get; }
            public string Title { get; }
            public double AvgSpeed { get; }
            public double AvgHitRate { get; }
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
