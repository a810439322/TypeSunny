using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Updater
{
    internal static class UpdaterConfigWriter
    {
        private const string InstalledVersionKey = "已安装版本";
        private const string InstalledReleaseTimeKey = "已安装发布UTC时间";

        public static void SaveInstalledRelease(string targetDir, string version, string publishedUtcTicks)
        {
            if (string.IsNullOrWhiteSpace(targetDir) || string.IsNullOrWhiteSpace(version))
                return;

            string configPath = Path.Combine(targetDir, "config.txt");
            var lines = File.Exists(configPath)
                ? File.ReadAllLines(configPath).ToList()
                : new List<string>();

            Upsert(lines, InstalledVersionKey, version);
            string normalizedTicks = "0";
            if (!string.IsNullOrWhiteSpace(publishedUtcTicks) &&
                long.TryParse(publishedUtcTicks, out long ticks) &&
                ticks > 0)
                normalizedTicks = ticks.ToString();
            Upsert(lines, InstalledReleaseTimeKey, normalizedTicks);

            string directory = Path.GetDirectoryName(configPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllLines(configPath, lines);
        }

        private static void Upsert(List<string> lines, string key, string value)
        {
            string prefix = key + "\t";
            string newLine = prefix + value;
            bool replaced = false;

            for (int i = 0; i < lines.Count; i++)
            {
                if (!lines[i].StartsWith(prefix, StringComparison.Ordinal))
                    continue;

                if (!replaced)
                {
                    lines[i] = newLine;
                    replaced = true;
                }
                else
                {
                    lines.RemoveAt(i);
                    i--;
                }
            }

            if (!replaced)
                lines.Add(newLine);
        }
    }
}
