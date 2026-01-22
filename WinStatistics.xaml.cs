using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TypeSunny.ArticleSender;

namespace TypeSunny
{
    /// <summary>
    /// 成绩统计窗口
    /// </summary>
    public partial class WinStatistics : Window
    {
        private Window owner;
        private List<DifficultyInfo> difficulties;

        public WinStatistics(Window owner)
        {
            InitializeComponent();
            this.owner = owner;

            // 应用主题颜色
            ApplyThemeColors();

            // 加载初始数据
            Loaded += async (s, e) =>
            {
                // 为 TabControl 添加 Loaded 事件，确保 TabItem 完全生成后应用主题
                if (tabControl != null)
                {
                    tabControl.Loaded += (tabSender, tabE) =>
                    {
                        System.Diagnostics.Debug.WriteLine($"[WinStatistics] TabControl Loaded 事件触发");
                        ApplyThemeColors();
                    };
                }

                // 窗口加载完成后再次应用主题，确保 TabControl 已完全初始化
                ApplyThemeColors();

                // 加载文来难度配置
                difficulties = await ArticleFetcher.GetDifficultiesAsync();

                // 加载所有 Tab 的今日数据
                LoadWenlaiStatistics(StatsRange.Today);
                LoadLocalStatistics(StatsRange.Today);
                LoadTrainerStatistics(StatsRange.Today);
            };
        }

        #region 主题应用

        /// <summary>
        /// 应用主题颜色
        /// </summary>
        private void ApplyThemeColors()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[WinStatistics] ApplyThemeColors 开始执行");

                // 读取主题颜色配置
                var windowBgStr = Config.GetString("窗体背景色");
                var windowFgStr = Config.GetString("窗体字体色");
                var btnBgStr = Config.GetString("按钮背景色");
                var btnFgStr = Config.GetString("按钮字体色");
                var menuBgStr = Config.GetString("菜单背景色");

                System.Diagnostics.Debug.WriteLine($"[WinStatistics] 读取颜色配置: 窗体背景={windowBgStr}, 菜单背景={menuBgStr}");

                // 颜色值需要 # 前缀
                var windowBg = System.Windows.Media.ColorConverter.ConvertFromString("#" + windowBgStr);
                var windowFg = System.Windows.Media.ColorConverter.ConvertFromString("#" + windowFgStr);
                var btnBg = System.Windows.Media.ColorConverter.ConvertFromString("#" + btnBgStr);
                var btnFg = System.Windows.Media.ColorConverter.ConvertFromString("#" + btnFgStr);
                var menuBg = System.Windows.Media.ColorConverter.ConvertFromString("#" + menuBgStr);

                if (windowBg != null)
                {
                    var bgColor = (System.Windows.Media.Color)windowBg;
                    var bgBrush = new System.Windows.Media.SolidColorBrush(bgColor);

                    mainBorder.Background = bgBrush;

                    // 判断是否为暗色主题
                    double brightness = (bgColor.R * 0.299 + bgColor.G * 0.587 + bgColor.B * 0.114) / 255.0;
                    bool isDarkTheme = brightness < 0.5;

                    // 边框颜色
                    System.Windows.Media.Color borderColor;
                    if (isDarkTheme)
                    {
                        borderColor = System.Windows.Media.Color.FromRgb(
                            (byte)Math.Min(255, bgColor.R + 50),
                            (byte)Math.Min(255, bgColor.G + 50),
                            (byte)Math.Min(255, bgColor.B + 50)
                        );
                    }
                    else
                    {
                        borderColor = System.Windows.Media.Color.FromRgb(
                            (byte)Math.Max(0, bgColor.R - 40),
                            (byte)Math.Max(0, bgColor.G - 40),
                            (byte)Math.Max(0, bgColor.B - 40)
                        );
                    }
                    mainBorder.BorderBrush = new System.Windows.Media.SolidColorBrush(borderColor);

                    // DataGrid 背景色
                    ApplyDataGridTheme(dgWenlaiStats, bgColor, isDarkTheme, borderColor);
                    ApplyDataGridTheme(dgLocalStats, bgColor, isDarkTheme, borderColor);
                    ApplyDataGridTheme(dgTrainerStats, bgColor, isDarkTheme, borderColor);
                }

                // 应用菜单背景色到 TabControl（参考设置页的 NavBorder 实现）
                if (menuBg != null && tabControl != null)
                {
                    var menuBgColor = (System.Windows.Media.Color)menuBg;
                    var menuBgBrush = new System.Windows.Media.SolidColorBrush(menuBgColor);
                    System.Diagnostics.Debug.WriteLine($"[WinStatistics] 设置 TabControl 背景: {menuBgColor}");
                    tabControl.Background = menuBgBrush;

                    // 设置 TabControl 的边框颜色（参考 NavBorder.BorderBrush）
                    var borderColor = System.Windows.Media.Color.FromRgb(
                        (byte)Math.Max(0, menuBgColor.R - 30),
                        (byte)Math.Max(0, menuBgColor.G - 30),
                        (byte)Math.Max(0, menuBgColor.B - 30)
                    );
                    tabControl.BorderBrush = new System.Windows.Media.SolidColorBrush(borderColor);
                    System.Diagnostics.Debug.WriteLine($"[WinStatistics] TabControl 边框色: {borderColor}, 边框厚度: {tabControl.BorderThickness}");
                }

                if (windowFg != null)
                {
                    var fgBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)windowFg);
                    txtTitle.Foreground = fgBrush;
                    txtStatus.Foreground = fgBrush;

                    // 使用 ItemContainerStyle 统一设置 TabItem 的样式
                    if (tabControl != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[WinStatistics] TabControl.Items.Count: {tabControl.Items.Count}");

                        var tabItemStyle = new Style(typeof(TabItem));
                        tabItemStyle.Setters.Add(new Setter(TabItem.ForegroundProperty, fgBrush));

                        // 如果有菜单背景色，创建自定义模板来完全控制 TabItem 外观
                        if (menuBg != null)
                        {
                            var menuBgColor = (System.Windows.Media.Color)menuBg;
                            var menuBgBrush = new System.Windows.Media.SolidColorBrush(menuBgColor);

                            // 创建自定义 TabItem 模板
                            var template = new System.Windows.Controls.ControlTemplate(typeof(TabItem));
                            var borderFactory = new System.Windows.FrameworkElementFactory(typeof(System.Windows.Controls.Border));
                            borderFactory.Name = "Border";
                            borderFactory.SetValue(System.Windows.Controls.Border.BackgroundProperty, menuBgBrush);
                            borderFactory.SetValue(System.Windows.Controls.Border.BorderThicknessProperty, new System.Windows.Thickness(1));
                            borderFactory.SetValue(System.Windows.Controls.Border.PaddingProperty, new System.Windows.Thickness(10, 5, 10, 5));

                            var contentFactory = new System.Windows.FrameworkElementFactory(typeof(System.Windows.Controls.ContentPresenter));
                            contentFactory.Name = "ContentSite";
                            contentFactory.SetValue(System.Windows.Controls.ContentPresenter.ContentSourceProperty, "Header");
                            contentFactory.SetValue(System.Windows.Controls.ContentPresenter.MarginProperty, new System.Windows.Thickness(0));
                            contentFactory.SetValue(System.Windows.Controls.ContentPresenter.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Center);
                            contentFactory.SetValue(System.Windows.Controls.ContentPresenter.VerticalAlignmentProperty, System.Windows.VerticalAlignment.Center);

                            borderFactory.AppendChild(contentFactory);
                            template.VisualTree = borderFactory;

                            // 选中状态触发器
                            var selectedBgColor = System.Windows.Media.Color.FromRgb(
                                (byte)Math.Max(0, menuBgColor.R - 20),
                                (byte)Math.Max(0, menuBgColor.G - 20),
                                (byte)Math.Max(0, menuBgColor.B - 20)
                            );
                            var selectedBgBrush = new System.Windows.Media.SolidColorBrush(selectedBgColor);

                            var selectedTrigger = new System.Windows.Trigger
                            {
                                Property = TabItem.IsSelectedProperty,
                                Value = true
                            };
                            selectedTrigger.Setters.Add(new System.Windows.Setter(System.Windows.Controls.Border.BackgroundProperty, selectedBgBrush));
                            selectedTrigger.Setters.Add(new System.Windows.Setter(TabItem.FontWeightProperty, System.Windows.FontWeights.Bold));
                            template.Triggers.Add(selectedTrigger);

                            // 鼠标悬停状态触发器
                            var hoverBgColor = System.Windows.Media.Color.FromRgb(
                                (byte)Math.Max(0, menuBgColor.R - 10),
                                (byte)Math.Max(0, menuBgColor.G - 10),
                                (byte)Math.Max(0, menuBgColor.B - 10)
                            );
                            var hoverBgBrush = new System.Windows.Media.SolidColorBrush(hoverBgColor);

                            var hoverTrigger = new System.Windows.Trigger
                            {
                                Property = TabItem.IsMouseOverProperty,
                                Value = true
                            };
                            hoverTrigger.Setters.Add(new System.Windows.Setter(System.Windows.Controls.Border.BackgroundProperty, hoverBgBrush));
                            template.Triggers.Add(hoverTrigger);

                            tabItemStyle.Setters.Add(new System.Windows.Setter(System.Windows.Controls.Control.TemplateProperty, template));
                        }

                        tabControl.ItemContainerStyle = tabItemStyle;
                        System.Diagnostics.Debug.WriteLine($"[WinStatistics] 已设置 TabControl.ItemContainerStyle");

                        // 强制刷新已生成的 TabItem
                        for (int i = 0; i < tabControl.Items.Count; i++)
                        {
                            var item = tabControl.ItemContainerGenerator.ContainerFromIndex(i) as TabItem;
                            if (item != null)
                            {
                                item.Style = tabItemStyle;
                            }
                        }
                    }

                    // DataGridColumnHeader 样式 - 添加背景色
                    var headerStyle = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
                    headerStyle.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, fgBrush));
                    headerStyle.Setters.Add(new Setter(System.Windows.Controls.Control.FontWeightProperty, System.Windows.FontWeights.Bold));
                    if (menuBg != null)
                    {
                        var menuBgColor = (System.Windows.Media.Color)menuBg;
                        var menuBgBrush = new System.Windows.Media.SolidColorBrush(menuBgColor);
                        headerStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, menuBgBrush));
                    }
                    if (dgWenlaiStats != null)
                    {
                        dgWenlaiStats.ColumnHeaderStyle = headerStyle;
                        dgWenlaiStats.Foreground = fgBrush;
                    }
                    if (dgLocalStats != null)
                    {
                        dgLocalStats.ColumnHeaderStyle = headerStyle;
                        dgLocalStats.Foreground = fgBrush;
                    }
                    if (dgTrainerStats != null)
                    {
                        dgTrainerStats.ColumnHeaderStyle = headerStyle;
                        dgTrainerStats.Foreground = fgBrush;
                    }

                    // RadioButton 样式
                    var radioStyle = new Style(typeof(RadioButton));
                    radioStyle.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, fgBrush));

                    // 文来统计 RadioButton
                    if (rbWenlaiToday != null) rbWenlaiToday.Style = radioStyle;
                    if (rbWenlaiDays7 != null) rbWenlaiDays7.Style = radioStyle;
                    if (rbWenlaiDays30 != null) rbWenlaiDays30.Style = radioStyle;
                    if (rbWenlaiDays365 != null) rbWenlaiDays365.Style = radioStyle;
                    if (rbWenlaiCustom != null) rbWenlaiCustom.Style = radioStyle;

                    // 本地文章统计 RadioButton
                    if (rbLocalToday != null) rbLocalToday.Style = radioStyle;
                    if (rbLocalDays7 != null) rbLocalDays7.Style = radioStyle;
                    if (rbLocalDays30 != null) rbLocalDays30.Style = radioStyle;
                    if (rbLocalDays365 != null) rbLocalDays365.Style = radioStyle;
                    if (rbLocalCustom != null) rbLocalCustom.Style = radioStyle;

                    // 打单器统计 RadioButton
                    if (rbTrainerToday != null) rbTrainerToday.Style = radioStyle;
                    if (rbTrainerDays7 != null) rbTrainerDays7.Style = radioStyle;
                    if (rbTrainerDays30 != null) rbTrainerDays30.Style = radioStyle;
                    if (rbTrainerDays365 != null) rbTrainerDays365.Style = radioStyle;
                    if (rbTrainerCustom != null) rbTrainerCustom.Style = radioStyle;

                    // StatusBar 主题色
                    if (statusBar != null)
                    {
                        if (menuBg != null)
                        {
                            statusBar.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)menuBg);
                        }
                        statusBar.Foreground = fgBrush;
                    }
                }

                if (btnBg != null && btnFg != null)
                {
                    var btnBgBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)btnBg);
                    var btnFgBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)btnFg);

                    // 查询按钮样式
                    if (btnWenlaiQuery != null)
                    {
                        btnWenlaiQuery.Background = btnBgBrush;
                        btnWenlaiQuery.Foreground = btnFgBrush;
                    }
                    if (btnLocalQuery != null)
                    {
                        btnLocalQuery.Background = btnBgBrush;
                        btnLocalQuery.Foreground = btnFgBrush;
                    }
                    if (btnTrainerQuery != null)
                    {
                        btnTrainerQuery.Background = btnBgBrush;
                        btnTrainerQuery.Foreground = btnFgBrush;
                    }
                    if (btnRefresh != null)
                    {
                        btnRefresh.Background = btnBgBrush;
                        btnRefresh.Foreground = btnFgBrush;
                    }
                    if (btnClose != null)
                    {
                        btnClose.Background = btnBgBrush;
                        btnClose.Foreground = btnFgBrush;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"应用主题颜色失败: {ex.Message}");
                // 使用默认样式
                mainBorder.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 245, 245));
                mainBorder.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(204, 204, 204));
            }
        }

        private void ApplyDataGridTheme(DataGrid dataGrid, System.Windows.Media.Color bgColor, bool isDarkTheme, System.Windows.Media.Color borderColor)
        {
            if (dataGrid == null) return;

            var bgBrush = new System.Windows.Media.SolidColorBrush(bgColor);
            dataGrid.Background = bgBrush;
            dataGrid.RowBackground = bgBrush;

            // 禁用交替行显示，或者设置正确的交替行颜色
            dataGrid.AlternationCount = 2;
            var alternateBg = bgColor;
            if (isDarkTheme)
            {
                alternateBg.R = (byte)Math.Min(255, alternateBg.R + 20);
                alternateBg.G = (byte)Math.Min(255, alternateBg.G + 20);
                alternateBg.B = (byte)Math.Min(255, alternateBg.B + 20);
            }
            else
            {
                alternateBg.R = (byte)Math.Max(0, alternateBg.R - 15);
                alternateBg.G = (byte)Math.Max(0, alternateBg.G - 15);
                alternateBg.B = (byte)Math.Max(0, alternateBg.B - 15);
            }
            dataGrid.AlternatingRowBackground = new System.Windows.Media.SolidColorBrush(alternateBg);

            // 设置边框为背景色
            dataGrid.BorderBrush = bgBrush;
            dataGrid.BorderThickness = new System.Windows.Thickness(1);

            // 设置 DataGrid 样式，确保所有区域的背景色正确
            var dataGridStyle = new Style(typeof(DataGrid));
            dataGridStyle.Setters.Add(new Setter(DataGrid.BackgroundProperty, bgBrush));
            dataGridStyle.Setters.Add(new Setter(DataGrid.RowBackgroundProperty, bgBrush));
            dataGridStyle.Setters.Add(new Setter(DataGrid.GridLinesVisibilityProperty, DataGridGridLinesVisibility.Horizontal));

            // 添加 ScrollViewer 样式
            var scrollViewerStyle = new Style(typeof(System.Windows.Controls.ScrollViewer));
            scrollViewerStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, bgBrush));
            scrollViewerStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BorderBrushProperty, bgBrush));
            dataGridStyle.Resources.Add(typeof(System.Windows.Controls.ScrollViewer), scrollViewerStyle);

            // 添加 TextBlock 样式
            var textBlockStyle = new Style(typeof(System.Windows.Controls.TextBlock));
            textBlockStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, bgBrush));
            dataGridStyle.Resources.Add(typeof(System.Windows.Controls.TextBlock), textBlockStyle);

            // 添加 Border 样式
            var borderStyle = new Style(typeof(System.Windows.Controls.Border));
            borderStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, bgBrush));
            borderStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BorderBrushProperty, bgBrush));
            dataGridStyle.Resources.Add(typeof(System.Windows.Controls.Border), borderStyle);

            // 添加 Grid 样式
            var gridStyle = new Style(typeof(System.Windows.Controls.Grid));
            gridStyle.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, bgBrush));
            dataGridStyle.Resources.Add(typeof(System.Windows.Controls.Grid), gridStyle);

            // 添加 RowStyle 样式，确保每一行的背景色正确
            var rowStyle = new Style(typeof(DataGridRow));
            rowStyle.Setters.Add(new Setter(DataGridRow.BackgroundProperty, bgBrush));
            dataGridStyle.Resources.Add(typeof(DataGridRow), rowStyle);

            dataGrid.Style = dataGridStyle;
        }

        private void ApplyButtonTheme(System.Windows.Media.Brush bgBrush, System.Windows.Media.Brush fgBrush, params Button[] buttons)
        {
            foreach (var btn in buttons)
            {
                if (btn != null)
                {
                    btn.Background = bgBrush;
                    btn.Foreground = fgBrush;
                }
            }
        }

        #endregion

        #region 窗口操作

        private void Border_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
                DragMove();
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

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            // 根据当前选中的 Tab 刷新数据
            if (tabControl == null) return;

            var selectedTab = tabControl.SelectedItem as TabItem;
            if (selectedTab == null) return;

            // 根据选中的 Tab 刷新对应的统计
            if (selectedTab.Header?.ToString() == "文来统计")
            {
                var range = GetSelectedWenlaiRange();
                _ = LoadWenlaiStatistics(range);
            }
            else if (selectedTab.Header?.ToString() == "本地文章统计")
            {
                var range = GetSelectedLocalRange();
                _ = LoadLocalStatistics(range);
            }
            else if (selectedTab.Header?.ToString() == "打单器统计")
            {
                var range = GetSelectedTrainerRange();
                _ = LoadTrainerStatistics(range);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        #endregion

        #region 文来统计

        private async System.Threading.Tasks.Task LoadWenlaiStatistics(StatsRange range, DateTime? customStart = null, DateTime? customEnd = null)
        {
            try
            {
                if (txtStatus != null)
                    txtStatus.Text = "加载中...";

                // 直接读取预计算的统计数据（无需时间范围筛选，显示全部累计数据）
                var stats = await Task.Run(() => WenlaiLog.ReadStatistics());

                // 绑定到 DataGrid - 只有在有数据时才绑定
                if (dgWenlaiStats != null)
                {
                    if (stats != null && stats.Count > 0)
                    {
                        dgWenlaiStats.ItemsSource = stats;
                    }
                    else
                    {
                        dgWenlaiStats.ItemsSource = null;
                    }
                }

                int totalCount = stats.Sum(s => s.Count);
                if (txtStatus != null)
                    txtStatus.Text = totalCount > 0 ? $"共 {totalCount} 条记录" : "暂无数据";
            }
            catch (Exception ex)
            {
                if (txtStatus != null)
                    txtStatus.Text = "加载失败";
                System.Diagnostics.Debug.WriteLine($"加载文来统计失败: {ex.Message}");
            }
        }

        private void WenlaiRadioButton_CheckedChanged(object sender, RoutedEventArgs e)
        {
            // 显示/隐藏自定义日期选择
            if (rbWenlaiCustom != null && spWenlaiCustomDates != null)
            {
                spWenlaiCustomDates.Visibility = rbWenlaiCustom.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            }

            // 如果不是自定义范围，直接加载
            if (rbWenlaiCustom == null || rbWenlaiCustom.IsChecked != true)
            {
                StatsRange range = GetSelectedWenlaiRange();
                _ = LoadWenlaiStatistics(range);
            }
        }

        private StatsRange GetSelectedWenlaiRange()
        {
            if (rbWenlaiToday != null && rbWenlaiToday.IsChecked == true) return StatsRange.Today;
            if (rbWenlaiDays7 != null && rbWenlaiDays7.IsChecked == true) return StatsRange.Days7;
            if (rbWenlaiDays30 != null && rbWenlaiDays30.IsChecked == true) return StatsRange.Days30;
            if (rbWenlaiDays365 != null && rbWenlaiDays365.IsChecked == true) return StatsRange.Days365;
            return StatsRange.Today;
        }

        private async void BtnWenlaiQuery_Click(object sender, RoutedEventArgs e)
        {
            // 新的统计方式使用预计算的全部累计数据，不再支持自定义日期范围
            // 直接加载所有统计数据
            await LoadWenlaiStatistics(StatsRange.Today);
        }

        #endregion

        #region 本地文章统计

        private async System.Threading.Tasks.Task LoadLocalStatistics(StatsRange range)
        {
            try
            {
                if (txtStatus != null)
                    txtStatus.Text = "加载中...";

                // 直接读取预计算的统计数据（无需时间范围筛选，显示全部累计数据）
                var stats = await Task.Run(() => ArticleLog.ReadStatistics());

                // 绑定到 DataGrid - 只有在有数据时才绑定
                if (dgLocalStats != null)
                {
                    if (stats != null && stats.Count > 0)
                    {
                        dgLocalStats.ItemsSource = stats;
                    }
                    else
                    {
                        dgLocalStats.ItemsSource = null;
                    }
                }

                if (txtStatus != null)
                    txtStatus.Text = stats != null && stats.Count > 0 ? $"共 {stats.Count} 本书，{stats.Sum(s => s.Count)} 条记录" : "暂无数据";
            }
            catch (Exception ex)
            {
                if (txtStatus != null)
                    txtStatus.Text = "加载失败";
                System.Diagnostics.Debug.WriteLine($"加载本地文章统计失败: {ex.Message}");
            }
        }

        private void LocalRadioButton_CheckedChanged(object sender, RoutedEventArgs e)
        {
            // 防止初始化时触发
            if (!this.IsLoaded)
                return;

            // 显示/隐藏自定义日期选择
            if (rbLocalCustom != null && spLocalCustomDates != null)
            {
                spLocalCustomDates.Visibility = rbLocalCustom.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            }

            // 如果不是自定义范围，直接加载
            if (rbLocalCustom == null || rbLocalCustom.IsChecked != true)
            {
                StatsRange range = GetSelectedLocalRange();
                _ = LoadLocalStatistics(range);
            }
        }

        private StatsRange GetSelectedLocalRange()
        {
            if (rbLocalToday != null && rbLocalToday.IsChecked == true) return StatsRange.Today;
            if (rbLocalDays7 != null && rbLocalDays7.IsChecked == true) return StatsRange.Days7;
            if (rbLocalDays30 != null && rbLocalDays30.IsChecked == true) return StatsRange.Days30;
            if (rbLocalDays365 != null && rbLocalDays365.IsChecked == true) return StatsRange.Days365;
            return StatsRange.Today;
        }

        private async void BtnLocalQuery_Click(object sender, RoutedEventArgs e)
        {
            // 新的统计方式使用预计算的全部累计数据，不再支持自定义日期范围
            // 直接加载所有统计数据
            await LoadLocalStatistics(StatsRange.Today);
        }

        #endregion

        #region 打单器统计

        private async System.Threading.Tasks.Task LoadTrainerStatistics(StatsRange range)
        {
            try
            {
                if (txtStatus != null)
                    txtStatus.Text = "加载中...";

                // 直接读取预计算的统计数据（无需时间范围筛选，显示全部累计数据）
                var localStats = await Task.Run(() => TrainerLog.ReadStatistics());

                // 转换为 TrainerStatisticsItem 格式
                var stats = localStats.Select(s => new TrainerStatisticsItem
                {
                    Title = s.BookName,
                    GroupCount = s.Count,
                    AvgSpeed = s.AvgSpeed,
                    AvgHitRate = s.AvgHitRate,
                    AvgAccuracy = s.AvgAccuracy,
                    AvgKPW = s.AvgKPW,
                    AvgCorrection = s.AvgCorrection,
                    TotalBacks = s.TotalBacks,
                    AvgCiRatio = s.AvgCiRatio,
                    BestSpeed = s.MaxSpeed,
                    TotalWords = s.TotalWords,
                    TotalInputWords = s.TotalWords  // 简化处理
                }).OrderByDescending(s => s.GroupCount).ToList();

                // 绑定到 DataGrid - 只有在有数据时才绑定
                if (dgTrainerStats != null)
                {
                    if (stats != null && stats.Count > 0)
                    {
                        dgTrainerStats.ItemsSource = stats;
                    }
                    else
                    {
                        dgTrainerStats.ItemsSource = null;
                    }
                }

                if (txtStatus != null)
                    txtStatus.Text = stats != null && stats.Count > 0 ? $"共 {stats.Count} 个练习项" : "暂无数据";
            }
            catch (Exception ex)
            {
                if (txtStatus != null)
                    txtStatus.Text = "加载失败";
                System.Diagnostics.Debug.WriteLine($"加载打单器统计失败: {ex.Message}");
            }
        }

        private void TrainerRadioButton_CheckedChanged(object sender, RoutedEventArgs e)
        {
            // 防止初始化时触发
            if (!this.IsLoaded)
                return;

            // 显示/隐藏自定义日期选择
            if (rbTrainerCustom != null && spTrainerCustomDates != null)
            {
                spTrainerCustomDates.Visibility = rbTrainerCustom.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            }

            // 如果不是自定义范围，直接加载
            if (rbTrainerCustom == null || rbTrainerCustom.IsChecked != true)
            {
                StatsRange range = GetSelectedTrainerRange();
                _ = LoadTrainerStatistics(range);
            }
        }

        private StatsRange GetSelectedTrainerRange()
        {
            if (rbTrainerToday != null && rbTrainerToday.IsChecked == true) return StatsRange.Today;
            if (rbTrainerDays7 != null && rbTrainerDays7.IsChecked == true) return StatsRange.Days7;
            if (rbTrainerDays30 != null && rbTrainerDays30.IsChecked == true) return StatsRange.Days30;
            if (rbTrainerDays365 != null && rbTrainerDays365.IsChecked == true) return StatsRange.Days365;
            return StatsRange.Today;
        }

        private async void BtnTrainerQuery_Click(object sender, RoutedEventArgs e)
        {
            // 新的统计方式使用预计算的全部累计数据，不再支持自定义日期范围
            // 直接加载所有统计数据
            await LoadTrainerStatistics(StatsRange.Today);
        }

        #endregion

        #region 辅助方法

        private void GetDateRange(StatsRange range, out DateTime startDate, out DateTime endDate)
        {
            endDate = DateTime.Now.Date.AddDays(1).AddTicks(-1); // 包含今天的结束时间

            switch (range)
            {
                case StatsRange.Today:
                    startDate = DateTime.Now.Date;
                    break;
                case StatsRange.Days7:
                    startDate = DateTime.Now.Date.AddDays(-6);
                    break;
                case StatsRange.Days30:
                    startDate = DateTime.Now.Date.AddDays(-29);
                    break;
                case StatsRange.Days365:
                    startDate = DateTime.Now.Date.AddDays(-364);
                    break;
                case StatsRange.Custom:
                default:
                    // 自定义范围由调用方设置
                    startDate = DateTime.Now.Date;
                    endDate = DateTime.Now.Date.AddDays(1).AddTicks(-1);
                    break;
            }
        }

        #endregion

        /// <summary>
        /// 刷新主题（公共方法，供外部调用）
        /// </summary>
        public void RefreshTheme()
        {
            System.Diagnostics.Debug.WriteLine($"[WinStatistics] RefreshTheme 被调用");
            ApplyThemeColors();
        }
    }

    #region 统计数据类

    // WenlaiStatisticsItem 和 LocalArticleStatisticsItem 已移至 ArticleLog.cs 中定义

    /// <summary>
    /// 打单器统计项
    /// </summary>
    public class TrainerStatisticsItem
    {
        public string Title { get; set; }           // 标题
        public int GroupCount { get; set; }         // 组数
        public double AvgSpeed { get; set; }        // 均速（字/分）
        public double AvgHitRate { get; set; }      // 均击（键/秒）
        public double AvgAccuracy { get; set; }     // 键准（%）
        public double AvgKPW { get; set; }          // 码长
        public double AvgCorrection { get; set; }   // 回改
        public int TotalBacks { get; set; }         // 总退格
        public double AvgCiRatio { get; set; }      // 打词率（%）
        public double BestSpeed { get; set; }       // 最佳成绩
        public int TotalWords { get; set; }         // 总字数
        public int TotalInputWords { get; set; }    // 实际输入字数（包括重打）
    }

    /// <summary>
    /// 统计范围枚举
    /// </summary>
    public enum StatsRange
    {
        Today,
        Days7,
        Days30,
        Days365,
        Custom
    }

    #endregion
}
