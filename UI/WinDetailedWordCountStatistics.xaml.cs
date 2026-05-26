using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using TypeSunny.Logs;
using TypeSunny.Utils;
using UiColors = TypeSunny.Utils.Colors;

namespace TypeSunny.UI
{
    public partial class WinDetailedWordCountStatistics : Window
    {
        private readonly Window owner;
        private DetailedWordCountSnapshot currentSnapshot;
        private List<DetailedWordCountItem> currentCategoryItems = new List<DetailedWordCountItem>();
        private List<DetailedWordCountItem> currentDetailItems = new List<DetailedWordCountItem>();
        private string focusedCategoryKey = "";
        private List<DetailedWordCountChartItem> currentChartItems = new List<DetailedWordCountChartItem>();
        private readonly Dictionary<string, List<FrameworkElement>> pieElementsByKey = new Dictionary<string, List<FrameworkElement>>();
        private readonly Dictionary<string, DetailedWordCountChartItem> pieItemsByKey = new Dictionary<string, DetailedWordCountChartItem>();
        private readonly DispatcherTimer pieHoverLeaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
        private string hoveredPieKey = "";
        private Brush chartTextBrush = Brushes.Black;
        private Brush chartHoverTextBrush = Brushes.Black;
        private Brush chartConnectorBrush = Brushes.Gray;
        private Brush chartHoverConnectorBrush = Brushes.White;
        private Brush chartSliceStrokeBrush = Brushes.White;
        private Brush chartHoverStrokeBrush = Brushes.White;
        private Brush difficultyTrackBrush = new SolidColorBrush(Color.FromArgb(34, 0, 0, 0));
        private Brush difficultyHoverBrush = new SolidColorBrush(Color.FromArgb(28, 0, 0, 0));
        private Brush panelBorderBrush;
        private Brush normalToggleBackground;
        private Brush activeToggleBackground;
        private Brush normalToggleForeground;
        private Brush activeToggleForeground;
        private bool isLoading;
        private bool isCustomMaximized;
        private Rect restoreBounds = new Rect();

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_LEFT = 10;
        private const int HT_RIGHT = 11;
        private const int HT_TOP = 12;
        private const int HT_TOPLEFT = 13;
        private const int HT_TOPRIGHT = 14;
        private const int HT_BOTTOM = 15;
        private const int HT_BOTTOMLEFT = 16;
        private const int HT_BOTTOMRIGHT = 17;

        private static readonly Color[] ChartColors =
        {
            Color.FromRgb(72, 145, 220),
            Color.FromRgb(238, 126, 74),
            Color.FromRgb(95, 176, 110),
            Color.FromRgb(213, 94, 130),
            Color.FromRgb(140, 111, 204),
            Color.FromRgb(190, 151, 68),
            Color.FromRgb(76, 174, 171),
            Color.FromRgb(150, 150, 150),
            Color.FromRgb(100, 120, 170)
        };

        public WinDetailedWordCountStatistics(Window owner)
        {
            InitializeComponent();
            this.owner = owner;
            this.EnableEscapeToClose();
            ApplyThemeColors();
            pieHoverLeaveTimer.Tick += (s, e) =>
            {
                pieHoverLeaveTimer.Stop();
                if (!IsMouseOverHoveredPieGroup())
                    ClearPieHover();
            };

            Loaded += (s, e) =>
            {
                CenterOverOwner();
                LoadStatistics();
            };
        }

        private void LoadStatistics()
        {
            LoadStatistics(DateTime.Now);
        }

        private void LoadStatistics(DateTime refreshTime)
        {
            try
            {
                isLoading = true;
                int totalWords = CounterLog.GetSum("字数") + CounterLog.Buffer[0];
                currentSnapshot = DetailedWordCountLog.LoadSnapshot(totalWords, refreshTime);

                txtTotalWords.Text = currentSnapshot.TotalWords.ToString();
                txtArticleWords.Text = currentSnapshot.ArticleWords.ToString();
                txtTrainerWords.Text = currentSnapshot.TrainerWords.ToString();
                txtRaceAttempts.Text = currentSnapshot.RaceAttemptCount.ToString();
                txtTypingDays.Text = currentSnapshot.TypingDays.ToString();
                txtStartDate.Text = string.IsNullOrWhiteSpace(currentSnapshot.StartDate) ? "-" : currentSnapshot.StartDate;

                RefreshDifficultyRows();
                RebuildCategoryItems();

                txtStatus.Text = "最后刷新：" + refreshTime.ToString("yyyy-MM-dd HH:mm:ss");
            }
            catch (Exception ex)
            {
                txtStatus.Text = "图表加载失败，请全量更新后重试";
                MessageBox.Show("详细字数统计图表加载失败，请全量更新后重试。\n\n" + ex.Message, "详细字数统计", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                isLoading = false;
            }
        }

        public void RefreshTheme()
        {
            ApplyThemeColors();
            RefreshDifficultyRows();
            ApplyCategorySelection();
        }

        private void RefreshDifficultyRows()
        {
            if (currentSnapshot == null)
                return;

            var difficultyRows = currentSnapshot.DifficultyItems
                .Select((i, index) => new StatisticsRow(i, currentSnapshot.DifficultyTotalWords, ToBrush(ChartColors[index % ChartColors.Length]), difficultyTrackBrush))
                .ToList();
            difficultyList.ItemsSource = difficultyRows;
        }

        private void RebuildCategoryItems()
        {
            if (currentSnapshot == null)
                return;

            string previousProjectKey = GetSelectionProjectKey(focusedCategoryKey);
            currentCategoryItems = DetailedWordCountLog.BuildCategoryDisplayItems(
                    currentSnapshot.VisibleCategoryItems,
                    tglMergeCategoryProjects.IsChecked == true)
                .Where(i => i.Key != DetailedWordCountLog.HistoryCategoryKey
                    || tglShowHistoryCategory.IsChecked == true)
                .ToList();

            if (!string.IsNullOrWhiteSpace(focusedCategoryKey)
                && currentCategoryItems.All(i => !string.Equals(i.Key, focusedCategoryKey, StringComparison.Ordinal)))
            {
                focusedCategoryKey = currentCategoryItems
                    .FirstOrDefault(i => GetSelectionProjectKey(i.Key) == previousProjectKey)
                    ?.Key ?? "";
            }

            ApplyCategorySelection();
        }

        private void ApplyCategorySelection()
        {
            RefreshCategoryDetailRows();
            BuildCategoryPieChart(currentCategoryItems);
        }

        private void RefreshCategoryDetailRows()
        {
            if (currentCategoryItems == null)
                currentCategoryItems = new List<DetailedWordCountItem>();

            currentDetailItems = string.IsNullOrWhiteSpace(focusedCategoryKey)
                ? currentCategoryItems.ToList()
                : currentCategoryItems
                    .Where(i => string.Equals(i.Key, focusedCategoryKey, StringComparison.Ordinal))
                    .ToList();

            int detailTotal = currentDetailItems.Sum(i => i.Words);
            dgCategory.ItemsSource = currentDetailItems
                .Select(i => new StatisticsRow(i, detailTotal))
                .ToList();

            UpdateCategoryFocusText(detailTotal);
        }

        private void UpdateCategoryFocusText(int detailTotal)
        {
            if (string.IsNullOrWhiteSpace(focusedCategoryKey))
            {
                txtCategoryFocus.Text = "当前显示全部分类";
                UpdateResetCategoryFocusButton();
                return;
            }

            var item = currentCategoryItems.FirstOrDefault(i => string.Equals(i.Key, focusedCategoryKey, StringComparison.Ordinal));
            txtCategoryFocus.Text = item == null
                ? "当前显示全部分类"
                : "当前筛选：" + item.DisplayName + " / " + detailTotal + "字";
            UpdateResetCategoryFocusButton();
        }

        private void BuildCategoryPieChart(List<DetailedWordCountItem> selectedItems)
        {
            currentChartItems = DetailedWordCountLog.BuildCategoryChartItems(selectedItems, 8);
            RedrawCategoryPieChart();
        }

        private void RedrawCategoryPieChart()
        {
            if (categoryPieCanvas == null || categoryPieHost == null)
                return;

            categoryPieCanvas.Children.Clear();
            pieElementsByKey.Clear();
            pieItemsByKey.Clear();
            hoveredPieKey = "";
            HidePieHoverInfo();

            double width = categoryPieHost.ActualWidth;
            double height = categoryPieHost.ActualHeight;
            if (width <= 20 || height <= 20)
                return;

            var chartItems = currentChartItems
                .Where(i => i != null && i.Words > 0)
                .ToList();
            int chartTotal = chartItems.Sum(i => i.Words);
            if (chartTotal <= 0)
                return;

            double labelBand = Math.Min(250, Math.Max(180, width * 0.28));
            double plotWidth = Math.Max(240, width - labelBand);
            double radius = Math.Max(90, Math.Min(plotWidth, height) * 0.38);
            double centerX = Math.Max(radius + 26, plotWidth * 0.5);
            double centerY = height * 0.5;
            double startAngle = -90;
            var labelLayouts = new List<PieLabelLayout>();

            for (int i = 0; i < chartItems.Count; i++)
            {
                var item = chartItems[i];
                double sweepAngle = 360d * item.Words / chartTotal;
                double endAngle = i == chartItems.Count - 1 ? 270 : startAngle + sweepAngle;
                Brush fill = ToBrush(ChartColors[i % ChartColors.Length]);

                Shape slice;
                if (chartItems.Count == 1)
                {
                    slice = DrawFullPieCircle(centerX, centerY, radius, fill, item, chartTotal);
                }
                else
                {
                    slice = new Path
                    {
                        Data = CreatePieSliceGeometry(centerX, centerY, radius, startAngle, endAngle),
                        Fill = fill,
                        Stroke = chartSliceStrokeBrush,
                        StrokeThickness = 1,
                        Tag = item.Key,
                        Cursor = item.Key == "category:chart:other" ? Cursors.Arrow : Cursors.Hand,
                        ToolTip = FormatPieTooltip(item, chartTotal)
                    };
                }
                RegisterPieElement(item.Key, item, slice);
                categoryPieCanvas.Children.Add(slice);

                labelLayouts.Add(BuildPieLabelLayout(item, chartTotal, centerX, centerY, radius, startAngle, endAngle, width, height));
                startAngle = endAngle;
            }

            foreach (var layout in BuildPieLabelLayouts(labelLayouts, height))
                DrawPieLabel(layout);
        }

        private Shape DrawFullPieCircle(
            double centerX,
            double centerY,
            double radius,
            Brush fill,
            DetailedWordCountChartItem item,
            int chartTotal)
        {
            var ellipse = new Ellipse
            {
                Width = radius * 2,
                Height = radius * 2,
                Fill = fill,
                Stroke = chartSliceStrokeBrush,
                StrokeThickness = 1,
                Tag = item.Key,
                Cursor = item.Key == "category:chart:other" ? Cursors.Arrow : Cursors.Hand,
                ToolTip = FormatPieTooltip(item, chartTotal)
            };
            Canvas.SetLeft(ellipse, centerX - radius);
            Canvas.SetTop(ellipse, centerY - radius);
            return ellipse;
        }

        private PieLabelLayout BuildPieLabelLayout(
            DetailedWordCountChartItem item,
            int chartTotal,
            double centerX,
            double centerY,
            double radius,
            double startAngle,
            double endAngle,
            double chartWidth,
            double chartHeight)
        {
            double midAngle = (startAngle + endAngle) * 0.5;
            Point edgePoint = PointOnCircle(centerX, centerY, radius + 4, midAngle);
            Point elbowPoint = PointOnCircle(centerX, centerY, radius + 24, midAngle);
            bool isRightSide = Math.Cos(DegreesToRadians(midAngle)) >= 0;
            double textWidth = 160;
            double textX = isRightSide
                ? Math.Min(chartWidth - textWidth - 14, elbowPoint.X + 10)
                : Math.Max(14, elbowPoint.X - textWidth - 10);
            double textY = Math.Max(8, Math.Min(chartHeight - 34, elbowPoint.Y - 13));

            return new PieLabelLayout
            {
                Item = item,
                ChartTotal = chartTotal,
                EdgePoint = edgePoint,
                ElbowPoint = elbowPoint,
                TextX = textX,
                TextY = textY,
                TextWidth = textWidth,
                IsRightSide = isRightSide
            };
        }

        private IEnumerable<PieLabelLayout> BuildPieLabelLayouts(List<PieLabelLayout> layouts, double chartHeight)
        {
            ArrangePieLabelRows(layouts.Where(l => l.IsRightSide).ToList(), chartHeight);
            ArrangePieLabelRows(layouts.Where(l => !l.IsRightSide).ToList(), chartHeight);
            return layouts;
        }

        private static void ArrangePieLabelRows(List<PieLabelLayout> layouts, double chartHeight)
        {
            const double rowHeight = 24;
            const double top = 8;
            double bottom = Math.Max(top, chartHeight - 34);
            double y = top;

            foreach (var layout in layouts.OrderBy(l => l.TextY).ThenByDescending(l => l.Item.Words))
            {
                layout.TextY = Math.Max(layout.TextY, y);
                y = layout.TextY + rowHeight;
            }

            double overflow = y - rowHeight - bottom;
            if (overflow <= 0)
                return;

            for (int i = layouts.Count - 1; i >= 0; i--)
            {
                var layout = layouts.OrderBy(l => l.TextY).ElementAt(i);
                double adjusted = Math.Max(top + i * rowHeight, layout.TextY - overflow);
                layout.TextY = adjusted;
            }
        }

        private void DrawPieLabel(PieLabelLayout layout)
        {
            double lineEndX = layout.IsRightSide
                ? layout.TextX - 4
                : layout.TextX + layout.TextWidth + 4;

            var connector = new Polyline
            {
                Stroke = chartConnectorBrush,
                StrokeThickness = 1,
                Points = new PointCollection
                {
                    layout.EdgePoint,
                    layout.ElbowPoint,
                    new Point(lineEndX, layout.TextY + 14)
                },
                Tag = layout.Item.Key,
                Cursor = layout.Item.Key == "category:chart:other" ? Cursors.Arrow : Cursors.Hand
            };
            RegisterPieElement(layout.Item.Key, layout.Item, connector);
            categoryPieCanvas.Children.Add(connector);

            var label = new TextBlock
            {
                Text = FormatPieDataLabel(layout.Item),
                Foreground = chartTextBrush,
                FontSize = 13,
                FontWeight = string.Equals(focusedCategoryKey, layout.Item.Key, StringComparison.Ordinal) ? FontWeights.Bold : FontWeights.Normal,
                Width = layout.TextWidth,
                TextAlignment = layout.IsRightSide ? TextAlignment.Left : TextAlignment.Right,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Tag = layout.Item.Key,
                Cursor = layout.Item.Key == "category:chart:other" ? Cursors.Arrow : Cursors.Hand,
                ToolTip = FormatPieTooltip(layout.Item, layout.ChartTotal)
            };
            RegisterPieElement(layout.Item.Key, layout.Item, label);
            Canvas.SetLeft(label, layout.TextX);
            Canvas.SetTop(label, layout.TextY);
            categoryPieCanvas.Children.Add(label);
        }

        private void RegisterPieElement(string key, DetailedWordCountChartItem item, FrameworkElement element)
        {
            if (string.IsNullOrWhiteSpace(key) || element == null)
                return;

            pieItemsByKey[key] = item;
            if (!pieElementsByKey.ContainsKey(key))
                pieElementsByKey[key] = new List<FrameworkElement>();

            pieElementsByKey[key].Add(element);
            element.MouseLeftButtonDown += CategoryPieSlice_MouseLeftButtonDown;
            element.MouseEnter += CategoryPieSlice_MouseEnter;
            element.MouseLeave += CategoryPieSlice_MouseLeave;
        }

        private void CategoryPieSlice_MouseEnter(object sender, MouseEventArgs e)
        {
            var element = sender as FrameworkElement;
            string key = element?.Tag as string;
            if (string.IsNullOrWhiteSpace(key))
                return;

            pieHoverLeaveTimer.Stop();
            SetPieHover(key);
        }

        private void CategoryPieSlice_MouseLeave(object sender, MouseEventArgs e)
        {
            pieHoverLeaveTimer.Stop();
            pieHoverLeaveTimer.Start();
        }

        private void SetPieHover(string key)
        {
            if (string.Equals(hoveredPieKey, key, StringComparison.Ordinal))
                return;

            ClearPieHover();
            hoveredPieKey = key;

            if (!pieElementsByKey.ContainsKey(key))
                return;

            foreach (var element in pieElementsByKey[key])
                AnimatePieSlice(element, true);

            if (pieItemsByKey.ContainsKey(key))
                ShowPieHoverInfo(pieItemsByKey[key], currentChartItems.Sum(i => i.Words));
        }

        private void ClearPieHover()
        {
            if (!string.IsNullOrWhiteSpace(hoveredPieKey) && pieElementsByKey.ContainsKey(hoveredPieKey))
            {
                foreach (var element in pieElementsByKey[hoveredPieKey])
                    AnimatePieSlice(element, false);
            }

            hoveredPieKey = "";
            HidePieHoverInfo();
        }

        private bool IsMouseOverHoveredPieGroup()
        {
            return !string.IsNullOrWhiteSpace(hoveredPieKey)
                && pieElementsByKey.ContainsKey(hoveredPieKey)
                && pieElementsByKey[hoveredPieKey].Any(e => e.IsMouseOver);
        }

        private void AnimatePieSlice(FrameworkElement element, bool isHovered)
        {
            if (element is Polyline line)
            {
                line.Stroke = isHovered ? chartHoverConnectorBrush : chartConnectorBrush;
                line.StrokeThickness = isHovered ? 2 : 1;
            }
            else if (element is Shape shape)
            {
                shape.Stroke = isHovered ? chartHoverStrokeBrush : chartSliceStrokeBrush;
                shape.StrokeThickness = isHovered ? 2.6 : 1;
                shape.RenderTransformOrigin = new Point(0.5, 0.5);
                var scale = shape.RenderTransform as ScaleTransform;
                if (scale == null)
                {
                    scale = new ScaleTransform(1, 1);
                    shape.RenderTransform = scale;
                }

                double target = isHovered ? 1.018 : 1;
                var animation = new DoubleAnimation(target, TimeSpan.FromMilliseconds(110))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
            }
            else if (element is TextBlock label)
            {
                label.Foreground = isHovered ? chartHoverTextBrush : chartTextBrush;
                label.FontWeight = isHovered || string.Equals(focusedCategoryKey, label.Tag as string, StringComparison.Ordinal)
                    ? FontWeights.Bold
                    : FontWeights.Normal;
            }
        }

        private void ShowPieHoverInfo(DetailedWordCountChartItem item, int totalWords)
        {
            if (item == null || totalWords <= 0 || txtCategoryPieHoverInfo == null || categoryPieHoverInfoBorder == null)
                return;

            txtCategoryPieHoverInfo.Text = FormatPieTooltip(item, totalWords);
            categoryPieHoverInfoBorder.Visibility = Visibility.Visible;
        }

        private void HidePieHoverInfo()
        {
            if (categoryPieHoverInfoBorder != null)
                categoryPieHoverInfoBorder.Visibility = Visibility.Collapsed;
        }

        private void DifficultyRow_MouseEnter(object sender, MouseEventArgs e)
        {
            AnimateDifficultyRow(sender as FrameworkElement, true);
        }

        private void DifficultyRow_MouseLeave(object sender, MouseEventArgs e)
        {
            AnimateDifficultyRow(sender as FrameworkElement, false);
        }

        private void AnimateDifficultyRow(FrameworkElement row, bool isHovered)
        {
            if (row == null)
                return;

            var scale = row.RenderTransform as ScaleTransform;
            if (scale == null || scale.IsFrozen)
            {
                scale = new ScaleTransform(1, 1);
                row.RenderTransform = scale;
            }

            var easing = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            var rowScaleAnimation = new DoubleAnimation(isHovered ? 1.012 : 1, TimeSpan.FromMilliseconds(120))
            {
                EasingFunction = easing
            };
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, rowScaleAnimation);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, rowScaleAnimation);

            var hoverBackground = FindVisualChildByName<Border>(row, "difficultyRowHoverBg");
            if (hoverBackground != null)
            {
                hoverBackground.Background = difficultyHoverBrush;
                hoverBackground.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(isHovered ? 0.78 : 0, TimeSpan.FromMilliseconds(120))
                {
                    EasingFunction = easing
                });
            }

            foreach (var name in new[] { "difficultyBarTrack", "difficultyBarFill" })
            {
                var bar = FindVisualChildByName<Border>(row, name);
                if (bar == null)
                    continue;

                bar.BeginAnimation(FrameworkElement.HeightProperty, new DoubleAnimation(isHovered ? 12 : 10, TimeSpan.FromMilliseconds(120))
                {
                    EasingFunction = easing
                });
            }
        }

        private static Geometry CreatePieSliceGeometry(double centerX, double centerY, double radius, double startAngle, double endAngle)
        {
            Point center = new Point(centerX, centerY);
            Point start = PointOnCircle(centerX, centerY, radius, startAngle);
            Point end = PointOnCircle(centerX, centerY, radius, endAngle);
            bool isLargeArc = Math.Abs(endAngle - startAngle) > 180;

            var figure = new PathFigure { StartPoint = center, IsClosed = true };
            figure.Segments.Add(new LineSegment(start, true));
            figure.Segments.Add(new ArcSegment(
                end,
                new Size(radius, radius),
                0,
                isLargeArc,
                SweepDirection.Clockwise,
                true));
            figure.Segments.Add(new LineSegment(center, true));

            return new PathGeometry(new[] { figure });
        }

        private static Point PointOnCircle(double centerX, double centerY, double radius, double angle)
        {
            double radians = DegreesToRadians(angle);
            return new Point(
                centerX + Math.Cos(radians) * radius,
                centerY + Math.Sin(radians) * radius);
        }

        private static double DegreesToRadians(double angle)
        {
            return Math.PI * angle / 180d;
        }

        private void ApplyThemeColors()
        {
            try
            {
                var windowBg = UiColors.FromString(Config.GetString("窗体背景色"));
                var windowFg = UiColors.FromString(Config.GetString("窗体字体色"));
                var menuBg = UiColors.FromString(Config.GetString("菜单背景色"));
                var menuFg = UiColors.FromString(Config.GetString("菜单字体色"));
                var btnBg = UiColors.FromString(Config.GetString("按钮背景色"));
                var btnFg = UiColors.FromString(Config.GetString("按钮字体色"));
                var windowBgBrush = (SolidColorBrush)windowBg;
                var windowFgBrush = (SolidColorBrush)windowFg;
                var menuBgBrush = (SolidColorBrush)menuBg;
                var menuFgBrush = (SolidColorBrush)menuFg;
                var btnBgBrush = (SolidColorBrush)btnBg;
                var btnFgBrush = (SolidColorBrush)btnFg;
                var readableFg = ThemeColorHelper.CreateReadableForegroundBrush(windowFgBrush, windowBgBrush);
                var readableMenuFg = ThemeColorHelper.CreateReadableForegroundBrush(menuFgBrush, menuBgBrush);
                var readableBtnFg = ThemeColorHelper.CreateReadableForegroundBrush(btnFgBrush, btnBgBrush);
                var secondaryFg = ThemeColorHelper.GetSecondaryTextBrush(windowBgBrush);
                var elevatedBg = CreateElevatedBackground(windowBgBrush);
                var alternateBg = CreateAlternateBackground(windowBgBrush);
                var hoverBg = CreateHoverBackground(windowBgBrush);
                var chartBg = CreateAdjustedBrush(windowBgBrush, ThemeColorHelper.IsDark(windowBgBrush.Color) ? 10 : -4);
                var border = new SolidColorBrush(ThemeColorHelper.GetSubtleBorderColor(windowBgBrush.Color));
                var activeBg = ThemeColorHelper.GetReadableHighlightBrush(windowBgBrush);
                var activeFg = new SolidColorBrush(ThemeColorHelper.IsDark(activeBg.Color)
                    ? System.Windows.Media.Colors.White
                    : System.Windows.Media.Colors.Black);

                panelBorderBrush = border;
                normalToggleBackground = alternateBg;
                activeToggleBackground = activeBg;
                normalToggleForeground = readableFg;
                activeToggleForeground = activeFg;
                difficultyTrackBrush = new SolidColorBrush(Color.FromArgb(70, border.Color.R, border.Color.G, border.Color.B));
                difficultyHoverBrush = hoverBg;
                chartTextBrush = readableFg;
                chartHoverTextBrush = readableFg;
                chartConnectorBrush = new SolidColorBrush(Color.FromArgb(170, border.Color.R, border.Color.G, border.Color.B));
                chartHoverConnectorBrush = readableFg;
                chartSliceStrokeBrush = new SolidColorBrush(Color.FromArgb(150, border.Color.R, border.Color.G, border.Color.B));
                chartHoverStrokeBrush = readableFg;

                Background = Brushes.Transparent;
                mainBorder.Background = windowBgBrush;
                mainBorder.BorderBrush = border;
                titleBarBorder.Background = menuBgBrush;
                titleBarGrid.Background = Brushes.Transparent;
                Foreground = readableFg;
                txtTitle.Foreground = readableMenuFg;
                txtStatus.Foreground = readableFg;
                txtDifficultyNote.Foreground = secondaryFg;
                txtCategoryFocus.Foreground = secondaryFg;
                statusBar.Background = menuBgBrush;
                statusBar.Foreground = readableFg;

                foreach (var panel in new[] { summaryTotalBorder, summaryArticleBorder, summaryTrainerBorder, summaryStartBorder, summaryRaceBorder, summaryTypingDaysBorder, categoryChartBorder, difficultyChartBorder, categoryDetailBorder })
                {
                    panel.BorderBrush = border;
                    panel.Background = elevatedBg;
                }

                categoryPieHost.Background = chartBg;
                categoryPieHoverInfoBorder.Background = elevatedBg;
                categoryPieHoverInfoBorder.BorderBrush = border;
                txtCategoryPieHoverInfo.Foreground = readableFg;
                ApplyDataGridTheme(dgCategory, elevatedBg, readableFg, menuBgBrush, border, alternateBg, hoverBg);
                ApplyTextElementTheme(Content as DependencyObject, readableFg, secondaryFg);

                txtTitle.Foreground = readableMenuFg;
                txtStatus.Foreground = readableFg;
                txtDifficultyNote.Foreground = secondaryFg;
                txtCategoryFocus.Foreground = secondaryFg;
                BtnMinimize.Foreground = readableMenuFg;
                BtnMaximize.Foreground = readableMenuFg;
                BtnClose.Foreground = readableMenuFg;
                BtnMinimize.Background = Brushes.Transparent;
                BtnMaximize.Background = Brushes.Transparent;
                BtnClose.Background = Brushes.Transparent;
                ApplyButtonTheme(btnRefresh, btnBgBrush, readableBtnFg, border);
                ApplyButtonTheme(btnResetCategoryFocus, btnBgBrush, readableBtnFg, border);
                ApplyToggleTheme(tglMergeCategoryProjects);
                ApplyToggleTheme(tglShowHistoryCategory);
            }
            catch
            {
                mainBorder.Background = new SolidColorBrush(Color.FromRgb(245, 245, 245));
                mainBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(204, 204, 204));
                Background = Brushes.Transparent;
                chartTextBrush = new SolidColorBrush(Color.FromRgb(30, 30, 30));
                chartHoverTextBrush = new SolidColorBrush(Color.FromRgb(0, 0, 0));
                chartConnectorBrush = new SolidColorBrush(Color.FromRgb(180, 180, 180));
                chartHoverConnectorBrush = new SolidColorBrush(Color.FromRgb(30, 30, 30));
                chartSliceStrokeBrush = new SolidColorBrush(Color.FromRgb(220, 220, 220));
                chartHoverStrokeBrush = new SolidColorBrush(Color.FromRgb(30, 30, 30));
                difficultyHoverBrush = new SolidColorBrush(Color.FromArgb(24, 0, 0, 0));
            }
        }

        private static void ApplyButtonTheme(Button button, Brush background, Brush foreground, Brush border)
        {
            if (button == null)
                return;

            button.Background = background;
            button.Foreground = foreground;
            button.BorderBrush = border;
        }

        private void ApplyToggleTheme(ToggleButton toggle)
        {
            if (toggle == null)
                return;

            toggle.BorderBrush = panelBorderBrush;
            toggle.Background = toggle.IsChecked == true ? activeToggleBackground : normalToggleBackground;
            toggle.Foreground = toggle.IsChecked == true ? activeToggleForeground : normalToggleForeground;
        }

        private void CenterOverOwner()
        {
            if (owner == null)
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
                return;
            }

            Left = owner.Left + Math.Max(0, (owner.ActualWidth - ActualWidth) / 2);
            Top = owner.Top + Math.Max(0, (owner.ActualHeight - ActualHeight) / 2);
        }

        private static Brush ToBrush(Color color)
        {
            return new SolidColorBrush(color);
        }

        private static SolidColorBrush CreateElevatedBackground(SolidColorBrush baseBrush)
        {
            return CreateAdjustedBrush(baseBrush, ThemeColorHelper.IsDark(baseBrush.Color) ? 18 : -8);
        }

        private static SolidColorBrush CreateAlternateBackground(SolidColorBrush baseBrush)
        {
            return CreateAdjustedBrush(baseBrush, ThemeColorHelper.IsDark(baseBrush.Color) ? 25 : -12);
        }

        private static SolidColorBrush CreateHoverBackground(SolidColorBrush baseBrush)
        {
            return CreateAdjustedBrush(baseBrush, ThemeColorHelper.IsDark(baseBrush.Color) ? 38 : -22);
        }

        private static SolidColorBrush CreateAdjustedBrush(SolidColorBrush baseBrush, int delta)
        {
            var color = baseBrush.Color;
            return new SolidColorBrush(Color.FromArgb(
                color.A,
                ClampToByte(color.R + delta),
                ClampToByte(color.G + delta),
                ClampToByte(color.B + delta)));
        }

        private static byte ClampToByte(int value)
        {
            return (byte)Math.Max(0, Math.Min(255, value));
        }

        private static void ApplyDataGridTheme(
            DataGrid dataGrid,
            Brush background,
            Brush foreground,
            Brush headerBackground,
            Brush border,
            Brush alternateBackground,
            Brush hoverBackground)
        {
            if (dataGrid == null)
                return;

            dataGrid.Background = background;
            dataGrid.Foreground = foreground;
            dataGrid.BorderBrush = border;
            dataGrid.RowBackground = background;
            dataGrid.AlternatingRowBackground = alternateBackground;
            dataGrid.HorizontalGridLinesBrush = border;
            dataGrid.VerticalGridLinesBrush = border;

            var headerStyle = new Style(typeof(DataGridColumnHeader));
            headerStyle.Setters.Add(new Setter(Control.BackgroundProperty, headerBackground));
            headerStyle.Setters.Add(new Setter(Control.ForegroundProperty, foreground));
            headerStyle.Setters.Add(new Setter(Control.BorderBrushProperty, border));
            headerStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
            dataGrid.ColumnHeaderStyle = headerStyle;

            var cellStyle = new Style(typeof(DataGridCell));
            cellStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            cellStyle.Setters.Add(new Setter(Control.ForegroundProperty, foreground));
            cellStyle.Setters.Add(new Setter(Control.BorderBrushProperty, border));
            dataGrid.CellStyle = cellStyle;

            var rowStyle = new Style(typeof(DataGridRow));
            rowStyle.Setters.Add(new Setter(Control.ForegroundProperty, foreground));
            var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Control.BackgroundProperty, hoverBackground));
            rowStyle.Triggers.Add(hoverTrigger);
            dataGrid.RowStyle = rowStyle;
        }

        private static void ApplyTextElementTheme(DependencyObject root, Brush foreground, Brush secondaryForeground)
        {
            if (root == null)
                return;

            if (root is TextBlock textBlock)
                textBlock.Foreground = textBlock.FontWeight == FontWeights.Bold ? foreground : secondaryForeground;
            else if (root is Control control)
                control.Foreground = foreground;

            int childCount = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < childCount; i++)
                ApplyTextElementTheme(VisualTreeHelper.GetChild(root, i), foreground, secondaryForeground);
        }

        private void CategoryOption_Changed(object sender, RoutedEventArgs e)
        {
            if (isLoading || currentSnapshot == null)
                return;

            RebuildCategoryItems();
            ApplyToggleTheme(sender as ToggleButton);
        }

        private static string GetSelectionProjectKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return "";

            if (key.StartsWith("category:wenlai", StringComparison.Ordinal))
                return "category:wenlai";

            if (key.StartsWith("category:trainer", StringComparison.Ordinal))
                return "category:trainer";

            if (key.StartsWith("category:race", StringComparison.Ordinal))
                return "category:race";

            return key;
        }

        private static string FormatPieDataLabel(DetailedWordCountChartItem item)
        {
            if (item == null)
                return "";

            return ShortenLabel(item.DisplayName, 18);
        }

        private static string FormatPieTooltip(DetailedWordCountChartItem item, int totalWords)
        {
            if (item == null || totalWords <= 0)
                return "";

            double percent = (double)item.Words / totalWords;
            return item.DisplayName + "：" + item.Words + "字，" + (percent * 100).ToString("F1") + "%";
        }

        private static string ShortenLabel(string value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
                return value ?? "";

            return value.Substring(0, Math.Max(1, maxLength - 1)) + "...";
        }

        private static bool IsVisualDescendantOf(DependencyObject child, DependencyObject parent)
        {
            while (child != null)
            {
                if (child == parent)
                    return true;

                child = VisualTreeHelper.GetParent(child);
            }

            return false;
        }

        private static T FindVisualChildByName<T>(DependencyObject root, string name)
            where T : FrameworkElement
        {
            if (root == null)
                return null;

            int childCount = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is T element && element.Name == name)
                    return element;

                var nested = FindVisualChildByName<T>(child, name);
                if (nested != null)
                    return nested;
            }

            return null;
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadStatistics();
        }

        private void BtnResetCategoryFocus_Click(object sender, RoutedEventArgs e)
        {
            focusedCategoryKey = "";
            RefreshCategoryDetailRows();
            RedrawCategoryPieChart();
        }

        private void UpdateResetCategoryFocusButton()
        {
            if (btnResetCategoryFocus == null)
                return;

            btnResetCategoryFocus.Visibility = string.IsNullOrWhiteSpace(focusedCategoryKey)
                ? Visibility.Collapsed
                : Visibility.Visible;
            btnResetCategoryFocus.Content = "显示全部";
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
        {
            ToggleMaximize();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var source = e.OriginalSource as DependencyObject;
            if (IsVisualDescendantOf(source, BtnMinimize)
                || IsVisualDescendantOf(source, BtnMaximize)
                || IsVisualDescendantOf(source, BtnClose))
            {
                return;
            }

            if (e.ClickCount == 2)
            {
                ToggleMaximize();
                return;
            }

            if (e.ClickCount == 1)
                DragMove();
        }

        private void ToggleMaximize()
        {
            if (isCustomMaximized)
            {
                Left = restoreBounds.Left;
                Top = restoreBounds.Top;
                Width = restoreBounds.Width;
                Height = restoreBounds.Height;
                isCustomMaximized = false;
                BtnMaximize.Content = "◻";
                return;
            }

            restoreBounds = new Rect(Left, Top, Width, Height);
            var workArea = SystemParameters.WorkArea;
            Left = workArea.Left;
            Top = workArea.Top;
            Width = workArea.Width;
            Height = workArea.Height;
            isCustomMaximized = true;
            BtnMaximize.Content = "◰";
        }

        private void ResizeBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (isCustomMaximized)
                return;

            var border = sender as FrameworkElement;
            if (border == null)
                return;

            int direction = 0;
            switch (border.Name)
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

            if (direction == 0)
                return;

            var handle = new WindowInteropHelper(this).Handle;
            if (handle == IntPtr.Zero)
                return;

            ReleaseCapture();
            SendMessage(handle, WM_NCLBUTTONDOWN, (IntPtr)direction, IntPtr.Zero);
        }

        private void CategoryPieHost_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            RedrawCategoryPieChart();
        }

        private void CategoryPieSlice_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var element = sender as FrameworkElement;
            string key = element.Tag as string;
            if (string.IsNullOrWhiteSpace(key) || key == "category:chart:other")
                return;

            focusedCategoryKey = string.Equals(focusedCategoryKey, key, StringComparison.Ordinal)
                ? ""
                : key;
            RefreshCategoryDetailRows();
            RedrawCategoryPieChart();
            e.Handled = true;
        }

        public sealed class StatisticsRow
        {
            public StatisticsRow(DetailedWordCountItem item, int totalWords, Brush barBrush = null, Brush trackBrush = null)
            {
                DisplayName = item.DisplayName;
                Words = item.Words;
                AttemptsText = item.Attempts > 0 ? item.Attempts.ToString() : "-";
                StartDate = string.IsNullOrWhiteSpace(item.StartDate) ? "-" : item.StartDate;
                double percent = totalWords > 0 ? (double)item.Words / totalWords : 0;
                PercentText = (percent * 100).ToString("F1") + "%";
                BarWidth = Math.Max(0, Math.Min(220, percent * 220));
                BarBrush = barBrush ?? Brushes.SteelBlue;
                TrackBrush = trackBrush ?? new SolidColorBrush(Color.FromArgb(34, 0, 0, 0));
            }

            public string DisplayName { get; private set; }
            public int Words { get; private set; }
            public string AttemptsText { get; private set; }
            public string PercentText { get; private set; }
            public string StartDate { get; private set; }
            public double BarWidth { get; private set; }
            public Brush BarBrush { get; private set; }
            public Brush TrackBrush { get; private set; }
        }

        private sealed class PieLabelLayout
        {
            public DetailedWordCountChartItem Item { get; set; }
            public int ChartTotal { get; set; }
            public Point EdgePoint { get; set; }
            public Point ElbowPoint { get; set; }
            public double TextX { get; set; }
            public double TextY { get; set; }
            public double TextWidth { get; set; }
            public bool IsRightSide { get; set; }
        }
    }
}
