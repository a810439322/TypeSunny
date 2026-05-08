using System;
using System.Collections.Generic;
using System.Linq;

namespace TypeSunny.UI
{
    public sealed class HomeToolbarEntry
    {
        public HomeToolbarEntry(string key, string displayName, string visibilityConfigKey)
        {
            Key = key;
            DisplayName = displayName;
            VisibilityConfigKey = visibilityConfigKey;
        }

        public string Key { get; private set; }
        public string DisplayName { get; private set; }
        public string VisibilityConfigKey { get; private set; }
    }

    public static class HomeToolbarSettings
    {
        public const string FeatureOrderConfigKey = "首页功能按钮顺序";
        public const string ShowSettingsConfigKey = "显示首页设置";
        public const string ShowLocalArticleConfigKey = "显示首页本地文章";
        public const string ShowRetryConfigKey = "显示首页重打";
        public const string ShowClipboardConfigKey = "显示首页剪贴板载文";
        public const string ShowGroupArticleConfigKey = "显示首页群载文";
        public const string ShowGroupPickerConfigKey = "显示首页选群";

        public static readonly HomeToolbarEntry[] FeatureEntries =
        {
            new HomeToolbarEntry("wenlai", "文来", "显示首页文来"),
            new HomeToolbarEntry("trainer", "晴练单", "显示首页练单"),
            new HomeToolbarEntry("shuang", "晴双拼", "显示首页晴双拼"),
            new HomeToolbarEntry("race", "赛文", "显示首页赛文")
        };

        public static readonly HomeToolbarEntry[] FixedModuleEntries =
        {
            new HomeToolbarEntry("settings", "设置", ShowSettingsConfigKey),
            new HomeToolbarEntry("localArticle", "本地文章模块", ShowLocalArticleConfigKey),
            new HomeToolbarEntry("retry", "重打", ShowRetryConfigKey),
            new HomeToolbarEntry("clipboard", "剪贴板载文", ShowClipboardConfigKey),
            new HomeToolbarEntry("groupArticle", "群载文", ShowGroupArticleConfigKey),
            new HomeToolbarEntry("groupPicker", "选群", ShowGroupPickerConfigKey)
        };

        public static string NormalizeFeatureOrder(string storedOrder)
        {
            return string.Join(",", GetFeatureEntries(storedOrder).Select(entry => entry.DisplayName));
        }

        public static IList<HomeToolbarEntry> GetVisibleFeatureEntries(
            string storedOrder,
            IDictionary<string, bool> visibility)
        {
            return GetFeatureEntries(storedOrder)
                .Where(entry => IsVisible(entry.VisibilityConfigKey, visibility))
                .ToList();
        }

        public static IList<HomeToolbarEntry> GetFeatureEntries(string storedOrder)
        {
            var result = new List<HomeToolbarEntry>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var token in SplitOrder(storedOrder))
            {
                var entry = FindFeatureEntry(token);
                if (entry == null || seen.Contains(entry.Key))
                    continue;

                seen.Add(entry.Key);
                result.Add(entry);
            }

            foreach (var entry in FeatureEntries)
            {
                if (seen.Contains(entry.Key))
                    continue;

                seen.Add(entry.Key);
                result.Add(entry);
            }

            return result;
        }

        public static HomeToolbarEntry FindFeatureEntry(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            string trimmed = value.Trim();
            return FeatureEntries.FirstOrDefault(entry =>
                string.Equals(entry.Key, trimmed, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(entry.DisplayName, trimmed, StringComparison.OrdinalIgnoreCase) ||
                IsLegacyName(entry, trimmed));
        }

        private static bool IsLegacyName(HomeToolbarEntry entry, string value)
        {
            if (entry.Key == "shuang")
                return string.Equals(value, "双拼练习", StringComparison.OrdinalIgnoreCase);
            if (entry.Key == "trainer")
                return string.Equals(value, "晴练单", StringComparison.OrdinalIgnoreCase);
            if (entry.Key == "race")
                return string.Equals(value, "🏆 赛文", StringComparison.OrdinalIgnoreCase);
            return false;
        }

        private static IEnumerable<string> SplitOrder(string storedOrder)
        {
            if (string.IsNullOrWhiteSpace(storedOrder))
                yield break;

            foreach (var part in storedOrder.Split(new[] { ',', '，', '|', ';', '；' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string token = part.Trim();
                if (token.Length > 0)
                    yield return token;
            }
        }

        private static bool IsVisible(string key, IDictionary<string, bool> visibility)
        {
            bool value;
            return visibility == null || !visibility.TryGetValue(key, out value) || value;
        }
    }
}
