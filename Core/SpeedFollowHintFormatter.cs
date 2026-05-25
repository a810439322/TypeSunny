using System;

namespace TypeSunny.Core
{
    internal struct SpeedFollowHint
    {
        public SpeedFollowHint(double value, bool isHitRate)
            : this()
        {
            Value = value;
            IsHitRate = isHitRate;
            Text = value.ToString("F2");
            ColorMetric = isHitRate ? value * 16.0 : value;
        }

        public double Value { get; private set; }
        public string Text { get; private set; }
        public bool IsHitRate { get; private set; }
        public double ColorMetric { get; private set; }
    }

    internal static class SpeedFollowHintFormatter
    {
        public static bool TryCreate(
            TxtSource txtSource,
            bool trainerShowsHitRate,
            double validSpeed,
            double hitRate,
            out SpeedFollowHint hint)
        {
            bool useHitRate = txtSource == TxtSource.trainer && trainerShowsHitRate;
            double value = useHitRate ? hitRate : validSpeed;

            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
            {
                hint = default(SpeedFollowHint);
                return false;
            }

            hint = new SpeedFollowHint(value, useHitRate);
            return true;
        }
    }
}
