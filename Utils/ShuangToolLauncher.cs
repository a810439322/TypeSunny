using System;
using System.Diagnostics;
using System.IO;

namespace TypeSunny.Utils
{
    internal static class ShuangToolLauncher
    {
        private const string RelativeIndexPath = @"Resources\Shuang\index.html";

        internal static string GetIndexPath(string baseDirectory)
        {
            if (string.IsNullOrWhiteSpace(baseDirectory))
                baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

            return Path.GetFullPath(Path.Combine(baseDirectory, RelativeIndexPath));
        }

        internal static bool IsAvailable(string baseDirectory)
        {
            return File.Exists(GetIndexPath(baseDirectory));
        }

        internal static void Open(string baseDirectory)
        {
            string indexPath = GetIndexPath(baseDirectory);
            if (!File.Exists(indexPath))
                throw new FileNotFoundException("找不到双拼练习页面，请确认资源已随程序一起发布。", indexPath);

            Process.Start(new ProcessStartInfo(indexPath)
            {
                UseShellExecute = true
            });
        }
    }
}
