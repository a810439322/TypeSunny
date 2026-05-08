using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using TypeSunny.Utils;

namespace TypeSunny.UI
{
    public partial class ShuangMissingDialog : Window
    {
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public ShuangMissingDialog(Window owner)
        {
            InitializeComponent();
            Owner = owner;

            txtVersion.Text = $"当前版本：{VersionManager.CurrentVersion}";
            txtChangelog.Text = "晴双拼资源不在当前安装版本中。需要下载完整包并重启程序替换。是否继续？";

            ApplyThemeColors();
        }

        private void ApplyThemeColors()
        {
            DialogTheming.Apply(
                mainBorder,
                new[] { txtTitle, txtVersion, txtChangelog, txtProgress },
                new[] { btnCancel },
                btnConfirm,
                progressBar);
        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            _cts.Cancel();
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            _cts.Cancel();
            Close();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            _cts.Cancel();
            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            _cts.Dispose();
            base.OnClosed(e);
        }

        private async void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            string url = VersionManager.FullPackageUrl;
            if (string.IsNullOrEmpty(url))
            {
                await VersionManager.CheckUpdateAsync(forceRefresh: true);
                url = VersionManager.FullPackageUrl;
            }

            if (string.IsNullOrEmpty(url))
            {
                var result = MessageBox.Show("无法获取更新源，是否打开下载页面？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes)
                    Process.Start(VersionManager.ReleasePage);
                return;
            }

            panelButtons.Visibility = Visibility.Collapsed;
            gridProgress.Visibility = Visibility.Visible;
            progressBar.Value = 0;
            progressBar.IsIndeterminate = false;
            txtProgress.Text = "正在下载完整包...";

            try
            {
                var progress = new Progress<(long downloaded, long? total)>(value =>
                {
                    if (!IsLoaded)
                        return;

                    long downloaded = value.downloaded;
                    long? total = value.total;
                    if (total.HasValue && total.Value > 0)
                    {
                        int percent = (int)(downloaded * 100 / total.Value);
                        progressBar.IsIndeterminate = false;
                        progressBar.Value = percent;
                        txtProgress.Text = $"正在下载... {downloaded / 1024}KB / {total.Value / 1024}KB ({percent}%)";
                    }
                    else
                    {
                        progressBar.IsIndeterminate = true;
                        txtProgress.Text = $"正在下载... {downloaded / 1024}KB";
                    }
                });

                await UpdatePackageDownloader.DownloadAndApplyAsync(url, progress, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("[ShuangMissingDialog] 用户取消下载");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ShuangMissingDialog] 下载失败: {ex.Message}");
                if (IsLoaded)
                {
                    MessageBox.Show($"下载失败：{ex.Message}\n请前往下载页面手动安装完整包。", "提示");
                    panelButtons.Visibility = Visibility.Visible;
                    gridProgress.Visibility = Visibility.Collapsed;
                }
            }
        }
    }
}
