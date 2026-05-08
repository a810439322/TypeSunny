using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace TypeSunny
{
    public enum UpdateSource
    {
        Gitee,
        GitHub
    }

    public static class VersionManager
    {
        private const string GiteeReleaseApi = "https://gitee.com/api/v5/repos/fuchuxuan/TypeSunny/releases/latest";
        private const string GitHubReleaseApi = "https://api.github.com/repos/a810439322/TypeSunny/releases/latest";
        private const string GiteeReleasePage = "https://gitee.com/fuchuxuan/TypeSunny/releases";
        private const string GitHubReleasePage = "https://github.com/a810439322/TypeSunny/releases";

        public static string CurrentVersion => GeneratedVersion.CurrentVersion;
        public static UpdateSource PreferredSource { get; private set; } = UpdateSource.Gitee;

        public static string LatestVersion
        {
            get => Config.GetString("最新版本");
            private set => Config.Set("最新版本", value);
        }

        public static string Changelog
        {
            get => Config.GetString("更新说明");
            private set => Config.Set("更新说明", value);
        }

        public static string UpdatePackageUrl
        {
            get => Config.GetString("更新包地址");
            private set => Config.Set("更新包地址", value);
        }

        public static string FullPackageUrl
        {
            get => Config.GetString("全量包地址");
            private set => Config.Set("全量包地址", value);
        }

        private static DateTime LastCheckTime
        {
            get
            {
                string timeStr = Config.GetString("上次检查更新时间");
                if (long.TryParse(timeStr, out long ticks))
                    return new DateTime(ticks);
                return DateTime.MinValue;
            }
            set => Config.Set("上次检查更新时间", value.Ticks.ToString());
        }

        public static string IgnoredVersion
        {
            get => Config.GetString("忽略的版本");
            set => Config.Set("忽略的版本", value);
        }

        private static DateTime DismissUntil
        {
            get
            {
                string timeStr = Config.GetString("今日不再提醒时间");
                if (long.TryParse(timeStr, out long ticks))
                    return new DateTime(ticks);
                return DateTime.MinValue;
            }
            set => Config.Set("今日不再提醒时间", value.Ticks.ToString());
        }

        public static bool ShouldCheckUpdate
        {
            get
            {
                var lastCheck = LastCheckTime;
                if (lastCheck == DateTime.MinValue)
                    return true;
                return (DateTime.Now - lastCheck).TotalHours >= 24;
            }
        }

        public static bool IsDismissedToday => DismissUntil.Date == DateTime.Today;

        public static bool HasUpdate
        {
            get
            {
                try
                {
                    string current = CurrentVersion;
                    string latest = LatestVersion;
                    if (string.IsNullOrEmpty(latest) || latest == "未知")
                        return false;
                    if (string.IsNullOrEmpty(current) || current == "未知")
                        return false;
                    return CompareVersions(latest, current) > 0;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[VersionManager] 检查更新状态失败: {ex.Message}");
                    return false;
                }
            }
        }

        public static bool ShouldShowReminder
        {
            get
            {
                if (!HasUpdate) return false;
                if (LatestVersion == IgnoredVersion) return false;
                if (IsDismissedToday) return false;
                return true;
            }
        }

        public static string ReleasePage =>
            PreferredSource == UpdateSource.Gitee ? GiteeReleasePage : GitHubReleasePage;

        private static int CompareVersions(string v1, string v2)
        {
            v1 = System.Text.RegularExpressions.Regex.Replace(v1, "[^0-9]", "");
            v2 = System.Text.RegularExpressions.Regex.Replace(v2, "[^0-9]", "");
            if (int.TryParse(v1, out int num1) && int.TryParse(v2, out int num2))
                return num1.CompareTo(num2);
            return string.Compare(v1, v2, StringComparison.Ordinal);
        }

        private static async Task<long> MeasureLatencyAsync(string url)
        {
            try
            {
                using (var client = new HttpClient(new HttpClientHandler { UseProxy = false }))
                {
                    client.Timeout = TimeSpan.FromSeconds(5);
                    client.DefaultRequestHeaders.Add("User-Agent", "TypeSunny-Updater");
                    var sw = Stopwatch.StartNew();
                    var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));
                    sw.Stop();
                    Debug.WriteLine($"[VersionManager] {url} latency: {sw.ElapsedMilliseconds}ms, status: {response.StatusCode}");
                    return sw.ElapsedMilliseconds;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[VersionManager] {url} unreachable: {ex.Message}");
                return long.MaxValue;
            }
        }

        private static async Task DetectBestSourceAsync()
        {
            var giteeTask = MeasureLatencyAsync("https://gitee.com/api/v5/repos/fuchuxuan/TypeSunny/releases");
            var githubTask = MeasureLatencyAsync("https://api.github.com/repos/a810439322/TypeSunny/releases");

            await Task.WhenAll(giteeTask, githubTask);

            long giteeMs = giteeTask.Result;
            long githubMs = githubTask.Result;

            PreferredSource = giteeMs <= githubMs ? UpdateSource.Gitee : UpdateSource.GitHub;
            Debug.WriteLine($"[VersionManager] 选择更新源: {PreferredSource} (Gitee={giteeMs}ms, GitHub={githubMs}ms)");
        }

        public static async Task<bool> CheckUpdateAsync(bool forceRefresh = false)
        {
            try
            {
                if (!forceRefresh && !ShouldCheckUpdate)
                {
                    Debug.WriteLine("[VersionManager] 距离上次检查不足24小时，跳过");
                    return false;
                }

                await DetectBestSourceAsync();

                string apiUrl = PreferredSource == UpdateSource.Gitee ? GiteeReleaseApi : GitHubReleaseApi;
                Debug.WriteLine($"[VersionManager] 开始检查更新，请求: {apiUrl}");

                using (var client = new HttpClient(new HttpClientHandler { UseProxy = false }))
                {
                    client.Timeout = TimeSpan.FromSeconds(10);
                    client.DefaultRequestHeaders.Add("User-Agent", "TypeSunny-Updater");
                    var response = await client.GetAsync(apiUrl);
                    if (!response.IsSuccessStatusCode)
                    {
                        Debug.WriteLine($"[VersionManager] HTTP请求失败: {response.StatusCode}");
                        return false;
                    }

                    string content = await response.Content.ReadAsStringAsync();
                    if (string.IsNullOrWhiteSpace(content))
                    {
                        Debug.WriteLine("[VersionManager] 返回内容为空");
                        return false;
                    }

                    var json = JObject.Parse(content);
                    string tagName = json["tag_name"]?.ToString()?.Trim() ?? "";
                    string latestVersion = tagName.TrimStart('v', 'V');

                    if (string.IsNullOrEmpty(latestVersion) ||
                        !System.Text.RegularExpressions.Regex.IsMatch(latestVersion, @"^\d{8}$"))
                    {
                        Debug.WriteLine($"[VersionManager] 版本号格式不正确: {latestVersion}");
                        return false;
                    }

                    LatestVersion = latestVersion;
                    Changelog = json["body"]?.ToString() ?? "";

                    string updateUrl = "";
                    string fullUrl = "";
                    var assets = json["assets"] as JArray;
                    if (assets != null)
                    {
                        foreach (var asset in assets)
                        {
                            string name = asset["name"]?.ToString() ?? "";
                            string url = asset["browser_download_url"]?.ToString() ?? "";
                            if (string.IsNullOrEmpty(fullUrl) && name.Contains("full"))
                                fullUrl = url;
                            else if (string.IsNullOrEmpty(updateUrl) && name.Contains("update"))
                                updateUrl = url;
                        }
                    }
                    UpdatePackageUrl = updateUrl;
                    FullPackageUrl = fullUrl;
                    LastCheckTime = DateTime.Now;

                    Debug.WriteLine($"[VersionManager] 获取到最新版本: {latestVersion}");
                    return HasUpdate;
                }
            }
            catch (TaskCanceledException)
            {
                Debug.WriteLine("[VersionManager] 请求超时");
                return false;
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine($"[VersionManager] 网络请求失败: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[VersionManager] 检查更新失败: {ex.GetType().Name} - {ex.Message}");
                return false;
            }
        }

        public static void DismissToday()
        {
            DismissUntil = DateTime.Today;
            Debug.WriteLine("[VersionManager] 用户选择今日不再提醒");
        }

        public static void IgnoreVersion()
        {
            IgnoredVersion = LatestVersion;
            Debug.WriteLine($"[VersionManager] 用户选择忽略版本: {LatestVersion}");
        }

        public static void DismissUpdateReminder()
        {
            LastCheckTime = DateTime.Now;
        }

        public static string GetVersionFileUrl() => GiteeReleaseApi;
    }
}
