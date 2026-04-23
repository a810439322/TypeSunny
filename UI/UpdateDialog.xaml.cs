using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace TypeSunny.UI
{
    public enum UpdateDialogResult
    {
        Update,
        DismissToday,
        IgnoreVersion,
        Closed
    }

    public partial class UpdateDialog : Window
    {
        public UpdateDialogResult Result { get; private set; } = UpdateDialogResult.Closed;

        public UpdateDialog(Window owner)
        {
            InitializeComponent();
            Owner = owner;

            txtVersion.Text = $"当前版本：{VersionManager.CurrentVersion}　→　最新版本：{VersionManager.LatestVersion}";
            txtChangelog.Text = string.IsNullOrEmpty(VersionManager.Changelog) ? "暂无更新说明" : VersionManager.Changelog;

            ApplyThemeColors();
        }

        private void ApplyThemeColors()
        {
            try
            {
                var windowBgStr = Config.GetString("窗体背景色");
                var windowFgStr = Config.GetString("窗体字体色");
                var btnBgStr = Config.GetString("按钮背景色");
                var btnFgStr = Config.GetString("按钮字体色");

                var bgColor = (Color)ColorConverter.ConvertFromString("#" + windowBgStr);
                var fgColor = (Color)ColorConverter.ConvertFromString("#" + windowFgStr);
                var btnBgColor = (Color)ColorConverter.ConvertFromString("#" + btnBgStr);
                var btnFgColor = (Color)ColorConverter.ConvertFromString("#" + btnFgStr);

                var bgBrush = new SolidColorBrush(bgColor);
                var fgBrush = new SolidColorBrush(fgColor);
                var btnBgBrush = new SolidColorBrush(btnBgColor);
                var btnFgBrush = new SolidColorBrush(btnFgColor);

                mainBorder.Background = bgBrush;

                double brightness = (bgColor.R * 0.299 + bgColor.G * 0.587 + bgColor.B * 0.114) / 255.0;
                bool isDark = brightness < 0.5;
                var borderColor = isDark
                    ? Color.FromRgb((byte)Math.Min(255, bgColor.R + 50), (byte)Math.Min(255, bgColor.G + 50), (byte)Math.Min(255, bgColor.B + 50))
                    : Color.FromRgb((byte)Math.Max(0, bgColor.R - 30), (byte)Math.Max(0, bgColor.G - 30), (byte)Math.Max(0, bgColor.B - 30));
                mainBorder.BorderBrush = new SolidColorBrush(borderColor);

                txtTitle.Foreground = fgBrush;
                txtVersion.Foreground = fgBrush;
                txtChangelog.Foreground = fgBrush;
                txtProgress.Foreground = fgBrush;

                btnIgnore.Background = btnBgBrush;
                btnIgnore.Foreground = btnFgBrush;
                btnDismiss.Background = btnBgBrush;
                btnDismiss.Foreground = btnFgBrush;

                var accentColor = (Color)ColorConverter.ConvertFromString("#" + Config.GetString("标题栏进度条颜色"));
                btnUpdate.Background = new SolidColorBrush(accentColor);
                btnUpdate.Foreground = new SolidColorBrush(Colors.White);
                progressBar.Foreground = new SolidColorBrush(accentColor);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UpdateDialog] 主题应用失败: {ex.Message}");
            }
        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Result = UpdateDialogResult.Closed;
            Close();
        }

        private void BtnIgnore_Click(object sender, RoutedEventArgs e)
        {
            Result = UpdateDialogResult.IgnoreVersion;
            VersionManager.IgnoreVersion();
            Close();
        }

        private void BtnDismiss_Click(object sender, RoutedEventArgs e)
        {
            Result = UpdateDialogResult.DismissToday;
            VersionManager.DismissToday();
            Close();
        }

        private async void BtnUpdate_Click(object sender, RoutedEventArgs e)
        {
            string url = VersionManager.UpdatePackageUrl;
            if (string.IsNullOrEmpty(url))
            {
                MessageBox.Show("更新地址为空，请前往 Gitee 手动下载。", "提示");
                return;
            }

            panelButtons.Visibility = Visibility.Collapsed;
            gridProgress.Visibility = Visibility.Visible;
            txtProgress.Text = "正在下载更新...";

            try
            {
                string tempDir = Path.Combine(Path.GetTempPath(), "TypeSunnyUpdate");
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
                Directory.CreateDirectory(tempDir);

                string zipPath = Path.Combine(tempDir, "update.zip");

                await DownloadFileAsync(url, zipPath);

                txtProgress.Text = "下载完成，正在启动更新...";

                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                string updaterPath = Path.Combine(appDir, "Updater.exe");

                if (!File.Exists(updaterPath))
                {
                    MessageBox.Show("未找到 Updater.exe，请前往 Gitee 下载全量包。", "提示");
                    panelButtons.Visibility = Visibility.Visible;
                    gridProgress.Visibility = Visibility.Collapsed;
                    return;
                }

                int pid = Process.GetCurrentProcess().Id;
                string mainExe = Process.GetCurrentProcess().MainModule.FileName;
                Process.Start(updaterPath, $"\"{zipPath}\" \"{appDir}\" {pid} \"{mainExe}\"");

                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UpdateDialog] 更新失败: {ex.Message}");
                MessageBox.Show($"更新失败：{ex.Message}\n请前往 Gitee 手动下载。", "提示");
                panelButtons.Visibility = Visibility.Visible;
                gridProgress.Visibility = Visibility.Collapsed;
            }
        }

        private async Task DownloadFileAsync(string url, string filePath)
        {
            using (var client = new HttpClient(new HttpClientHandler { UseProxy = false }))
            {
                client.Timeout = TimeSpan.FromMinutes(5);
                using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    long? totalBytes = response.Content.Headers.ContentLength;

                    using (var stream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                    {
                        var buffer = new byte[8192];
                        long downloaded = 0;
                        int bytesRead;

                        while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead);
                            downloaded += bytesRead;

                            if (totalBytes.HasValue && totalBytes.Value > 0)
                            {
                                int percent = (int)(downloaded * 100 / totalBytes.Value);
                                progressBar.Value = percent;
                                txtProgress.Text = $"正在下载... {downloaded / 1024}KB / {totalBytes.Value / 1024}KB ({percent}%)";
                            }
                            else
                            {
                                txtProgress.Text = $"正在下载... {downloaded / 1024}KB";
                            }
                        }
                    }
                }
            }
        }
    }
}
