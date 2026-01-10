using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Markup;
using TypeSunny.ArticleSender;


namespace TypeSunny
{
    /// <summary>
    /// WinConfig.xaml 的交互逻辑
    /// </summary>
    public partial class WinConfig : Window
    {
        public WinConfig()
        {
            InitializeComponent();
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

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 定义配置项分类 - 每个分类指定标签列和值列
            var categories = new[]
            {
                new
                {
                    Title = "界面设置",
                    LabelColumn = 0,
                    ValueColumn = 1,
                    Items = new[]
                    {
                        "主题模式",
                        "窗体背景色", "窗体字体色",
                        "跟打区背景色", "跟打区字体色",
                        "发文区字体色",
                        "打对色", "打错色",
                        "按钮背景色", "按钮字体色",
                        "菜单背景色", "菜单字体色",
                        "字体"
                    }
                },
                new
                {
                    Title = "跟打设置",
                    LabelColumn = 3,
                    ValueColumn = 4,
                    Items = new[]
                    {
                        "盲打模式",
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
                        "跟打区字体大小",
                        "发文区字体大小",
                        "成绩区字体大小"
                    }
                },
                new
                {
                    Title = "网络设置",
                    LabelColumn = 6,
                    ValueColumn = 7,
                    Items = new[]
                    {
                        "文来接口地址",
                        "文来字数",
                        "文来难度",
                        "赛文服务器地址",
                        "赛文输入法"
                    }
                },
                new
                {
                    Title = "其他设置",
                    LabelColumn = 9,
                    ValueColumn = 10,
                    Items = new[]
                    {
                        "成绩签名",
                        "成绩屏蔽(逗号分隔)",
                        "软件更新Q群",
                        "作者邮箱QQ"
                    }
                }
            };

            // 每列的行计数器
            Dictionary<int, int> rowCounters = new Dictionary<int, int>();

            foreach (var category in categories)
            {
                if (!rowCounters.ContainsKey(category.LabelColumn))
                    rowCounters[category.LabelColumn] = 0;

                // 添加分类标题
                var titleBlock = new TextBlock
                {
                    Text = category.Title,
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(10, 10, 10, 5),
                    Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 200, 255))
                };

                GridMain.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Grid.SetRow(titleBlock, rowCounters[category.LabelColumn]);
                Grid.SetColumn(titleBlock, category.LabelColumn);
                Grid.SetColumnSpan(titleBlock, 2); // 标题跨越标签列和值列
                GridMain.Children.Add(titleBlock);
                rowCounters[category.LabelColumn]++;

                // 添加该分类下的配置项
                foreach (var itemKey in category.Items)
                {
                    if (!Config.dicts.ContainsKey(itemKey))
                        continue;

                    string itemValue = Config.dicts[itemKey];

                    GridMain.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                    // 创建标签
                    var tbk = new TextBlock
                    {
                        Text = itemKey,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(10, 3, 10, 3),
                        FontSize = 14
                    };
                    Grid.SetRow(tbk, rowCounters[category.LabelColumn]);
                    Grid.SetColumn(tbk, category.LabelColumn);
                    GridMain.Children.Add(tbk);

                    FrameworkElement valueControl = null;

                    // 根据配置项类型创建对应的控件
                    if (itemValue == "是" || itemValue == "否")
                    {
                        var chk = new CheckBox
                        {
                            IsChecked = itemValue == "是",
                            VerticalAlignment = VerticalAlignment.Center,
                            Margin = new Thickness(10, 3, 10, 3)
                        };
                        valueControl = chk;
                    }
                    else if (itemKey == "主题模式")
                    {
                        var cb = new ComboBox
                        {
                            Width = 200,
                            VerticalAlignment = VerticalAlignment.Center,
                            Margin = new Thickness(10, 3, 10, 3)
                        };
                        cb.Items.Add("明");
                        cb.Items.Add("暗");
                        cb.Items.Add("自定义");
                        cb.SelectedIndex = itemValue == "明" ? 0 : itemValue == "暗" ? 1 : 2;
                        cb.SelectionChanged += ThemeMode_SelectionChanged;
                        valueControl = cb;
                    }
                    else if (ColorConfigItems.Contains(itemKey))
                    {
                        var btn = new Button
                        {
                            Width = 200,
                            Height = 30,
                            HorizontalAlignment = HorizontalAlignment.Left,
                            VerticalAlignment = VerticalAlignment.Center,
                            Margin = new Thickness(10, 3, 10, 3),
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
                            VerticalAlignment = VerticalAlignment.Center,
                            Margin = new Thickness(10, 3, 10, 3),
                            Tag = "Font"
                        };

                        // 添加自定义字体
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

                        // 添加系统字体
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
                            VerticalAlignment = VerticalAlignment.Center,
                            Margin = new Thickness(10, 3, 10, 3),
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
                        continue;
                    }
                    else if (itemKey == "文来难度")
                    {
                        var difficultyStats = GetDifficultyStats();
                        var cb = new ComboBox
                        {
                            Width = 200,
                            VerticalAlignment = VerticalAlignment.Center,
                            Margin = new Thickness(10, 3, 10, 3)
                        };

                        // 检查是否未登录（所有难度的文章数都为0）
                        int totalCount = difficultyStats.Values.Sum();
                        if (totalCount == 0)
                        {
                            // 未登录，显示提示信息
                            cb.Items.Add("文来登录后可选");
                            cb.SelectedIndex = 0;
                            cb.IsEnabled = false; // 禁用下拉框
                        }
                        else
                        {
                            // 已登录，动态生成难度选项
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

                                // 从 ArticleFetcher 获取难度名称
                                var difficulties = ArticleFetcher.GetDifficulties();
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
                            if (!string.IsNullOrEmpty(itemValue))
                            {
                                int.TryParse(itemValue, out currentDifficultyId);
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
                        }

                        valueControl = cb;
                    }
                    else
                    {
                        var tb = new TextBox
                        {
                            Text = itemValue,
                            Width = 200,
                            VerticalAlignment = VerticalAlignment.Center,
                            Margin = new Thickness(10, 3, 10, 3)
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

                    if (valueControl != null)
                    {
                        Grid.SetRow(valueControl, rowCounters[category.LabelColumn]);
                        Grid.SetColumn(valueControl, category.ValueColumn); // 使用值列
                        GridMain.Children.Add(valueControl);
                    }

                    rowCounters[category.LabelColumn]++;
                }
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

            string selectedTheme = "";
            switch (cb.SelectedIndex)
            {
                case 0:
                    selectedTheme = "明";
                    break;
                case 1:
                    selectedTheme = "暗";
                    break;
                case 2:
                    selectedTheme = "自定义";
                    break;
            }

            // 应用主题（明或暗时会自动设置颜色）
            ThemeHelper.ApplyTheme(selectedTheme);

            // 更新界面上的颜色按钮显示
            if (selectedTheme != "自定义")
            {
                foreach (var item in GridMain.Children)
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
                    foreach (var item in GridMain.Children)
                    {
                        if (item is ComboBox cb)
                        {
                            // 查找是否是主题模式的ComboBox
                            int rowIndex = (int)cb.GetValue(Grid.RowProperty);
                            foreach (var child in GridMain.Children)
                            {
                                if (child is TextBlock tb && (int)tb.GetValue(Grid.RowProperty) == rowIndex)
                                {
                                    if (tb.Text == "主题模式")
                                    {
                                        cb.SelectedIndex = 2; // 切换到自定义
                                        break;
                                    }
                                }
                            }
                        }
                    }
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

            foreach (var item in GridMain.Children)
            {
                if (item.GetType() == typeof(TextBox))
                {
                    var tb = (TextBox)item;
                    int rowIndex = (int)tb.GetValue(Grid.RowProperty);
                    int colIndex = (int)tb.GetValue(Grid.ColumnProperty);

                    // 查找同一行、左边一列的TextBlock标签
                    string labelText = FindLabel(rowIndex, colIndex - 1);
                    if (!string.IsNullOrEmpty(labelText))
                    {
                        key.Add(labelText);
                        value.Add(tb.Text);
                    }
                }
                else if (item.GetType() == typeof(CheckBox))
                {
                    var cb = (CheckBox)item;
                    int rowIndex = (int)cb.GetValue(Grid.RowProperty);
                    int colIndex = (int)cb.GetValue(Grid.ColumnProperty);

                    string labelText = FindLabel(rowIndex, colIndex - 1);
                    if (!string.IsNullOrEmpty(labelText))
                    {
                        key.Add(labelText);
                        value.Add(cb.IsChecked == true ? "是" : "否");
                    }
                }
                else if (item.GetType() == typeof(ComboBox))
                {
                    var cb = (ComboBox)item;
                    int rowIndex = (int)cb.GetValue(Grid.RowProperty);
                    int colIndex = (int)cb.GetValue(Grid.ColumnProperty);

                    string labelText = FindLabel(rowIndex, colIndex - 1);
                    if (string.IsNullOrEmpty(labelText))
                        continue;

                    if (labelText == "主题模式")
                    {
                        key.Add(labelText);
                        if (cb.SelectedIndex == 0)
                            value.Add("明");
                        else if (cb.SelectedIndex == 1)
                            value.Add("暗");
                        else
                            value.Add("自定义");
                    }
                    else if (labelText == "字体")
                    {
                        key.Add(labelText);
                        if (cb.SelectedIndex >= 0 && cb.SelectedIndex < cb.Items.Count)
                            value.Add(cb.Items[cb.SelectedIndex].ToString());
                        else
                            value.Add("微软雅黑");
                    }
                    else if (labelText == "盲打模式")
                    {
                        key.Add(labelText);
                        if (cb.SelectedIndex == 1) // 盲打
                            value.Add("是");
                        else
                            value.Add("否");

                        key.Add("看打模式");
                        if (cb.SelectedIndex == 2) // 看打
                            value.Add("是");
                        else
                            value.Add("否");
                    }
                    else if (labelText == "文来难度")
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
                            // 未登录状态，保持空值
                            value.Add("");
                        }
                    }
                }
                else if (item.GetType() == typeof(Button))
                {
                    var btn = (Button)item;
                    int rowIndex = (int)btn.GetValue(Grid.RowProperty);
                    int colIndex = (int)btn.GetValue(Grid.ColumnProperty);

                    string labelText = FindLabel(rowIndex, colIndex - 1);
                    if (!string.IsNullOrEmpty(labelText))
                    {
                        key.Add(labelText);
                        value.Add(btn.Content.ToString());
                    }
                }
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

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            List<string> key = new List<string>();
            List<string> value = new List<string>();

            foreach (var item in GridMain.Children)
            {
                if (item.GetType() == typeof(TextBox))
                {
                    var tb = (TextBox)item;
                    int rowIndex = (int)tb.GetValue(Grid.RowProperty);
                    int colIndex = (int)tb.GetValue(Grid.ColumnProperty);

                    string labelText = FindLabel(rowIndex, colIndex - 1);
                    if (!string.IsNullOrEmpty(labelText))
                    {
                        key.Add(labelText);
                        value.Add(tb.Text);
                    }
                }
                else if (item.GetType() == typeof(CheckBox))
                {
                    var cb = (CheckBox)item;
                    int rowIndex = (int)cb.GetValue(Grid.RowProperty);
                    int colIndex = (int)cb.GetValue(Grid.ColumnProperty);

                    string labelText = FindLabel(rowIndex, colIndex - 1);
                    if (!string.IsNullOrEmpty(labelText))
                    {
                        key.Add(labelText);
                        value.Add(cb.IsChecked == true ? "是" : "否");
                    }
                }
                else if (item.GetType() == typeof(ComboBox))
                {
                    var cb = (ComboBox)item;
                    int rowIndex = (int)cb.GetValue(Grid.RowProperty);
                    int colIndex = (int)cb.GetValue(Grid.ColumnProperty);

                    string labelText = FindLabel(rowIndex, colIndex - 1);
                    if (string.IsNullOrEmpty(labelText))
                        continue;

                    if (labelText == "主题模式")
                    {
                        key.Add(labelText);
                        if (cb.SelectedIndex == 0)
                            value.Add("明");
                        else if (cb.SelectedIndex == 1)
                            value.Add("暗");
                        else
                            value.Add("自定义");
                    }
                    else if (labelText == "字体")
                    {
                        key.Add(labelText);
                        if (cb.SelectedIndex >= 0 && cb.SelectedIndex < cb.Items.Count)
                            value.Add(cb.Items[cb.SelectedIndex].ToString());
                        else
                            value.Add("微软雅黑");
                    }
                    else if (labelText == "盲打模式")
                    {
                        key.Add(labelText);
                        if (cb.SelectedIndex == 1) // 盲打
                            value.Add("是");
                        else
                            value.Add("否");

                        key.Add("看打模式");
                        if (cb.SelectedIndex == 2) // 看打
                            value.Add("是");
                        else
                            value.Add("否");
                    }
                    else if (labelText == "文来难度")
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
                            // 未登录状态，保持空值
                            value.Add("");
                        }
                    }
                }
                else if (item.GetType() == typeof(Button))
                {
                    var btn = (Button)item;
                    int rowIndex = (int)btn.GetValue(Grid.RowProperty);
                    int colIndex = (int)btn.GetValue(Grid.ColumnProperty);

                    string labelText = FindLabel(rowIndex, colIndex - 1);
                    if (!string.IsNullOrEmpty(labelText))
                    {
                        key.Add(labelText);
                        value.Add(btn.Content.ToString());
                    }
                }
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
        /// 查找指定行和列的TextBlock标签
        /// </summary>
        private string FindLabel(int row, int col)
        {
            foreach (var child in GridMain.Children)
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
    }
}
