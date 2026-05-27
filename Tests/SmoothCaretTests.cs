using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Reflection;
using TypeSunny.UI;

namespace TypeSunny
{
    internal static class Config
    {
            public static readonly System.Collections.Generic.Dictionary<string, string> dicts =
                new System.Collections.Generic.Dictionary<string, string>
            {
                { "平滑光标", "是" },
                { "平滑光标模式", "动态" },
                { "平滑光标固定时长", "200" },
                { "平滑光标快", "140" },
                { "平滑光标中", "200" },
                { "平滑光标慢", "280" }
            };

        public static string GetString(string key)
        {
            string value;
            return dicts.TryGetValue(key, out value) ? value : "";
        }
    }
}

namespace TypeSunny.Tests
{
    internal static class SmoothCaretTests
    {
        private static int _failures;

        private static int Main()
        {
            Run("switch disables smooth caret", SwitchDisablesSmoothCaret);
            Run("fixed mode uses fixed duration", FixedModeUsesFixedDuration);
            Run("dynamic timing adapts to typing cadence", DynamicTimingAdaptsToTypingCadence);
            Run("dynamic timing is smoothed between samples", DynamicTimingIsSmoothedBetweenSamples);
            Run("background timing matches caret timing", BackgroundTimingMatchesCaretTiming);
            Run("dynamic timing targets smooth but faster range", DynamicTimingTargetsSmoothButFasterRange);
            Run("duration values can be configured", DurationValuesCanBeConfigured);
            Run("unknown mode values fall back to dynamic", UnknownModeValuesFallBackToDynamic);
            Run("set position applies immediately", SetPositionAppliesImmediately);
            Run("animate position uses render transform", AnimatePositionUsesRenderTransform);
            Run("animate position reaches target", AnimatePositionReachesTarget);
            Run("second animation continues from current visual value", SecondAnimationContinuesFromCurrentVisualValue);
            Run("track position preserves in-flight animation", TrackPositionPreservesInFlightAnimation);
            Run("track position follows target without in-flight animation", TrackPositionFollowsTargetWithoutInFlightAnimation);
            Run("stop blinking clears opacity animation", StopBlinkingClearsOpacityAnimation);

            if (_failures == 0)
            {
                Console.WriteLine("All SmoothCaret tests passed.");
                return 0;
            }

            Console.WriteLine(_failures + " SmoothCaret test(s) failed.");
            return 1;
        }

        private static void SwitchDisablesSmoothCaret()
        {
            SetConfig("平滑光标", "否");

            try
            {
                var timing = new SmoothMotionTiming();
                timing.RecordInput(TimeSpan.FromMilliseconds(0));
                timing.RecordInput(TimeSpan.FromMilliseconds(90));

                AssertEqual(0, timing.GetCurrentDurationMilliseconds());
            }
            finally
            {
                SetConfig("平滑光标", "是");
            }
        }

        private static void FixedModeUsesFixedDuration()
        {
            SetConfig("平滑光标模式", "固定");
            SetConfig("平滑光标固定时长", "180");

            try
            {
                var timing = new SmoothMotionTiming();
                timing.RecordInput(TimeSpan.FromMilliseconds(0));
                timing.RecordInput(TimeSpan.FromMilliseconds(90));

                AssertEqual(180, timing.GetCurrentDurationMilliseconds());
                AssertEqual(180, SmoothCaret.GetDurationMilliseconds());
            }
            finally
            {
                SetConfig("平滑光标模式", "动态");
                SetConfig("平滑光标固定时长", "200");
            }
        }

        private static void DynamicTimingAdaptsToTypingCadence()
        {
            var timing = new SmoothMotionTiming();

            timing.RecordInput(TimeSpan.FromMilliseconds(0));
            timing.RecordInput(TimeSpan.FromMilliseconds(90));
            int fastDuration = timing.GetDynamicDurationMilliseconds();

            timing.Reset();
            timing.RecordInput(TimeSpan.FromMilliseconds(0));
            timing.RecordInput(TimeSpan.FromMilliseconds(600));
            int slowDuration = timing.GetDynamicDurationMilliseconds();

            AssertTrue(fastDuration < slowDuration, "Expected faster typing cadence to use a shorter animation.");
            AssertTrue(fastDuration >= 120 && fastDuration <= 160, "Expected fast duration to stay near the configured fast value.");
            AssertTrue(slowDuration >= 260 && slowDuration <= 300, "Expected slow duration to stay near the configured slow value.");
        }

        private static void DynamicTimingIsSmoothedBetweenSamples()
        {
            var timing = new SmoothMotionTiming();

            timing.RecordInput(TimeSpan.FromMilliseconds(0));
            timing.RecordInput(TimeSpan.FromMilliseconds(600));
            int slowDuration = timing.GetDynamicDurationMilliseconds();
            timing.RecordInput(TimeSpan.FromMilliseconds(660));
            int smoothedFastDuration = timing.GetDynamicDurationMilliseconds();

            AssertTrue(smoothedFastDuration < slowDuration, "Expected a fast sample to reduce the duration.");
            AssertTrue(smoothedFastDuration > 180, "Expected smoothing to avoid snapping directly to the fastest duration.");
        }

        private static void DynamicTimingTargetsSmoothButFasterRange()
        {
            var timing = new SmoothMotionTiming();

            timing.RecordInput(TimeSpan.FromMilliseconds(0));
            timing.RecordInput(TimeSpan.FromMilliseconds(250));
            int duration = timing.GetDynamicDurationMilliseconds();

            AssertTrue(duration >= 180 && duration <= 230, "Expected normal typing cadence to stay near the configured medium value.");
        }

        private static void BackgroundTimingMatchesCaretTiming()
        {
            var timing = new SmoothMotionTiming();

            AssertEqual(timing.GetCurrentDurationMilliseconds(), timing.GetBackgroundDurationMilliseconds());

            timing.RecordInput(TimeSpan.FromMilliseconds(0));
            timing.RecordInput(TimeSpan.FromMilliseconds(90));
            AssertEqual(timing.GetCurrentDurationMilliseconds(), timing.GetBackgroundDurationMilliseconds());

            timing.Reset();
            timing.RecordInput(TimeSpan.FromMilliseconds(0));
            timing.RecordInput(TimeSpan.FromMilliseconds(600));
            AssertEqual(timing.GetCurrentDurationMilliseconds(), timing.GetBackgroundDurationMilliseconds());
        }

        private static void UnknownModeValuesFallBackToDynamic()
        {
            SetConfig("平滑光标模式", "medium");

            try
            {
                var timing = new SmoothMotionTiming();
                timing.RecordInput(TimeSpan.FromMilliseconds(0));
                timing.RecordInput(TimeSpan.FromMilliseconds(90));

                AssertEqual(140, timing.GetCurrentDurationMilliseconds());
            }
            finally
            {
                SetConfig("平滑光标模式", "动态");
            }
        }

        private static void DurationValuesCanBeConfigured()
        {
            SetConfig("平滑光标快", "100");
            SetConfig("平滑光标中", "180");
            SetConfig("平滑光标慢", "260");

            try
            {
                SetConfig("平滑光标模式", "固定");
                SetConfig("平滑光标固定时长", "180");
                AssertEqual(180, SmoothCaret.GetDurationMilliseconds());
                SetConfig("平滑光标模式", "动态");

                var timing = new SmoothMotionTiming();
                timing.RecordInput(TimeSpan.FromMilliseconds(0));
                timing.RecordInput(TimeSpan.FromMilliseconds(90));
                AssertEqual(100, timing.GetDynamicDurationMilliseconds());

                timing.Reset();
                timing.RecordInput(TimeSpan.FromMilliseconds(0));
                timing.RecordInput(TimeSpan.FromMilliseconds(600));
                AssertEqual(260, timing.GetDynamicDurationMilliseconds());
            }
            finally
            {
                SetConfig("平滑光标模式", "动态");
                SetConfig("平滑光标固定时长", "200");
                SetConfig("平滑光标快", "140");
                SetConfig("平滑光标中", "200");
                SetConfig("平滑光标慢", "280");
            }
        }

        private static void SetPositionAppliesImmediately()
        {
            var caret = new SmoothCaret(30, null, startBlinking: false);

            caret.SetPosition(12, 34, 56);

            AssertNear(12, Canvas.GetLeft(caret.Element), 0.01, "left");
            AssertNear(34, Canvas.GetTop(caret.Element), 0.01, "top");
            AssertNear(56, caret.Element.Height, 0.01, "height");
            AssertNear(0, GetTransformX(caret), 0.01, "transform x");
            AssertNear(0, GetTransformY(caret), 0.01, "transform y");
        }

        private static void AnimatePositionUsesRenderTransform()
        {
            Window window;
            SmoothCaret caret = CreateHostedCaret(out window);

            try
            {
                caret.SetPosition(0, 0, 30);
                caret.AnimatePosition(100, 50, 30);

                AssertTrue(caret.Element.RenderTransform is TranslateTransform, "Expected SmoothCaret to animate a TranslateTransform.");
                AssertNear(100, Canvas.GetLeft(caret.Element), 0.01, "base left");
                AssertNear(50, Canvas.GetTop(caret.Element), 0.01, "base top");
                AssertTrue(GetTransformX(caret) < 0, "Expected transform x to carry the in-flight visual offset.");
                AssertTrue(GetTransformY(caret) < 0, "Expected transform y to carry the in-flight visual offset.");
            }
            finally
            {
                window.Close();
            }
        }

        private static void AnimatePositionReachesTarget()
        {
            Window window;
            SmoothCaret caret = CreateHostedCaret(out window);

            try
            {
                caret.SetPosition(0, 0, 30);
                caret.AnimatePosition(100, 200, 40);
                PumpFor(650);

                AssertNear(100, GetVisualLeft(caret), 0.5, "left");
                AssertNear(200, GetVisualTop(caret), 0.5, "top");
                AssertNear(40, caret.Element.Height, 0.5, "height");
            }
            finally
            {
                window.Close();
            }
        }

        private static void SecondAnimationContinuesFromCurrentVisualValue()
        {
            Window window;
            SmoothCaret caret = CreateHostedCaret(out window);

            try
            {
                caret.SetPosition(0, 0, 30);

                caret.AnimatePosition(100, 0, 30);
                PumpFor(60);
                double inFlightLeft = GetVisualLeft(caret);
                AssertTrue(inFlightLeft > 0 && inFlightLeft < 100, "Expected the first animation to be in flight.");

                caret.AnimatePosition(200, 0, 30);
                double pinnedLeft = GetVisualLeft(caret);
                AssertTrue(pinnedLeft > 0, "Expected the second animation to pin the current visual value instead of jumping to base.");

                PumpFor(650);
                AssertNear(200, GetVisualLeft(caret), 0.5, "left");
            }
            finally
            {
                window.Close();
            }
        }

        private static void TrackPositionPreservesInFlightAnimation()
        {
            Window window;
            SmoothCaret caret = CreateHostedCaret(out window);

            try
            {
                caret.SetPosition(0, 0, 30);
                caret.AnimatePosition(100, 0, 30);
                PumpFor(60);

                AssertTrue(GetVisualLeft(caret) > 0 && GetVisualLeft(caret) < 100, "Expected the animation to be in flight.");

                caret.TrackPosition(140, 0, 30);

                AssertNear(140, Canvas.GetLeft(caret.Element), 0.01, "base left");
                AssertTrue(GetTransformX(caret) < 0, "Expected tracking to preserve the in-flight transform animation.");

                PumpFor(650);
                AssertNear(140, GetVisualLeft(caret), 0.5, "left");
            }
            finally
            {
                window.Close();
            }
        }

        private static void TrackPositionFollowsTargetWithoutInFlightAnimation()
        {
            var caret = new SmoothCaret(30, null, startBlinking: false);

            caret.SetPosition(20, 30, 30);
            caret.TrackPosition(90, 110, 40);

            AssertNear(90, GetVisualLeft(caret), 0.01, "left");
            AssertNear(110, GetVisualTop(caret), 0.01, "top");
            AssertNear(40, caret.Element.Height, 0.01, "height");
        }

        private static void StopBlinkingClearsOpacityAnimation()
        {
            var caret = new SmoothCaret(30, null, startBlinking: true);

            caret.StopBlinking();

            AssertNear(1, caret.Element.Opacity, 0.01, "opacity");
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

        private static SmoothCaret CreateHostedCaret(out Window window)
        {
            var canvas = new Canvas
            {
                Width = 300,
                Height = 300
            };

            window = new Window
            {
                Width = 320,
                Height = 320,
                Content = canvas,
                ShowActivated = false,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                Left = -10000,
                Top = -10000
            };

            var caret = new SmoothCaret(canvas, 0, 0, 30, null, startBlinking: false);
            window.Show();
            canvas.UpdateLayout();
            PumpFor(20);
            return caret;
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

        private static void AssertEqual(int expected, int actual)
        {
            if (expected != actual)
                throw new Exception("Expected " + expected + ", got " + actual + ".");
        }

        private static void AssertNear(double expected, double actual, double tolerance, string name)
        {
            if (Math.Abs(expected - actual) > tolerance)
                throw new Exception("Expected " + name + " ~= " + expected + ", got " + actual + ".");
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition)
                throw new Exception(message);
        }

        private static void SetConfig(string key, string value)
        {
            Type configType = Type.GetType("TypeSunny.Config");
            if (configType == null)
                return;

            var dictField = configType.GetField("dicts", BindingFlags.Static | BindingFlags.Public);
            var dict = dictField == null ? null : dictField.GetValue(null) as System.Collections.Generic.Dictionary<string, string>;
            if (dict != null)
                dict[key] = value;
        }

        private static double GetVisualLeft(SmoothCaret caret)
        {
            return Canvas.GetLeft(caret.Element) + GetTransformX(caret);
        }

        private static double GetVisualTop(SmoothCaret caret)
        {
            return Canvas.GetTop(caret.Element) + GetTransformY(caret);
        }

        private static double GetTransformX(SmoothCaret caret)
        {
            var transform = caret.Element.RenderTransform as TranslateTransform;
            return transform == null ? 0 : transform.X;
        }

        private static double GetTransformY(SmoothCaret caret)
        {
            var transform = caret.Element.RenderTransform as TranslateTransform;
            return transform == null ? 0 : transform.Y;
        }
    }
}
