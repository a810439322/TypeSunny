using System;
using System.IO;
using Updater;

namespace TypeSunny.Tests
{
    internal static class UpdaterSelfUpdateTests
    {
        private static int Main()
        {
            string root = Path.Combine(Path.GetTempPath(), "typesunny-updater-self-update-test-" + Guid.NewGuid().ToString("N"));

            try
            {
                string runningUpdater = Path.Combine(root, "temp", "Updater.exe");
                string targetUpdater = Path.Combine(root, "app", "Updater.exe");

                AssertFalse(
                    "staged updater must allow package updater to replace installed updater",
                    Program.ShouldSkipUpdaterEntry("Updater.exe", targetUpdater, runningUpdater));

                AssertTrue(
                    "running updater should not try to overwrite itself",
                    Program.ShouldSkipUpdaterEntry("Updater.exe", runningUpdater, runningUpdater));

                AssertFalse(
                    "non-updater entries should never be skipped by updater self-check",
                    Program.ShouldSkipUpdaterEntry("晴跟打.exe", targetUpdater, runningUpdater));

                Console.WriteLine("All updater self-update tests passed.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
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
