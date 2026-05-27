using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace TypeSunny.UI
{
    public static class SmoothBackground
    {
        public static void Apply(Control control, Brush background, int durationMs)
        {
            if (control == null)
                return;

            ApplyCore(control, () => control.Background, value => control.Background = value, background, durationMs);
        }

        public static void Apply(Border border, Brush background, int durationMs)
        {
            if (border == null)
                return;

            ApplyCore(border, () => border.Background, value => border.Background = value, background, durationMs);
        }

        public static void Apply(TextBlock textBlock, Brush background, int durationMs)
        {
            if (textBlock == null)
                return;

            ApplyCore(textBlock, () => textBlock.Background, value => textBlock.Background = value, background, durationMs);
        }

        private static void ApplyCore(UIElement element, Func<Brush> getBackground, Action<Brush> setBackground, Brush background, int durationMs)
        {
            if (background == null || durationMs <= 0)
            {
                setBackground(background);
                return;
            }

            double targetOpacity = GetTargetOpacity(background);
            var animatedBrush = CreateAnimatedBrush(background);
            if (animatedBrush == null)
            {
                setBackground(background);
                return;
            }

            setBackground(animatedBrush);
            animatedBrush.BeginAnimation(Brush.OpacityProperty, CreateFadeIn(durationMs, targetOpacity));
        }

        private static SolidColorBrush CreateAnimatedBrush(Brush background)
        {
            var solidBrush = background as SolidColorBrush;
            if (solidBrush == null)
                return null;

            return new SolidColorBrush(solidBrush.Color)
            {
                Opacity = 0
            };
        }

        private static double GetTargetOpacity(Brush background)
        {
            return background != null ? background.Opacity : 1;
        }

        private static DoubleAnimation CreateFadeIn(int durationMs, double targetOpacity)
        {
            return new DoubleAnimation(0, targetOpacity, new Duration(TimeSpan.FromMilliseconds(durationMs)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
                FillBehavior = FillBehavior.HoldEnd
            };
        }
    }
}
