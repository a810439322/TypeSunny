using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace TypeSunny.Utils
{
    internal static class DialogTheming
    {
        internal static void Apply(
            Border mainBorder,
            TextBlock[] foregroundTexts,
            Button[] normalButtons,
            Button accentButton,
            ProgressBar progressBar)
        {
            try
            {
                var p = LoadPalette();

                if (mainBorder != null)
                {
                    mainBorder.Background = p.WindowBg;
                    mainBorder.BorderBrush = new SolidColorBrush(p.BorderColor);
                }

                if (foregroundTexts != null)
                {
                    foreach (var text in foregroundTexts)
                    {
                        if (text != null)
                            text.Foreground = p.WindowFg;
                    }
                }

                if (normalButtons != null)
                {
                    foreach (var button in normalButtons)
                    {
                        ApplyButtonTheme(button, p, false);
                    }
                }

                if (accentButton != null)
                {
                    ApplyButtonTheme(accentButton, p, true);
                    if (progressBar != null)
                        progressBar.Foreground = p.Accent;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DialogTheming] 主题应用失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 对整个窗口递归应用主题色：窗口本体上色，再遍历所有子控件按类型上色。
        /// 普通按钮使用统一的弹窗按钮模板，避免走系统默认高亮色。
        /// </summary>
        internal static void ApplyToWindow(Window window)
        {
            if (window == null) return;
            try
            {
                var p = LoadPalette();
                window.Background = p.WindowBg;
                window.Foreground = p.WindowFg;
                ApplyToVisualTree(window.Content as DependencyObject, p);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DialogTheming] 窗口主题应用失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 给窗口套上自定义无边框主题 chrome：菜单色标题栏 + 圆角 + 关闭按钮 + 可拖拽，
        /// 并对内部内容递归上色。必须在窗口 Show/ShowDialog 之前调用。
        /// </summary>
        internal static void ApplyChromelessTheme(Window window)
        {
            if (window == null) return;
            try
            {
                var p = LoadPalette();
                var menuBg = LoadMenuBg();
                var menuFg = LoadMenuFg();

                // 取出并暂时脱离原内容，避免重新挂载时报 "已有父节点"
                var originalContent = window.Content as UIElement;
                window.Content = null;

                // 切到无边框 + 透明背景模式（必须在 Show 前调用）
                window.WindowStyle = WindowStyle.None;
                window.AllowsTransparency = true;
                window.Background = Brushes.Transparent;
                window.Foreground = p.WindowFg;

                // 外层圆角主边框
                var mainBorder = new Border
                {
                    Background = p.WindowBg,
                    BorderBrush = new SolidColorBrush(p.BorderColor),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(7),
                    ClipToBounds = true,
                    Margin = new Thickness(5)
                };

                var rootGrid = new Grid();
                rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
                rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                // 标题栏
                var titleBarBorder = new Border
                {
                    Background = menuBg,
                    CornerRadius = new CornerRadius(6, 6, 0, 0),
                    ClipToBounds = true
                };
                Grid.SetRow(titleBarBorder, 0);

                var titleBarGrid = new Grid { Background = Brushes.Transparent };
                titleBarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                titleBarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                titleBarGrid.MouseLeftButtonDown += (s, e) =>
                {
                    if (e.ChangedButton == MouseButton.Left && e.ClickCount == 1)
                    {
                        try { window.DragMove(); }
                        catch { /* 窗口状态不允许拖动时忽略 */ }
                    }
                };

                var titleTextBlock = new TextBlock
                {
                    Text = window.Title ?? string.Empty,
                    Foreground = menuFg,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 14,
                    Margin = new Thickness(12, 0, 0, 0)
                };
                Grid.SetColumn(titleTextBlock, 0);
                titleBarGrid.Children.Add(titleTextBlock);

                var closeButton = BuildCloseButton(menuFg, window);
                Grid.SetColumn(closeButton, 1);
                titleBarGrid.Children.Add(closeButton);

                titleBarBorder.Child = titleBarGrid;
                rootGrid.Children.Add(titleBarBorder);

                // 原内容塞入 Row 1，再递归上色
                if (originalContent != null)
                {
                    Grid.SetRow(originalContent, 1);
                    rootGrid.Children.Add(originalContent);
                    ApplyToVisualTree(originalContent, p);
                }

                mainBorder.Child = rootGrid;
                window.Content = mainBorder;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DialogTheming] 无边框主题应用失败: {ex.Message}");
            }
        }

        private static Button BuildCloseButton(SolidColorBrush fg, Window window)
        {
            var btn = new Button
            {
                Content = "✕",
                Width = 36,
                Height = 30,
                Background = Brushes.Transparent,
                Foreground = fg,
                BorderThickness = new Thickness(0),
                FontSize = 13,
                Cursor = Cursors.Hand,
                Focusable = false,
                Template = GetCloseButtonTemplate()
            };
            btn.Click += (s, e) => window.Close();
            return btn;
        }

        private static ControlTemplate _cachedCloseButtonTemplate;

        private static ControlTemplate GetCloseButtonTemplate()
        {
            if (_cachedCloseButtonTemplate != null)
                return _cachedCloseButtonTemplate;

            const string xaml = @"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Button'>
    <Border x:Name='border' Background='{TemplateBinding Background}' CornerRadius='0,6,0,0'>
        <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center' TextBlock.Foreground='{TemplateBinding Foreground}'/>
    </Border>
    <ControlTemplate.Triggers>
        <Trigger Property='IsMouseOver' Value='True'>
            <Setter TargetName='border' Property='Background' Value='#E81123'/>
            <Setter Property='Foreground' Value='White'/>
        </Trigger>
    </ControlTemplate.Triggers>
</ControlTemplate>";

            _cachedCloseButtonTemplate = (ControlTemplate)XamlReader.Parse(xaml);
            _cachedCloseButtonTemplate.Seal();
            return _cachedCloseButtonTemplate;
        }

        private static SolidColorBrush LoadMenuBg()
        {
            var s = Config.GetString("菜单背景色");
            var c = (Color)ColorConverter.ConvertFromString("#" + s);
            return new SolidColorBrush(c);
        }

        private static SolidColorBrush LoadMenuFg()
        {
            var s = Config.GetString("菜单字体色");
            var c = (Color)ColorConverter.ConvertFromString("#" + s);
            return new SolidColorBrush(c);
        }

        private static void ApplyToVisualTree(DependencyObject node, Palette p)
        {
            if (node == null) return;

            ApplyToElement(node, p);

            // 容器递归
            if (node is Panel panel)
            {
                foreach (UIElement child in panel.Children)
                    ApplyToVisualTree(child, p);
            }
            else if (node is ContentControl cc)
            {
                if (cc.Content is DependencyObject inner)
                    ApplyToVisualTree(inner, p);
            }
            else if (node is Decorator dec)
            {
                ApplyToVisualTree(dec.Child, p);
            }
            else if (node is ItemsControl ic)
            {
                foreach (var item in ic.Items)
                {
                    if (item is DependencyObject d)
                        ApplyToVisualTree(d, p);
                }
            }
        }

        private static void ApplyToElement(DependencyObject node, Palette p)
        {
            switch (node)
            {
                case Button btn:
                    ApplyButtonTheme(btn, p, false);
                    break;

                case TextBox tb:
                    tb.Background = p.InputBg;
                    tb.Foreground = p.InputFg;
                    tb.BorderBrush = new SolidColorBrush(p.BorderColor);
                    tb.CaretBrush = p.InputFg;
                    break;

                case PasswordBox pb:
                    pb.Background = p.InputBg;
                    pb.Foreground = p.InputFg;
                    pb.BorderBrush = new SolidColorBrush(p.BorderColor);
                    pb.CaretBrush = p.InputFg;
                    break;

                case CheckBox cb:
                    cb.Foreground = p.WindowFg;
                    break;

                case Label lbl:
                    lbl.Foreground = p.WindowFg;
                    break;

                case TextBlock tbk:
                    tbk.Foreground = p.WindowFg;
                    break;

                case ProgressBar pbar:
                    pbar.Background = p.InputBg;
                    pbar.Foreground = p.Accent;
                    pbar.BorderBrush = new SolidColorBrush(p.BorderColor);
                    break;

                case ComboBox combo:
                    combo.Background = p.InputBg;
                    combo.Foreground = p.InputFg;
                    combo.BorderBrush = new SolidColorBrush(p.BorderColor);
                    break;
            }
        }

        private struct Palette
        {
            public SolidColorBrush WindowBg;
            public SolidColorBrush WindowFg;
            public SolidColorBrush ButtonBg;
            public SolidColorBrush ButtonHoverBg;
            public SolidColorBrush ButtonPressedBg;
            public SolidColorBrush ButtonFg;
            public SolidColorBrush AccentHoverBg;
            public SolidColorBrush AccentPressedBg;
            public SolidColorBrush InputBg;   // 跟打区背景色
            public SolidColorBrush InputFg;   // 跟打区字体色
            public SolidColorBrush Accent;    // 标题栏进度条颜色
            public Color BorderColor;
        }

        private static void ApplyButtonTheme(Button button, Palette p, bool isAccent)
        {
            if (button == null)
                return;

            button.Background = isAccent ? p.Accent : p.ButtonBg;
            button.Foreground = isAccent ? Brushes.White : p.ButtonFg;
            button.BorderBrush = new SolidColorBrush(p.BorderColor);
            button.FocusVisualStyle = null;
            if (button.ReadLocalValue(Control.TemplateProperty) == DependencyProperty.UnsetValue)
                button.Template = GetDialogButtonTemplate(p, isAccent);
        }

        private static ControlTemplate GetDialogButtonTemplate(Palette p, bool isAccent)
        {
            string border = p.BorderColor.ToString();
            string hover = (isAccent ? p.AccentHoverBg : p.ButtonHoverBg).Color.ToString();
            string pressed = (isAccent ? p.AccentPressedBg : p.ButtonPressedBg).Color.ToString();

            string xaml = $@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Button'>
    <Border x:Name='border'
            Background='{{TemplateBinding Background}}'
            BorderBrush='{border}'
            BorderThickness='{{TemplateBinding BorderThickness}}'
            CornerRadius='4'
            Padding='{{TemplateBinding Padding}}'>
        <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center' TextBlock.Foreground='{{TemplateBinding Foreground}}' RecognizesAccessKey='True'/>
    </Border>
    <ControlTemplate.Triggers>
        <Trigger Property='IsMouseOver' Value='True'>
            <Setter TargetName='border' Property='Background' Value='{hover}'/>
        </Trigger>
        <Trigger Property='IsPressed' Value='True'>
            <Setter TargetName='border' Property='Background' Value='{pressed}'/>
        </Trigger>
        <Trigger Property='IsEnabled' Value='False'>
            <Setter Property='Opacity' Value='0.55'/>
        </Trigger>
    </ControlTemplate.Triggers>
</ControlTemplate>";

            return (ControlTemplate)XamlReader.Parse(xaml);
        }

        private static Palette LoadPalette()
        {
            var windowBgStr = Config.GetString("窗体背景色");
            var windowFgStr = Config.GetString("窗体字体色");
            var btnBgStr = Config.GetString("按钮背景色");
            var btnFgStr = Config.GetString("按钮字体色");
            var inputBgStr = Config.GetString("跟打区背景色");
            var inputFgStr = Config.GetString("跟打区字体色");
            var accentStr = Config.GetString("标题栏进度条颜色");

            var bgColor = (Color)ColorConverter.ConvertFromString("#" + windowBgStr);
            var fgColor = (Color)ColorConverter.ConvertFromString("#" + windowFgStr);
            var btnBgColor = (Color)ColorConverter.ConvertFromString("#" + btnBgStr);
            var btnFgColor = (Color)ColorConverter.ConvertFromString("#" + btnFgStr);
            var inputBgColor = (Color)ColorConverter.ConvertFromString("#" + inputBgStr);
            var inputFgColor = (Color)ColorConverter.ConvertFromString("#" + inputFgStr);
            var accentColor = (Color)ColorConverter.ConvertFromString("#" + accentStr);

            double brightness = (bgColor.R * 0.299 + bgColor.G * 0.587 + bgColor.B * 0.114) / 255.0;
            bool isDark = brightness < 0.5;
            var borderColor = isDark
                ? Color.FromRgb((byte)Math.Min(255, bgColor.R + 50), (byte)Math.Min(255, bgColor.G + 50), (byte)Math.Min(255, bgColor.B + 50))
                : Color.FromRgb((byte)Math.Max(0, bgColor.R - 30), (byte)Math.Max(0, bgColor.G - 30), (byte)Math.Max(0, bgColor.B - 30));

            return new Palette
            {
                WindowBg = new SolidColorBrush(bgColor),
                WindowFg = new SolidColorBrush(fgColor),
                ButtonBg = new SolidColorBrush(btnBgColor),
                ButtonHoverBg = new SolidColorBrush(ShiftColor(btnBgColor, 15)),
                ButtonPressedBg = new SolidColorBrush(ShiftColor(btnBgColor, 30)),
                ButtonFg = new SolidColorBrush(btnFgColor),
                AccentHoverBg = new SolidColorBrush(ShiftColor(accentColor, 15)),
                AccentPressedBg = new SolidColorBrush(ShiftColor(accentColor, 30)),
                InputBg = new SolidColorBrush(inputBgColor),
                InputFg = new SolidColorBrush(inputFgColor),
                Accent = new SolidColorBrush(accentColor),
                BorderColor = borderColor
            };
        }

        private static Color ShiftColor(Color color, int amount)
        {
            double brightness = (color.R * 0.299 + color.G * 0.587 + color.B * 0.114) / 255.0;
            int delta = brightness < 0.5 ? amount : -amount;
            return Color.FromRgb(
                ClampColor(color.R + delta),
                ClampColor(color.G + delta),
                ClampColor(color.B + delta));
        }

        private static byte ClampColor(int value)
        {
            if (value < 0) return 0;
            if (value > 255) return 255;
            return (byte)value;
        }
    }
}
