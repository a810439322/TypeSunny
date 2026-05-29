using System;

namespace TypeSunny.Tests
{
    internal static class VersionReleaseIdentityTests
    {
        private static int Main()
        {
            try
            {
                var older = new DateTime(2026, 5, 29, 1, 10, 0, DateTimeKind.Utc);
                var newer = new DateTime(2026, 5, 29, 5, 20, 0, DateTimeKind.Utc);

                AssertTrue(
                    "newer date version should update without publication time",
                    ReleaseIdentity.HasUpdate("20260530", "20260529", DateTime.MinValue, "20260529", DateTime.MinValue));

                AssertTrue(
                    "same version with newer release time should update",
                    ReleaseIdentity.HasUpdate("20260529", "20260529", newer, "20260529", older));

                AssertFalse(
                    "same version with same release time should not update",
                    ReleaseIdentity.HasUpdate("20260529", "20260529", newer, "20260529", newer));

                AssertTrue(
                    "same version without installed metadata should update once when release time exists",
                    ReleaseIdentity.HasUpdate("20260529", "20260529", newer, "", DateTime.MinValue));

                string latestIdentity = ReleaseIdentity.Build("20260529", newer);
                AssertTrue(
                    "matching timed identity should suppress reminder",
                    ReleaseIdentity.IsIgnored(latestIdentity, "20260529", newer));

                AssertFalse(
                    "legacy version-only ignore should not suppress a timed same-day release",
                    ReleaseIdentity.IsIgnored("20260529", "20260529", "20260529", newer));

                AssertFalse(
                    "legacy three-argument ignore should not suppress a timed same-day release",
                    ReleaseIdentity.IsIgnored("20260529", "20260529", newer));

                AssertTrue(
                    "legacy version-only ignore should still suppress a newer date version",
                    ReleaseIdentity.IsIgnored("20260530", "20260530", "20260529", newer));

                AssertTrue(
                    "legacy version-only ignore should still suppress releases without time",
                    ReleaseIdentity.IsIgnored("20260529", "20260529", "20260529", DateTime.MinValue));

                AssertEqual(
                    "beijing display should use UTC plus eight hours",
                    "2026-05-29 13:20 北京时间",
                    ReleaseIdentity.FormatBeijingTime(newer));

                AssertEqual(
                    "empty beijing display for missing time",
                    "",
                    ReleaseIdentity.FormatBeijingTime(DateTime.MinValue));

                Console.WriteLine("All VersionReleaseIdentity tests passed.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        private static void AssertTrue(string name, bool condition)
        {
            if (!condition)
                throw new Exception(name + ": expected true, got false.");
        }

        private static void AssertFalse(string name, bool condition)
        {
            if (condition)
                throw new Exception(name + ": expected false, got true.");
        }

        private static void AssertEqual(string name, string expected, string actual)
        {
            if (actual != expected)
                throw new Exception(name + ": expected [" + expected + "], got [" + actual + "].");
        }
    }
}
