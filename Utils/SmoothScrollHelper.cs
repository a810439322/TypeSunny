using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using TypeSunny;

namespace TypeSunny.Utils
{
    public static class SmoothScrollHelper
    {
        public static readonly DependencyProperty VerticalOffsetProperty =
            DependencyProperty.RegisterAttached(
                "VerticalOffset",
                typeof(double),
                typeof(SmoothScrollHelper),
                new PropertyMetadata(0.0, OnVerticalOffsetChanged));

        public static double GetVerticalOffset(DependencyObject obj)
        {
            return (double)obj.GetValue(VerticalOffsetProperty);
        }

        public static void SetVerticalOffset(DependencyObject obj, double value)
        {
            obj.SetValue(VerticalOffsetProperty, value);
        }

        public static bool AnimateScrollTo(
            ScrollViewer sv,
            double target,
            int durationMs,
            EasingFunctionBase ease,
            Action started = null,
            Action completed = null)
        {
            if (sv == null)
                return false;

            if (durationMs <= 0)
            {
                sv.BeginAnimation(VerticalOffsetProperty, null);
                sv.SetValue(VerticalOffsetProperty, target);
                if (started != null)
                    started();
                sv.ScrollToVerticalOffset(target);
                if (completed != null)
                    completed();
                return true;
            }

            double current = sv.VerticalOffset;
            sv.BeginAnimation(VerticalOffsetProperty, null);
            sv.SetValue(VerticalOffsetProperty, current);

            var animation = new DoubleAnimation(current, target, new Duration(TimeSpan.FromMilliseconds(durationMs)))
            {
                EasingFunction = ease,
                FillBehavior = FillBehavior.Stop
            };

            animation.Completed += delegate
            {
                sv.BeginAnimation(VerticalOffsetProperty, null);
                sv.SetValue(VerticalOffsetProperty, target);
                sv.ScrollToVerticalOffset(target);
                if (completed != null)
                    completed();
            };

            if (started != null)
                started();
            sv.BeginAnimation(VerticalOffsetProperty, animation);
            return true;
        }

        private static void OnVerticalOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var scrollViewer = d as ScrollViewer;
            if (scrollViewer == null)
                return;

            scrollViewer.ScrollToVerticalOffset((double)e.NewValue);
        }
    }
}
