using System;
using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;

namespace TypeSunny
{
    /// <summary>
    /// 版本管理类：处理版本检测和更新提醒
    /// </summary>
    public static class VersionManager
    {
        // GitHub 版本文件 URL（使用 jsDelivr CDN 加速）
        private const string VersionFileUrl = "https://cdn.jsdelivr.net/gh/a810439322/TypeSunny@master/Version/version.txt";

        // 当前版本（从 GeneratedVersion.cs 读取，由 MSBuild 在编译时生成）
        public static string CurrentVersion => GeneratedVersion.CurrentVersion;

        // 最新版本（从 GitHub 获取，缓存到配置中）
        public static string LatestVersion
        {
            get => Config.GetString("最新版本");
            private set => Config.Set("最新版本", value);
        }

        // 上次检查更新时间
        private static DateTime LastCheckTime
        {
            get
            {
                string timeStr = Config.GetString("上次检查更新时间");
                if (long.TryParse(timeStr, out long ticks))
                {
                    return new DateTime(ticks);
                }
                return DateTime.MinValue;
            }
            set
            {
                Config.Set("上次检查更新时间", value.Ticks.ToString());
            }
        }

        // 是否需要检查更新（距离上次检查超过24小时）
        public static bool ShouldCheckUpdate
        {
            get
            {
                var lastCheck = LastCheckTime;
                if (lastCheck == DateTime.MinValue)
                    return true; // 从未检查过

                return (DateTime.Now - lastCheck).TotalHours >= 24;
            }
        }

        // 检查是否有新版本可用
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

                    // 日期格式版本号比较（如 260121 > 260120）
                    return CompareVersions(latest, current) > 0;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[VersionManager] 检查更新状态失败: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// 比较两个版本号（日期格式 YYMMDD）
        /// 返回值：>0 表示 v1 > v2，<0 表示 v1 < v2，=0 表示相等
        /// </summary>
        private static int CompareVersions(string v1, string v2)
        {
            // 移除可能的 v 前缀和非数字字符
            v1 = System.Text.RegularExpressions.Regex.Replace(v1, "[^0-9]", "");
            v2 = System.Text.RegularExpressions.Regex.Replace(v2, "[^0-9]", "");

            if (int.TryParse(v1, out int num1) && int.TryParse(v2, out int num2))
            {
                return num1.CompareTo(num2);
            }
            return string.Compare(v1, v2, StringComparison.Ordinal);
        }

        /// <summary>
        /// 检查更新（异步方法）
        /// </summary>
        public static async Task<bool> CheckUpdateAsync(bool forceRefresh = false)
        {
            try
            {
                // 如果不是强制刷新，且距离上次检查不足24小时，跳过
                if (!forceRefresh && !ShouldCheckUpdate)
                {
                    Debug.WriteLine($"[VersionManager] 距离上次检查不足24小时，跳过");
                    return false;  // 跳过检查时不返回 HasUpdate，避免重复显示提醒
                }

                Debug.WriteLine($"[VersionManager] 开始检查更新，请求: {VersionFileUrl}");

                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(10);

                    // 发送请求
                    var response = await client.GetAsync(VersionFileUrl);

                    // 检查响应状态
                    if (!response.IsSuccessStatusCode)
                    {
                        Debug.WriteLine($"[VersionManager] HTTP请求失败: {response.StatusCode}");
                        return false;
                    }

                    // 读取内容
                    string content = await response.Content.ReadAsStringAsync();
                    if (string.IsNullOrWhiteSpace(content))
                    {
                        Debug.WriteLine("[VersionManager] 版本文件内容为空");
                        return false;
                    }

                    string latestVersion = content.Trim();
                    Debug.WriteLine($"[VersionManager] 获取到最新版本: {latestVersion}");

                    // 验证版本号格式（应该是6位数字）
                    if (!System.Text.RegularExpressions.Regex.IsMatch(latestVersion, @"^\d{6}$"))
                    {
                        Debug.WriteLine($"[VersionManager] 版本号格式不正确: {latestVersion}");
                        return false;
                    }

                    // 更新最新版本（通过 Config 保存）
                    Config.Set("最新版本", latestVersion);
                    LastCheckTime = DateTime.Now;

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

        /// <summary>
        /// 隐藏更新提醒（用户点击后调用）
        /// </summary>
        public static void DismissUpdateReminder()
        {
            try
            {
                // 更新上次检查时间，这样24小时内不会再提醒
                LastCheckTime = DateTime.Now;
                Debug.WriteLine($"[VersionManager] 用户已关闭更新提醒");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[VersionManager] 关闭更新提醒失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取版本文件 URL（供配置页面显示）
        /// </summary>
        public static string GetVersionFileUrl()
        {
            return VersionFileUrl;
        }
    }
}
