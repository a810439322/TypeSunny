using System;
using System.Windows.Media;

namespace TypeSunny.Utils
{
    internal static partial class ThemeColorHelper
    {
        private const int SubtleBorderDelta = 40;
        private const double DarkBrightnessThreshold = 0.5;
        private const double MinimumReadableContrastRatio = 4.5;

        private const int MenuSeparatorLightenDelta = 50;
        private const int MenuSeparatorDarkenDelta = 40;
        private const int MenuItemHoverLightenDelta = 12;
        private const int MenuItemHoverDarkenDelta = 12;
        private const int DragHandleLightenTarget = 170;
        private const int DragHandleDarkenTarget = 100;

        public static Color GetSubtleBorderColor(Color backgroundColor)
        {
            int delta = IsDark(backgroundColor) ? SubtleBorderDelta : -SubtleBorderDelta;

            return Color.FromArgb(
                backgroundColor.A,
                ClampToByte(backgroundColor.R + delta),
                ClampToByte(backgroundColor.G + delta),
                ClampToByte(backgroundColor.B + delta));
        }

        public static SolidColorBrush CreateSubtleBorderBrush(Color backgroundColor)
        {
            return new SolidColorBrush(GetSubtleBorderColor(backgroundColor));
        }

        public static SolidColorBrush CreateSubtleBorderBrush(SolidColorBrush backgroundBrush)
        {
            if (backgroundBrush == null)
                return new SolidColorBrush(Color.FromRgb(173, 173, 173));

            return CreateSubtleBorderBrush(backgroundBrush.Color);
        }

        public static Color GetReadableForegroundColor(Color foregroundColor, Color backgroundColor)
        {
            if (GetContrastRatio(foregroundColor, backgroundColor) >= MinimumReadableContrastRatio)
                return foregroundColor;

            Color targetColor = IsDark(backgroundColor)
                ? Color.FromArgb(foregroundColor.A, 255, 255, 255)
                : Color.FromArgb(foregroundColor.A, 0, 0, 0);

            for (double amount = 0.05; amount <= 1.0001; amount += 0.05)
            {
                Color candidate = Blend(foregroundColor, targetColor, amount);
                if (GetContrastRatio(candidate, backgroundColor) >= MinimumReadableContrastRatio)
                    return candidate;
            }

            return targetColor;
        }

        public static SolidColorBrush CreateReadableForegroundBrush(
            SolidColorBrush foregroundBrush,
            SolidColorBrush backgroundBrush)
        {
            Color foregroundColor = foregroundBrush != null
                ? foregroundBrush.Color
                : Color.FromRgb(0, 0, 0);
            if (backgroundBrush == null)
                return new SolidColorBrush(foregroundColor);

            return new SolidColorBrush(GetReadableForegroundColor(foregroundColor, backgroundBrush.Color));
        }

        public static bool IsDark(Color color)
        {
            return GetPerceivedBrightness(color) < DarkBrightnessThreshold;
        }

        private static Color Blend(Color source, Color target, double amount)
        {
            return Color.FromArgb(
                source.A,
                BlendChannel(source.R, target.R, amount),
                BlendChannel(source.G, target.G, amount),
                BlendChannel(source.B, target.B, amount));
        }

        private static byte BlendChannel(byte source, byte target, double amount)
        {
            return (byte)Math.Round(source + (target - source) * amount);
        }

        private static double GetContrastRatio(Color first, Color second)
        {
            double firstLuminance = GetRelativeLuminance(first);
            double secondLuminance = GetRelativeLuminance(second);
            double lighter = Math.Max(firstLuminance, secondLuminance);
            double darker = Math.Min(firstLuminance, secondLuminance);

            return (lighter + 0.05) / (darker + 0.05);
        }

        private static double GetRelativeLuminance(Color color)
        {
            return 0.2126 * Linearize(color.R)
                   + 0.7152 * Linearize(color.G)
                   + 0.0722 * Linearize(color.B);
        }

        private static double Linearize(byte value)
        {
            double channel = value / 255.0;
            return channel <= 0.03928
                ? channel / 12.92
                : Math.Pow((channel + 0.055) / 1.055, 2.4);
        }

        private static double GetPerceivedBrightness(Color color)
        {
            return (color.R * 0.299 + color.G * 0.587 + color.B * 0.114) / 255.0;
        }

        private static byte ClampToByte(int value)
        {
            return (byte)Math.Max(0, Math.Min(255, value));
        }

        public static Color GetMenuSeparatorColor(Color menuBackground)
        {
            int delta = IsDark(menuBackground) ? MenuSeparatorLightenDelta : -MenuSeparatorDarkenDelta;
            return Color.FromArgb(
                menuBackground.A,
                ClampToByte(menuBackground.R + delta),
                ClampToByte(menuBackground.G + delta),
                ClampToByte(menuBackground.B + delta));
        }

        public static Color GetMenuItemHoverBackgroundColor(Color menuBackground)
        {
            int delta = IsDark(menuBackground) ? MenuItemHoverLightenDelta : -MenuItemHoverDarkenDelta;
            return Color.FromArgb(
                menuBackground.A,
                ClampToByte(menuBackground.R + delta),
                ClampToByte(menuBackground.G + delta),
                ClampToByte(menuBackground.B + delta));
        }

        public static Color GetSecondaryTextColor(Color background)
        {
            byte channel = IsDark(background) ? (byte)DragHandleLightenTarget : (byte)DragHandleDarkenTarget;
            return Color.FromArgb(255, channel, channel, channel);
        }

        public static Color GetReadableHighlightColor(Color background)
        {
            Color baseHighlight = IsDark(background)
                ? Color.FromRgb(140, 200, 255)
                : Color.FromRgb(60, 120, 180);
            return GetReadableForegroundColor(baseHighlight, background);
        }
    }
}
