using System;

namespace TypeSunny.Tests
{
    internal static class CompiledReleaseIdentityTests
    {
        private static int Main()
        {
            try
            {
                AssertEqual("compiled version follows generated version", GeneratedVersion.CurrentVersion, CompiledReleaseIdentity.Version);
                AssertTrue("compiled package ticks are non-negative", CompiledReleaseIdentity.PackagePublishedUtcTicks >= 0);
                AssertEqual(
                    "compiled package utc matches ticks",
                    ReleaseIdentity.FromUtcTicks(CompiledReleaseIdentity.PackagePublishedUtcTicks),
                    CompiledReleaseIdentity.PackagePublishedUtc);

                Console.WriteLine("All compiled release identity tests passed.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        private static void AssertEqual(string name, string expected, string actual)
        {
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
                throw new Exception(name + ": expected [" + expected + "], got [" + actual + "].");
        }

        private static void AssertEqual(string name, long expected, long actual)
        {
            if (expected != actual)
                throw new Exception(name + ": expected [" + expected + "], got [" + actual + "].");
        }

        private static void AssertTrue(string name, bool value)
        {
            if (!value)
                throw new Exception(name + ": expected true.");
        }

        private static void AssertEqual(string name, DateTime expected, DateTime actual)
        {
            if (expected != actual)
                throw new Exception(name + ": expected [" + expected.ToString("O") + "], got [" + actual.ToString("O") + "].");
        }
    }
}
