using System;
using TypeSunny.Versioning;

namespace TypeSunny.Tests
{
    internal static class VersionCheckPolicyTests
    {
        private static int Main()
        {
            try
            {
                AssertTrue(VersionCheckPolicy.ShouldCheck(VersionCheckTrigger.Startup, false, false));
                AssertFalse(VersionCheckPolicy.ShouldCheck(VersionCheckTrigger.Startup, true, true));
                AssertTrue(VersionCheckPolicy.ShouldCheck(VersionCheckTrigger.Timer, false, true));
                AssertFalse(VersionCheckPolicy.ShouldCheck(VersionCheckTrigger.Timer, true, true));
                AssertFalse(VersionCheckPolicy.ShouldCheck(VersionCheckTrigger.Timer, false, false));
                AssertTrue(VersionCheckPolicy.ShouldCheck(VersionCheckTrigger.Manual, true, false));

                AssertTrue(VersionCheckPolicy.ShouldForceRefresh(VersionCheckTrigger.Startup));
                AssertTrue(VersionCheckPolicy.ShouldForceRefresh(VersionCheckTrigger.Manual));
                AssertFalse(VersionCheckPolicy.ShouldForceRefresh(VersionCheckTrigger.Timer));

                Console.WriteLine("All VersionCheckPolicy tests passed.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("FAIL: " + ex.Message);
                return 1;
            }
        }

        private static void AssertTrue(bool condition)
        {
            if (!condition)
                throw new Exception("Expected true, got false.");
        }

        private static void AssertFalse(bool condition)
        {
            if (condition)
                throw new Exception("Expected false, got true.");
        }
    }
}
