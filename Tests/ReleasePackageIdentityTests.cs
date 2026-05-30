using System;
using Newtonsoft.Json.Linq;

namespace TypeSunny.Tests
{
    internal static class ReleasePackageIdentityTests
    {
        private static int Main()
        {
            try
            {
                var release = JObject.Parse(@"{ ""created_at"": ""2026-05-30T10:00:00Z"" }");
                var updateAsset = JObject.Parse(@"{ ""created_at"": ""2026-05-30T10:10:00Z"" }");
                var fullAsset = JObject.Parse(@"{ ""updated_at"": ""2026-05-30T10:20:00Z"" }");
                var manifest = JObject.Parse(@"{ ""version"": ""20260530"", ""package_published_at"": ""2026-05-30T10:30:00Z"" }");
                var manifestWithTicks = JObject.Parse(@"{ ""version"": ""20260530"", ""package_published_utc_ticks"": ""639158779230000000"", ""package_published_at"": ""2026-05-30T10:30:00Z"" }");

                DateTime releaseUtc = ReleasePackageIdentity.ParseReleasePublishedUtc(release);
                DateTime updateUtc = ReleasePackageIdentity.ParseAssetPublishedUtc(updateAsset);
                DateTime fullUtc = ReleasePackageIdentity.ParseAssetPublishedUtc(fullAsset);
                DateTime manifestUtc = ReleasePackageIdentity.ParseManifestPublishedUtc(manifest);
                DateTime manifestTicksUtc = ReleasePackageIdentity.ParseManifestPublishedUtc(manifestWithTicks);

                AssertEqual("release time", Utc(2026, 5, 30, 10, 0, 0), releaseUtc);
                AssertEqual("update asset time", Utc(2026, 5, 30, 10, 10, 0), updateUtc);
                AssertEqual("full asset fallback time", Utc(2026, 5, 30, 10, 20, 0), fullUtc);
                AssertEqual("manifest package time", Utc(2026, 5, 30, 10, 30, 0), manifestUtc);
                AssertEqual("manifest package ticks win over string time", new DateTime(639158779230000000, DateTimeKind.Utc), manifestTicksUtc);

                AssertEqual(
                    "manifest time wins",
                    manifestUtc,
                    ReleasePackageIdentity.ResolvePublishedUtc(manifestUtc, updateUtc, fullUtc, releaseUtc));
                AssertEqual(
                    "update zip asset time wins without manifest",
                    updateUtc,
                    ReleasePackageIdentity.ResolvePublishedUtc(DateTime.MinValue, updateUtc, fullUtc, releaseUtc));
                AssertEqual(
                    "full zip asset time is fallback when update asset has no time",
                    fullUtc,
                    ReleasePackageIdentity.ResolvePublishedUtc(DateTime.MinValue, DateTime.MinValue, fullUtc, releaseUtc));
                AssertEqual(
                    "release time is final fallback",
                    releaseUtc,
                    ReleasePackageIdentity.ResolvePublishedUtc(DateTime.MinValue, DateTime.MinValue, DateTime.MinValue, releaseUtc));

                AssertTrue("package manifest asset name", ReleasePackageIdentity.IsPackageManifestAsset("TypeSunny-20260530-package.json"));
                AssertFalse("update zip is not manifest", ReleasePackageIdentity.IsPackageManifestAsset("TypeSunny-20260530-update.zip"));

                Console.WriteLine("All release package identity tests passed.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        private static DateTime Utc(int year, int month, int day, int hour, int minute, int second)
            => new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc);

        private static void AssertEqual(string name, DateTime expected, DateTime actual)
        {
            if (expected != actual)
                throw new Exception(name + ": expected [" + expected.ToString("O") + "], got [" + actual.ToString("O") + "].");
        }

        private static void AssertTrue(string name, bool value)
        {
            if (!value)
                throw new Exception(name + ": expected true.");
        }

        private static void AssertFalse(string name, bool value)
        {
            if (value)
                throw new Exception(name + ": expected false.");
        }
    }
}
