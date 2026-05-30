using System;
using System.IO;
using System.IO.Compression;

namespace TypeSunny.Utils
{
    internal static class UpdatePackageStager
    {
        internal static string StageUpdater(string packagePath, string stageDir, string installedUpdaterPath)
        {
            if (string.IsNullOrWhiteSpace(packagePath))
                throw new ArgumentException("packagePath 不能为空", nameof(packagePath));
            if (string.IsNullOrWhiteSpace(stageDir))
                throw new ArgumentException("stageDir 不能为空", nameof(stageDir));
            if (string.IsNullOrWhiteSpace(installedUpdaterPath))
                throw new ArgumentException("installedUpdaterPath 不能为空", nameof(installedUpdaterPath));

            Directory.CreateDirectory(stageDir);
            string stagedUpdaterPath = Path.Combine(stageDir, "Updater.exe");

            using (var archive = ZipFile.OpenRead(packagePath))
            {
                foreach (var entry in archive.Entries)
                {
                    if (entry.Name.Equals("Updater.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        entry.ExtractToFile(stagedUpdaterPath, true);
                        return stagedUpdaterPath;
                    }
                }
            }

            File.Copy(installedUpdaterPath, stagedUpdaterPath, true);
            return stagedUpdaterPath;
        }
    }
}
