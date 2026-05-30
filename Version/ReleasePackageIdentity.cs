using System;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace TypeSunny
{
    internal static class ReleasePackageIdentity
    {
        public static bool IsPackageManifestAsset(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            return name.EndsWith("-package.json", StringComparison.OrdinalIgnoreCase) ||
                   name.EndsWith("-manifest.json", StringComparison.OrdinalIgnoreCase);
        }

        public static DateTime ParseReleasePublishedUtc(JObject json)
            => ParseFirstUtc(json, "published_at", "created_at", "updated_at");

        public static DateTime ParseAssetPublishedUtc(JObject json)
            => ParseFirstUtc(json, "published_at", "created_at", "updated_at");

        public static DateTime ParseManifestPublishedUtc(JObject json)
        {
            DateTime ticksUtc = ParseUtcTicks(json, "package_published_utc_ticks");
            if (ticksUtc != DateTime.MinValue)
                return ticksUtc;

            return ParseFirstUtc(json, "package_published_at", "published_at", "created_at", "updated_at");
        }

        public static DateTime ResolvePublishedUtc(
            DateTime manifestPublishedUtc,
            DateTime updateAssetPublishedUtc,
            DateTime fullAssetPublishedUtc,
            DateTime releasePublishedUtc)
        {
            manifestPublishedUtc = ReleaseIdentity.NormalizeUtc(manifestPublishedUtc);
            if (manifestPublishedUtc != DateTime.MinValue)
                return manifestPublishedUtc;

            updateAssetPublishedUtc = ReleaseIdentity.NormalizeUtc(updateAssetPublishedUtc);
            if (updateAssetPublishedUtc != DateTime.MinValue)
                return updateAssetPublishedUtc;

            fullAssetPublishedUtc = ReleaseIdentity.NormalizeUtc(fullAssetPublishedUtc);
            if (fullAssetPublishedUtc != DateTime.MinValue)
                return fullAssetPublishedUtc;

            return ReleaseIdentity.NormalizeUtc(releasePublishedUtc);
        }

        private static DateTime ParseFirstUtc(JObject json, params string[] fields)
        {
            if (json == null)
                return DateTime.MinValue;

            foreach (string field in fields)
            {
                string value = json[field]?.ToString();
                DateTime parsed = ParseUtc(value);
                if (parsed != DateTime.MinValue)
                    return parsed;
            }

            return DateTime.MinValue;
        }

        private static DateTime ParseUtcTicks(JObject json, params string[] fields)
        {
            if (json == null)
                return DateTime.MinValue;

            foreach (string field in fields)
            {
                string value = json[field]?.ToString();
                if (long.TryParse(value, out long ticks) && ticks > 0)
                    return ReleaseIdentity.FromUtcTicks(ticks);
            }

            return DateTime.MinValue;
        }

        private static DateTime ParseUtc(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return DateTime.MinValue;

            if (DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var dto))
            {
                return dto.UtcDateTime;
            }

            return DateTime.MinValue;
        }
    }
}
