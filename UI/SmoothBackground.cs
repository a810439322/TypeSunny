using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace TypeSunny.UI
{
    public static class SmoothBackground
    {
        private static readonly DependencyProperty TargetOpacityProperty =
            DependencyProperty.RegisterAttached(
                "TargetOpacity",
                typeof(double),
                typeof(SmoothBackground),
                new PropertyMetadata(double.NaN));

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
            if (IsSameBackground(getBackground(), background))
                return;

            if (background == null || durationMs <= 0)
            {
                setBackground(background);
                return;
            }

            double targetOpacity = GetTargetOpacity(background);
            var animatedBrush = CreateAnimatedBrush(background, targetOpacity);
            if (animatedBrush == null)
            {
                setBackground(background);
                return;
            }

            setBackground(animatedBrush);
            animatedBrush.BeginAnimation(Brush.OpacityProperty, CreateFadeIn(durationMs, targetOpacity));
        }

        private static SolidColorBrush CreateAnimatedBrush(Brush background, double targetOpacity)
        {
            var solidBrush = background as SolidColorBrush;
            if (solidBrush == null)
                return null;

            var animatedBrush = new SolidColorBrush(solidBrush.Color)
            {
                Opacity = 0
            };
            animatedBrush.SetValue(TargetOpacityProperty, targetOpacity);
            return animatedBrush;
        }

        private static double GetTargetOpacity(Brush background)
        {
            return background != null ? background.Opacity : 1;
        }

        private static bool IsSameBackground(Brush current, Brush target)
        {
            if (ReferenceEquals(current, target))
                return true;

            if (current == null || target == null)
                return false;

            var currentSolid = current as SolidColorBrush;
            var targetSolid = target as SolidColorBrush;
            if (currentSolid == null || targetSolid == null)
                return false;

            if (currentSolid.Color != targetSolid.Color)
                return false;

            double targetOpacity = GetTargetOpacity(targetSolid);
            double animatedTargetOpacity = (double)currentSolid.GetValue(TargetOpacityProperty);
            if (!double.IsNaN(animatedTargetOpacity))
                return Math.Abs(animatedTargetOpacity - targetOpacity) < 0.001;

            return Math.Abs(currentSolid.Opacity - targetOpacity) < 0.001;
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
