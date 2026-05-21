using System;
using System.Windows.Media;
using TypeSunny.Utils;

namespace System.Windows.Media
{
    public class SolidColorBrush
    {
        public SolidColorBrush(Color color)
        {
            Color = color;
        }

        public Color Color { get; private set; }
    }

    public struct Color
    {
        public byte A;
        public byte R;
        public byte G;
        public byte B;

        public static Color FromRgb(byte r, byte g, byte b)
        {
            return FromArgb(255, r, g, b);
        }

        public static Color FromArgb(byte a, byte r, byte g, byte b)
        {
            return new Color { A = a, R = r, G = g, B = b };
        }
    }
}

namespace TypeSunny.Tests
{
    internal static class ThemeColorHelperTests
    {
        private static int _failures;

        private static int Main()
        {
            Run("black background gets a lighter border", BlackBackgroundGetsLighterBorder);
            Run("dark theme background keeps hue while lightening", DarkThemeBackgroundKeepsHueWhileLightening);
            Run("white background gets a darker border", WhiteBackgroundGetsDarkerBorder);
            Run("light theme background keeps hue while darkening", LightThemeBackgroundKeepsHueWhileDarkening);
            Run("brush factory uses the derived border color", BrushFactoryUsesDerivedBorderColor);
            Run("dark background lightens unreadable foreground", DarkBackgroundLightensUnreadableForeground);
            Run("readable foreground stays unchanged", ReadableForegroundStaysUnchanged);
            Run("light background darkens unreadable foreground", LightBackgroundDarkensUnreadableForeground);

            if (_failures == 0)
            {
                Console.WriteLine("All ThemeColorHelper tests passed.");
                return 0;
            }

            Console.WriteLine(_failures + " ThemeColorHelper test(s) failed.");
            return 1;
        }

        private static void BlackBackgroundGetsLighterBorder()
        {
            AssertColor(255, 40, 40, 40, ThemeColorHelper.GetSubtleBorderColor(Color.FromRgb(0, 0, 0)));
        }

        private static void DarkThemeBackgroundKeepsHueWhileLightening()
        {
            AssertColor(255, 85, 85, 85, ThemeColorHelper.GetSubtleBorderColor(Color.FromRgb(45, 45, 45)));
        }

        private static void WhiteBackgroundGetsDarkerBorder()
        {
            AssertColor(255, 215, 215, 215, ThemeColorHelper.GetSubtleBorderColor(Color.FromRgb(255, 255, 255)));
        }

        private static void LightThemeBackgroundKeepsHueWhileDarkening()
        {
            AssertColor(255, 207, 207, 207, ThemeColorHelper.GetSubtleBorderColor(Color.FromRgb(247, 247, 247)));
        }

        private static void BrushFactoryUsesDerivedBorderColor()
        {
            var brush = ThemeColorHelper.CreateSubtleBorderBrush(Color.FromRgb(45, 45, 45));
            AssertColor(255, 85, 85, 85, brush.Color);
        }

        private static void DarkBackgroundLightensUnreadableForeground()
        {
            Color adjusted = ThemeColorHelper.GetReadableForegroundColor(
                Color.FromRgb(0, 0, 0),
                Color.FromRgb(30, 30, 30));

            AssertTrue(adjusted.R > 0, "Expected black foreground to be lightened on a dark background.");
            AssertContrastAtLeast(4.5, adjusted, Color.FromRgb(30, 30, 30));
        }

        private static void ReadableForegroundStaysUnchanged()
        {
            Color adjusted = ThemeColorHelper.GetReadableForegroundColor(
                Color.FromRgb(211, 211, 211),
                Color.FromRgb(30, 30, 30));

            AssertColor(255, 211, 211, 211, adjusted);
        }

        private static void LightBackgroundDarkensUnreadableForeground()
        {
            Color adjusted = ThemeColorHelper.GetReadableForegroundColor(
                Color.FromRgb(220, 220, 220),
                Color.FromRgb(237, 237, 237));

            AssertTrue(adjusted.R < 220, "Expected pale foreground to be darkened on a light background.");
            AssertContrastAtLeast(4.5, adjusted, Color.FromRgb(237, 237, 237));
        }

        private static void Run(string name, Action test)
        {
            try
            {
                test();
                Console.WriteLine("PASS: " + name);
            }
            catch (Exception ex)
            {
                _failures++;
                Console.WriteLine("FAIL: " + name);
                Console.WriteLine(ex.Message);
            }
        }

        private static void AssertColor(byte expectedA, byte expectedR, byte expectedG, byte expectedB, Color actual)
        {
            if (actual.A != expectedA || actual.R != expectedR || actual.G != expectedG || actual.B != expectedB)
            {
                throw new Exception(string.Format(
                    "Expected #{0:X2}{1:X2}{2:X2}{3:X2}, got #{4:X2}{5:X2}{6:X2}{7:X2}.",
                    expectedA,
                    expectedR,
                    expectedG,
                    expectedB,
                    actual.A,
                    actual.R,
                    actual.G,
                    actual.B));
            }
        }

        private static void AssertContrastAtLeast(double minimum, Color foreground, Color background)
        {
            double ratio = GetContrastRatio(foreground, background);
            if (ratio < minimum)
                throw new Exception(string.Format("Expected contrast >= {0}, got {1:0.00}.", minimum, ratio));
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

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition)
                throw new Exception(message);
        }
    }
}
