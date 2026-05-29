using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using TypeSunny.Utils;

namespace TypeSunny
{
    internal static class Config
    {
        internal static string SmoothLineWrap = "是";

        public static bool GetBool(string key)
        {
            return key == "平滑换行" && SmoothLineWrap == "是";
        }
    }
}

namespace TypeSunny.Tests
{
    internal static class SmoothScrollTests
    {
        private static int _failures;

        private static int Main()
        {
            Run("animation reaches target offset", AnimationReachesTargetOffset);
            Run("legacy disabled config still animates", LegacyDisabledConfigStillAnimates);
            Run("second animation continues from current visual offset", SecondAnimationContinuesFromCurrentVisualOffset);

            if (_failures == 0)
            {
                Console.WriteLine("All SmoothScroll tests passed.");
                return 0;
            }

            Console.WriteLine(_failures + " SmoothScroll test(s) failed.");
            return 1;
        }

        private static void AnimationReachesTargetOffset()
        {
            SetConfig("平滑换行", "是");
            Window window;
            ScrollViewer sv = CreateHostedScrollViewer(out window);

            try
            {
                bool started = false;
                bool completed = false;

                bool scrolled = SmoothScrollHelper.AnimateScrollTo(
                    sv,
                    120,
                    125,
                    new CubicEase { EasingMode = EasingMode.EaseInOut },
                    () => started = true,
                    () => completed = true);

                AssertTrue(scrolled, "Expected AnimateScrollTo to report that it scrolled.");
                AssertTrue(started, "Expected started callback.");
                PumpFor(180);
                AssertTrue(completed, "Expected completed callback.");
                AssertNear(120, sv.VerticalOffset, 0.5, "VerticalOffset");
            }
            finally
            {
                window.Close();
            }
        }

        private static void LegacyDisabledConfigStillAnimates()
        {
            SetConfig("平滑换行", "否");
            Window window;
            ScrollViewer sv = CreateHostedScrollViewer(out window);

            try
            {
                bool completed = false;
                bool scrolled = SmoothScrollHelper.AnimateScrollTo(
                    sv,
                    90,
                    125,
                    new CubicEase { EasingMode = EasingMode.EaseInOut },
                    null,
                    () => completed = true);

                AssertTrue(scrolled, "Expected disabled smooth scroll path to still perform the scroll.");
                AssertTrue(!completed, "Expected legacy disabled value to keep the animated path.");
                PumpFor(20);
                AssertTrue(sv.VerticalOffset > 0 && sv.VerticalOffset < 90, "Expected smooth scroll animation to be in flight.");
                PumpFor(180);
                AssertTrue(completed, "Expected animated path to complete.");
                AssertNear(90, sv.VerticalOffset, 0.5, "VerticalOffset");
            }
            finally
            {
                SetConfig("平滑换行", "是");
                window.Close();
            }
        }

        private static void SecondAnimationContinuesFromCurrentVisualOffset()
        {
            SetConfig("平滑换行", "是");
            Window window;
            ScrollViewer sv = CreateHostedScrollViewer(out window);

            try
            {
                SmoothScrollHelper.AnimateScrollTo(
                    sv,
                    200,
                    125,
                    new CubicEase { EasingMode = EasingMode.EaseInOut });

                PumpFor(60);
                double inFlightOffset = sv.VerticalOffset;
                AssertTrue(inFlightOffset > 0 && inFlightOffset < 200, "Expected the first scroll animation to be in flight.");

                SmoothScrollHelper.AnimateScrollTo(
                    sv,
                    320,
                    125,
                    new CubicEase { EasingMode = EasingMode.EaseInOut });

                double pinnedOffset = SmoothScrollHelper.GetVerticalOffset(sv);
                AssertTrue(pinnedOffset > 0, "Expected the second animation to pin the current visual offset instead of jumping to base.");

                PumpFor(180);
                AssertNear(320, sv.VerticalOffset, 0.5, "VerticalOffset");
            }
            finally
            {
                SetConfig("平滑换行", "是");
                window.Close();
            }
        }

        private static ScrollViewer CreateHostedScrollViewer(out Window window)
        {
            var stack = new StackPanel();
            for (int i = 0; i < 40; i++)
            {
                stack.Children.Add(new TextBlock
                {
                    Text = "Line " + i,
                    Height = 20
                });
            }

            var sv = new ScrollViewer
            {
                Width = 200,
                Height = 100,
                Content = stack
            };

            window = new Window
            {
                Width = 220,
                Height = 140,
                Content = sv,
                ShowActivated = false,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                Left = -10000,
                Top = -10000
            };

            window.Show();
            sv.UpdateLayout();
            PumpFor(20);
            return sv;
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

        private static void SetConfig(string key, string value)
        {
            if (key == "平滑换行")
                Config.SmoothLineWrap = value;
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
