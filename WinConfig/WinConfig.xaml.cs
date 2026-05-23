using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Documents;
using System.Windows.Shapes;
using System.Windows.Markup;
using TypeSunny.ArticleSender;
using TypeSunny.Net;
using TypeSunny.UI;
using TypeSunny.Core;
using TypeSunny.Utils;
using TypeSunny.Versioning;
using TypeSunny.Personalization;


namespace TypeSunny
{
    /// <summary>
    /// WinConfig.xaml 的交互逻辑
    /// </summary>
    public partial class WinConfig : Window
    {
        // 当前选中的分类索引
        private int _currentCategoryIndex = 0;
        // 自定义最大化状态
        private bool _isCustomMaximized = false;
        private Rect _restoreBounds = new Rect();
        // 互斥模式的 CheckBox 引用
        private CheckBox _copybookCheckBox;
        private CheckBox _tracingCheckBox;
        private System.Windows.Threading.DispatcherTimer _configSavedRefreshTimer;
        private bool _hasPendingConfigSavedRefresh;
        private readonly List<Action> _categoryFallbackSaves = new List<Action>();

        // 配置分类数据结构
        private class ConfigCategory
        {
            public string Title { get; set; }
            public string[] Items { get; set; }
        }

        private List<ConfigCategory> _categories;

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

        public WinConfig()
        {
            InitializeComponent();

            // 当窗口激活时刷新文来登录状态
            Activated += async (s, e) =>
            {
                await RefreshWenlaiLoginStatusIfNeeded();
            };
        }

        /// <summary>
        /// 获取难度统计数据
        /// </summary>
        private Dictionary<int, int> GetDifficultyStats()
        {
            Dictionary<int, int> stats = new Dictionary<int, int>();
            try
            {
                // 使用 ArticleFetcher 来获取难度数据（会自动携带 cookie）
                var difficulties = ArticleFetcher.GetDifficulties();

                // 转换成字典格式
                foreach (var difficulty in difficulties)
                {
                    stats[difficulty.Id] = difficulty.Count;
                }
            }
            catch
            {
                // 如果获取失败，返回空字典
            }

            return stats;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 先应用当前选中的 Logo
            ApplyCurrentLogo();

            // 先应用主题颜色
            ApplyThemeColors();

            // 初始化并记录当前的文来登录状态
            try
            {
                var wenlaiHelper = new WenlaiHelper();
                bool isLoggedIn = wenlaiHelper.IsLoggedIn();
                Tag = isLoggedIn; // 记录初始登录状态
                System.Diagnostics.Debug.WriteLine($"[WinConfig] 初始文来登录状态: {isLoggedIn}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WinConfig] 检查文来登录状态失败: {ex.Message}");
                Tag = false;
            }

            // 初始化分类数据
            _categories = new List<ConfigCategory>
            {
                new ConfigCategory
                {
                    Title = "主题",
                    Items = new[]
                    {
                        "主题模式",
                        "当前Logo",
                        "窗体背景色", "窗体字体色",
                        "跟打区背景色", "跟打区字体色",
                        "发文区字体色",
                        "打对色", "打错色",
                        "按钮背景色", "按钮字体色",
                        "菜单背景色", "菜单字体色",
                        "字体"
                    }
                },
                new ConfigCategory
                {
                    Title = "首页",
                    Items = new string[0]
                },
                new ConfigCategory
                {
                    Title = "跟打",
                    Items = new[]
                    {
                        "错字重打",
                        "  错字重复次数",
                        "慢字重打",
                        "  慢字标准(单位:秒)",
                        "  慢字重复次数",
                        "重打跳转模式",
                        "贪吃蛇模式",
                        "  贪吃蛇前显字数",
                        "  贪吃蛇后显字数",
                        "字帖模式",
                        "临摹模式",
                        "速度跟随提示",
                        "禁用回改",
                        "禁止F3重打",
                        "显示进度条",
                        "自动发送成绩",
                        "盲打模式"
                    }
                },
                new ConfigCategory
                {
                    Title = "字提",
                    Items = new[]
                    {
                        "启用字提",
                        "字提方案",
                        "字提字体",
                        "字提字体大小",
                        "字提编码下显",
                        "字提选重数字角标"
                    }
                },
                new ConfigCategory
                {
                    Title = "词提",
                    Items = new[]
                    {
                        "启用词提",
                        "词提方案",
                        "词提编码下显",
                        "词提选重数字角标",
                        "词提不拆行",
                        "词提1简色",
                        "词提2简色",
                        "词提3简色",
                        "词提4码色",
                        "词提选重色",
                        "词提关闭所有颜色"
                    }
                },
                new ConfigCategory
                {
                    Title = "文来",
                    Items = new[]
                    {
                        "文来接口地址",
                        "文来字数",
                        "文来难度",
                        "文来分类",
                        "文来换段模式",
                        "字数模式"
                    }
                },
                new ConfigCategory
                {
                    Title = "赛文",
                    Items = new[]
                    {
                        "赛文服务器地址",
                        "赛文输入法"
                    }
                },
                new ConfigCategory
                {
                    Title = "过滤",
                    Items = new string[0]
                },
                new ConfigCategory
                {
                    Title = "成绩",
                    Items = new[]
                    {
                        "成绩显示时间",
                        "成绩签名"
                    }
                },
                new ConfigCategory
                {
                    Title = "预测",
                    Items = new[]
                    {
                        "启用预测",
                        "发文附带预测"
                    }
                },
                new ConfigCategory
                {
                    Title = "其他",
                    Items = new[]
                    {
                        "当前版本",
                        "最新版本",
                        "修复安装",
                        "软件更新Q群",
                        "作者邮箱QQ"
                    }
                }
            };

            // 生成导航按钮
            GenerateNavButtons();

            // 显示默认分类（第一个）
            ShowCategory(0);

            // 窗口完全加载后，确保文来难度数据正确显示
            await Dispatcher.BeginInvoke(new Action(async () =>
            {
                await Task.Delay(100); // 稍微延迟，确保 UI 完全初始化
                await ReloadWenlaiDifficultyConfig();
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        /// <summary>
        /// 生成导航按钮
        /// </summary>
        private void GenerateNavButtons()
        {
            NavPanel.Children.Clear();

            // 获取按钮背景色和字体色
            string btnBgColor = Config.GetString("按钮背景色");
            string btnFgColor = Config.GetString("按钮字体色");

            // 获取导航按钮样式（覆盖默认的悬停效果）
            var navButtonStyle = FindResource("NavButtonStyle") as Style;

            for (int i = 0; i < _categories.Count; i++)
            {
                var category = _categories[i];
                var navButton = new Button
                {
                    Content = category.Title,
                    Tag = i,
                    FontSize = 14,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Margin = new Thickness(10, 5, 10, 5),
                    Padding = new Thickness(15, 10, 15, 10),
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand,
                    Style = navButtonStyle // 应用自定义样式，覆盖WPF默认的悬停效果
                };

                // 应用按钮背景色和字体色
                if (i == 0)
                {
                    // 选中状态 - 使用菜单背景色和字体色
                    try
                    {
                        var bgColor = (Color)ColorConverter.ConvertFromString("#" + btnBgColor);
                        var fgColor = (Color)ColorConverter.ConvertFromString("#" + btnFgColor);
                        navButton.Background = new SolidColorBrush(bgColor);
                        navButton.Foreground = new SolidColorBrush(fgColor);
                    }
                    catch
                    {
                        navButton.Background = new SolidColorBrush(Color.FromRgb(235, 235, 235));
                        navButton.Foreground = new SolidColorBrush(Color.FromRgb(0, 0, 0));
                    }
                    navButton.FontWeight = FontWeights.Bold;
                }
                else
                {
                    // 普通状态 - 使用按钮背景色和字体色
                    try
                    {
                        var bgColor = (Color)ColorConverter.ConvertFromString("#" + btnBgColor);
                        var fgColor = (Color)ColorConverter.ConvertFromString("#" + btnFgColor);
                        navButton.Background = new SolidColorBrush(bgColor);
                        navButton.Foreground = new SolidColorBrush(fgColor);
                    }
                    catch
                    {
                        navButton.Background = new SolidColorBrush(Color.FromRgb(235, 235, 235));
                        navButton.Foreground = new SolidColorBrush(Color.FromRgb(0, 0, 0));
                    }
                }

                navButton.Click += NavButton_Click;
                navButton.MouseEnter += NavButton_MouseEnter;
                navButton.MouseLeave += NavButton_MouseLeave;
                NavPanel.Children.Add(navButton);
            }
        }

        /// <summary>
        /// 导航按钮鼠标悬停
        /// </summary>
        private void NavButton_MouseEnter(object sender, MouseEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;

            var currentColor = ((SolidColorBrush)button.Background).Color;

            // 计算背景亮度
            double bgBrightness = (currentColor.R * 299 + currentColor.G * 587 + currentColor.B * 114) / 1000;

            Color hoverColor;
            if (bgBrightness < 128) // 背景较深（接近黑色），变浅
            {
                hoverColor = Color.FromRgb(
                    (byte)Math.Min(255, currentColor.R + 35),
                    (byte)Math.Min(255, currentColor.G + 35),
                    (byte)Math.Min(255, currentColor.B + 35)
                );
            }
            else // 背景较浅（接近白色），变深
            {
                hoverColor = Color.FromRgb(
                    (byte)Math.Max(0, currentColor.R - 25),
                    (byte)Math.Max(0, currentColor.G - 25),
                    (byte)Math.Max(0, currentColor.B - 25)
                );
            }

            button.Background = new SolidColorBrush(hoverColor);
        }

        /// <summary>
        /// 导航按钮鼠标离开
        /// </summary>
        private void NavButton_MouseLeave(object sender, MouseEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;

            // 恢复按钮的正常颜色
            int index = (int)button.Tag;

            // 获取按钮背景色和字体色
            string btnBgColor = Config.GetString("按钮背景色");
            string btnFgColor = Config.GetString("按钮字体色");

            // 所有按钮都使用按钮背景色和字体色，只有字体粗细区分选中状态
            try
            {
                var bgColor = (Color)ColorConverter.ConvertFromString("#" + btnBgColor);
                var fgColor = (Color)ColorConverter.ConvertFromString("#" + btnFgColor);
                button.Background = new SolidColorBrush(bgColor);
                button.Foreground = new SolidColorBrush(fgColor);
            }
            catch
            {
                button.Background = new SolidColorBrush(Color.FromRgb(235, 235, 235));
                button.Foreground = new SolidColorBrush(Color.FromRgb(0, 0, 0));
            }

            if (index == _currentCategoryIndex)
            {
                button.FontWeight = FontWeights.Bold;
            }
            else
            {
                button.FontWeight = FontWeights.Normal;
            }
        }

        /// <summary>
        /// 导航按钮点击处理
        /// </summary>
        private void NavButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;

            int categoryIndex = (int)button.Tag;
            SaveCurrentCategoryControls();

            // 更新导航按钮样式
            UpdateNavButtonStyles(categoryIndex);

            // 切换内容区
            ShowCategory(categoryIndex);
        }

        /// <summary>
        /// 更新导航按钮样式
        /// </summary>
        private void UpdateNavButtonStyles(int selectedIndex)
        {
            // 获取按钮背景色和字体色
            string btnBgColor = Config.GetString("按钮背景色");
            string btnFgColor = Config.GetString("按钮字体色");

            for (int i = 0; i < NavPanel.Children.Count; i++)
            {
                if (NavPanel.Children[i] is Button button)
                {
                    // 所有按钮都使用按钮背景色和字体色，只有字体粗细区分选中状态
                    try
                    {
                        var bgColor = (Color)ColorConverter.ConvertFromString("#" + btnBgColor);
                        var fgColor = (Color)ColorConverter.ConvertFromString("#" + btnFgColor);
                        button.Background = new SolidColorBrush(bgColor);
                        button.Foreground = new SolidColorBrush(fgColor);
                    }
                    catch
                    {
                        button.Background = new SolidColorBrush(Color.FromRgb(235, 235, 235));
                        button.Foreground = new SolidColorBrush(Color.FromRgb(0, 0, 0));
                    }

                    if (i == selectedIndex)
                    {
                        button.FontWeight = FontWeights.Bold;
                    }
                    else
                    {
                        button.FontWeight = FontWeights.Normal;
                    }
                }
            }
            _currentCategoryIndex = selectedIndex;
        }

        /// <summary>
        /// 显示指定分类的内容
        /// </summary>
        private void ShowCategory(int categoryIndex)
        {
            if (categoryIndex < 0 || categoryIndex >= _categories.Count)
                return;

            var category = _categories[categoryIndex];

            // 清空内容区
            ContentPanel.Children.Clear();
            ContentPanel.RowDefinitions.Clear();
            _categoryFallbackSaves.Clear();

            // 添加分类标题
            var titleBlock = new TextBlock
            {
                Text = category.Title,
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 20),
                Foreground = new SolidColorBrush(Color.FromRgb(100, 200, 255))
            };

            ContentPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto, MinHeight = 40 });
            Grid.SetRow(titleBlock, 0);
            Grid.SetColumnSpan(titleBlock, 2); // 标题跨两列
            ContentPanel.Children.Add(titleBlock);

            // 添加该分类下的配置项
            int currentRow = 1;
            if (category.Title == "词提")
            {
                AddCiTiLegend(currentRow);
                currentRow++;
            }

            foreach (var rawItemKey in category.Items)
            {
                // 子项缩进：以空格开头的项视为子项，去掉前缀空格得到实际 key
                string itemKey = rawItemKey.TrimStart();
                bool isChild = rawItemKey != itemKey;

                if (!Config.dicts.ContainsKey(itemKey))
                {
                    // 新配置项可能还不存在，给默认空值让控件能正常创建
                    Config.dicts[itemKey] = "";
                }

                string itemValue = Config.dicts[itemKey];

                ContentPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto, MinHeight = 38 });

                // 创建标签
                var tbk = new TextBlock
                {
                    Text = itemKey,
                    Margin = new Thickness(isChild ? 20 : 0, 10, 20, 10),
                    FontSize = 14,
                    MinWidth = 120
                };
                // 字帖高度选项添加悬停提示
                if (itemKey == "字帖编码高度" || itemKey == "字帖候选框高度" || itemKey == "字帖错字高度")
                {
                    tbk.ToolTip = ">0 往下调，<0 往上调，建议以 0.1 为单位微调";
                }
                if (itemKey == "禁用回改")
                {
                    tbk.ToolTip = "该模式下禁止使用退格、Esc、Ctrl+Z，空格或回车后没上屏内容则强制上屏一个空格。努力提升键准吧少年！";
                }
                if (itemKey == "词提不拆行")
                {
                    tbk.ToolTip = "开启后，同一词组的字不会被拆到两行显示；行尾放不下时整词换行，每行字数可能不等。";
                }
                if (itemKey == "启用预测")
                {
                    tbk.ToolTip = "开启后在标题栏显示个人预测；低置信冷启动阶段可能暂不显示预测。关闭后不会显示预测，也不会在发文成绩中附带预测。";
                }
                if (itemKey == "发文附带预测")
                {
                    tbk.ToolTip = "只有启用预测且当前预测置信度大于80%时，才会把预测信息附加到发文成绩最后。";
                }

                FrameworkElement labelControl = CreateLabelControl(tbk);
                Grid.SetRow(labelControl, currentRow);
                Grid.SetColumn(labelControl, 0);
                ContentPanel.Children.Add(labelControl);

                // 创建值控件
                FrameworkElement valueControl = CreateValueControl(itemKey, itemValue);

                if (valueControl != null)
                {
                    Grid.SetRow(valueControl, currentRow);
                    Grid.SetColumn(valueControl, 1);
                    ContentPanel.Children.Add(valueControl);
                }

                currentRow++;
            }

            // 如果是"成绩"分类，在常规项后面内嵌成绩显示项拖拽列表
            if (category.Title == "成绩")
            {
                AppendScoreItemsList(currentRow);
            }

            // 如果是"预测"分类，在常规项后面内嵌预测显示项拖拽列表
            if (category.Title == "预测")
            {
                AppendPredictionItemsList(currentRow);
            }

            // 如果是"首页"分类，构建首页入口排序与显示设置
            if (category.Title == "首页")
            {
                AppendHomeToolbarSettings(currentRow);
                currentRow += 2;
            }

            // 如果是"过滤"分类，构建自定义过滤设置UI
            if (category.Title == "过滤")
            {
                AppendFilterSettings(currentRow);
            }
        }

        private void AddCiTiLegend(int row)
        {
            ContentPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto, MinHeight = 56 });

            var legend = new TextBlock
            {
                Text = "颜色说明：红色 = 1简词（1键编码）  橙色 = 2简词（2键编码）\n蓝色 = 3简词（3键编码）  灰色 = 4码及以上  绿色 = 选重（需要按数字键选字）",
                Margin = new Thickness(0, 0, 0, 12),
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap
            };

            Grid.SetRow(legend, row);
            Grid.SetColumnSpan(legend, 2);
            ContentPanel.Children.Add(legend);
        }

        private FrameworkElement CreateLabelControl(TextBlock labelBlock)
        {
            labelBlock.Tag = "ConfigLabelText";
            if (labelBlock.ToolTip == null)
            {
                return labelBlock;
            }

            Thickness labelMargin = labelBlock.Margin;
            double labelMinWidth = labelBlock.MinWidth;
            labelBlock.Margin = new Thickness(0);
            labelBlock.MinWidth = 0;

            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = labelMargin,
                MinWidth = labelMinWidth,
                VerticalAlignment = VerticalAlignment.Center
            };
            panel.Children.Add(labelBlock);

            var tooltipText = labelBlock.ToolTip?.ToString() ?? "";
            var helpBtn = new TextBlock
            {
                Text = "?",
                Tag = "ConfigTooltipIndicator",
                Cursor = Cursors.Hand,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Width = 16,
                Height = 16,
                Margin = new Thickness(6, 0, 0, 0),
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 200, 255))
            };

            var popup = new Popup
            {
                AllowsTransparency = true,
                Placement = PlacementMode.Bottom,
                PlacementTarget = helpBtn,
                StaysOpen = false,
                Child = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(50, 50, 50)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(100, 200, 255)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(10, 6, 10, 6),
                    MaxWidth = 320,
                    Child = new TextBlock
                    {
                        Text = tooltipText,
                        Foreground = Brushes.White,
                        FontSize = 12,
                        TextWrapping = TextWrapping.Wrap
                    }
                }
            };

            helpBtn.MouseLeftButtonUp += (s, e) =>
            {
                popup.IsOpen = !popup.IsOpen;
                e.Handled = true;
            };

            panel.Children.Add(helpBtn);

            return panel;
        }

        /// <summary>
        /// 创建值控件
        /// </summary>
        private FrameworkElement CreateValueControl(string itemKey, string itemValue)
        {
            FrameworkElement valueControl = null;

            if (itemKey == "成绩显示时间")
            {
                var options = new[] {
                    "关闭",
                    "HH:mm",
                    "HH:mm:ss",
                    "MM-dd HH:mm",
                    "MM-dd HH:mm:ss",
                    "MM/dd HH:mm",
                    "MM月dd日 HH:mm",
                    "yyyy-MM-dd HH:mm",
                    "yyyy/MM/dd HH:mm:ss",
                };
                var cb = new ComboBox
                {
                    Width = 200,
                    Margin = new Thickness(0, 8, 0, 8),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Foreground = this.Foreground,
                    Background = this.Background,
                };
                foreach (var opt in options)
                    cb.Items.Add(opt);
                int idx = Array.IndexOf(options, itemValue);
                cb.SelectedIndex = idx >= 0 ? idx : 3;
                cb.SelectionChanged += (s, e) =>
                {
                    if (cb.SelectedIndex >= 0)
                    {
                        Config.Set("成绩显示时间", options[cb.SelectedIndex]);
                        RefreshMainWindowResults();
                    }
                };
                valueControl = cb;
            }
            // 根据配置项类型创建对应的控件
            else if (itemValue == "是" || itemValue == "否")
            {
                var chk = new CheckBox
                {
                    IsChecked = itemValue == "是",
                    Margin = new Thickness(0, 8, 0, 8),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Style = FindResource("ModernToggleStyle") as Style
                };

                // 为"显示进度条"添加实时刷新事件
                if (itemKey == "显示进度条")
                {
                    chk.Checked += (obj, evt) =>
                    {
                        Config.Set("显示进度条", "是");
                        UpdateMainWindowProgressBar();
                    };
                    chk.Unchecked += (obj, evt) =>
                    {
                        Config.Set("显示进度条", "否");
                        UpdateMainWindowProgressBar();
                    };
                }

                // 字帖模式和临摹模式互斥
                if (itemKey == "字帖模式")
                {
                    _copybookCheckBox = chk;
                    chk.Checked += (obj, evt) =>
                    {
                        if (_tracingCheckBox != null && _tracingCheckBox.IsChecked == true)
                            _tracingCheckBox.IsChecked = false;
                    };
                }
                if (itemKey == "临摹模式")
                {
                    _tracingCheckBox = chk;
                    chk.Checked += (obj, evt) =>
                    {
                        if (_copybookCheckBox != null && _copybookCheckBox.IsChecked == true)
                            _copybookCheckBox.IsChecked = false;
                    };
                }

                if (itemKey == "词提编码下显")
                {
                    chk.Checked += (obj, evt) =>
                    {
                        var ciTiCheckBox = FindCheckBoxByLabel("启用词提");
                        if (ciTiCheckBox != null)
                            ciTiCheckBox.IsChecked = true;

                        var ziTiDisplayCheckBox = FindCheckBoxByLabel("字提编码下显");
                        if (ziTiDisplayCheckBox != null && ziTiDisplayCheckBox.IsChecked == true)
                            ziTiDisplayCheckBox.IsChecked = false;

                        var ciTiBadgeCheckBox = FindCheckBoxByLabel("词提选重数字角标");
                        if (ciTiBadgeCheckBox != null && ciTiBadgeCheckBox.IsChecked == true)
                            ciTiBadgeCheckBox.IsChecked = false;
                    };
                }

                if (itemKey == "词提选重数字角标")
                {
                    chk.Checked += (obj, evt) =>
                    {
                        var ciTiDisplayCheckBox = FindCheckBoxByLabel("词提编码下显");
                        if (ciTiDisplayCheckBox != null && ciTiDisplayCheckBox.IsChecked == true)
                            ciTiDisplayCheckBox.IsChecked = false;
                    };
                }

                if (itemKey == "字提编码下显")
                {
                    chk.Checked += (obj, evt) =>
                    {
                        var ziTiCheckBox = FindCheckBoxByLabel("启用字提");
                        if (ziTiCheckBox != null)
                            ziTiCheckBox.IsChecked = true;

                        var ciTiDisplayCheckBox = FindCheckBoxByLabel("词提编码下显");
                        if (ciTiDisplayCheckBox != null && ciTiDisplayCheckBox.IsChecked == true)
                            ciTiDisplayCheckBox.IsChecked = false;

                        var ziTiBadgeCheckBox = FindCheckBoxByLabel("字提选重数字角标");
                        if (ziTiBadgeCheckBox != null && ziTiBadgeCheckBox.IsChecked == true)
                            ziTiBadgeCheckBox.IsChecked = false;
                    };
                }

                if (itemKey == "字提选重数字角标")
                {
                    chk.Checked += (obj, evt) =>
                    {
                        var ziTiDisplayCheckBox = FindCheckBoxByLabel("字提编码下显");
                        if (ziTiDisplayCheckBox != null && ziTiDisplayCheckBox.IsChecked == true)
                            ziTiDisplayCheckBox.IsChecked = false;
                    };
                }

                if (itemKey == "启用词提")
                {
                    chk.Unchecked += (obj, evt) =>
                    {
                        var ciTiDisplayCheckBox = FindCheckBoxByLabel("词提编码下显");
                        if (ciTiDisplayCheckBox != null && ciTiDisplayCheckBox.IsChecked == true)
                            ciTiDisplayCheckBox.IsChecked = false;

                        var ciTiNoSplitCheckBox = FindCheckBoxByLabel("词提不拆行");
                        if (ciTiNoSplitCheckBox != null && ciTiNoSplitCheckBox.IsChecked == true)
                            ciTiNoSplitCheckBox.IsChecked = false;
                    };
                }

                if (itemKey == "启用字提")
                {
                    chk.Unchecked += (obj, evt) =>
                    {
                        var ziTiDisplayCheckBox = FindCheckBoxByLabel("字提编码下显");
                        if (ziTiDisplayCheckBox != null && ziTiDisplayCheckBox.IsChecked == true)
                            ziTiDisplayCheckBox.IsChecked = false;
                    };
                }

                valueControl = chk;
            }
            else if (itemKey == "主题模式")
            {
                // 创建一个 StackPanel 来放 ComboBox 和按钮
                var panel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, 5, 0, 5)
                };

                var cb = new ComboBox
                {
                    Width = 200,
                    Margin = new Thickness(0, 8, 0, 8),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Tag = "ThemeModeComboBox"
                };

                // 动态加载所有可用主题
                var themes = ThemeManager.GetAvailableThemes();
                foreach (var theme in themes)
                {
                    cb.Items.Add(theme);
                }

                // 设置当前选中的主题
                int selectedIndex = 0;
                for (int i = 0; i < themes.Length; i++)
                {
                    if (themes[i] == itemValue)
                    {
                        selectedIndex = i;
                        break;
                    }
                }
                cb.SelectedIndex = selectedIndex;
                cb.SelectionChanged += ThemeMode_SelectionChanged;

                panel.Children.Add(cb);

                // 新建主题按钮
                var newThemeBtn = new Button
                {
                    Content = "新建主题",
                    Width = 70,
                    Height = 28,
                    Margin = new Thickness(8, 0, 0, 0),
                    Tag = "NewTheme"
                };
                newThemeBtn.Click += NewTheme_Click;
                panel.Children.Add(newThemeBtn);

                // 重命名主题按钮（只对自定义主题显示）
                var renameThemeBtn = new Button
                {
                    Content = "重命名",
                    Width = 60,
                    Height = 28,
                    Margin = new Thickness(8, 0, 0, 0),
                    Tag = "RenameTheme",
                    Visibility = ThemeManager.IsBuiltInTheme(itemValue) ? Visibility.Collapsed : Visibility.Visible
                };

                // 根据选中主题显示/隐藏按钮
                cb.SelectionChanged += (s, e) =>
                {
                    var comboBox = s as ComboBox;
                    if (comboBox != null && comboBox.SelectedItem != null)
                    {
                        string selectedTheme = comboBox.SelectedItem.ToString();
                        renameThemeBtn.Visibility = ThemeManager.IsBuiltInTheme(selectedTheme) ? Visibility.Collapsed : Visibility.Visible;
                    }
                };

                renameThemeBtn.Click += RenameTheme_Click;
                panel.Children.Add(renameThemeBtn);

                valueControl = panel;
            }
            else if (ColorConfigItems.Contains(itemKey))
            {
                var btn = new Button
                {
                    Width = 200,
                    Height = 30,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(0, 5, 0, 5),
                    Tag = itemKey,
                    Template = CreateColorButtonTemplate()
                };
                try
                {
                    var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#" + itemValue);
                    btn.Background = new SolidColorBrush(color);
                    btn.Content = itemValue;
                }
                catch
                {
                    btn.Content = itemValue;
                }
                btn.Click += ColorButton_Click;
                valueControl = btn;
            }
            else if (itemKey == "字体")
            {
                var cb = new ComboBox
                {
                    Width = 200,
                    Margin = new Thickness(0, 8, 0, 8),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Tag = "Font"
                };

                LoadFontItems(cb);

                // 设置当前选中项（精确匹配或前缀匹配，兼容 face name 差异）
                int matchIndex = -1;
                for (int i = 0; i < cb.Items.Count; i++)
                {
                    string item = cb.Items[i].ToString();
                    if (item == itemValue)
                    {
                        matchIndex = i;
                        break;
                    }
                    if (item.StartsWith(itemValue) || itemValue.StartsWith(item))
                    {
                        matchIndex = i;
                    }
                }
                if (matchIndex >= 0)
                    cb.SelectedIndex = matchIndex;
                valueControl = cb;
            }
            else if (itemKey == "字提方案")
            {
                var cb = new ComboBox
                {
                    Width = 200,
                    Margin = new Thickness(0, 8, 0, 8),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Tag = "ZiTiScheme"
                };

                // 获取可用的字提方案
                var schemes = ZiTiHelper.GetAvailableSchemes();

                if (schemes.Count > 0)
                {
                    foreach (var scheme in schemes)
                    {
                        cb.Items.Add(scheme);
                    }

                    // 设置当前选中项
                    if (!string.IsNullOrEmpty(itemValue) && schemes.Contains(itemValue))
                    {
                        cb.SelectedIndex = schemes.IndexOf(itemValue);
                    }
                    else if (cb.Items.Count > 0)
                    {
                        cb.SelectedIndex = 0;
                    }
                }
                else
                {
                    cb.Items.Add("无可用方案");
                    cb.IsEnabled = false;
                    cb.SelectedIndex = 0;
                }

                valueControl = cb;
            }
            else if (itemKey == "词提方案")
            {
                var cb = new ComboBox
                {
                    Width = 200,
                    Margin = new Thickness(0, 8, 0, 8),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Tag = "CiTiScheme"
                };

                var schemes = CiTiHelper.GetAvailableSchemes();

                if (schemes.Count > 0)
                {
                    foreach (var scheme in schemes)
                        cb.Items.Add(scheme);

                    if (!string.IsNullOrEmpty(itemValue) && schemes.Contains(itemValue))
                        cb.SelectedIndex = schemes.IndexOf(itemValue);
                    else if (cb.Items.Count > 0)
                        cb.SelectedIndex = 0;
                }
                else
                {
                    cb.Items.Add("无可用方案");
                    cb.IsEnabled = false;
                    cb.SelectedIndex = 0;
                }

                valueControl = cb;
            }
            else if (itemKey == "当前Logo")
            {
                var cb = new ComboBox
                {
                    Width = 200,
                    Margin = new Thickness(0, 8, 0, 8),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Tag = "CurrentLogo"
                };

                // 获取所有可用的 Logo
                var logos = MainWindow.GetAvailableLogos();
                foreach (var logo in logos)
                {
                    cb.Items.Add(logo);
                }

                // 设置当前选中项
                int selectedIndex = 0;
                for (int i = 0; i < logos.Length; i++)
                {
                    if (logos[i] == itemValue)
                    {
                        selectedIndex = i;
                        break;
                    }
                }
                cb.SelectedIndex = selectedIndex;
                cb.SelectionChanged += Logo_SelectionChanged;

                valueControl = cb;
            }
            else if (itemKey == "字提字体")
            {
                var cb = new ComboBox
                {
                    Width = 200,
                    Margin = new Thickness(0, 8, 0, 8),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Tag = "ZiTiFont"
                };

                LoadFontItems(cb);

                // 设置当前选中项（精确匹配或前缀匹配，兼容 face name 差异）
                int matchIndex = -1;
                for (int i = 0; i < cb.Items.Count; i++)
                {
                    string item = cb.Items[i].ToString();
                    if (item == itemValue)
                    {
                        matchIndex = i;
                        break;
                    }
                    if (item.StartsWith(itemValue) || itemValue.StartsWith(item))
                    {
                        matchIndex = i;
                    }
                }
                if (matchIndex >= 0)
                    cb.SelectedIndex = matchIndex;
                valueControl = cb;
            }
            else if (itemKey == "盲打模式")
            {
                var cb = new ComboBox
                {
                    Width = 200,
                    Margin = new Thickness(0, 8, 0, 8),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Tag = "TypingMode"
                };
                cb.Items.Add("跟打");
                cb.Items.Add("盲打");
                cb.Items.Add("看打");

                bool isBlind = Config.GetBool("盲打模式");
                bool isLook = Config.GetBool("看打模式");
                cb.SelectedIndex = isLook ? 2 : isBlind ? 1 : 0;
                valueControl = cb;
            }
            else if (itemKey == "看打模式")
            {
                // 跳过，已在盲打模式中处理
                return null;
            }
            else if (itemKey == "文来换段模式")
            {
                var cb = new ComboBox
                {
                    Width = 200,
                    Margin = new Thickness(0, 8, 0, 8),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Tag = "WenlaiSegmentMode"
                };
                cb.Items.Add("自动");
                cb.Items.Add("手动");

                string mode = Config.GetString("文来换段模式");
                cb.SelectedIndex = (mode == "手动") ? 1 : 0;

                // 选择改变时显示提示
                cb.SelectionChanged += (s, e) =>
                {
                    if (cb.SelectedIndex == 1) // 选择了"手动"
                    {
                        MessageBox.Show(
                            "手动换段模式：\n\n" +
                            "打完一段后不会自动发送下一段\n" +
                            "需要按 Ctrl+P 发下一段\n" +
                            "或按 Ctrl+O 发上一段",
                            "文来换段模式",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                };

                valueControl = cb;
            }
            else if (itemKey == "重打跳转模式")
            {
                var cb = new ComboBox
                {
                    Width = 200,
                    Margin = new Thickness(0, 8, 0, 8),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Tag = "RetypeJumpMode"
                };
                cb.Items.Add("自动");
                cb.Items.Add("手动");

                string mode = Config.GetString("重打跳转模式");
                cb.SelectedIndex = (mode == "手动") ? 1 : 0;
                cb.SelectionChanged += (s, e) =>
                {
                    if (cb.SelectedIndex == 1)
                    {
                        MessageBox.Show(
                            "手动重打跳转模式：\n\n" +
                            "打完后如果触发错字重打或慢字重打，先停在成绩页\n" +
                            "按空格或回车进入重打",
                            "重打跳转模式",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                };

                valueControl = cb;
            }
            else if (itemKey == "字数模式")
            {
                var cb = new ComboBox
                {
                    Width = 200,
                    Margin = new Thickness(0, 8, 0, 8),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Tag = "LengthMode"
                };
                cb.Items.Add("智能分段");
                cb.Items.Add("精确字数");

                string mode = Config.GetString("字数模式");
                cb.SelectedIndex = (mode == "精确字数") ? 1 : 0;

                valueControl = cb;
            }
            else if (itemKey == "文来难度")
            {
                // 创建一个加载中的占位控件
                var loadingPanel = new StackPanel
                {
                    Width = 200,
                    Margin = new Thickness(0, 8, 0, 8),
                    HorizontalAlignment = HorizontalAlignment.Left
                };
                var loadingText = new TextBlock { Text = "加载中..." };
                loadingPanel.Children.Add(loadingText);
                valueControl = loadingPanel;

                // 异步加载难度数据，加载完成后替换控件
                _ = LoadDifficultyDataAsync(loadingPanel, itemValue);
            }
            else if (itemKey == "文来分类")
            {
                var loadingPanel = new StackPanel
                {
                    Width = 200,
                    Margin = new Thickness(0, 8, 0, 8),
                    HorizontalAlignment = HorizontalAlignment.Left
                };
                var loadingText = new TextBlock { Text = "加载中..." };
                loadingPanel.Children.Add(loadingText);
                valueControl = loadingPanel;

                _ = LoadCategoryDataAsync(loadingPanel, itemValue);
            }
            else if (itemKey == "当前版本")
            {
                // 当前版本：只读文本框
                var tb = new TextBox
                {
                    Text = VersionManager.CurrentVersion,
                    Width = 200,
                    Margin = new Thickness(0, 8, 0, 8),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    IsReadOnly = true,
                    Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 245, 245)),
                    Cursor = System.Windows.Input.Cursors.Arrow
                };
                valueControl = tb;
            }
            else if (itemKey == "最新版本")
            {
                var panel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, 5, 0, 5)
                };

                var tb = new TextBox
                {
                    Text = itemValue,
                    Width = 120,
                    Height = 28,
                    Margin = new Thickness(0, 3, 5, 3),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    IsReadOnly = true,
                    Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 245, 245)),
                    Cursor = System.Windows.Input.Cursors.Arrow,
                    VerticalAlignment = VerticalAlignment.Center
                };
                panel.Children.Add(tb);

                var refreshBtn = new Button
                {
                    Content = "刷新",
                    Width = 60,
                    Height = 28,
                    Margin = new Thickness(0, 0, 5, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };

                var updateBtn = new Button
                {
                    Content = "立即更新",
                    Width = 70,
                    Height = 28,
                    FontWeight = FontWeights.Bold,
                    VerticalAlignment = VerticalAlignment.Center,
                    Visibility = VersionManager.HasUpdate ? Visibility.Visible : Visibility.Collapsed
                };

                refreshBtn.Click += async (s, e) =>
                {
                    refreshBtn.IsEnabled = false;
                    tb.Text = "检查中...";
                    try
                    {
                        await VersionManager.CheckUpdateAsync(
                            forceRefresh: VersionCheckPolicy.ShouldForceRefresh(VersionCheckTrigger.Manual));
                        tb.Text = VersionManager.LatestVersion;
                        updateBtn.Visibility = VersionManager.HasUpdate ? Visibility.Visible : Visibility.Collapsed;
                    }
                    catch (Exception ex)
                    {
                        tb.Text = "检查失败";
                        System.Diagnostics.Debug.WriteLine($"检查更新失败: {ex.Message}");
                    }
                    finally
                    {
                        refreshBtn.IsEnabled = true;
                    }
                };

                updateBtn.Click += (s, e) =>
                {
                    var dialog = new UI.UpdateDialog(this);
                    dialog.ShowDialog();
                };

                panel.Children.Add(refreshBtn);
                panel.Children.Add(updateBtn);

                valueControl = panel;
            }
            else if (itemKey == "修复安装")
            {
                var panel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, 5, 0, 5)
                };

                var repairBtn = new Button
                {
                    Content = "下载全量包并修复",
                    Height = 28,
                    Padding = new Thickness(10, 0, 10, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };

                repairBtn.Click += async (s, e) =>
                {
                    var result = MessageBox.Show(
                        "将下载全量安装包并覆盖本地文件，完成后软件会自动重启。\n\n确定继续？",
                        "修复安装",
                        MessageBoxButton.OKCancel,
                        MessageBoxImage.Question);

                    if (result != MessageBoxResult.OK)
                        return;

                    repairBtn.IsEnabled = false;
                    repairBtn.Content = "获取下载地址...";

                    try
                    {
                        if (string.IsNullOrWhiteSpace(VersionManager.FullPackageUrl))
                            await VersionManager.CheckUpdateAsync(forceRefresh: true);

                        if (string.IsNullOrWhiteSpace(VersionManager.FullPackageUrl))
                        {
                            MessageBox.Show("未能获取全量包地址，请稍后重试或前往发布页手动下载。",
                                "修复安装", MessageBoxButton.OK, MessageBoxImage.Warning);
                            repairBtn.IsEnabled = true;
                            repairBtn.Content = "下载全量包并修复";
                            return;
                        }

                        var progressWin = new System.Windows.Window
                        {
                            Title = "修复安装",
                            Width = 360,
                            Height = 130,
                            WindowStartupLocation = WindowStartupLocation.CenterOwner,
                            Owner = this,
                            ResizeMode = ResizeMode.NoResize
                        };
                        var progressPanel = new StackPanel { Margin = new Thickness(16) };
                        var progressLabel = new TextBlock { Text = "正在下载...", Margin = new Thickness(0, 0, 0, 8) };
                        var progressBar = new System.Windows.Controls.ProgressBar { Height = 20, Minimum = 0, Maximum = 100 };
                        progressPanel.Children.Add(progressLabel);
                        progressPanel.Children.Add(progressBar);
                        progressWin.Content = progressPanel;

                        var cts = new CancellationTokenSource();
                        progressWin.Closing += (_, __) => cts.Cancel();
                        progressWin.Show();

                        var progress = new Progress<(long downloaded, long? total)>(p =>
                        {
                            if (p.total.HasValue && p.total.Value > 0)
                            {
                                progressBar.Value = (double)p.downloaded / p.total.Value * 100;
                                progressLabel.Text = $"正在下载... {p.downloaded / 1024 / 1024:F1} MB / {p.total.Value / 1024 / 1024:F1} MB";
                            }
                            else
                            {
                                progressLabel.Text = $"正在下载... {p.downloaded / 1024 / 1024:F1} MB";
                            }
                        });

                        await Utils.UpdatePackageDownloader.DownloadAndApplyAsync(
                            VersionManager.FullPackageUrl, progress, cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        repairBtn.IsEnabled = true;
                        repairBtn.Content = "下载全量包并修复";
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"修复安装失败：{ex.Message}", "修复安装", MessageBoxButton.OK, MessageBoxImage.Error);
                        repairBtn.IsEnabled = true;
                        repairBtn.Content = "下载全量包并修复";
                    }
                };

                panel.Children.Add(repairBtn);
                valueControl = panel;
            }
            else if (itemKey == "文来接口地址" || itemKey == "赛文服务器地址")
            {
                var panel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, 5, 0, 5)
                };

                var tb = new TextBox
                {
                    Text = itemValue,
                    Width = 200,
                    Height = 28,
                    Margin = new Thickness(0, 3, 5, 3),
                    VerticalAlignment = VerticalAlignment.Center
                };
                panel.Children.Add(tb);

                var resetBtn = new Button
                {
                    Content = "恢复默认",
                    Width = 65,
                    Height = 28,
                    VerticalAlignment = VerticalAlignment.Center
                };
                resetBtn.Click += (s, e) =>
                {
                    tb.Text = "https://qingfawen.fcxxz.com/";
                    SaveConfigValue(itemKey, tb.Text);
                };
                panel.Children.Add(resetBtn);

                valueControl = panel;
            }
            else
            {
                var tb = new TextBox
                {
                    Text = itemValue,
                    Width = 200,
                    Margin = new Thickness(0, 8, 0, 8),
                    HorizontalAlignment = HorizontalAlignment.Left
                };

                // 设置只读配置项
                if (itemKey == "软件更新Q群" || itemKey == "作者邮箱QQ")
                {
                    tb.IsReadOnly = true;
                    tb.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 245, 245));
                    tb.Cursor = System.Windows.Input.Cursors.Arrow;
                }

                if (itemKey == "文来字数")
                    tb.ToolTip = "不填默认发500字左右的整段文";
                valueControl = tb;
            }

            AttachAutoSave(itemKey, valueControl);
            return valueControl;
        }

        /// <summary>
        /// 加载字体项
        /// </summary>
        private void LoadFontItems(ComboBox cb)
        {
            System.Globalization.CultureInfo cn = System.Globalization.CultureInfo.GetCultureInfo("zh-CN");
            System.Globalization.CultureInfo en = System.Globalization.CultureInfo.GetCultureInfo("en-US");
            System.IO.DirectoryInfo dr = new System.IO.DirectoryInfo("字体");
            if (dr.Exists)
            {
                foreach (var f in dr.GetFiles("*.ttf"))
                {
                    try
                    {
                        var fullname = f.FullName;
                        System.Windows.Media.GlyphTypeface gf = new System.Windows.Media.GlyphTypeface(new Uri(fullname));
                        // 用 Win32FamilyNames（WPF FontFamily 渲染时匹配的名称）
                        var familyNames = gf.Win32FamilyNames;
                        string familyName = "";
                        if (familyNames.ContainsKey(cn))
                            familyName = familyNames[cn];
                        else if (familyNames.ContainsKey(en))
                            familyName = familyNames[en];
                        if (familyName != "")
                            cb.Items.Add("#" + familyName);
                    }
                    catch { }
                }
            }

            foreach (System.Windows.Media.FontFamily fontfamily in System.Windows.Media.Fonts.SystemFontFamilies)
            {
                LanguageSpecificStringDictionary lsd = fontfamily.FamilyNames;
                if (lsd.ContainsKey(System.Windows.Markup.XmlLanguage.GetLanguage("zh-cn")))
                {
                    string fontname = null;
                    if (lsd.TryGetValue(System.Windows.Markup.XmlLanguage.GetLanguage("zh-cn"), out fontname))
                        cb.Items.Add(fontname);
                }
                else
                {
                    string fontname = null;
                    if (lsd.TryGetValue(System.Windows.Markup.XmlLanguage.GetLanguage("en-us"), out fontname))
                        cb.Items.Add(fontname);
                }
            }
        }

        /// <summary>
        /// 刷新文来登录状态（如果需要）
        /// </summary>
        private async Task RefreshWenlaiLoginStatusIfNeeded()
        {
            try
            {
                // 获取当前登录状态
                bool currentlyLoggedIn = false;
                try
                {
                    var wenlaiHelper = new WenlaiHelper();
                    currentlyLoggedIn = wenlaiHelper.IsLoggedIn();
                }
                catch
                {
                    // 如果检查失败，认为未登录
                    currentlyLoggedIn = false;
                }

                // 获取上次记录的登录状态
                if (Tag is bool lastLoggedIn)
                {
                    // 如果状态改变了，重新加载难度数据
                    if (lastLoggedIn != currentlyLoggedIn)
                    {
                        System.Diagnostics.Debug.WriteLine($"[WinConfig] 文来登录状态改变: {lastLoggedIn} -> {currentlyLoggedIn}，刷新难度数据");
                        Tag = currentlyLoggedIn;

                        // 重新加载文来难度配置项
                        await ReloadWenlaiDifficultyConfig();
                    }
                }
                else
                {
                    // 首次记录登录状态
                    Tag = currentlyLoggedIn;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WinConfig] 刷新文来登录状态失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 重新加载文来难度配置项（公共方法，供外部调用）
        /// </summary>
        public async Task ReloadWenlaiDifficultyConfig()
        {
            try
            {
                // 遍历所有配置项，找到文来难度
                string itemKey = "文来难度";
                string itemValue = Config.GetString(itemKey) ?? "";
                string labelText = itemKey; // 使用配置项名称作为显示文本

                // 查找现有的文来难度控件
                var existingPanel = FindConfigItemControl(itemKey, labelText);
                if (existingPanel is StackPanel panel)
                {
                    System.Diagnostics.Debug.WriteLine($"[WinConfig] 找到文来难度控件，重新加载");

                    // 清空现有内容
                    panel.Children.Clear();

                    // 显示加载中状态
                    var loadingText = new TextBlock { Text = "加载中..." };
                    panel.Children.Add(loadingText);

                    // 异步加载难度数据
                    await LoadDifficultyDataAsync(panel, itemValue);
                }

                // 更新记录的登录状态为当前实际状态
                try
                {
                    var wenlaiHelper = new WenlaiHelper();
                    bool currentlyLoggedIn = wenlaiHelper.IsLoggedIn();
                    Tag = currentlyLoggedIn;
                    System.Diagnostics.Debug.WriteLine($"[WinConfig] 重新加载后更新登录状态: {currentlyLoggedIn}");
                }
                catch { }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WinConfig] 重新加载文来难度失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 查找配置项控件
        /// </summary>
        private Panel FindConfigItemControl(string itemKey, string labelText)
        {
            // 遍历设置面板的所有子元素
            var settingsPanel = FindName("panelSettings") as Panel;
            if (settingsPanel == null) return null;

            foreach (var child in settingsPanel.Children)
            {
                if (child is Grid grid)
                {
                    foreach (var gridChild in grid.Children)
                    {
                        if (gridChild is TextBlock tb && tb.Text == labelText)
                        {
                            // 找到对应的文本标签，获取其兄弟元素（值控件）
                            var column = Grid.GetColumn(tb);
                            foreach (UIElement sibling in grid.Children)
                            {
                                if (sibling is Panel panel && Grid.GetColumn(sibling) == 1)
                                {
                                    return panel;
                                }
                            }
                        }
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// 异步加载难度数据
        /// </summary>
        private async Task LoadDifficultyDataAsync(Panel container, string currentValue)
        {
            try
            {
                // 使用真正的异步方法获取难度数据
                var difficulties = await ArticleFetcher.GetDifficultiesAsync();

                // 构建难度统计字典
                var difficultyStats = new Dictionary<int, int>();
                foreach (var difficulty in difficulties)
                {
                    difficultyStats[difficulty.Id] = difficulty.Count;
                }

                // 回到UI线程更新界面
                await Dispatcher.InvokeAsync(() =>
                {
                    container.Children.Clear();

                    // 直接检查登录状态，而不是通过文章数判断
                    bool isLoggedIn = false;
                    try
                    {
                        var wenlaiHelper = new WenlaiHelper();
                        isLoggedIn = wenlaiHelper.IsLoggedIn();
                        System.Diagnostics.Debug.WriteLine($"[WinConfig] 文来登录状态: {isLoggedIn}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[WinConfig] 检查文来登录状态失败: {ex.Message}");
                    }

                    if (!isLoggedIn)
                    {
                        // 未登录，显示登录按钮
                        var loginBtn = new Button
                        {
                            Content = "文来登录",
                            Width = 150,
                            Height = 30,
                            HorizontalAlignment = HorizontalAlignment.Left,
                            Margin = new Thickness(0, 8, 0, 8)
                        };
                        loginBtn.Click += async (s, e) =>
                        {
                            // 创建 WenlaiHelper 并显示登录对话框
                            var wenlaiHelper = new WenlaiHelper();
                            bool? loginResult = wenlaiHelper.ShowLoginDialog(this);

                            // 只有登录成功后才刷新
                            if (loginResult == true)
                            {
                                // 同步cookies到ArticleFetcher，确保能获取到登录后的数据
                                try
                                {
                                    var accountManager = new AccountSystemManager();
                                    var account = accountManager.GetAccount("文来");
                                    if (account != null && !string.IsNullOrWhiteSpace(account.Cookies))
                                    {
                                        string serverUrl = Config.GetString("文来接口地址");
                                        ArticleFetcher.LoadCookiesFromString(serverUrl, account.Cookies);
                                        System.Diagnostics.Debug.WriteLine($"[WinConfig] 已同步文来cookies到ArticleFetcher");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[WinConfig] 同步cookies失败: {ex.Message}");
                                }

                                // 清除难度缓存，确保获取最新的难度数据
                                ArticleFetcher.ClearDifficultyCache();

                                // 登录后重新加载难度数据
                                await LoadDifficultyDataAsync(container, currentValue);

                                // 刷新主窗口的文来菜单状态
                                try
                                {
                                    var mainWindow = Application.Current.MainWindow as MainWindow;
                                    if (mainWindow != null)
                                    {
                                        _ = mainWindow.Dispatcher.BeginInvoke(new Action(() =>
                                        {
                                            var initMethod = mainWindow.GetType().GetMethod("InitializeWenlaiMenu",
                                                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                            initMethod?.Invoke(mainWindow, null);
                                        }), System.Windows.Threading.DispatcherPriority.Normal);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"刷新文来菜单失败: {ex.Message}");
                                }
                            }
                        };
                        container.Children.Add(loginBtn);
                    }
                    else
                    {
                        // 已登录，创建下拉框
                        var cb = new ComboBox
                        {
                            Width = 200,
                            Margin = new Thickness(0, 8, 0, 8),
                            HorizontalAlignment = HorizontalAlignment.Left
                        };

                        // 计算总文章数（用于显示）
                        int totalCount = difficultyStats.Values.Sum();

                        // 先添加"随机"选项
                        cb.Items.Add($"随机 ({totalCount}段)");

                        // Tag用于存储难度ID映射 (ComboBox索引 -> 难度ID)
                        var difficultyMapping = new Dictionary<int, int>();
                        difficultyMapping[0] = 0; // 索引0对应随机（ID为0）

                        // 按难度ID排序并添加
                        var sortedDifficulties = difficultyStats.OrderBy(kv => kv.Key);
                        int comboBoxIndex = 1;
                        foreach (var kvp in sortedDifficulties)
                        {
                            int difficultyId = kvp.Key;
                            int count = kvp.Value;

                            // 跳过文章数为0的难度
                            if (count == 0)
                                continue;

                            // 从难度列表获取难度名称
                            var diffInfo = difficulties.FirstOrDefault(d => d.Id == difficultyId);
                            string difficultyName = diffInfo?.Name ?? difficultyId.ToString();

                            cb.Items.Add($"{difficultyName} ({count}段)");
                            difficultyMapping[comboBoxIndex] = difficultyId;
                            comboBoxIndex++;
                        }

                        // 保存映射到Tag，用于保存配置时反查
                        cb.Tag = difficultyMapping;

                        // 设置当前选中项
                        int currentDifficultyId = 0;
                        if (!string.IsNullOrEmpty(currentValue))
                        {
                            int.TryParse(currentValue, out currentDifficultyId);
                        }

                        // 根据难度ID找到对应的ComboBox索引
                        cb.SelectedIndex = 0; // 默认选中随机
                        if (currentDifficultyId > 0)
                        {
                            var matchingIndex = difficultyMapping.FirstOrDefault(kv => kv.Value == currentDifficultyId).Key;
                            if (matchingIndex > 0)
                            {
                                cb.SelectedIndex = matchingIndex;
                            }
                        }

                        cb.SelectionChanged += (s, e) => SaveWenlaiDifficultySelection(cb);
                        container.Children.Add(cb);
                    }
                });
            }
            catch (Exception)
            {
                // 加载失败，显示错误信息
                await Dispatcher.InvokeAsync(() =>
                {
                    container.Children.Clear();
                    var errorText = new TextBlock
                    {
                        Text = "加载失败",
                        Foreground = new SolidColorBrush(Color.FromRgb(200, 50, 50)),
                        Margin = new Thickness(0, 8, 0, 8)
                    };
                    container.Children.Add(errorText);
                });
            }
        }

        /// <summary>
        /// 异步加载分类数据
        /// </summary>
        private async Task LoadCategoryDataAsync(Panel container, string currentValue)
        {
            try
            {
                var categories = await ArticleFetcher.GetCategoriesAsync();

                await Dispatcher.InvokeAsync(() =>
                {
                    container.Children.Clear();

                    var cb = new ComboBox
                    {
                        Width = 200,
                        Margin = new Thickness(0, 8, 0, 8),
                        HorizontalAlignment = HorizontalAlignment.Left
                    };

                    cb.Items.Add("全部");
                    var codeMapping = new Dictionary<int, string>();
                    codeMapping[0] = "";

                    int idx = 1;
                    foreach (var cat in categories)
                    {
                        cb.Items.Add(cat.Name);
                        codeMapping[idx] = cat.Code;
                        idx++;
                    }

                    cb.Tag = codeMapping;
                    cb.SelectedIndex = 0;
                    if (!string.IsNullOrEmpty(currentValue))
                    {
                        var match = codeMapping.FirstOrDefault(kv => kv.Value == currentValue);
                        if (match.Value != null && match.Key > 0)
                            cb.SelectedIndex = match.Key;
                    }

                    cb.SelectionChanged += (s, e) => SaveWenlaiCategorySelection(cb);
                    container.Children.Add(cb);
                });
            }
            catch (Exception)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    container.Children.Clear();
                    container.Children.Add(new TextBlock
                    {
                        Text = "加载失败",
                        Foreground = new SolidColorBrush(Color.FromRgb(200, 50, 50)),
                        Margin = new Thickness(0, 8, 0, 8)
                    });
                });
            }
        }

        private void SaveWenlaiDifficultySelection(ComboBox comboBox)
        {
            if (comboBox?.Tag is Dictionary<int, int> difficultyMapping &&
                difficultyMapping.ContainsKey(comboBox.SelectedIndex))
            {
                int difficultyId = difficultyMapping[comboBox.SelectedIndex];
                SaveConfigValue("文来难度", difficultyId == 0 ? "" : difficultyId.ToString());
            }
        }

        private void SaveWenlaiCategorySelection(ComboBox comboBox)
        {
            if (comboBox?.Tag is Dictionary<int, string> codeMapping &&
                codeMapping.ContainsKey(comboBox.SelectedIndex))
            {
                SaveConfigValue("文来分类", codeMapping[comboBox.SelectedIndex] ?? "");
            }
        }

        // 颜色配置项列表
        private static readonly string[] ColorConfigItems =
        {
            "窗体背景色", "窗体字体色",
            "跟打区背景色", "跟打区字体色",
            "发文区字体色",
            "打对色", "打错色",
            "按钮背景色", "按钮字体色",
            "菜单背景色", "菜单字体色",
            "词提1简色", "词提2简色",
            "词提3简色", "词提4码色",
            "词提选重色"
        };


        // 主题模式切换事件
        private void ThemeMode_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            ComboBox cb = sender as ComboBox;
            if (cb == null || cb.SelectedIndex < 0) return;

            string selectedTheme = cb.SelectedItem.ToString();

            // 先更新 Config 中的主题模式
            Config.Set("主题模式", selectedTheme);

            // 应用主题
            ThemeManager.ApplyCurrentTheme();

            // 通知主窗口刷新主题
            NotifyMainWindowThemeRefresh();

            // 通知所有打开的统计窗口和排行榜窗口刷新主题
            NotifyAllWindowsThemeRefresh();

            // 更新界面上的颜色按钮显示
            foreach (var item in ContentPanel.Children)
            {
                if (item is Button btn && btn.Tag != null)
                {
                    string colorKey = btn.Tag.ToString();
                    string colorHex = Config.GetString(colorKey);

                    try
                    {
                        var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#" + colorHex);
                        btn.Background = new SolidColorBrush(color);
                        btn.Content = colorHex;
                    }
                    catch { }
                }
            }

            // 实时更新设置窗口的颜色
            ApplyThemeColors();
        }

        /// <summary>
        /// 通知主窗口刷新主题
        /// </summary>
        private void NotifyMainWindowThemeRefresh()
        {
            try
            {
                var mainWindow = Application.Current.MainWindow as MainWindow;
                if (mainWindow != null)
                {
                    // 使用 Dispatcher 在主窗口线程上执行刷新
                    mainWindow.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        // 调用主窗口的 ReloadCfg 方法
                        var reloadMethod = mainWindow.GetType().GetMethod("ReloadCfg",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        reloadMethod?.Invoke(mainWindow, null);
                    }), System.Windows.Threading.DispatcherPriority.Normal);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"通知主窗口刷新主题失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 通知所有打开的统计窗口和排行榜窗口刷新主题
        /// </summary>
        private void NotifyAllWindowsThemeRefresh()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[WinConfig] NotifyAllWindowsThemeRefresh 开始执行");
                int statsCount = 0, trainerCount = 0;

                // 遍历所有打开的窗口
                foreach (Window window in Application.Current.Windows)
                {
                    // 如果是成绩统计窗口，调用其 RefreshTheme 方法
                    if (window is WinStatistics statsWindow)
                    {
                        statsCount++;
                        System.Diagnostics.Debug.WriteLine($"[WinConfig] 找到成绩统计窗口，调用 RefreshTheme");
                        statsWindow.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            statsWindow.RefreshTheme();
                        }), System.Windows.Threading.DispatcherPriority.Normal);
                    }
                    // 如果是练单器窗口，调用其 RefreshTheme 方法
                    else if (window is WinTrainer trainerWindow)
                    {
                        trainerCount++;
                        System.Diagnostics.Debug.WriteLine($"[WinConfig] 找到练单器窗口，调用 RefreshTheme");
                        trainerWindow.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            trainerWindow.RefreshTheme();
                        }), System.Windows.Threading.DispatcherPriority.Normal);
                    }
                    // 如果是文章管理器窗口，调用其 RefreshTheme 方法
                    else if (window is WinArticle articleWindow)
                    {
                        articleWindow.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            articleWindow.RefreshTheme();
                        }), System.Windows.Threading.DispatcherPriority.Normal);
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[WinConfig] 找到 {statsCount} 个成绩统计窗口, {trainerCount} 个练单器窗口");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"通知窗口刷新主题失败: {ex.Message}");
            }
        }

        // 新建主题事件
        private void NewTheme_Click(object sender, RoutedEventArgs e)
        {
            // 获取当前主题的所有颜色配置
            var currentColors = new ThemeManager.ThemeColors
            {
                WindowBackground = Config.GetString("窗体背景色"),
                WindowForeground = Config.GetString("窗体字体色"),
                DisplayBackground = Config.GetString("跟打区背景色"),
                DisplayForeground = Config.GetString("跟打区字体色"),
                ArticleForeground = Config.GetString("发文区字体色"),
                CorrectBackground = Config.GetString("打对色"),
                IncorrectBackground = Config.GetString("打错色"),
                ButtonBackground = Config.GetString("按钮背景色"),
                ButtonForeground = Config.GetString("按钮字体色"),
                MenuBackground = Config.GetString("菜单背景色"),
                MenuForeground = Config.GetString("菜单字体色"),
                ProgressBarColor = Config.GetString("标题栏进度条颜色")
            };

            // 查找下一个可用的自定义主题序号
            int nextIndex = 1;
            var themes = ThemeManager.GetAvailableThemes();
            while (themes.Contains($"自定义主题{nextIndex}"))
            {
                nextIndex++;
            }

            string newThemeName = $"自定义主题{nextIndex}";

            // 保存新主题
            ThemeManager.SaveTheme(newThemeName, currentColors);

            // 切换到新主题
            Config.Set("主题模式", newThemeName);

            // 重新加载当前分类
            ShowCategory(_currentCategoryIndex);
        }

        // 重命名主题事件
        private void RenameTheme_Click(object sender, RoutedEventArgs e)
        {
            string currentTheme = Config.GetString("主题模式");

            // 不能重命名内置主题
            if (ThemeManager.IsBuiltInTheme(currentTheme))
            {
                MessageBox.Show("内置主题（明、暗、pain）不能重命名。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 创建重命名对话框
            var window = new Window
            {
                Title = "重命名主题",
                Width = 350,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            var panel = new StackPanel { Margin = new Thickness(20) };

            var label = new TextBlock
            {
                Text = $"将主题 \"{currentTheme}\" 重命名为：",
                Margin = new Thickness(0, 0, 0, 10)
            };
            panel.Children.Add(label);

            var textBox = new TextBox
            {
                Margin = new Thickness(0, 0, 0, 15),
                Text = currentTheme
            };
            panel.Children.Add(textBox);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var okBtn = new Button
            {
                Content = "确定",
                Width = 70,
                Height = 28,
                Margin = new Thickness(0, 0, 10, 0)
            };
            var cancelBtn = new Button
            {
                Content = "取消",
                Width = 70,
                Height = 28
            };

            buttonPanel.Children.Add(okBtn);
            buttonPanel.Children.Add(cancelBtn);
            panel.Children.Add(buttonPanel);

            window.Content = panel;

            okBtn.Click += (s, args) =>
            {
                string newName = textBox.Text.Trim();

                if (string.IsNullOrEmpty(newName))
                {
                    MessageBox.Show("主题名称不能为空。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (newName == currentTheme)
                {
                    window.Close();
                    return;
                }

                // 检查新名称是否与内置主题冲突
                if (ThemeManager.IsBuiltInTheme(newName))
                {
                    MessageBox.Show("不能使用内置主题名称（明、暗、pain）。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 检查新名称是否已存在
                var existingThemes = ThemeManager.GetAvailableThemes();
                if (existingThemes.Contains(newName))
                {
                    MessageBox.Show($"主题 \"{newName}\" 已存在。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 重命名主题
                if (ThemeManager.RenameTheme(currentTheme, newName))
                {
                    // 如果当前正在使用这个主题，更新配置
                    if (Config.GetString("主题模式") == currentTheme)
                    {
                        Config.Set("主题模式", newName);
                    }

                    // 刷新主题列表
                    ShowCategory(_currentCategoryIndex);

                    window.Close();
                }
                else
                {
                    MessageBox.Show("重命名失败。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };

            cancelBtn.Click += (s, args) => window.Close();

            window.ShowDialog();
        }

        // ========== 过滤设置 ==========

        private System.Windows.Threading.DispatcherTimer _previewDebounceTimer;

        private void AppendFilterSettings(int startRow)
        {
            int row = startRow;
            Brush subtitleBrush = new SolidColorBrush(Color.FromRgb(100, 150, 200));
            Brush hintBrush = new SolidColorBrush(Color.FromRgb(150, 150, 150));

            // 从主题配置读取输入框颜色
            Brush inputBg, inputFg;
            try
            {
                string bgHex = Config.GetString("窗体背景色");
                string fgHex = Config.GetString("窗体字体色");
                var bgColor = (Color)ColorConverter.ConvertFromString("#" + bgHex);
                var fgColor = (Color)ColorConverter.ConvertFromString("#" + fgHex);
                // 输入框背景比窗体背景稍亮/暗一点，增加区分度
                byte offsetR = (byte)(bgColor.R > 128 ? Math.Max(0, bgColor.R - 15) : Math.Min(255, bgColor.R + 20));
                byte offsetG = (byte)(bgColor.G > 128 ? Math.Max(0, bgColor.G - 15) : Math.Min(255, bgColor.G + 20));
                byte offsetB = (byte)(bgColor.B > 128 ? Math.Max(0, bgColor.B - 15) : Math.Min(255, bgColor.B + 20));
                inputBg = new SolidColorBrush(Color.FromRgb(offsetR, offsetG, offsetB));
                inputFg = new SolidColorBrush(fgColor);
            }
            catch
            {
                inputBg = SystemColors.WindowBrush;
                inputFg = SystemColors.WindowTextBrush;
            }

            // --- 生效范围 ---
            var scopeRow = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };

            var scopeHeader = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            scopeHeader.Children.Add(new TextBlock
            {
                Text = "生效范围",
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 16, 0)
            });
            scopeHeader.Children.Add(new TextBlock
            {
                Text = "赛文始终不受过滤影响",
                FontSize = 12,
                Foreground = hintBrush,
                VerticalAlignment = VerticalAlignment.Center
            });
            scopeRow.Children.Add(scopeHeader);

            var scopeGrid = new UniformGrid { Rows = 1, Columns = 4 };
            scopeGrid.SizeChanged += (s, e) =>
            {
                double w = e.NewSize.Width;
                if (w >= 480) { scopeGrid.Columns = 4; scopeGrid.Rows = 1; }
                else if (w >= 240) { scopeGrid.Columns = 2; scopeGrid.Rows = 2; }
                else { scopeGrid.Columns = 1; scopeGrid.Rows = 4; }
            };
            string[] scopeNames = { "文来", "本地发文", "练单器", "剪贴板" };
            foreach (var name in scopeNames)
            {
                string configKey = "过滤_生效_" + name;

                var itemPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, 4, 0, 4),
                    VerticalAlignment = VerticalAlignment.Center
                };

                itemPanel.Children.Add(new TextBlock
                {
                    Text = name,
                    FontSize = 14,
                    Width = 60,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 8, 0)
                });

                var chk = new CheckBox
                {
                    IsChecked = Config.GetBool(configKey),
                    VerticalAlignment = VerticalAlignment.Center,
                    Style = FindResource("ModernToggleStyle") as Style
                };
                chk.Checked += (s, e) => Config.Set(configKey, "是");
                chk.Unchecked += (s, e) => Config.Set(configKey, "否");
                itemPanel.Children.Add(chk);

                scopeGrid.Children.Add(itemPanel);
            }
            scopeRow.Children.Add(scopeGrid);

            ContentPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(scopeRow, row);
            Grid.SetColumnSpan(scopeRow, 2);
            ContentPanel.Children.Add(scopeRow);
            row++;

            // --- 文来最大重试次数 ---
            var retryLabel = new TextBlock
            {
                Text = "文来最大重试次数",
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 10, 20, 10),
                VerticalAlignment = VerticalAlignment.Center
            };
            ContentPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto, MinHeight = 38 });
            Grid.SetRow(retryLabel, row);
            Grid.SetColumn(retryLabel, 0);
            ContentPanel.Children.Add(retryLabel);

            var retryPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 5) };
            var retryBox = new TextBox
            {
                Text = Config.GetString("过滤_文来最大重试"),
                Width = 60,
                VerticalAlignment = VerticalAlignment.Center,
                Background = inputBg,
                Foreground = inputFg
            };
            AttachTextBoxAutoSave(
                retryBox,
                "过滤_文来最大重试",
                canSave: text => int.TryParse(text, out int val) && val >= 1 && val <= 50);
            AddCategoryFallbackSave(() =>
            {
                if (int.TryParse(retryBox.Text, out int val) && val >= 1 && val <= 50)
                    SaveConfigValue("过滤_文来最大重试", retryBox.Text, scheduleRefresh: false);
            });
            retryPanel.Children.Add(retryBox);
            var retryHint = new TextBlock
            {
                Text = "  文来遇到被屏蔽的文章时，自动换下一篇的最大次数",
                FontSize = 12,
                Foreground = hintBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            retryPanel.Children.Add(retryHint);
            Grid.SetRow(retryPanel, row);
            Grid.SetColumn(retryPanel, 1);
            ContentPanel.Children.Add(retryPanel);
            row++;

            // --- 简单模式 ---
            row = AppendFilterSection(row, "关键词（普通用户推荐）", subtitleBrush, hintBrush, inputBg, inputFg,
                "屏蔽关键词", "过滤_黑名单关键词", "一行一个，包含该词的文章将被跳过",
                "替换关键词", "过滤_替换关键词", "查找", "替换为（留空则删除）",
                out TextBox bkKeywordBox, out TextBox rpKeywordBox);

            // --- 高级模式 ---
            row = AppendFilterSection(row, "正则表达式（进阶用户）", subtitleBrush, hintBrush, inputBg, inputFg,
                "屏蔽正则", "过滤_黑名单正则", "一行一条正则，匹配到的文章将被跳过",
                "替换正则", "过滤_替换正则", "正则表达式", "替换为（留空则删除）",
                out TextBox bkRegexBox, out TextBox rpRegexBox);

            // --- 配置参考说明 ---
            var helpSep = new Border
            {
                Height = 1,
                Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                Margin = new Thickness(0, 15, 0, 10)
            };
            ContentPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(helpSep, row);
            Grid.SetColumnSpan(helpSep, 2);
            ContentPanel.Children.Add(helpSep);
            row++;

            var helpTitlePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            helpTitlePanel.Children.Add(new Border
            {
                Width = 3,
                Background = subtitleBrush,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Stretch
            });
            helpTitlePanel.Children.Add(new TextBlock
            {
                Text = "配置参考",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = subtitleBrush
            });
            ContentPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(helpTitlePanel, row);
            Grid.SetColumnSpan(helpTitlePanel, 2);
            ContentPanel.Children.Add(helpTitlePanel);
            row++;

            var helpText = new TextBlock
            {
                Text = "【关键词】一行一个。屏蔽：包含该词的文章会被跳过。替换：旧词=>新词，省略=>新词则删除。\n" +
                       "  例：（求全订）         → 删除所有（求全订）\n" +
                       "  例：旧词=>新词         → 把旧词替换成新词\n\n" +
                       "【正则】一行一条。屏蔽：匹配到的文章会被跳过。替换：正则=>替换内容，省略则删除匹配项。\n" +
                       "  例：。{7,}             → 匹配连续7个以上句号\n" +
                       "  例：\\d{3,}            → 匹配3位以上数字\n" +
                       "  例：[a-zA-Z]+          → 匹配英文单词",
                FontSize = 12,
                Foreground = hintBrush,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(12, 0, 0, 15)
            };
            ContentPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(helpText, row);
            Grid.SetColumnSpan(helpText, 2);
            ContentPanel.Children.Add(helpText);
            row++;

            // --- 效果预览 ---
            var previewSep = new Border
            {
                Height = 1,
                Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                Margin = new Thickness(0, 15, 0, 10)
            };
            ContentPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(previewSep, row);
            Grid.SetColumnSpan(previewSep, 2);
            ContentPanel.Children.Add(previewSep);
            row++;

            var previewTitlePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            previewTitlePanel.Children.Add(new Border
            {
                Width = 3,
                Background = subtitleBrush,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Stretch
            });
            previewTitlePanel.Children.Add(new TextBlock
            {
                Text = "效果预览",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = subtitleBrush
            });
            ContentPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(previewTitlePanel, row);
            Grid.SetColumnSpan(previewTitlePanel, 2);
            ContentPanel.Children.Add(previewTitlePanel);
            row++;

            var previewInput = new TextBox
            {
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                MinHeight = 60,
                MaxHeight = 120,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(12, 5, 0, 5),
                Background = inputBg,
                Foreground = inputFg
            };
            SetPlaceholder(previewInput, "粘贴测试文本...");
            ContentPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(previewInput, row);
            Grid.SetColumnSpan(previewInput, 2);
            ContentPanel.Children.Add(previewInput);
            row++;

            var previewResult = new RichTextBox
            {
                IsReadOnly = true,
                MinHeight = 60,
                MaxHeight = 200,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(12, 5, 0, 15),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                Background = inputBg,
                Foreground = inputFg
            };
            ContentPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(previewResult, row);
            Grid.SetColumnSpan(previewResult, 2);
            ContentPanel.Children.Add(previewResult);
            row++;

            // 防抖定时器
            _previewDebounceTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _previewDebounceTimer.Tick += (s, e) =>
            {
                _previewDebounceTimer.Stop();
                UpdateFilterPreview(previewInput, previewResult, bkKeywordBox, rpKeywordBox, bkRegexBox, rpRegexBox);
            };

            previewInput.TextChanged += (s, e) =>
            {
                _previewDebounceTimer.Stop();
                _previewDebounceTimer.Start();
            };
        }

        private int AppendFilterSection(int row, string title, Brush titleBrush, Brush hintBrush, Brush inputBg, Brush inputFg,
            string label1, string configKey1, string placeholder1,
            string label2, string configKey2, string leftHeader, string rightHeader,
            out TextBox box1, out TextBox box2)
        {
            // 分隔线
            var separator = new Border
            {
                Height = 1,
                Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                Margin = new Thickness(0, 15, 0, 10)
            };
            ContentPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(separator, row);
            Grid.SetColumnSpan(separator, 2);
            ContentPanel.Children.Add(separator);
            row++;

            // 区域标题（带左侧色条）
            var titlePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            titlePanel.Children.Add(new Border
            {
                Width = 3,
                Background = titleBrush,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Stretch
            });
            titlePanel.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = titleBrush
            });
            ContentPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(titlePanel, row);
            Grid.SetColumnSpan(titlePanel, 2);
            ContentPanel.Children.Add(titlePanel);
            row++;

            // 整个内容区域缩进
            var contentWrapper = new StackPanel { Margin = new Thickness(12, 0, 0, 0) };

            // Box 1: 黑名单（单框）
            contentWrapper.Children.Add(new TextBlock { Text = label1, FontSize = 14, Margin = new Thickness(0, 5, 0, 2) });
            contentWrapper.Children.Add(new TextBlock
            {
                Text = placeholder1,
                FontSize = 12,
                Foreground = hintBrush,
                Margin = new Thickness(0, 0, 0, 4),
                TextWrapping = TextWrapping.Wrap
            });

            box1 = new TextBox
            {
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                MinHeight = 80,
                MaxHeight = 200,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Text = RegexFilter.DecodeMultiline(Config.GetString(configKey1)),
                Margin = new Thickness(0, 0, 0, 12),
                Background = inputBg,
                Foreground = inputFg
            };
            var capturedKey1 = configKey1;
            var capturedBox1 = box1;
            box1.TextChanged += (s, e) =>
            {
                RestartPreviewTimer();
            };
            AttachTextBoxAutoSave(
                capturedBox1,
                capturedKey1,
                text => RegexFilter.EncodeMultiline(text),
                canSave: null);
            AddCategoryFallbackSave(() =>
                SaveConfigValue(capturedKey1, RegexFilter.EncodeMultiline(capturedBox1.Text), scheduleRefresh: false));
            contentWrapper.Children.Add(box1);

            // Box 2: 替换（左右双框）
            contentWrapper.Children.Add(new TextBlock { Text = label2, FontSize = 14, Margin = new Thickness(0, 5, 0, 2) });
            contentWrapper.Children.Add(new TextBlock
            {
                Text = "左边填要查找的内容，右边填替换成什么（留空则删除），行数一一对应",
                FontSize = 12,
                Foreground = hintBrush,
                Margin = new Thickness(0, 0, 0, 4),
                TextWrapping = TextWrapping.Wrap
            });

            // 列头
            var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 2) };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var leftLabel = new TextBlock { Text = leftHeader, FontSize = 12, Foreground = hintBrush };
            var rightLabel = new TextBlock { Text = rightHeader, FontSize = 12, Foreground = hintBrush };
            Grid.SetColumn(leftLabel, 0);
            Grid.SetColumn(rightLabel, 2);
            headerGrid.Children.Add(leftLabel);
            headerGrid.Children.Add(rightLabel);
            contentWrapper.Children.Add(headerGrid);

            // 双框容器
            var dualGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            dualGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            dualGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            dualGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var leftBox = new TextBox
            {
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                MinHeight = 80,
                MaxHeight = 200,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Background = inputBg,
                Foreground = inputFg
            };
            var rightBox = new TextBox
            {
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                MinHeight = 80,
                MaxHeight = 200,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Background = inputBg,
                Foreground = inputFg
            };

            // 从 config 解析 => 格式，拆成左右
            LoadDualBoxes(configKey2, leftBox, rightBox);
            leftBox.Tag = rightBox;

            var capturedKey2 = configKey2;
            bool updating = false;
            TextChangedEventHandler syncHandler = (s, e) =>
            {
                if (updating) return;
                updating = true;
                RestartPreviewTimer();
                updating = false;
            };
            leftBox.TextChanged += syncHandler;
            rightBox.TextChanged += syncHandler;
            RoutedEventHandler saveDualHandler = (s, e) => SaveDualBoxes(capturedKey2, leftBox, rightBox);
            leftBox.LostFocus += saveDualHandler;
            rightBox.LostFocus += saveDualHandler;
            AddCategoryFallbackSave(() => SaveDualBoxes(capturedKey2, leftBox, rightBox));

            Grid.SetColumn(leftBox, 0);
            Grid.SetColumn(rightBox, 2);
            dualGrid.Children.Add(leftBox);
            dualGrid.Children.Add(rightBox);
            contentWrapper.Children.Add(dualGrid);

            // box2 指向 leftBox，用于预览时读取
            box2 = leftBox;

            ContentPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(contentWrapper, row);
            Grid.SetColumnSpan(contentWrapper, 2);
            ContentPanel.Children.Add(contentWrapper);
            row++;

            return row;
        }

        private void LoadDualBoxes(string configKey, TextBox leftBox, TextBox rightBox)
        {
            string decoded = RegexFilter.DecodeMultiline(Config.GetString(configKey));
            if (string.IsNullOrEmpty(decoded)) return;

            var leftLines = new List<string>();
            var rightLines = new List<string>();
            foreach (var line in decoded.Split('\n'))
            {
                int idx = line.IndexOf("=>", StringComparison.Ordinal);
                if (idx >= 0)
                {
                    leftLines.Add(line.Substring(0, idx));
                    rightLines.Add(line.Substring(idx + 2));
                }
                else
                {
                    leftLines.Add(line);
                    rightLines.Add("");
                }
            }
            leftBox.Text = string.Join("\n", leftLines);
            rightBox.Text = string.Join("\n", rightLines);
        }

        private void SaveDualBoxes(string configKey, TextBox leftBox, TextBox rightBox)
        {
            SaveConfigValue(configKey, RegexFilter.EncodeMultiline(BuildDualBoxesText(leftBox, rightBox)), scheduleRefresh: false);
        }

        private string BuildDualBoxesText(TextBox leftBox, TextBox rightBox)
        {
            var leftLines = leftBox.Text.Split('\n');
            var rightLines = rightBox.Text.Split('\n');
            int count = Math.Max(leftLines.Length, rightLines.Length);
            var result = new List<string>();
            for (int i = 0; i < count; i++)
            {
                string left = i < leftLines.Length ? leftLines[i].TrimEnd('\r') : "";
                string right = i < rightLines.Length ? rightLines[i].TrimEnd('\r') : "";
                if (string.IsNullOrEmpty(left) && string.IsNullOrEmpty(right)) continue;
                if (string.IsNullOrEmpty(right))
                    result.Add(left);
                else
                    result.Add(left + "=>" + right);
            }
            return string.Join("\n", result);
        }

        private void RestartPreviewTimer()
        {
            if (_previewDebounceTimer != null)
            {
                _previewDebounceTimer.Stop();
                _previewDebounceTimer.Start();
            }
        }

        private void SetPlaceholder(TextBox textBox, string placeholder)
        {
            Brush normalBrush = textBox.Foreground;
            Brush placeholderBrush = new SolidColorBrush(Color.FromRgb(140, 140, 140));

            if (string.IsNullOrEmpty(textBox.Text))
            {
                textBox.Text = placeholder;
                textBox.Foreground = placeholderBrush;
                textBox.Tag = true;
            }
            textBox.GotFocus += (s, e) =>
            {
                if (textBox.Tag is true)
                {
                    textBox.Text = "";
                    textBox.ClearValue(TextBox.ForegroundProperty);
                    textBox.Tag = false;
                }
            };
            textBox.LostFocus += (s, e) =>
            {
                if (string.IsNullOrEmpty(textBox.Text))
                {
                    textBox.Text = placeholder;
                    textBox.Foreground = placeholderBrush;
                    textBox.Tag = true;
                }
            };
        }

        private void UpdateFilterPreview(TextBox input, RichTextBox output,
            TextBox bkKw, TextBox rpKw, TextBox bkRx, TextBox rpRx)
        {
            string text = (input.Tag is true) ? "" : input.Text;
            if (string.IsNullOrEmpty(text))
            {
                output.Document.Blocks.Clear();
                output.Document.Blocks.Add(new Paragraph(new Run("在上方输入文本查看过滤效果")
                {
                    Foreground = new SolidColorBrush(Color.FromRgb(140, 140, 140))
                }));
                return;
            }

            string bkKwText = (bkKw.Tag is true) ? "" : bkKw.Text;
            string bkRxText = (bkRx.Tag is true) ? "" : bkRx.Text;
            string rpKwText = GetDualBoxesPreviewText(rpKw, "过滤_替换关键词");
            string rpRxText = GetDualBoxesPreviewText(rpRx, "过滤_替换正则");

            var result = RegexFilter.Preview(text, bkKwText, rpKwText, bkRxText, rpRxText);

            output.Document.Blocks.Clear();

            if (result.IsBlocked)
            {
                var para = new Paragraph();
                para.Inlines.Add(new Run("  该文本命中屏蔽规则：" + result.BlockReason)
                {
                    Foreground = new SolidColorBrush(System.Windows.Media.Colors.White),
                    Background = new SolidColorBrush(Color.FromRgb(220, 50, 50))
                });
                output.Document.Blocks.Add(para);

                if (result.Diffs.Count > 0)
                {
                    var diffPara = new Paragraph();
                    diffPara.Inlines.Add(new Run("\n替换结果（屏蔽前）：\n") { FontSize = 11 });
                    diffPara.Inlines.Add(new Run(result.Text));
                    output.Document.Blocks.Add(diffPara);
                }
            }
            else if (result.Diffs.Count > 0)
            {
                var para = new Paragraph();
                para.Inlines.Add(new Run("替换前：") { FontWeight = FontWeights.Bold, FontSize = 11 });
                para.Inlines.Add(new Run("\n" + text + "\n\n"));
                para.Inlines.Add(new Run("替换后：") { FontWeight = FontWeights.Bold, FontSize = 11 });
                para.Inlines.Add(new Run("\n" + result.Text + "\n\n"));
                para.Inlines.Add(new Run($"共 {result.Diffs.Count} 处替换") { FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(100, 180, 100)) });
                output.Document.Blocks.Add(para);
            }
            else
            {
                var para = new Paragraph(new Run("  无变化，文本未命中任何过滤规则")
                {
                    Foreground = new SolidColorBrush(Color.FromRgb(100, 180, 100))
                });
                output.Document.Blocks.Add(para);
            }
        }

        private string GetDualBoxesPreviewText(TextBox leftBox, string configKey)
        {
            if (leftBox != null && leftBox.Tag is TextBox rightBox)
                return BuildDualBoxesText(leftBox, rightBox);

            return RegexFilter.DecodeMultiline(Config.GetString(configKey));
        }

        // 强制显示的成绩项（勾选不可取消，但可参与排序）
        private static readonly HashSet<string> ForceShowItems = new HashSet<string> { "速度", "击键", "字数", "键准" };

        /// <summary>
        /// 在成绩分类页面内嵌成绩显示项拖拽排序列表
        /// </summary>
        private void AppendScoreItemsList(int startRow)
        {
            // 小标题
            var optTitle = new TextBlock
            {
                Text = "成绩显示项（拖拽调整顺序，勾选控制显隐）",
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 150, 200)),
                Margin = new Thickness(0, 10, 0, 5)
            };
            ContentPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(optTitle, startRow);
            Grid.SetColumnSpan(optTitle, 2);
            ContentPanel.Children.Add(optTitle);

            // 读取当前顺序
            var currentOrder = Core.Score.GetScoreOrder();

            // 单列可拖拽 ListBox
            var listBox = new ListBox
            {
                MinHeight = 200,
                MaxHeight = 500,
                AllowDrop = true,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 5, 0, 0)
            };

            var checkboxes = new Dictionary<string, CheckBox>();

            foreach (var item in currentOrder)
            {
                bool isForced = ForceShowItems.Contains(item);

                var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(2) };
                var dragHandle = new TextBlock
                {
                    Text = "☰",
                    FontSize = 14,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 8, 0),
                    Cursor = System.Windows.Input.Cursors.SizeAll
                };
                var chk = new CheckBox
                {
                    Content = item == "重打" ? "重打/首打" : item,
                    IsChecked = isForced || Config.GetBool("显示_" + item),
                    IsEnabled = !isForced,  // 强制项不可取消勾选
                    VerticalAlignment = VerticalAlignment.Center
                };
                checkboxes[item] = chk;
                sp.Children.Add(dragHandle);
                sp.Children.Add(chk);

                var lbi = new ListBoxItem
                {
                    Content = sp,
                    Tag = item,
                    AllowDrop = true,
                    Padding = new Thickness(4, 2, 4, 2)
                };
                listBox.Items.Add(lbi);
            }

            // 保存当前配置的 lambda
            Action saveConfig = () =>
            {
                var orderItems = new List<string>();
                foreach (ListBoxItem lbi in listBox.Items)
                {
                    string itemName = lbi.Tag as string;
                    if (itemName != null && checkboxes.ContainsKey(itemName))
                    {
                        bool isForced = ForceShowItems.Contains(itemName);
                        Config.Set("显示_" + itemName, isForced || checkboxes[itemName].IsChecked == true);
                        orderItems.Add(itemName);
                    }
                }
                Config.Set("成绩显示顺序", string.Join(",", orderItems));
            };

            // CheckBox 勾选变化时自动保存
            foreach (var chk in checkboxes.Values)
            {
                chk.Checked += (s, e) => saveConfig();
                chk.Unchecked += (s, e) => saveConfig();
            }

            // 拖拽排序逻辑（带浮动效果和插入线提示）
            ListBoxItem draggedItem = null;
            Point dragStartPoint = default;
            DragAdorner currentAdorner = null;
            InsertionLineAdorner insertionAdorner = null;

            listBox.PreviewMouseLeftButtonDown += (s, e) =>
            {
                dragStartPoint = e.GetPosition(listBox);
                var hitItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
                if (hitItem != null)
                    draggedItem = hitItem;
            };

            listBox.PreviewMouseMove += (s, e) =>
            {
                if (draggedItem == null || e.LeftButton != System.Windows.Input.MouseButtonState.Pressed)
                    return;
                Point pos = e.GetPosition(listBox);
                if (Math.Abs(pos.X - dragStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(pos.Y - dragStartPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    // 创建浮动装饰层
                    var adornerLayer = AdornerLayer.GetAdornerLayer(listBox);
                    if (adornerLayer != null)
                    {
                        currentAdorner = new DragAdorner(listBox, draggedItem, 0.6);
                        adornerLayer.Add(currentAdorner);
                    }

                    draggedItem.Opacity = 0.3;
                    DragDrop.DoDragDrop(listBox, draggedItem, DragDropEffects.Move);

                    // 清理
                    draggedItem.Opacity = 1.0;
                    if (currentAdorner != null && adornerLayer != null)
                    {
                        adornerLayer.Remove(currentAdorner);
                        currentAdorner = null;
                    }
                    if (insertionAdorner != null && adornerLayer != null)
                    {
                        adornerLayer.Remove(insertionAdorner);
                        insertionAdorner = null;
                    }
                    draggedItem = null;
                }
            };

            listBox.DragOver += (s, e) =>
            {
                e.Effects = DragDropEffects.Move;
                e.Handled = true;

                // 更新浮动装饰层位置
                if (currentAdorner != null)
                {
                    currentAdorner.UpdatePosition(e.GetPosition(listBox));
                }

                // 更新插入线位置
                var adornerLayer = AdornerLayer.GetAdornerLayer(listBox);
                if (adornerLayer != null)
                {
                    if (insertionAdorner != null)
                    {
                        adornerLayer.Remove(insertionAdorner);
                        insertionAdorner = null;
                    }

                    var targetItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
                    if (targetItem != null)
                    {
                        Point posInTarget = e.GetPosition(targetItem);
                        bool insertBefore = posInTarget.Y < targetItem.ActualHeight / 2;
                        insertionAdorner = new InsertionLineAdorner(listBox, targetItem, insertBefore);
                        adornerLayer.Add(insertionAdorner);
                    }
                }
            };

            listBox.DragLeave += (s, e) =>
            {
                var adornerLayer = AdornerLayer.GetAdornerLayer(listBox);
                if (insertionAdorner != null && adornerLayer != null)
                {
                    adornerLayer.Remove(insertionAdorner);
                    insertionAdorner = null;
                }
            };

            listBox.Drop += (s, e) =>
            {
                var source = e.Data.GetData(typeof(ListBoxItem)) as ListBoxItem;
                if (source == null) return;

                var targetItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
                if (targetItem == null || targetItem == source) return;

                int sourceIndex = listBox.Items.IndexOf(source);
                int targetIndex = listBox.Items.IndexOf(targetItem);
                if (sourceIndex < 0 || targetIndex < 0) return;

                // 根据鼠标在目标项的上半/下半决定插入位置
                Point posInTarget = e.GetPosition(targetItem);
                bool insertBefore = posInTarget.Y < targetItem.ActualHeight / 2;

                listBox.Items.RemoveAt(sourceIndex);
                int finalIndex = listBox.Items.IndexOf(targetItem);
                if (!insertBefore) finalIndex++;
                listBox.Items.Insert(finalIndex, source);

                // 拖拽完成后自动保存顺序
                saveConfig();
            };

            ContentPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(listBox, startRow + 1);
            Grid.SetColumnSpan(listBox, 2);
            ContentPanel.Children.Add(listBox);
        }

        /// <summary>
        /// 在预测分类页面内嵌预测显示项拖拽排序列表
        /// </summary>
        private void AppendPredictionItemsList(int startRow)
        {
            var optTitle = new TextBlock
            {
                Text = "预测显示项（拖拽调整顺序，勾选控制显隐；速度必开，其他项目可选）",
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 150, 200)),
                Margin = new Thickness(0, 10, 0, 5)
            };
            ContentPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(optTitle, startRow);
            Grid.SetColumnSpan(optTitle, 2);
            ContentPanel.Children.Add(optTitle);

            var currentOrder = PersonalScorePredictionFormatter.NormalizeOrder(Config.GetString("预测显示顺序"));
            var listBox = new ListBox
            {
                MinHeight = 160,
                MaxHeight = 360,
                AllowDrop = true,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 5, 0, 0)
            };

            var checkboxes = new Dictionary<string, CheckBox>();
            foreach (string item in currentOrder)
            {
                bool isForced = PersonalScorePredictionFormatter.IsForceShowItem(item);

                var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(2) };
                var dragHandle = new TextBlock
                {
                    Text = "☰",
                    FontSize = 14,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 8, 0),
                    Cursor = System.Windows.Input.Cursors.SizeAll
                };
                var chk = new CheckBox
                {
                    Content = isForced ? item + "（必开）" : item,
                    IsChecked = isForced || Config.GetBool("预测显示_" + item),
                    IsEnabled = !isForced,
                    VerticalAlignment = VerticalAlignment.Center
                };
                checkboxes[item] = chk;
                sp.Children.Add(dragHandle);
                sp.Children.Add(chk);

                var lbi = new ListBoxItem
                {
                    Content = sp,
                    Tag = item,
                    AllowDrop = true,
                    Padding = new Thickness(4, 2, 4, 2)
                };
                listBox.Items.Add(lbi);
            }

            Action saveConfig = () =>
            {
                var orderItems = new List<string>();
                foreach (ListBoxItem lbi in listBox.Items)
                {
                    string itemName = lbi.Tag as string;
                    if (itemName != null && checkboxes.ContainsKey(itemName))
                    {
                        bool isForced = PersonalScorePredictionFormatter.IsForceShowItem(itemName);
                        Config.Set("预测显示_" + itemName, isForced || checkboxes[itemName].IsChecked == true);
                        orderItems.Add(itemName);
                    }
                }
                Config.Set("预测显示顺序", string.Join(",", orderItems));
                ScheduleConfigSavedRefresh();
            };

            foreach (var chk in checkboxes.Values)
            {
                chk.Checked += (s, e) => saveConfig();
                chk.Unchecked += (s, e) => saveConfig();
            }

            ListBoxItem draggedItem = null;
            Point dragStartPoint = default;
            DragAdorner currentAdorner = null;
            InsertionLineAdorner insertionAdorner = null;

            listBox.PreviewMouseLeftButtonDown += (s, e) =>
            {
                dragStartPoint = e.GetPosition(listBox);
                var hitItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
                if (hitItem != null)
                    draggedItem = hitItem;
            };

            listBox.PreviewMouseMove += (s, e) =>
            {
                if (draggedItem == null || e.LeftButton != System.Windows.Input.MouseButtonState.Pressed)
                    return;

                Point pos = e.GetPosition(listBox);
                if (Math.Abs(pos.X - dragStartPoint.X) <= SystemParameters.MinimumHorizontalDragDistance &&
                    Math.Abs(pos.Y - dragStartPoint.Y) <= SystemParameters.MinimumVerticalDragDistance)
                    return;

                var adornerLayer = AdornerLayer.GetAdornerLayer(listBox);
                if (adornerLayer != null)
                {
                    currentAdorner = new DragAdorner(listBox, draggedItem, 0.6);
                    adornerLayer.Add(currentAdorner);
                }

                draggedItem.Opacity = 0.3;
                DragDrop.DoDragDrop(listBox, draggedItem, DragDropEffects.Move);

                draggedItem.Opacity = 1.0;
                if (currentAdorner != null && adornerLayer != null)
                    adornerLayer.Remove(currentAdorner);
                if (insertionAdorner != null && adornerLayer != null)
                    adornerLayer.Remove(insertionAdorner);

                currentAdorner = null;
                insertionAdorner = null;
                draggedItem = null;
            };

            listBox.DragOver += (s, e) =>
            {
                e.Effects = DragDropEffects.Move;
                e.Handled = true;

                if (currentAdorner != null)
                    currentAdorner.UpdatePosition(e.GetPosition(listBox));

                var adornerLayer = AdornerLayer.GetAdornerLayer(listBox);
                if (adornerLayer == null)
                    return;

                if (insertionAdorner != null)
                {
                    adornerLayer.Remove(insertionAdorner);
                    insertionAdorner = null;
                }

                var targetItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
                if (targetItem != null)
                {
                    Point posInTarget = e.GetPosition(targetItem);
                    bool insertBefore = posInTarget.Y < targetItem.ActualHeight / 2;
                    insertionAdorner = new InsertionLineAdorner(listBox, targetItem, insertBefore);
                    adornerLayer.Add(insertionAdorner);
                }
            };

            listBox.DragLeave += (s, e) =>
            {
                var adornerLayer = AdornerLayer.GetAdornerLayer(listBox);
                if (insertionAdorner != null && adornerLayer != null)
                {
                    adornerLayer.Remove(insertionAdorner);
                    insertionAdorner = null;
                }
            };

            listBox.Drop += (s, e) =>
            {
                var source = e.Data.GetData(typeof(ListBoxItem)) as ListBoxItem;
                if (source == null) return;

                var targetItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
                if (targetItem == null || targetItem == source) return;

                int sourceIndex = listBox.Items.IndexOf(source);
                if (sourceIndex < 0) return;

                Point posInTarget = e.GetPosition(targetItem);
                bool insertBefore = posInTarget.Y < targetItem.ActualHeight / 2;

                listBox.Items.RemoveAt(sourceIndex);
                int finalIndex = listBox.Items.IndexOf(targetItem);
                if (!insertBefore) finalIndex++;
                listBox.Items.Insert(finalIndex, source);

                saveConfig();
            };

            ContentPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(listBox, startRow + 1);
            Grid.SetColumnSpan(listBox, 2);
            ContentPanel.Children.Add(listBox);
        }

        /// <summary>
        /// 在首页分类页面内嵌首页功能按钮拖拽排序列表。
        /// </summary>
        private void AppendHomeToolbarSettings(int startRow)
        {
            var optTitle = new TextBlock
            {
                Text = "首页按纽显示",
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 150, 200)),
                Margin = new Thickness(0, 10, 0, 5)
            };
            ContentPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(optTitle, startRow);
            Grid.SetColumnSpan(optTitle, 2);
            ContentPanel.Children.Add(optTitle);

            var visibility = HomeToolbarSettings.FeatureEntries.ToDictionary(
                entry => entry.VisibilityConfigKey,
                entry => Config.GetBool(entry.VisibilityConfigKey));
            var currentOrder = HomeToolbarSettings.GetFeatureEntries(
                Config.GetString(HomeToolbarSettings.FeatureOrderConfigKey));

            var listBox = new ListBox
            {
                MinHeight = 150,
                MaxHeight = 260,
                AllowDrop = true,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 5, 0, 0)
            };

            var checkboxes = new Dictionary<string, CheckBox>();

            foreach (var entry in currentOrder)
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(2) };
                row.Children.Add(new TextBlock
                {
                    Text = "☰",
                    FontSize = 14,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 8, 0),
                    Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 120))
                });

                var chk = new CheckBox
                {
                    IsChecked = !visibility.ContainsKey(entry.VisibilityConfigKey) || visibility[entry.VisibilityConfigKey],
                    Style = FindResource("ModernToggleStyle") as Style,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 4, 8, 4)
                };
                checkboxes[entry.Key] = chk;
                row.Children.Add(chk);
                var label = new TextBlock
                {
                    Text = entry.DisplayName,
                    FontSize = 13,
                    VerticalAlignment = VerticalAlignment.Center
                };
                row.Children.Add(label);

                var item = new ListBoxItem
                {
                    Content = row,
                    Tag = entry.Key,
                    AllowDrop = true,
                    Padding = new Thickness(4, 2, 4, 2)
                };
                listBox.Items.Add(item);
            }

            Action saveConfig = () =>
            {
                var orderedNames = new List<string>();
                foreach (ListBoxItem item in listBox.Items)
                {
                    var entry = HomeToolbarSettings.FindFeatureEntry(item.Tag as string);
                    if (entry == null)
                        continue;

                    orderedNames.Add(entry.DisplayName);
                    if (checkboxes.ContainsKey(entry.Key))
                        Config.Set(entry.VisibilityConfigKey, checkboxes[entry.Key].IsChecked == true);
                }

                Config.Set(HomeToolbarSettings.FeatureOrderConfigKey, string.Join(",", orderedNames));
                RefreshMainWindowHomeToolbar();
            };

            foreach (var chk in checkboxes.Values)
            {
                chk.Checked += (s, e) => saveConfig();
                chk.Unchecked += (s, e) => saveConfig();
            }

            ListBoxItem draggedItem = null;
            Point dragStartPoint = default;
            DragAdorner currentAdorner = null;
            InsertionLineAdorner insertionAdorner = null;

            listBox.PreviewMouseLeftButtonDown += (s, e) =>
            {
                if (FindAncestor<CheckBox>((DependencyObject)e.OriginalSource) != null)
                {
                    draggedItem = null;
                    return;
                }

                dragStartPoint = e.GetPosition(listBox);
                draggedItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
            };

            listBox.PreviewMouseMove += (s, e) =>
            {
                if (draggedItem == null || e.LeftButton != MouseButtonState.Pressed)
                    return;

                Point pos = e.GetPosition(listBox);
                if (Math.Abs(pos.X - dragStartPoint.X) <= SystemParameters.MinimumHorizontalDragDistance &&
                    Math.Abs(pos.Y - dragStartPoint.Y) <= SystemParameters.MinimumVerticalDragDistance)
                    return;

                var adornerLayer = AdornerLayer.GetAdornerLayer(listBox);
                if (adornerLayer != null)
                {
                    currentAdorner = new DragAdorner(listBox, draggedItem, 0.6);
                    adornerLayer.Add(currentAdorner);
                }

                draggedItem.Opacity = 0.3;
                DragDrop.DoDragDrop(listBox, draggedItem, DragDropEffects.Move);

                draggedItem.Opacity = 1.0;
                if (currentAdorner != null && adornerLayer != null)
                {
                    adornerLayer.Remove(currentAdorner);
                    currentAdorner = null;
                }
                if (insertionAdorner != null && adornerLayer != null)
                {
                    adornerLayer.Remove(insertionAdorner);
                    insertionAdorner = null;
                }
                draggedItem = null;
            };

            listBox.DragOver += (s, e) =>
            {
                e.Effects = DragDropEffects.Move;
                e.Handled = true;

                if (currentAdorner != null)
                    currentAdorner.UpdatePosition(e.GetPosition(listBox));

                var adornerLayer = AdornerLayer.GetAdornerLayer(listBox);
                if (adornerLayer == null)
                    return;

                if (insertionAdorner != null)
                {
                    adornerLayer.Remove(insertionAdorner);
                    insertionAdorner = null;
                }

                var targetItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
                if (targetItem != null)
                {
                    Point posInTarget = e.GetPosition(targetItem);
                    bool insertBefore = posInTarget.Y < targetItem.ActualHeight / 2;
                    insertionAdorner = new InsertionLineAdorner(listBox, targetItem, insertBefore);
                    adornerLayer.Add(insertionAdorner);
                }
            };

            listBox.DragLeave += (s, e) =>
            {
                var adornerLayer = AdornerLayer.GetAdornerLayer(listBox);
                if (insertionAdorner != null && adornerLayer != null)
                {
                    adornerLayer.Remove(insertionAdorner);
                    insertionAdorner = null;
                }
            };

            listBox.Drop += (s, e) =>
            {
                var adornerLayer = AdornerLayer.GetAdornerLayer(listBox);
                if (insertionAdorner != null && adornerLayer != null)
                {
                    adornerLayer.Remove(insertionAdorner);
                    insertionAdorner = null;
                }

                var source = e.Data.GetData(typeof(ListBoxItem)) as ListBoxItem;
                if (source == null) return;

                var targetItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
                if (targetItem == null || targetItem == source) return;

                int sourceIndex = listBox.Items.IndexOf(source);
                int targetIndex = listBox.Items.IndexOf(targetItem);
                if (sourceIndex < 0 || targetIndex < 0) return;

                Point posInTarget = e.GetPosition(targetItem);
                bool insertBefore = posInTarget.Y < targetItem.ActualHeight / 2;

                listBox.Items.RemoveAt(sourceIndex);
                int finalIndex = listBox.Items.IndexOf(targetItem);
                if (!insertBefore) finalIndex++;
                listBox.Items.Insert(finalIndex, source);
                saveConfig();
            };

            ContentPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(listBox, startRow + 1);
            Grid.SetColumnSpan(listBox, 2);
            ContentPanel.Children.Add(listBox);

            AppendFixedHomeModuleSettings(startRow + 2);
        }

        private void AppendFixedHomeModuleSettings(int startRow)
        {
            var title = new TextBlock
            {
                Text = "固定模块",
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 150, 200)),
                Margin = new Thickness(0, 12, 0, 5)
            };
            ContentPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(title, startRow);
            Grid.SetColumnSpan(title, 2);
            ContentPanel.Children.Add(title);

            int rowIndex = startRow + 1;
            foreach (var entry in HomeToolbarSettings.FixedModuleEntries)
            {
                ContentPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto, MinHeight = 38 });

                var row = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(2, 2, 0, 2)
                };
                var chk = new CheckBox
                {
                    IsChecked = Config.GetBool(entry.VisibilityConfigKey),
                    Style = FindResource("ModernToggleStyle") as Style,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 4, 8, 4)
                };
                var label = new TextBlock
                {
                    Text = entry.DisplayName,
                    FontSize = 13,
                    VerticalAlignment = VerticalAlignment.Center
                };

                chk.Checked += (s, e) =>
                {
                    Config.Set(entry.VisibilityConfigKey, true);
                    RefreshMainWindowHomeToolbar();
                };
                chk.Unchecked += (s, e) =>
                {
                    Config.Set(entry.VisibilityConfigKey, false);
                    RefreshMainWindowHomeToolbar();
                };

                row.Children.Add(chk);
                row.Children.Add(label);

                Grid.SetRow(row, rowIndex);
                Grid.SetColumnSpan(row, 2);
                ContentPanel.Children.Add(row);
                rowIndex++;
            }
        }

        /// <summary>
        /// 向上查找指定类型的可视树祖先
        /// </summary>
        private static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T t)
                    return t;
                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        // 刷新主题界面
        private void RefreshThemeUI()
        {
            // 重新加载当前分类
            ShowCategory(_currentCategoryIndex);
        }

        // Logo 切换事件
        private void Logo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            ComboBox cb = sender as ComboBox;
            if (cb == null || cb.SelectedIndex < 0) return;

            string selectedLogo = cb.SelectedItem.ToString();
            System.Diagnostics.Debug.WriteLine($"[Logo_SelectionChanged] 选择的Logo: {selectedLogo}");

            // 更新 Config 中的 Logo
            Config.Set("当前Logo", selectedLogo);

            // 更新设置窗口自己的 Logo
            ApplyCurrentLogo();

            // 通知主窗口更新 Logo（如果主窗口已打开）
            try
            {
                var mainWindow = Application.Current.MainWindow as MainWindow;
                if (mainWindow != null)
                {
                    mainWindow.ApplyCurrentLogo();
                    System.Diagnostics.Debug.WriteLine($"[Logo_SelectionChanged] 成功调用主窗口 ApplyCurrentLogo");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[Logo_SelectionChanged] 主窗口为空");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Logo_SelectionChanged] 调用 ApplyCurrentLogo 失败: {ex.Message}");
            }

            // 通知已打开的本地文章管理器和练单器同步 Logo。
            try
            {
                foreach (Window window in Application.Current.Windows)
                {
                    if (window is WinArticle articleWindow)
                    {
                        articleWindow.RefreshTheme();
                    }

                    if (window is WinTrainer trainerWindow)
                    {
                        trainerWindow.RefreshTheme();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Logo_SelectionChanged] 刷新文章管理器或练单器Logo失败: {ex.Message}");
            }
        }


        private static ControlTemplate CreateColorButtonTemplate()
        {
            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background")
            {
                RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
            });
            border.SetValue(Border.BorderBrushProperty, new SolidColorBrush(System.Windows.Media.Colors.Gray));
            border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(content);
            template.VisualTree = border;
            return template;
        }

        // 颜色选择按钮点击事件
        private void ColorButton_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            if (btn == null) return;

            // 获取当前颜色
            string currentColor = btn.Content.ToString();

            // 创建并显示颜色选择窗口
            WinColorPicker colorPicker = new WinColorPicker(currentColor);
            colorPicker.Owner = this;

            if (colorPicker.ShowDialog() == true)
            {
                // 更新按钮背景色和内容
                string colorHex = colorPicker.SelectedColor;

                try
                {
                    var wpfColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#" + colorHex);
                    btn.Background = new SolidColorBrush(wpfColor);
                    btn.Content = colorHex;
                    string colorKey = btn.Tag?.ToString();
                    if (!string.IsNullOrEmpty(colorKey))
                        SaveConfigValue(colorKey, colorHex, scheduleRefresh: false);

                    // 用户修改了颜色，自动切换到自定义模式
                    // 查找主题模式的 ComboBox
                    foreach (var item in ContentPanel.Children)
                    {
                        if (item is StackPanel panel)
                        {
                            var cb = panel.Children.OfType<ComboBox>().FirstOrDefault();
                            if (cb != null)
                            {
                                Config.Set("主题模式", "自定义");
                                int customIndex = cb.Items.IndexOf("自定义");
                                if (customIndex >= 0)
                                {
                                    cb.SelectedIndex = customIndex;
                                }
                                break;
                            }
                        }
                    }

                    // 通知主窗口刷新主题
                    NotifyMainWindowThemeRefresh();

                    // 通知所有打开的统计窗口和排行榜窗口刷新主题
                    NotifyAllWindowsThemeRefresh();

                    // 实时更新设置窗口的颜色
                    ApplyThemeColors();
                }
                catch
                {
                    MessageBox.Show("颜色格式错误", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public delegate void DelegateConfigSaved();

        public event DelegateConfigSaved ConfigSaved;

        private static readonly HashSet<string> AutoSaveSkipItems = new HashSet<string>
        {
            "主题模式",
            "当前Logo",
            "成绩显示时间",
            "当前版本",
            "最新版本",
            "修复安装",
            "软件更新Q群",
            "作者邮箱QQ"
        };

        private void AttachAutoSave(string itemKey, FrameworkElement valueControl)
        {
            if (valueControl == null || string.IsNullOrEmpty(itemKey) || AutoSaveSkipItems.Contains(itemKey))
                return;

            if (valueControl is TextBox textBox)
            {
                AttachTextBoxAutoSave(textBox, itemKey);
            }
            else if (valueControl is CheckBox checkBox)
            {
                AttachCheckBoxAutoSave(checkBox, itemKey);
            }
            else if (valueControl is ComboBox comboBox)
            {
                AttachComboBoxAutoSave(comboBox, itemKey);
            }
            else if (valueControl is StackPanel panel &&
                     (itemKey == "文来接口地址" || itemKey == "赛文服务器地址"))
            {
                var nestedTextBox = panel.Children.OfType<TextBox>().FirstOrDefault();
                if (nestedTextBox != null)
                    AttachTextBoxAutoSave(nestedTextBox, itemKey);
            }
        }

        private void AttachTextBoxAutoSave(TextBox textBox, string itemKey,
            Func<string, string> normalize = null,
            Func<string, bool> canSave = null)
        {
            if (textBox == null || textBox.IsReadOnly)
                return;

            Action save = () =>
            {
                string text = textBox.Text ?? "";
                if (canSave != null && !canSave(text))
                    return;

                string value = normalize != null ? normalize(text) : text;
                SaveConfigValue(itemKey, value);
            };

            textBox.LostFocus += (s, e) => save();
            textBox.KeyDown += (s, e) =>
            {
                if (!textBox.AcceptsReturn && e.Key == Key.Enter)
                {
                    save();
                    e.Handled = true;
                    Keyboard.ClearFocus();
                }
            };
        }

        private void AttachCheckBoxAutoSave(CheckBox checkBox, string itemKey)
        {
            if (checkBox == null)
                return;

            checkBox.Checked += (s, e) =>
            {
                bool changed = SaveConfigValue(itemKey, "是");
                if (changed && itemKey == "启用预测")
                    ShowPredictionEnableTip();
            };
            checkBox.Unchecked += (s, e) => SaveConfigValue(itemKey, "否");
        }

        private void ShowPredictionEnableTip()
        {
            MessageBox.Show(
                this,
                "预测功能会先学习你的跟打记录。预测置信度低于30%时，标题栏只显示基础难度，不会显示预测速度或个难；继续跟打几篇后，样本足够就会自动显示。",
                "预测功能说明",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void AttachComboBoxAutoSave(ComboBox comboBox, string itemKey)
        {
            if (comboBox == null)
                return;

            comboBox.SelectionChanged += (s, e) => SaveControlValue(comboBox, itemKey);
        }

        private void SaveControlValue(FrameworkElement control, string labelText)
        {
            if (control == null || string.IsNullOrEmpty(labelText))
                return;

            var key = new List<string>();
            var value = new List<string>();
            ExtractControlValue(control, labelText, key, value);
            FilterUnchangedConfigValues(key, value);
            ApplyCodeDisplayMutualExclusion(key, value);
            SaveChangedConfigValues(key, value);
        }

        private void SaveCurrentCategoryControls()
        {
            if (ContentPanel == null)
                return;

            var key = new List<string>();
            var value = new List<string>();

            foreach (var item in ContentPanel.Children)
            {
                if (!(item is FrameworkElement fe)) continue;

                int colIndex = (int)fe.GetValue(Grid.ColumnProperty);
                if (colIndex != 1) continue;

                int rowIndex = (int)fe.GetValue(Grid.RowProperty);
                string labelText = FindLabelInContentPanel(rowIndex, 0);
                if (string.IsNullOrEmpty(labelText)) continue;

                ExtractControlValue(item, labelText, key, value);
            }

            FilterUnchangedConfigValues(key, value);
            ApplyCodeDisplayMutualExclusion(key, value);
            SaveChangedConfigValues(key, value);

            foreach (var save in _categoryFallbackSaves.ToArray())
            {
                try
                {
                    save();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"设置页兜底保存失败: {ex.Message}");
                }
            }
        }

        private static void FilterUnchangedConfigValues(List<string> key, List<string> value)
        {
            for (int i = key.Count - 1; i >= 0; i--)
            {
                if (value[i] == Config.GetString(key[i]))
                {
                    key.RemoveAt(i);
                    value.RemoveAt(i);
                }
            }
        }

        private void AddCategoryFallbackSave(Action save)
        {
            if (save != null)
                _categoryFallbackSaves.Add(save);
        }

        private bool SaveConfigValue(string key, string value, bool scheduleRefresh = true)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            value = value ?? "";
            if (value == Config.GetString(key))
                return false;

            Config.Set(key, value);
            SaveCodeDisplayMutualExclusion(key, value);

            if (scheduleRefresh)
                ScheduleConfigSavedRefresh();

            return true;
        }

        private void ScheduleConfigSavedRefresh()
        {
            if (ConfigSaved == null)
                return;

            if (_configSavedRefreshTimer == null)
            {
                _configSavedRefreshTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(350)
                };
                _configSavedRefreshTimer.Tick += (s, e) =>
                {
                    _configSavedRefreshTimer.Stop();
                    _hasPendingConfigSavedRefresh = false;
                    InvokeConfigSavedWithoutSwitchingWindows();
                };
            }

            _hasPendingConfigSavedRefresh = true;
            _configSavedRefreshTimer.Stop();
            _configSavedRefreshTimer.Start();
        }

        private void FlushConfigSavedRefresh()
        {
            if (_configSavedRefreshTimer != null)
                _configSavedRefreshTimer.Stop();

            if (_hasPendingConfigSavedRefresh)
            {
                _hasPendingConfigSavedRefresh = false;
                InvokeConfigSavedWithoutSwitchingWindows();
            }
        }

        private void InvokeConfigSavedWithoutSwitchingWindows()
        {
            bool settingsWasActive = IsActive;
            ConfigSaved?.Invoke();
            RestoreSettingsActivationAfterConfigSaved(settingsWasActive);
        }

        private void RestoreSettingsActivationAfterConfigSaved(bool settingsWasActive)
        {
            if (!settingsWasActive || !IsVisible)
                return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!IsVisible || IsActive)
                    return;

                Activate();
            }), System.Windows.Threading.DispatcherPriority.ContextIdle);
        }

        private static void ApplyCodeDisplayMutualExclusion(List<string> key, List<string> value)
        {
            int citiIndex = key.IndexOf("词提编码下显");
            if (citiIndex >= 0 && value[citiIndex] == "是")
            {
                UpsertConfigValue(key, value, "启用词提", "是");
                UpsertConfigValue(key, value, "字提编码下显", "否");
                UpsertConfigValue(key, value, "词提选重数字角标", "否");
            }

            int citiBadgeIndex = key.IndexOf("词提选重数字角标");
            if (citiBadgeIndex >= 0 && value[citiBadgeIndex] == "是")
                UpsertConfigValue(key, value, "词提编码下显", "否");

            int zitiIndex = key.IndexOf("字提编码下显");
            if (zitiIndex >= 0 && value[zitiIndex] == "是")
            {
                UpsertConfigValue(key, value, "启用字提", "是");
                UpsertConfigValue(key, value, "词提编码下显", "否");
                UpsertConfigValue(key, value, "字提选重数字角标", "否");
            }

            int zitiBadgeIndex = key.IndexOf("字提选重数字角标");
            if (zitiBadgeIndex >= 0 && value[zitiBadgeIndex] == "是")
                UpsertConfigValue(key, value, "字提编码下显", "否");

            int citiEnabledIndex = key.IndexOf("启用词提");
            if (citiEnabledIndex >= 0 && value[citiEnabledIndex] == "否")
            {
                UpsertConfigValue(key, value, "词提编码下显", "否");
                UpsertConfigValue(key, value, "词提不拆行", "否");
            }
        }

        private static void UpsertConfigValue(List<string> key, List<string> value, string targetKey, string targetValue)
        {
            int index = key.IndexOf(targetKey);
            if (index >= 0)
            {
                value[index] = targetValue;
                return;
            }

            key.Add(targetKey);
            value.Add(targetValue);
        }

        private bool SaveChangedConfigValues(List<string> key, List<string> value)
        {
            bool modified = false;
            for (int i = 0; i < key.Count; i++)
            {
                if (SaveConfigValue(key[i], value[i]))
                    modified = true;
            }

            return modified;
        }

        private static void SaveCodeDisplayMutualExclusion(string key, string value)
        {
            if (key == "词提编码下显" && value == "是")
            {
                Config.Set("启用词提", "是");
                Config.Set("字提编码下显", "否");
                Config.Set("词提选重数字角标", "否");
            }
            if (key == "词提选重数字角标" && value == "是")
            {
                Config.Set("词提编码下显", "否");
            }
            if (key == "字提编码下显" && value == "是")
            {
                Config.Set("启用字提", "是");
                Config.Set("词提编码下显", "否");
                Config.Set("字提选重数字角标", "否");
            }
            if (key == "字提选重数字角标" && value == "是")
            {
                Config.Set("字提编码下显", "否");
            }
            if (key == "启用词提" && value == "否")
            {
                Config.Set("词提编码下显", "否");
                Config.Set("词提不拆行", "否");
            }
            if (key == "启用字提" && value == "否")
            {
                Config.Set("字提编码下显", "否");
            }
        }

        /// <summary>
        /// 提取控件值
        /// </summary>
        private void ExtractControlValue(object item, string labelText, List<string> key, List<string> value)
        {
            // 跳过不需要保存的配置项
            if (labelText == "当前版本" || labelText == "最新版本" || labelText == "上次检查更新时间")
            {
                return;
            }

            if (item is TextBox tb)
            {
                key.Add(labelText);
                value.Add(tb.Text);
            }
            else if (item is CheckBox chk)
            {
                key.Add(labelText);
                value.Add(chk.IsChecked == true ? "是" : "否");
            }
            else if (item is ComboBox comboBox)
            {
                if (labelText == "主题模式")
                {
                    key.Add(labelText);
                    value.Add(comboBox.SelectedIndex >= 0 && comboBox.SelectedIndex < comboBox.Items.Count
                        ? comboBox.Items[comboBox.SelectedIndex].ToString()
                        : "明");
                }
                else if (labelText == "字体")
                {
                    if (comboBox.SelectedIndex >= 0 && comboBox.SelectedIndex < comboBox.Items.Count)
                    {
                        key.Add(labelText);
                        value.Add(comboBox.Items[comboBox.SelectedIndex].ToString());
                    }
                }
                else if (labelText == "当前Logo")
                {
                    key.Add(labelText);
                    value.Add(comboBox.SelectedIndex >= 0 && comboBox.SelectedIndex < comboBox.Items.Count
                        ? comboBox.Items[comboBox.SelectedIndex].ToString()
                        : "sunny");
                }
                else if (labelText == "字提字体")
                {
                    if (comboBox.SelectedIndex >= 0 && comboBox.SelectedIndex < comboBox.Items.Count)
                    {
                        key.Add(labelText);
                        value.Add(comboBox.Items[comboBox.SelectedIndex].ToString());
                    }
                }
                else if (labelText == "字提方案")
                {
                    key.Add(labelText);
                    value.Add(comboBox.SelectedIndex >= 0 && comboBox.SelectedIndex < comboBox.Items.Count
                        ? comboBox.Items[comboBox.SelectedIndex].ToString()
                        : "");
                }
                else if (labelText == "词提方案")
                {
                    key.Add(labelText);
                    value.Add(comboBox.IsEnabled && comboBox.SelectedIndex >= 0 && comboBox.SelectedIndex < comboBox.Items.Count
                        ? comboBox.Items[comboBox.SelectedIndex].ToString()
                        : "");
                }
                else if (labelText == "盲打模式")
                {
                    key.Add(labelText);
                    if (comboBox.SelectedIndex == 1) // 盲打
                        value.Add("是");
                    else
                        value.Add("否");

                    key.Add("看打模式");
                    if (comboBox.SelectedIndex == 2) // 看打
                        value.Add("是");
                    else
                        value.Add("否");
                }
                else if (labelText == "文来换段模式")
                {
                    key.Add(labelText);
                    value.Add(comboBox.SelectedIndex == 1 ? "手动" : "自动");
                }
                else if (labelText == "重打跳转模式")
                {
                    key.Add(labelText);
                    value.Add(comboBox.SelectedIndex == 1 ? "手动" : "自动");
                }
                else if (labelText == "字数模式")
                {
                    key.Add(labelText);
                    value.Add(comboBox.SelectedIndex == 1 ? "精确字数" : "智能分段");
                }
                else if (labelText == "文来难度")
                {
                    key.Add(labelText);

                    // 检查是否有映射表（已登录状态）
                    if (comboBox.Tag is Dictionary<int, int> difficultyMapping)
                    {
                        // 从映射表获取实际的难度ID
                        if (difficultyMapping.ContainsKey(comboBox.SelectedIndex))
                        {
                            int difficultyId = difficultyMapping[comboBox.SelectedIndex];
                            value.Add(difficultyId == 0 ? "" : difficultyId.ToString());
                        }
                        else
                        {
                            value.Add(""); // 默认随机
                        }
                    }
                    else
                    {
                        // 未登录状态，保持空值
                        value.Add("");
                    }
                }
            }
            else if (item is Button btn)
            {
                key.Add(labelText);
                value.Add(btn.Content.ToString());
            }
            else if (item is StackPanel panel)
            {
                // 处理包含 TextBox + 按钮的 StackPanel（如文来接口地址、赛文服务器地址）
                var textBox = panel.Children.OfType<TextBox>().FirstOrDefault();
                if (textBox != null && (labelText == "文来接口地址" || labelText == "赛文服务器地址"))
                {
                    key.Add(labelText);
                    value.Add(textBox.Text);
                }
                else
                {
                    // 处理主题模式的 StackPanel（包含 ComboBox + Button）
                    var cb = panel.Children.OfType<ComboBox>().FirstOrDefault();
                    if (cb != null && labelText == "主题模式")
                    {
                        key.Add(labelText);
                        value.Add(cb.SelectedIndex >= 0 && cb.SelectedIndex < cb.Items.Count
                            ? cb.Items[cb.SelectedIndex].ToString()
                            : "明");
                    }
                    // 处理文来难度的 StackPanel（包含加载中状态的 ComboBox 或登录按钮）
                    else if (cb != null && labelText == "文来难度")
                    {
                        key.Add(labelText);

                        // 检查是否有映射表（已登录状态）
                        if (cb.Tag is Dictionary<int, int> difficultyMapping)
                        {
                            // 从映射表获取实际的难度ID
                            if (difficultyMapping.ContainsKey(cb.SelectedIndex))
                            {
                                int difficultyId = difficultyMapping[cb.SelectedIndex];
                                value.Add(difficultyId == 0 ? "" : difficultyId.ToString());
                            }
                            else
                            {
                                value.Add(""); // 默认随机
                            }
                        }
                        else
                        {
                            // 未登录状态或加载中，保持空值
                            value.Add("");
                        }
                    }
                    // 处理文来分类的 StackPanel
                    else if (cb != null && labelText == "文来分类")
                    {
                        key.Add(labelText);
                        if (cb.Tag is Dictionary<int, string> codeMapping)
                        {
                            if (codeMapping.ContainsKey(cb.SelectedIndex))
                                value.Add(codeMapping[cb.SelectedIndex]);
                            else
                                value.Add("");
                        }
                        else
                        {
                            value.Add("");
                        }
                    }
                }
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveCurrentCategoryControls();
            FlushConfigSavedRefresh();
            Config.WriteConfig(0);
        }

        /// <summary>
        /// 在内容区查找标签
        /// </summary>
        private string FindLabelInContentPanel(int row, int col)
        {
            foreach (var child in ContentPanel.Children)
            {
                if (child is FrameworkElement element &&
                    (int)element.GetValue(Grid.RowProperty) == row &&
                    (int)element.GetValue(Grid.ColumnProperty) == col)
                {
                    string labelText = GetLabelText(element);
                    if (!string.IsNullOrEmpty(labelText))
                    {
                        return labelText;
                    }
                }
            }
            return string.Empty;
        }

        private string GetLabelText(FrameworkElement element)
        {
            if (element is TextBlock textBlock &&
                Equals(textBlock.Tag, "ConfigLabelText") &&
                textBlock.FontWeight != FontWeights.Bold)
            {
                return textBlock.Text;
            }

            if (element is Panel panel)
            {
                foreach (var child in panel.Children)
                {
                    if (child is TextBlock childTextBlock &&
                        Equals(childTextBlock.Tag, "ConfigLabelText") &&
                        childTextBlock.FontWeight != FontWeights.Bold)
                    {
                        return childTextBlock.Text;
                    }
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// 根据标签文本查找对应的 CheckBox
        /// </summary>
        private CheckBox FindCheckBoxByLabel(string label)
        {
            foreach (var child in ContentPanel.Children)
            {
                if (child is FrameworkElement element &&
                    (int)element.GetValue(Grid.ColumnProperty) == 0 &&
                    GetLabelText(element) == label)
                {
                    int row = (int)element.GetValue(Grid.RowProperty);
                    foreach (var c in ContentPanel.Children)
                    {
                        if (c is CheckBox chk &&
                            (int)chk.GetValue(Grid.RowProperty) == row &&
                            (int)chk.GetValue(Grid.ColumnProperty) == 1)
                            return chk;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// 实时更新MainWindow的进度条显示状态
        /// </summary>
        private void UpdateMainWindowProgressBar()
        {
            try
            {
                // 查找MainWindow
                foreach (Window window in Application.Current.Windows)
                {
                    if (window is MainWindow mainWindow)
                    {
                        // 使用Dispatcher在UI线程上更新
                        mainWindow.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            bool showProgressBar = Config.GetBool("显示进度条");
                            if (!showProgressBar)
                            {
                                // 隐藏进度条
                                var progressBar = mainWindow.FindName("TitleProgressBar") as System.Windows.Shapes.Rectangle;
                                if (progressBar != null)
                                {
                                    progressBar.Width = 0;
                                }
                            }
                        }), System.Windows.Threading.DispatcherPriority.Normal);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新进度条显示失败: {ex.Message}");
            }
        }

        private void RefreshMainWindowResults()
        {
            try
            {
                foreach (Window window in Application.Current.Windows)
                {
                    if (window is MainWindow mainWindow)
                    {
                        mainWindow.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            mainWindow.UpdateTypingStat();
                        }), System.Windows.Threading.DispatcherPriority.Normal);
                        break;
                    }
                }
            }
            catch (Exception) { }
        }

        private void RefreshMainWindowHomeToolbar()
        {
            try
            {
                foreach (Window window in Application.Current.Windows)
                {
                    if (window is MainWindow mainWindow)
                    {
                        mainWindow.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            mainWindow.ApplyHomeToolbarSettings();
                        }), System.Windows.Threading.DispatcherPriority.Normal);
                        break;
                    }
                }
            }
            catch (Exception) { }
        }

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

        /// <summary>
        /// 应用主题颜色到设置窗口
        /// </summary>
        private void ApplyThemeColors()
        {
            try
            {
                // 获取主题颜色
                string windowBgColor = Config.GetString("窗体背景色");
                string windowFgColor = Config.GetString("窗体字体色");
                string menuBgColor = Config.GetString("菜单背景色");
                string menuFgColor = Config.GetString("菜单字体色");

                // 转换颜色
                var bgBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#" + windowBgColor));
                var fgBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#" + windowFgColor));
                var menuBgBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#" + menuBgColor));
                var menuFgBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#" + menuFgColor));

                // 应用到窗口（保持透明，拖动区域透明）
                this.Background = Brushes.Transparent;
                this.Foreground = fgBrush;

                // 应用到主边框
                MainBorder.Background = bgBrush;
                MainBorder.BorderBrush = ThemeColorHelper.CreateSubtleBorderBrush(bgBrush);

                // 应用到标题栏
                TitleBarBorder.Background = menuBgBrush;
                TitleBarText.Foreground = menuFgBrush;

                // 应用到导航栏
                NavBorder.Background = menuBgBrush;
                NavBorder.BorderBrush = ThemeColorHelper.CreateSubtleBorderBrush(menuBgBrush);

                // 重新生成导航按钮以应用新的按钮背景色和字体色
                GenerateNavButtons();

                // 更新分类标题颜色
                UpdateCategoryTitleColor();

                // 更新全局 ComboBox 样式
                UpdateComboBoxTheme(bgBrush, fgBrush);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"应用主题颜色失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新分类标题颜色
        /// </summary>
        private void UpdateCategoryTitleColor()
        {
            try
            {
                foreach (var child in ContentPanel.Children)
                {
                    if (child is TextBlock tb && tb.FontWeight == FontWeights.Bold && tb.FontSize == 20)
                    {
                        // 这是分类标题
                        tb.Foreground = new SolidColorBrush(Color.FromRgb(100, 200, 255));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新分类标题颜色失败: {ex.Message}");
            }
        }

        private void UpdateComboBoxTheme(SolidColorBrush bgBrush, SolidColorBrush fgBrush)
        {
            try
            {
                var buttonBgBrush = new SolidColorBrush(Color.FromRgb(
                    (byte)Math.Min(255, bgBrush.Color.R + 20),
                    (byte)Math.Min(255, bgBrush.Color.G + 20),
                    (byte)Math.Min(255, bgBrush.Color.B + 20)
                ));

                var borderBrush = ThemeColorHelper.CreateSubtleBorderBrush(bgBrush);

                var hoverBrush = new SolidColorBrush(Color.FromRgb(
                    (byte)Math.Min(255, buttonBgBrush.Color.R + 15),
                    (byte)Math.Min(255, buttonBgBrush.Color.G + 15),
                    (byte)Math.Min(255, buttonBgBrush.Color.B + 15)
                ));

                Application.Current.Resources["ComboBoxBackground"] = buttonBgBrush;
                Application.Current.Resources["ComboBoxForeground"] = fgBrush;
                Application.Current.Resources["ComboBoxBorderBrush"] = borderBrush;
                Application.Current.Resources["ComboBoxDropDownBackground"] = bgBrush;
                Application.Current.Resources["ComboBoxItemHoverBackground"] = hoverBrush;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新ComboBox主题失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 应用当前选中的 Logo
        /// </summary>
        private void ApplyCurrentLogo()
        {
            try
            {
                string currentLogo = Config.GetString("当前Logo");
                string iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "ico", $"{currentLogo}.ico");
                if (System.IO.File.Exists(iconPath))
                {
                    var iconUri = new Uri(iconPath, UriKind.Absolute);
                    // 更新窗口图标（任务栏、Alt+Tab等）
                    this.Icon = new BitmapImage(iconUri);
                    // 更新标题栏图标（窗口左上角显示的图标）
                    TitleBarIcon.Source = new BitmapImage(iconUri);
                    System.Diagnostics.Debug.WriteLine($"[WinConfig] 应用Logo成功: {iconPath}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[WinConfig] Logo文件不存在: {iconPath}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WinConfig] 应用Logo失败: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 拖拽浮动装饰层：在鼠标位置显示被拖拽项的半透明副本
    /// </summary>
    internal class DragAdorner : Adorner
    {
        private readonly VisualBrush _brush;
        private readonly double _opacity;
        private Point _position;
        private readonly Size _itemSize;

        public DragAdorner(UIElement adornedElement, ListBoxItem draggedItem, double opacity)
            : base(adornedElement)
        {
            _opacity = opacity;
            _itemSize = new Size(draggedItem.ActualWidth, draggedItem.ActualHeight);
            _brush = new VisualBrush(draggedItem)
            {
                Opacity = _opacity
            };
            IsHitTestVisible = false;
        }

        public void UpdatePosition(Point pos)
        {
            _position = pos;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            var rect = new Rect(
                _position.X - _itemSize.Width / 2,
                _position.Y - _itemSize.Height / 2,
                _itemSize.Width,
                _itemSize.Height);
            drawingContext.DrawRectangle(_brush, null, rect);
        }
    }

    /// <summary>
    /// 插入线装饰层：在目标项的上方或下方绘制一条蓝色水平线
    /// </summary>
    internal class InsertionLineAdorner : Adorner
    {
        private readonly ListBoxItem _targetItem;
        private readonly bool _insertBefore;

        public InsertionLineAdorner(UIElement adornedElement, ListBoxItem targetItem, bool insertBefore)
            : base(adornedElement)
        {
            _targetItem = targetItem;
            _insertBefore = insertBefore;
            IsHitTestVisible = false;
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            var transform = _targetItem.TransformToAncestor(AdornedElement);
            var itemPos = transform.Transform(new Point(0, 0));

            double y = _insertBefore ? itemPos.Y : itemPos.Y + _targetItem.ActualHeight;

            var pen = new Pen(Brushes.DodgerBlue, 2);
            drawingContext.DrawLine(pen, new Point(itemPos.X, y), new Point(itemPos.X + _targetItem.ActualWidth, y));

            // 左右两端画小三角指示
            double triangleSize = 4;
            var leftTriangle = new StreamGeometry();
            using (var ctx = leftTriangle.Open())
            {
                ctx.BeginFigure(new Point(itemPos.X, y), true, true);
                ctx.LineTo(new Point(itemPos.X + triangleSize * 2, y - triangleSize), false, false);
                ctx.LineTo(new Point(itemPos.X + triangleSize * 2, y + triangleSize), false, false);
            }
            drawingContext.DrawGeometry(Brushes.DodgerBlue, null, leftTriangle);

            var rightTriangle = new StreamGeometry();
            double rightX = itemPos.X + _targetItem.ActualWidth;
            using (var ctx = rightTriangle.Open())
            {
                ctx.BeginFigure(new Point(rightX, y), true, true);
                ctx.LineTo(new Point(rightX - triangleSize * 2, y - triangleSize), false, false);
                ctx.LineTo(new Point(rightX - triangleSize * 2, y + triangleSize), false, false);
            }
            drawingContext.DrawGeometry(Brushes.DodgerBlue, null, rightTriangle);
        }
    }
}
