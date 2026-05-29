using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TypeSunny;

namespace TypeSunny.Tests
{
    internal static class TrainerConfigFileAccessTests
    {
        private static int Main()
        {
            try
            {
                WriteValuesRetriesWhileFileIsTemporarilyLocked();
                ReadIntoReleasesFileHandle();

                Console.WriteLine("All TrainerConfig file access tests passed.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
                return 1;
            }
        }

        private static void WriteValuesRetriesWhileFileIsTemporarilyLocked()
        {
            string testDir = CreateTempDirectory();
            string originalPath = TrainerConfig.Path;

            try
            {
                string configPath = Path.Combine(testDir, "TrainerConfig.txt");
                File.WriteAllText(configPath, "old\tvalue" + Environment.NewLine);
                TrainerConfig.Path = configPath;

                var values = new Dictionary<string, string>
                {
                    { "alpha", "beta" },
                    { "number", "42" }
                };

                using (var blocker = new FileStream(configPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    Task writeTask = Task.Run(() => TrainerConfig.WriteValues(values));
                    Thread.Sleep(150);

                    if (writeTask.IsCompleted)
                        throw new Exception("Write completed while the file was still locked.");

                    blocker.Dispose();

                    if (!writeTask.Wait(3000))
                        throw new Exception("Write did not finish after the file lock was released.");

                    if (writeTask.IsFaulted)
                        throw writeTask.Exception.GetBaseException();
                }

                string text = File.ReadAllText(configPath);
                AssertContains("saved alpha value", text, "alpha\tbeta");
                AssertContains("saved number value", text, "number\t42");
            }
            finally
            {
                TrainerConfig.Path = originalPath;
                Directory.Delete(testDir, true);
            }
        }

        private static void ReadIntoReleasesFileHandle()
        {
            string testDir = CreateTempDirectory();
            string originalPath = TrainerConfig.Path;

            try
            {
                string configPath = Path.Combine(testDir, "TrainerConfig.txt");
                File.WriteAllText(configPath, "alpha\tbeta" + Environment.NewLine);
                TrainerConfig.Path = configPath;

                var values = new Dictionary<string, string>();
                TrainerConfig.ReadInto(values);

                AssertEqual("read value", "beta", values["alpha"]);

                using (new FileStream(configPath, FileMode.Open, FileAccess.Write, FileShare.None))
                {
                }
            }
            finally
            {
                TrainerConfig.Path = originalPath;
                Directory.Delete(testDir, true);
            }
        }

        private static string CreateTempDirectory()
        {
            string path = Path.Combine(Path.GetTempPath(), "typesunny-trainer-config-access-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static void AssertContains(string name, string text, string expected)
        {
            if (!text.Contains(expected))
                throw new Exception(name + ": expected to contain " + expected + ".");
        }

        private static void AssertEqual(string name, string expected, string actual)
        {
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
                throw new Exception(name + ": expected " + expected + ", got " + actual + ".");
        }
    }
}
