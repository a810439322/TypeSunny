using System;
using System.Diagnostics;
using System.Windows.Controls;
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
                var windowBgStr = Config.GetString("窗体背景色");
                var windowFgStr = Config.GetString("窗体字体色");
                var btnBgStr = Config.GetString("按钮背景色");
                var btnFgStr = Config.GetString("按钮字体色");

                var bgColor = (Color)ColorConverter.ConvertFromString("#" + windowBgStr);
                var fgColor = (Color)ColorConverter.ConvertFromString("#" + windowFgStr);
                var btnBgColor = (Color)ColorConverter.ConvertFromString("#" + btnBgStr);
                var btnFgColor = (Color)ColorConverter.ConvertFromString("#" + btnFgStr);

                var bgBrush = new SolidColorBrush(bgColor);
                var fgBrush = new SolidColorBrush(fgColor);
                var btnBgBrush = new SolidColorBrush(btnBgColor);
                var btnFgBrush = new SolidColorBrush(btnFgColor);

                if (mainBorder != null)
                    mainBorder.Background = bgBrush;

                double brightness = (bgColor.R * 0.299 + bgColor.G * 0.587 + bgColor.B * 0.114) / 255.0;
                bool isDark = brightness < 0.5;
                var borderColor = isDark
                    ? Color.FromRgb((byte)Math.Min(255, bgColor.R + 50), (byte)Math.Min(255, bgColor.G + 50), (byte)Math.Min(255, bgColor.B + 50))
                    : Color.FromRgb((byte)Math.Max(0, bgColor.R - 30), (byte)Math.Max(0, bgColor.G - 30), (byte)Math.Max(0, bgColor.B - 30));
                if (mainBorder != null)
                    mainBorder.BorderBrush = new SolidColorBrush(borderColor);

                if (foregroundTexts != null)
                {
                    foreach (var text in foregroundTexts)
                    {
                        if (text != null)
                            text.Foreground = fgBrush;
                    }
                }

                if (normalButtons != null)
                {
                    foreach (var button in normalButtons)
                    {
                        if (button == null)
                            continue;
                        button.Background = btnBgBrush;
                        button.Foreground = btnFgBrush;
                    }
                }

                if (accentButton != null)
                {
                    var accentColor = (Color)ColorConverter.ConvertFromString("#" + Config.GetString("标题栏进度条颜色"));
                    accentButton.Background = new SolidColorBrush(accentColor);
                    accentButton.Foreground = new SolidColorBrush(System.Windows.Media.Colors.White);
                    if (progressBar != null)
                        progressBar.Foreground = new SolidColorBrush(accentColor);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DialogTheming] 主题应用失败: {ex.Message}");
            }
        }
    }
}
