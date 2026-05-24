using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
                        if (button == null)
                            continue;
                        button.Background = p.ButtonBg;
                        button.Foreground = p.ButtonFg;
                    }
                }

                if (accentButton != null)
                {
                    accentButton.Background = p.Accent;
                    accentButton.Foreground = new SolidColorBrush(System.Windows.Media.Colors.White);
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
        /// 主按钮（IsDefault=true）使用 accent 颜色。
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
                    btn.Background = btn.IsDefault ? p.Accent : p.ButtonBg;
                    btn.Foreground = btn.IsDefault ? new SolidColorBrush(System.Windows.Media.Colors.White) : p.ButtonFg;
                    btn.BorderBrush = new SolidColorBrush(p.BorderColor);
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
            public SolidColorBrush ButtonFg;
            public SolidColorBrush InputBg;   // 跟打区背景色
            public SolidColorBrush InputFg;   // 跟打区字体色
            public SolidColorBrush Accent;    // 标题栏进度条颜色
            public Color BorderColor;
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
                ButtonFg = new SolidColorBrush(btnFgColor),
                InputBg = new SolidColorBrush(inputBgColor),
                InputFg = new SolidColorBrush(inputFgColor),
                Accent = new SolidColorBrush(accentColor),
                BorderColor = borderColor
            };
        }
    }
}
