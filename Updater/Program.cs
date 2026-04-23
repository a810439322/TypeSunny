using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace Updater
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length < 4)
            {
                Console.WriteLine("用法: Updater.exe <zip路径> <目标目录> <主程序PID> <主程序路径>");
                return 1;
            }

            string zipPath = args[0];
            string targetDir = args[1];
            int pid = int.Parse(args[2]);
            string mainExePath = args[3];

            Console.WriteLine("晴跟打 更新程序");
            Console.WriteLine("==================");

            try
            {
                Console.WriteLine("等待主程序退出...");
                try
                {
                    var process = Process.GetProcessById(pid);
                    if (!process.WaitForExit(30000))
                    {
                        Console.WriteLine("主程序未在30秒内退出，强制结束...");
                        process.Kill();
                        process.WaitForExit(5000);
                    }
                }
                catch (ArgumentException)
                {
                    // 进程已退出
                }

                Thread.Sleep(500);

                Console.WriteLine("正在解压更新文件...");
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

                        // 跳过自身
                        if (entry.Name.Equals("Updater.exe", StringComparison.OrdinalIgnoreCase))
                            continue;

                        Console.WriteLine($"  更新: {entry.FullName}");
                        entry.ExtractToFile(destPath, true);
                    }
                }

                Console.WriteLine("更新完成，正在启动主程序...");
                Process.Start(mainExePath);

                try { File.Delete(zipPath); } catch { }

                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"更新失败: {ex.Message}");
                Console.WriteLine("请手动下载全量包进行更新。");
                Console.WriteLine("按任意键退出...");
                Console.ReadKey();
                return 1;
            }
        }
    }
}
