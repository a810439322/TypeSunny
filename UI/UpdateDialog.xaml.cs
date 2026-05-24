using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using TypeSunny.Utils;

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

            this.EnableEscapeToClose();

            txtVersion.Text = $"当前版本：{VersionManager.CurrentVersion}　→　最新版本：{VersionManager.LatestVersion}";
            txtChangelog.Text = string.IsNullOrEmpty(VersionManager.Changelog) ? "暂无更新说明" : VersionManager.Changelog;

            btnUpdate.Content = FormatButtonText("立即更新", VersionManager.UpdatePackageSize);
            btnFullUpdate.Content = FormatButtonText("全量更新", VersionManager.FullPackageSize);

            ApplyThemeColors();
        }

        private static string FormatButtonText(string label, long bytes)
        {
            if (bytes <= 0) return label;
            double mb = bytes / 1024.0 / 1024.0;
            return $"{label} ({mb:F1}MB)";
        }

        private void ApplyThemeColors()
        {
            DialogTheming.Apply(
                mainBorder,
                new[] { txtTitle, txtVersion, txtChangelog, txtProgress },
                new[] { btnIgnore, btnDismiss, btnFullUpdate },
                btnUpdate,
                progressBar);
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
            => await RunDownloadAsync(() => VersionManager.UpdatePackageUrl);

        private async void BtnFullUpdate_Click(object sender, RoutedEventArgs e)
            => await RunDownloadAsync(() => VersionManager.FullPackageUrl);

        private async Task RunDownloadAsync(Func<string> urlGetter)
        {
            string url = urlGetter();
            if (string.IsNullOrEmpty(url))
            {
                await VersionManager.CheckUpdateAsync(forceRefresh: true);
                url = urlGetter();
            }
            if (string.IsNullOrEmpty(url))
            {
                MessageBox.Show("更新地址为空，请前往 Gitee 手动下载。", "提示");
                return;
            }

            string updaterPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Updater.exe");
            if (!File.Exists(updaterPath))
            {
                var result = MessageBox.Show("未找到 Updater.exe，需要下载全量包。\n是否打开下载页面？", "提示",
                    MessageBoxButton.YesNo);
                if (result == MessageBoxResult.Yes)
                    Process.Start(VersionManager.ReleasePage);
                return;
            }

            panelButtons.Visibility = Visibility.Collapsed;
            gridProgress.Visibility = Visibility.Visible;
            txtProgress.Text = "正在下载更新...";

            try
            {
                var progress = new Progress<(long downloaded, long? total)>(value =>
                {
                    long downloaded = value.downloaded;
                    long? total = value.total;
                    if (total.HasValue && total.Value > 0)
                    {
                        int percent = (int)(downloaded * 100 / total.Value);
                        progressBar.Value = percent;
                        txtProgress.Text = $"正在下载... {downloaded / 1024}KB / {total.Value / 1024}KB ({percent}%)";
                    }
                    else
                    {
                        txtProgress.Text = $"正在下载... {downloaded / 1024}KB";
                    }
                });

                await UpdatePackageDownloader.DownloadAndApplyAsync(url, progress, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UpdateDialog] 更新失败: {ex.Message}");
                MessageBox.Show($"更新失败：{ex.Message}\n请前往 Gitee 手动下载。", "提示");
                panelButtons.Visibility = Visibility.Visible;
                gridProgress.Visibility = Visibility.Collapsed;
            }
        }
    }
}
