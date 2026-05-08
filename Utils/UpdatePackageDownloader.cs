using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace TypeSunny.Utils
{
    internal static class UpdatePackageDownloader
    {
        internal static async Task DownloadAndApplyAsync(
            string packageUrl,
            IProgress<(long downloaded, long? total)> progress,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(packageUrl))
                throw new ArgumentException("packageUrl 不能为空", nameof(packageUrl));

            string tempDir = Path.Combine(Path.GetTempPath(), "TypeSunnyUpdate");
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
            Directory.CreateDirectory(tempDir);

            string zipPath = Path.Combine(tempDir, "update.zip");
            await DownloadFileAsync(packageUrl, zipPath, progress, ct);
            ct.ThrowIfCancellationRequested();

            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string updaterPath = Path.Combine(appDir, "Updater.exe");
            int pid = Process.GetCurrentProcess().Id;
            string mainExe = Process.GetCurrentProcess().MainModule.FileName;
            string appDirClean = appDir.TrimEnd('\\');

            Process.Start(updaterPath, $"\"{zipPath}\" \"{appDirClean}\" {pid} \"{mainExe}\"");
            Application.Current.Shutdown();
        }

        private static async Task DownloadFileAsync(
            string url,
            string filePath,
            IProgress<(long downloaded, long? total)> progress,
            CancellationToken ct)
        {
            using (var client = new HttpClient(new HttpClientHandler { UseProxy = false }))
            {
                client.Timeout = TimeSpan.FromMinutes(5);
                using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct))
                {
                    response.EnsureSuccessStatusCode();
                    long? totalBytes = response.Content.Headers.ContentLength;

                    using (var stream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                    {
                        var buffer = new byte[8192];
                        long downloaded = 0;
                        int bytesRead;

                        while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead, ct);
                            downloaded += bytesRead;
                            progress?.Report((downloaded, totalBytes));
                        }
                    }
                }
            }
        }
    }
}
