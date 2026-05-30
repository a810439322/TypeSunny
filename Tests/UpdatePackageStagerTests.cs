using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using TypeSunny.Utils;

namespace TypeSunny.Tests
{
    internal static class UpdatePackageStagerTests
    {
        private static int Main()
        {
            string dir = Path.Combine(Path.GetTempPath(), "typesunny-update-stager-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);

            try
            {
                string installedUpdater = Path.Combine(dir, "installed", "Updater.exe");
                Directory.CreateDirectory(Path.GetDirectoryName(installedUpdater));
                File.WriteAllText(installedUpdater, "installed-updater");

                string packageWithUpdater = Path.Combine(dir, "with-updater.zip");
                CreatePackage(packageWithUpdater, ("Updater.exe", "package-updater"), ("晴跟打.exe", "main"));

                string staged = UpdatePackageStager.StageUpdater(
                    packageWithUpdater,
                    Path.Combine(dir, "stage-from-package"),
                    installedUpdater);

                AssertEqual("staged updater path", Path.Combine(dir, "stage-from-package", "Updater.exe"), staged);
                AssertEqual("uses updater from package", "package-updater", File.ReadAllText(staged));

                string packageWithoutUpdater = Path.Combine(dir, "without-updater.zip");
                CreatePackage(packageWithoutUpdater, ("晴跟打.exe", "main"));

                string fallback = UpdatePackageStager.StageUpdater(
                    packageWithoutUpdater,
                    Path.Combine(dir, "stage-from-installed"),
                    installedUpdater);

                AssertEqual("fallback updater path", Path.Combine(dir, "stage-from-installed", "Updater.exe"), fallback);
                AssertEqual("falls back to installed updater", "installed-updater", File.ReadAllText(fallback));

                Console.WriteLine("All update package stager tests passed.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch { }
            }
        }

        private static void CreatePackage(string path, params (string Name, string Content)[] entries)
        {
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.ReadWrite))
            using (var archive = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                foreach (var entryData in entries)
                {
                    var entry = archive.CreateEntry(entryData.Name);
                    using (var writer = new StreamWriter(entry.Open()))
                        writer.Write(entryData.Content);
                }
            }
        }

        private static void AssertEqual(string name, string expected, string actual)
        {
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
                throw new Exception(name + ": expected [" + expected + "], got [" + actual + "].");
        }
    }
}
