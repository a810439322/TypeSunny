using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using TypeSunny.UI;

namespace TypeSunny.Tests
{
    internal static class SmoothBackgroundTests
    {
        private static int _failures;

        private static int Main()
        {
            Run("fade in applies target brush and opacity animation", FadeInAppliesTargetBrushAndOpacityAnimation);
            Run("fade in uses caret easing curve", FadeInUsesCaretEasingCurve);
            Run("fade in preserves target brush opacity", FadeInPreservesTargetBrushOpacity);
            Run("apply preserves existing element opacity", ApplyPreservesExistingElementOpacity);
            Run("apply does not clear external brush animation", ApplyDoesNotClearExternalBrushAnimation);
            Run("clear removes brush without leaving opacity dimmed", ClearRemovesBrushWithoutLeavingOpacityDimmed);

            if (_failures == 0)
            {
                Console.WriteLine("All SmoothBackground tests passed.");
                return 0;
            }

            Console.WriteLine(_failures + " SmoothBackground test(s) failed.");
            return 1;
        }

        private static void FadeInAppliesTargetBrushAndOpacityAnimation()
        {
            Window window;
            var border = CreateHostedBorder(out window);
            var brush = Brushes.Red;

            try
            {
                SmoothBackground.Apply(border, brush, 160);

                var animatedBrush = border.Background as SolidColorBrush;
                AssertTrue(animatedBrush != null, "Expected a SolidColorBrush background.");
                AssertTrue(animatedBrush.Color == Colors.Red, "Expected target color to be applied immediately.");
                AssertTrue(animatedBrush.Opacity < 1, "Expected background brush fade-in to start below full opacity.");

                PumpFor(220);
                AssertNear(1, animatedBrush.Opacity, 0.01, "brush opacity");
            }
            finally
            {
                window.Close();
            }
        }

        private static void FadeInUsesCaretEasingCurve()
        {
            var createFadeIn = typeof(SmoothBackground).GetMethod("CreateFadeIn", BindingFlags.NonPublic | BindingFlags.Static);
            AssertTrue(createFadeIn != null, "Expected SmoothBackground.CreateFadeIn to exist.");

            var animation = createFadeIn.Invoke(null, new object[] { 200, 1.0 }) as DoubleAnimation;
            AssertTrue(animation != null, "Expected CreateFadeIn to return a DoubleAnimation.");
            AssertNear(200, animation.Duration.TimeSpan.TotalMilliseconds, 0.01, "animation duration");

            var ease = animation.EasingFunction as CubicEase;
            AssertTrue(ease != null, "Expected background fade to use CubicEase like the caret.");
            AssertTrue(ease.EasingMode == EasingMode.EaseInOut, "Expected background fade to use EaseInOut like the caret.");
        }

        private static void ClearRemovesBrushWithoutLeavingOpacityDimmed()
        {
            var block = new TextBlock();

            SmoothBackground.Apply(block, Brushes.Green, 160);
            PumpFor(40);
            SmoothBackground.Apply(block, null, 160);

            AssertTrue(block.Background == null, "Expected clear to remove background.");
        }

        private static void FadeInPreservesTargetBrushOpacity()
        {
            Window window;
            var border = CreateHostedBorder(out window);
            var brush = new SolidColorBrush(Colors.Blue)
            {
                Opacity = 0.45
            };

            try
            {
                SmoothBackground.Apply(border, brush, 120);

                var animatedBrush = border.Background as SolidColorBrush;
                AssertTrue(animatedBrush != null, "Expected a SolidColorBrush background.");
                AssertTrue(animatedBrush.Opacity < 0.45, "Expected background brush fade-in to start below target opacity.");

                PumpFor(180);
                AssertNear(0.45, animatedBrush.Opacity, 0.01, "brush opacity");
            }
            finally
            {
                window.Close();
            }
        }

        private static void ApplyPreservesExistingElementOpacity()
        {
            var block = new TextBlock
            {
                Opacity = 0.35
            };

            SmoothBackground.Apply(block, Brushes.Red, 0);

            AssertNear(0.35, block.Opacity, 0.01, "element opacity");
        }

        private static void ApplyDoesNotClearExternalBrushAnimation()
        {
            var block = new TextBlock();
            var externalBrush = new SolidColorBrush(Colors.Green);
            block.Background = externalBrush;
            externalBrush.BeginAnimation(Brush.OpacityProperty, new DoubleAnimation(
                0.2,
                0.8,
                new Duration(TimeSpan.FromMilliseconds(300))));

            PumpFor(30);
            AssertTrue(externalBrush.HasAnimatedProperties, "Expected external brush animation to be running before applying a new background.");
            SmoothBackground.Apply(block, Brushes.Red, 120);

            AssertTrue(externalBrush.HasAnimatedProperties, "Expected external brush animation to remain untouched.");
        }

        private static Border CreateHostedBorder(out Window window)
        {
            var border = new Border
            {
                Width = 80,
                Height = 30
            };

            window = new Window
            {
                Width = 120,
                Height = 80,
                Content = border,
                ShowActivated = false,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                Left = -10000,
                Top = -10000
            };

            window.Show();
            border.UpdateLayout();
            PumpFor(20);
            return border;
        }

        private static void PumpFor(int milliseconds)
        {
            var frame = new DispatcherFrame();
            var timer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(milliseconds)
            };

            timer.Tick += (sender, args) =>
            {
                timer.Stop();
                frame.Continue = false;
            };

            timer.Start();
            Dispatcher.PushFrame(frame);
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

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition)
                throw new Exception(message);
        }

        private static void AssertNear(double expected, double actual, double tolerance, string name)
        {
            if (Math.Abs(expected - actual) > tolerance)
                throw new Exception("Expected " + name + " ~= " + expected + ", got " + actual + ".");
        }
    }
}
