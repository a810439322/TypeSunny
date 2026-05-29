using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace Updater
{
    class Program
    {
        private static string _logPath;

        static void Log(string msg)
        {
            string line = $"[{DateTime.Now:HH:mm:ss}] {msg}";
            Console.WriteLine(line);
            try { File.AppendAllText(_logPath, line + Environment.NewLine); } catch { }
        }

        static int Main(string[] args)
        {
            _logPath = Path.Combine(Path.GetTempPath(), "TypeSunnyUpdater.log");
            try { File.Delete(_logPath); } catch { }

            Log($"Updater started, args count: {args.Length}");
            for (int i = 0; i < args.Length; i++)
                Log($"  args[{i}] = {args[i]}");

            if (args.Length < 4)
            {
                Log("参数不足，需要: <zip路径> <目标目录> <主程序PID> <主程序路径>");
                try { Console.ReadKey(); } catch { }
                return 1;
            }

            string zipPath = args[0];
            string targetDir = args[1];
            string mainExePath = args[3];
            string installedVersion = args.Length >= 5 ? args[4] : "";
            string installedReleaseUtcTicks = args.Length >= 6 ? args[5] : "";

            if (!int.TryParse(args[2], out int pid))
            {
                Log($"PID 解析失败: {args[2]}");
                try { Console.ReadKey(); } catch { }
                return 1;
            }

            Log($"zip: {zipPath}");
            Log($"target: {targetDir}");
            Log($"pid: {pid}");
            Log($"mainExe: {mainExePath}");
            Log($"zip exists: {File.Exists(zipPath)}");

            try
            {
                Log("等待主程序退出...");
                if (pid > 0)
                {
                    try
                    {
                        var process = Process.GetProcessById(pid);
                        if (!process.WaitForExit(30000))
                        {
                            Log("主程序未在30秒内退出，强制结束...");
                            process.Kill();
                            process.WaitForExit(5000);
                        }
                    }
                    catch (ArgumentException)
                    {
                        Log("主程序已退出");
                    }
                    catch (Exception ex)
                    {
                        Log($"等待主程序时出错: {ex.Message}");
                    }
                }
                else
                {
                    Log("PID 为 0，跳过等待");
                }

                Thread.Sleep(500);

                Log("正在解压更新文件...");
                using (var archive = ZipFile.OpenRead(zipPath))
                {
                    foreach (var entry in archive.Entries)
                    {
                        if (string.IsNullOrEmpty(entry.Name))
                            continue;

                        string destPath = Path.Combine(targetDir, entry.FullName);
                        string destDir = Path.GetDirectoryName(destPath);
                        if (!Directory.Exists(destDir))
                            Directory.CreateDirectory(destDir);

                        if (entry.Name.Equals("Updater.exe", StringComparison.OrdinalIgnoreCase))
                            continue;

                        Log($"  更新: {entry.FullName}");
                        entry.ExtractToFile(destPath, true);
                    }
                }

                try
                {
                    UpdaterConfigWriter.SaveInstalledRelease(targetDir, installedVersion, installedReleaseUtcTicks);
                }
                catch (Exception ex)
                {
                    Log($"保存已安装版本信息失败: {ex.Message}");
                }

                Log("更新完成，正在启动主程序...");
                Process.Start(mainExePath);

                try { File.Delete(zipPath); } catch { }

                Log("Done");
                return 0;
            }
            catch (Exception ex)
            {
                Log($"更新失败: {ex}");
                try { Console.WriteLine("按任意键退出..."); Console.ReadKey(); } catch { }
                return 1;
            }
        }
    }
}
