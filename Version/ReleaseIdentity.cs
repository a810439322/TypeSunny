using System;

namespace TypeSunny
{
    internal static class ReleaseIdentity
    {
        private static readonly TimeSpan BeijingOffset = TimeSpan.FromHours(8);

        public static bool HasUpdate(
            string latestVersion,
            string currentVersion,
            DateTime latestPublishedUtc,
            string installedVersion,
            DateTime installedPublishedUtc)
        {
            if (string.IsNullOrWhiteSpace(latestVersion) || latestVersion == "未知")
                return false;
            if (string.IsNullOrWhiteSpace(currentVersion) || currentVersion == "未知")
                return false;

            int versionComparison = CompareVersions(latestVersion, currentVersion);
            if (versionComparison != 0)
                return versionComparison > 0;

            latestPublishedUtc = NormalizeUtc(latestPublishedUtc);
            installedPublishedUtc = NormalizeUtc(installedPublishedUtc);
            if (latestPublishedUtc == DateTime.MinValue)
                return false;

            if (string.IsNullOrWhiteSpace(installedVersion) ||
                CompareVersions(installedVersion, currentVersion) != 0)
            {
                return true;
            }

            if (installedPublishedUtc == DateTime.MinValue)
                return true;

            return latestPublishedUtc > installedPublishedUtc;
        }

        public static string Build(string version, DateTime publishedUtc)
        {
            if (string.IsNullOrWhiteSpace(version))
                return "";

            long ticks = ToUtcTicks(publishedUtc);
            return ticks > 0 ? version + "|" + ticks : version;
        }

        public static bool IsIgnored(string ignoredIdentity, string latestVersion, DateTime latestPublishedUtc)
            => IsIgnored(ignoredIdentity, latestVersion, latestVersion, latestPublishedUtc);

        public static bool IsIgnored(
            string ignoredIdentity,
            string latestVersion,
            string currentVersion,
            DateTime latestPublishedUtc)
        {
            if (string.IsNullOrWhiteSpace(ignoredIdentity) || string.IsNullOrWhiteSpace(latestVersion))
                return false;

            string latestIdentity = Build(latestVersion, latestPublishedUtc);
            if (string.Equals(ignoredIdentity, latestIdentity, StringComparison.Ordinal))
                return true;

            if (!string.Equals(ignoredIdentity, latestVersion, StringComparison.Ordinal))
                return false;

            return ToUtcTicks(latestPublishedUtc) == 0 ||
                   CompareVersions(latestVersion, currentVersion) > 0;
        }

        public static string FormatBeijingTime(DateTime utcTime)
        {
            utcTime = NormalizeUtc(utcTime);
            if (utcTime == DateTime.MinValue)
                return "";

            DateTime beijingTime = utcTime.Add(BeijingOffset);
            return beijingTime.ToString("yyyy-MM-dd HH:mm") + " 北京时间";
        }

        public static long ToUtcTicks(DateTime value)
        {
            value = NormalizeUtc(value);
            return value == DateTime.MinValue ? 0 : value.Ticks;
        }

        public static DateTime FromUtcTicks(long ticks)
        {
            return ticks > 0 ? new DateTime(ticks, DateTimeKind.Utc) : DateTime.MinValue;
        }

        public static DateTime NormalizeUtc(DateTime value)
        {
            if (value == DateTime.MinValue)
                return DateTime.MinValue;
            if (value.Kind == DateTimeKind.Utc)
                return value;
            if (value.Kind == DateTimeKind.Local)
                return value.ToUniversalTime();
            return DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        private static int CompareVersions(string v1, string v2)
        {
            v1 = DigitsOnly(v1);
            v2 = DigitsOnly(v2);
            if (long.TryParse(v1, out long num1) && long.TryParse(v2, out long num2))
                return num1.CompareTo(num2);
            return string.Compare(v1, v2, StringComparison.Ordinal);
        }

        private static string DigitsOnly(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            var chars = new char[value.Length];
            int count = 0;
            foreach (char c in value)
            {
                if (char.IsDigit(c))
                    chars[count++] = c;
            }
            return new string(chars, 0, count);
        }
    }
}
