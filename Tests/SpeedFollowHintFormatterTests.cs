using System;
using TypeSunny.Core;

namespace TypeSunny.Tests
{
    internal static class SpeedFollowHintFormatterTests
    {
        private static int Main()
        {
            try
            {
                TrainerSourceUsesHitRateWhenSettingIsEnabled();
                TrainerSourceUsesSpeedWhenSettingIsDisabled();
                NonTrainerSourceAlwaysUsesSpeed();
                InvalidSelectedMetricHidesHint();

                Console.WriteLine("All SpeedFollowHintFormatter tests passed.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        private static void TrainerSourceUsesHitRateWhenSettingIsEnabled()
        {
            SpeedFollowHint hint;
            bool created = SpeedFollowHintFormatter.TryCreate(
                TxtSource.trainer,
                true,
                123.45,
                6.789,
                out hint);

            AssertTrue("trainer hit hint created", created);
            AssertEqual("trainer hit text", "6.79", hint.Text);
            AssertTrue("trainer hit flag", hint.IsHitRate);
            AssertDoubleEqual("trainer hit color metric", 108.624, hint.ColorMetric);
        }

        private static void TrainerSourceUsesSpeedWhenSettingIsDisabled()
        {
            SpeedFollowHint hint;
            bool created = SpeedFollowHintFormatter.TryCreate(
                TxtSource.trainer,
                false,
                123.456,
                6.78,
                out hint);

            AssertTrue("trainer speed hint created", created);
            AssertEqual("trainer speed text", "123.46", hint.Text);
            AssertFalse("trainer speed flag", hint.IsHitRate);
            AssertDoubleEqual("trainer speed color metric", 123.456, hint.ColorMetric);
        }

        private static void NonTrainerSourceAlwaysUsesSpeed()
        {
            SpeedFollowHint hint;
            bool created = SpeedFollowHintFormatter.TryCreate(
                TxtSource.articlesender,
                true,
                98.765,
                7.89,
                out hint);

            AssertTrue("non-trainer speed hint created", created);
            AssertEqual("non-trainer speed text", "98.77", hint.Text);
            AssertFalse("non-trainer speed flag", hint.IsHitRate);
        }

        private static void InvalidSelectedMetricHidesHint()
        {
            SpeedFollowHint hint;

            bool trainerHitCreated = SpeedFollowHintFormatter.TryCreate(
                TxtSource.trainer,
                true,
                120,
                0,
                out hint);

            bool speedCreated = SpeedFollowHintFormatter.TryCreate(
                TxtSource.qq,
                true,
                double.NaN,
                6.5,
                out hint);

            AssertFalse("invalid trainer hit hides hint", trainerHitCreated);
            AssertFalse("invalid speed hides hint", speedCreated);
        }

        private static void AssertTrue(string name, bool condition)
        {
            if (!condition)
                throw new Exception(name + " expected true, got false.");
        }

        private static void AssertFalse(string name, bool condition)
        {
            if (condition)
                throw new Exception(name + " expected false, got true.");
        }

        private static void AssertEqual(string name, string expected, string actual)
        {
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
                throw new Exception(name + " expected [" + expected + "], got [" + actual + "].");
        }

        private static void AssertDoubleEqual(string name, double expected, double actual)
        {
            if (Math.Abs(expected - actual) > 0.000001)
                throw new Exception(name + " expected [" + expected + "], got [" + actual + "].");
        }
    }
}
