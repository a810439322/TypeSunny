using System;
using System.IO;
using System.Linq;
using Updater;

namespace TypeSunny.Tests
{
    internal static class UpdaterConfigWriterTests
    {
        private static int Main()
        {
            string dir = Path.Combine(Path.GetTempPath(), "typesunny-updater-config-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);

            try
            {
                string configPath = Path.Combine(dir, "config.txt");
                File.WriteAllLines(configPath, new[]
                {
                    "窗口高度\t750.4",
                    "已安装版本\t20260528",
                    "已安装发布UTC时间\t639156000000000000"
                });

                UpdaterConfigWriter.SaveInstalledRelease(dir, "20260529", "639157152000000000");
                string[] lines = File.ReadAllLines(configPath);

                AssertContains("preserves existing unrelated config", lines, "窗口高度\t750.4");
                AssertContains("updates installed version", lines, "已安装版本\t20260529");
                AssertContains("updates installed release time", lines, "已安装发布UTC时间\t639157152000000000");
                AssertCount("installed version key should appear once", lines, "已安装版本\t", 1);
                AssertCount("installed release time key should appear once", lines, "已安装发布UTC时间\t", 1);

                string newConfigDir = Path.Combine(dir, "new");
                Directory.CreateDirectory(newConfigDir);
                UpdaterConfigWriter.SaveInstalledRelease(newConfigDir, "20260530", "639158016000000000");
                string[] newLines = File.ReadAllLines(Path.Combine(newConfigDir, "config.txt"));

                AssertContains("appends installed version to new config", newLines, "已安装版本\t20260530");
                AssertContains("appends installed release time to new config", newLines, "已安装发布UTC时间\t639158016000000000");

                UpdaterConfigWriter.SaveInstalledRelease(newConfigDir, "20260531", "0");
                string[] clearedLines = File.ReadAllLines(Path.Combine(newConfigDir, "config.txt"));
                AssertContains("clears stale installed release time when time is missing", clearedLines, "已安装发布UTC时间\t0");
                AssertCount("cleared installed release time key should appear once", clearedLines, "已安装发布UTC时间\t", 1);

                Console.WriteLine("All UpdaterConfigWriter tests passed.");
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

        private static void AssertContains(string name, string[] lines, string expected)
        {
            if (!lines.Contains(expected))
                throw new Exception(name + ": expected line [" + expected + "].");
        }

        private static void AssertCount(string name, string[] lines, string prefix, int expected)
        {
            int actual = lines.Count(line => line.StartsWith(prefix, StringComparison.Ordinal));
            if (actual != expected)
                throw new Exception(name + ": expected " + expected + ", got " + actual + ".");
        }
    }
}
