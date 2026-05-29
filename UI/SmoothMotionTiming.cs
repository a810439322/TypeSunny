using System;
using System.Reflection;

namespace TypeSunny.UI
{
        public class SmoothMotionTiming
    {
        public const int OffDurationMilliseconds = 0;
        public const int FastDurationMilliseconds = 140;
        public const int MediumDurationMilliseconds = 200;
        public const int SlowDurationMilliseconds = 280;

        private const double SmoothingWeight = 0.3;

        private TimeSpan? _lastInputTime;
        private double? _smoothedDurationMilliseconds;

        public static int GetDurationMilliseconds()
        {
            return GetConfiguredDuration("平滑光标固定时长", MediumDurationMilliseconds);
        }

        public static bool IsDynamic(string modeValue)
        {
            return modeValue != "固定";
        }

        public void Reset()
        {
            _lastInputTime = null;
            _smoothedDurationMilliseconds = null;
        }

        public void RecordInput()
        {
            RecordInput(TimeSpan.FromTicks(DateTime.UtcNow.Ticks));
        }

        public void RecordInput(TimeSpan timestamp)
        {
            if (_lastInputTime.HasValue)
            {
                double intervalMs = (timestamp - _lastInputTime.Value).TotalMilliseconds;
                if (intervalMs > 0)
                {
                    double targetDuration = MapIntervalToDuration(intervalMs);
                    _smoothedDurationMilliseconds = _smoothedDurationMilliseconds.HasValue
                        ? _smoothedDurationMilliseconds.Value * (1.0 - SmoothingWeight) + targetDuration * SmoothingWeight
                        : targetDuration;
                }
            }

            _lastInputTime = timestamp;
        }

        public int GetDynamicDurationMilliseconds()
        {
            if (!_smoothedDurationMilliseconds.HasValue)
                return GetConfiguredDuration("平滑光标中", MediumDurationMilliseconds);

            return ClampToInt(
                _smoothedDurationMilliseconds.Value,
                GetConfiguredDuration("平滑光标快", FastDurationMilliseconds),
                GetConfiguredDuration("平滑光标慢", SlowDurationMilliseconds));
        }

        public int GetCurrentDurationMilliseconds()
        {
            string modeValue = GetConfigString("平滑光标模式");
            if (IsDynamic(modeValue))
                return GetDynamicDurationMilliseconds();

            return GetDurationMilliseconds();
        }

        public int GetBackgroundDurationMilliseconds()
        {
            return GetCurrentDurationMilliseconds();
        }

        private static double MapIntervalToDuration(double intervalMs)
        {
            const double mediumIntervalMs = 250;

            if (intervalMs <= 90)
                return GetConfiguredDuration("平滑光标快", FastDurationMilliseconds);
            if (intervalMs >= 600)
                return GetConfiguredDuration("平滑光标慢", SlowDurationMilliseconds);

            int fast = GetConfiguredDuration("平滑光标快", FastDurationMilliseconds);
            int medium = GetConfiguredDuration("平滑光标中", MediumDurationMilliseconds);
            int slow = GetConfiguredDuration("平滑光标慢", SlowDurationMilliseconds);

            if (intervalMs <= mediumIntervalMs)
            {
                double fastToMediumRatio = (intervalMs - 90) / (mediumIntervalMs - 90);
                return fast + fastToMediumRatio * (medium - fast);
            }

            double mediumToSlowRatio = (intervalMs - mediumIntervalMs) / (600 - mediumIntervalMs);
            return medium + mediumToSlowRatio * (slow - medium);
        }

        private static int ClampToInt(double value, int min, int max)
        {
            int rounded = (int)Math.Round(value);
            if (rounded < min)
                return min;
            if (rounded > max)
                return max;

            return rounded;
        }

        private static string GetConfigString(string key)
        {
            Type configType = Type.GetType("TypeSunny.Config");
            if (configType == null)
                return null;

            MethodInfo getString = configType.GetMethod("GetString", BindingFlags.Static | BindingFlags.Public);
            if (getString == null)
                return null;

            return getString.Invoke(null, new object[] { key }) as string;
        }

        private static int GetConfiguredDuration(string key, int fallback)
        {
            string value = GetConfigString(key);
            int duration;
            if (!int.TryParse(value, out duration))
                return fallback;

            if (duration < 0)
                return 0;
            if (duration > 2000)
                return 2000;

            return duration;
        }
    }
}
