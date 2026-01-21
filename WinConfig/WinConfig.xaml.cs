using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Markup;
using TypeSunny.ArticleSender;
using TypeSunny.Net;


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
                    Title = "跟打",
                    Items = new[]
                    {
                        "盲打模式",
                        "速度跟随提示",
                        "错字重打",
                        "禁止F3重打",
                        "错字重复次数",
                        "慢字重打",
                        "慢字标准(单位:秒)",
                        "慢字重复次数",
                        "贪吃蛇模式",
                        "贪吃蛇前显字数",
                        "贪吃蛇后显字数",
                        "显示进度条",
                        "自动发送成绩"
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
                        "字提字体大小"
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
                        "文来换段模式",
                        "字数模式",
                        "赛文服务器地址",
                        "赛文输入法"
                    }
                },
                new ConfigCategory
                {
                    Title = "其他",
                    Items = new[]
                    {
                        "当前版本",
                        "最新版本",
                        "成绩签名",
                        "成绩显示项",
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
            foreach (var itemKey in category.Items)
            {
                if (!Config.dicts.ContainsKey(itemKey))
                    continue;

                string itemValue = Config.dicts[itemKey];

                ContentPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto, MinHeight = 38 });

                // 创建标签
                var tbk = new TextBlock
                {
                    Text = itemKey,
                    Margin = new Thickness(0, 10, 20, 10),
                    FontSize = 14,
                    MinWidth = 120
                };
                Grid.SetRow(tbk, currentRow);
                Grid.SetColumn(tbk, 0);
                ContentPanel.Children.Add(tbk);

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
        }

        /// <summary>
        /// 创建值控件
        /// </summary>
        private FrameworkElement CreateValueControl(string itemKey, string itemValue)
        {
            FrameworkElement valueControl = null;

            // 根据配置项类型创建对应的控件
            if (itemValue == "是" || itemValue == "否")
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
                    Tag = itemKey
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

                // 设置当前选中项
                for (int i = 0; i < cb.Items.Count; i++)
                {
                    if (cb.Items[i].ToString() == itemValue)
                    {
                        cb.SelectedIndex = i;
                        break;
                    }
                }
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

                // 设置当前选中项
                for (int i = 0; i < cb.Items.Count; i++)
                {
                    if (cb.Items[i].ToString() == itemValue)
                    {
                        cb.SelectedIndex = i;
                        break;
                    }
                }
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
            else if (itemKey == "成绩显示项")
            {
                // 创建一个按钮，点击后打开成绩显示项配置窗口
                var btn = new Button
                {
                    Content = "配置成绩显示项",
                    Width = 150,
                    Height = 30,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(0, 5, 0, 5)
                };
                btn.Click += (s, e) => ShowScoreBlockConfig();
                valueControl = btn;
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
                // 最新版本：文本框 + 刷新按钮
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
                    Margin = new Thickness(0, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                refreshBtn.Click += async (s, e) =>
                {
                    refreshBtn.IsEnabled = false;
                    tb.Text = "检查中...";
                    try
                    {
                        await VersionManager.CheckUpdateAsync(forceRefresh: true);
                        tb.Text = VersionManager.LatestVersion;
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
                panel.Children.Add(refreshBtn);

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
                        var s = gf.FamilyNames;
                        string fontname = "";
                        if (s.ContainsKey(cn))
                            fontname = s[cn];
                        else if (s.ContainsKey(en))
                            fontname = s[en];
                        if (fontname != "")
                            cb.Items.Add("#" + fontname);
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
                                        mainWindow.Dispatcher.BeginInvoke(new Action(() =>
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

                        container.Children.Add(cb);
                    }
                });
            }
            catch (Exception ex)
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

        // 颜色配置项列表
        private static readonly string[] ColorConfigItems =
        {
            "窗体背景色", "窗体字体色",
            "跟打区背景色", "跟打区字体色",
            "发文区字体色",
            "打对色", "打错色",
            "按钮背景色", "按钮字体色",
            "菜单背景色", "菜单字体色"
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
                int statsCount = 0, leaderboardCount = 0, trainerCount = 0;

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
                    // 如果是排行榜窗口，调用其 RefreshTheme 方法
                    else if (window is WinRaceLeaderboard leaderboardWindow)
                    {
                        leaderboardCount++;
                        System.Diagnostics.Debug.WriteLine($"[WinConfig] 找到排行榜窗口，调用 RefreshTheme");
                        leaderboardWindow.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            leaderboardWindow.RefreshTheme();
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
                }

                System.Diagnostics.Debug.WriteLine($"[WinConfig] 找到 {statsCount} 个成绩统计窗口, {leaderboardCount} 个排行榜窗口, {trainerCount} 个练单器窗口");
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

        // 显示成绩显示项配置窗口
        private void ShowScoreBlockConfig()
        {
            // 创建窗口
            var window = new Window
            {
                Title = "成绩显示项配置",
                Width = 400,
                Height = 500,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            // 创建主面板
            var mainPanel = new StackPanel
            {
                Margin = new Thickness(20)
            };

            // 标题
            var title = new TextBlock
            {
                Text = "选择要显示的成绩项",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 15)
            };
            mainPanel.Children.Add(title);

            // 强制显示项（不可取消）
            var forceTitle = new TextBlock
            {
                Text = "强制显示（不可取消）",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 150, 200)),
                Margin = new Thickness(0, 0, 0, 5)
            };
            mainPanel.Children.Add(forceTitle);

            var forcePanel = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
            foreach (var item in new[] { "字数", "速度", "击键", "键准", "段号" })
            {
                var chk = new CheckBox
                {
                    Content = item,
                    Margin = new Thickness(5),
                    IsChecked = true,
                    IsEnabled = false  // 强制显示，不可取消
                };
                forcePanel.Children.Add(chk);
            }
            mainPanel.Children.Add(forcePanel);

            // 可选项
            var optTitle = new TextBlock
            {
                Text = "可选显示项",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 150, 200)),
                Margin = new Thickness(0, 0, 0, 5)
            };
            mainPanel.Children.Add(optTitle);

            // 成绩项列表（去掉看打相关）
            var scoreItems = new[]
            {
                "码长", "重打", "总键数", "键法", "回改", "退格",
                "废码", "打词率", "选重", "标顶", "用时", "错字",
                "盲打正确率", "盲打模式", "签名"
            };

            var checkboxes = new Dictionary<string, CheckBox>();

            // 创建两列布局
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            int row = 0;
            int col = 0;
            foreach (var item in scoreItems)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var chk = new CheckBox
                {
                    Content = item,
                    Margin = new Thickness(5),
                    IsChecked = Config.GetBool("显示_" + item)  // true=显示，false=不显示
                };
                Grid.SetRow(chk, row);
                Grid.SetColumn(chk, col);
                grid.Children.Add(chk);
                checkboxes[item] = chk;

                row++;
                if (row >= 8) // 每列8项
                {
                    row = 0;
                    col = 1;
                }
            }

            mainPanel.Children.Add(grid);

            // 按钮面板
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 20, 0, 0)
            };

            var saveBtn = new Button
            {
                Content = "保存",
                Width = 80,
                Height = 30,
                Margin = new Thickness(0, 0, 10, 0)
            };
            saveBtn.Click += (s, e) =>
            {
                // 保存配置（强制显示项始终显示）
                Config.Set("显示_字数", true);
                Config.Set("显示_速度", true);
                Config.Set("显示_击键", true);
                Config.Set("显示_键准", true);
                // 段号（ArticleMark）始终显示，没有对应的配置项

                // 可选项根据勾选状态保存
                foreach (var kvp in checkboxes)
                {
                    // 勾选=显示=true，不勾选=不显示=false
                    Config.Set("显示_" + kvp.Key, kvp.Value.IsChecked == true);
                }

                window.Close();
            };

            var cancelBtn = new Button
            {
                Content = "取消",
                Width = 80,
                Height = 30
            };
            cancelBtn.Click += (s, e) => window.Close();

            buttonPanel.Children.Add(saveBtn);
            buttonPanel.Children.Add(cancelBtn);
            mainPanel.Children.Add(buttonPanel);

            window.Content = mainPanel;
            window.ShowDialog();
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

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        public delegate void DelegateConfigSaved();

        public event DelegateConfigSaved ConfigSaved;
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            List<string> key = new List<string>();
            List<string> value = new List<string>();

            foreach (var item in ContentPanel.Children)
            {
                if (!(item is FrameworkElement fe)) continue;

                // 只处理值列的控件（Column = 1）
                int colIndex = (int)fe.GetValue(Grid.ColumnProperty);
                if (colIndex != 1) continue;

                int rowIndex = (int)fe.GetValue(Grid.RowProperty);

                // 查找同一行标签列的 TextBlock（Column = 0）
                string labelText = FindLabelInContentPanel(rowIndex, 0);
                if (string.IsNullOrEmpty(labelText)) continue;

                // 根据控件类型提取值
                ExtractControlValue(item, labelText, key, value);
            }

            bool modified = false;
            for (int i = 0; i < key.Count; i++)
            {
                if (value[i] != Config.GetString(key[i]))
                {
                    modified = true;
                    Config.Set(key[i], value[i]);
                }
            }
            if (modified)
            {
                ConfigSaved();
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
                    key.Add(labelText);
                    value.Add(comboBox.SelectedIndex >= 0 && comboBox.SelectedIndex < comboBox.Items.Count
                        ? comboBox.Items[comboBox.SelectedIndex].ToString()
                        : "微软雅黑");
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
                    key.Add(labelText);
                    value.Add(comboBox.SelectedIndex >= 0 && comboBox.SelectedIndex < comboBox.Items.Count
                        ? comboBox.Items[comboBox.SelectedIndex].ToString()
                        : "TumanPUA");
                }
                else if (labelText == "字提方案")
                {
                    key.Add(labelText);
                    value.Add(comboBox.SelectedIndex >= 0 && comboBox.SelectedIndex < comboBox.Items.Count
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
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            List<string> key = new List<string>();
            List<string> value = new List<string>();

            foreach (var item in ContentPanel.Children)
            {
                if (!(item is FrameworkElement fe)) continue;

                // 只处理值列的控件（Column = 1）
                int colIndex = (int)fe.GetValue(Grid.ColumnProperty);
                if (colIndex != 1) continue;

                int rowIndex = (int)fe.GetValue(Grid.RowProperty);

                // 查找同一行标签列的 TextBlock（Column = 0）
                string labelText = FindLabelInContentPanel(rowIndex, 0);
                if (string.IsNullOrEmpty(labelText)) continue;

                // 根据控件类型提取值
                ExtractControlValue(item, labelText, key, value);
            }

            bool modified = false;
            for (int i = 0; i < key.Count; i++)
            {
                if (value[i] != Config.GetString(key[i]))
                {
                    modified = true;
                }
            }
            if (modified)
            {
                if (MessageBox.Show("设置已修改，是否保存？",
                                    "保存设置",
                                    MessageBoxButton.YesNo,
                                    MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    for (int i = 0; i < key.Count; i++)
                    {
                        if (value[i] != Config.GetString(key[i]))
                        {
                            Config.Set(key[i], value[i]);
                        }
                    }

                    ConfigSaved();
                }
            }
        }

        /// <summary>
        /// 在内容区查找标签
        /// </summary>
        private string FindLabelInContentPanel(int row, int col)
        {
            foreach (var child in ContentPanel.Children)
            {
                if (child is TextBlock tb &&
                    (int)tb.GetValue(Grid.RowProperty) == row &&
                    (int)tb.GetValue(Grid.ColumnProperty) == col &&
                    tb.FontWeight != FontWeights.Bold) // 排除分类标题
                {
                    return tb.Text;
                }
            }
            return string.Empty;
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

                // 应用到标题栏
                TitleBarBorder.Background = menuBgBrush;
                TitleBarText.Foreground = menuFgBrush;

                // 应用到导航栏
                NavBorder.Background = menuBgBrush;
                NavBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(
                    (byte)Math.Max(0, menuBgBrush.Color.R - 30),
                    (byte)Math.Max(0, menuBgBrush.Color.G - 30),
                    (byte)Math.Max(0, menuBgBrush.Color.B - 30)
                ));

                // 重新生成导航按钮以应用新的按钮背景色和字体色
                GenerateNavButtons();

                // 更新分类标题颜色
                UpdateCategoryTitleColor();
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
}
