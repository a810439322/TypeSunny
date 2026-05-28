using System;
using System.Collections.Generic;
using System.Linq;

namespace TypeSunny.UI
{
    internal static class TrainerMainWindowConfigScope
    {
        public const string EnabledConfigKey = "练单主窗口单独记忆";
        public const string Prefix = "练单场景_";

        private static bool _isTrainerScopeActive;

        private static readonly HashSet<string> ScopedKeys = new HashSet<string>
        {
            "窗口高度",
            "窗口宽度",
            "窗口坐标X",
            "窗口坐标Y",
            "一键极简",
            "一键极简后窗口高度",
            "成绩面板展开",
            "展开窗口高度",
            "发文区跟打区比例",
            "成绩区高度比例",
            "首页功能按钮顺序",
            "显示首页文来",
            "显示首页练单",
            "显示首页晴双拼",
            "显示首页赛文",
            "显示首页设置",
            "显示首页本地文章",
            "显示首页重打",
            "显示首页剪贴板载文",
            "显示首页群载文",
            "显示首页选群"
        };

        public static bool IsTrainerScopeActive
        {
            get { return _isTrainerScopeActive; }
        }

        public static bool IsEnabled
        {
            get { return Config.GetBool(EnabledConfigKey); }
        }

        public static void EnterTrainerScope()
        {
            _isTrainerScopeActive = true;
        }

        public static void ExitTrainerScope()
        {
            _isTrainerScopeActive = false;
        }

        public static bool IsScopedKey(string key)
        {
            return ScopedKeys.Contains(key);
        }

        public static string ResolveKey(string key)
        {
            if (!_isTrainerScopeActive || !IsEnabled || !IsScopedKey(key))
                return key;

            return Prefix + key;
        }

        public static string ResolveActiveScopeKey(string key)
        {
            if (!_isTrainerScopeActive || !IsScopedKey(key))
                return key;

            return Prefix + key;
        }

        public static bool HasCurrentScopeValue(string key)
        {
            return Config.dicts.ContainsKey(ResolveKey(key));
        }

        public static string GetString(string key)
        {
            string scopedKey = ResolveKey(key);
            if (scopedKey != key && Config.dicts.ContainsKey(scopedKey) && Config.dicts[scopedKey] != "")
                return Config.dicts[scopedKey];

            return Config.GetString(key);
        }

        public static bool GetBool(string key)
        {
            return GetString(key) == "是";
        }

        public static int GetInt(string key)
        {
            int value;
            return int.TryParse(GetString(key), out value) ? value : 0;
        }

        public static double GetDouble(string key)
        {
            double value;
            return double.TryParse(GetString(key), out value) ? value : 0;
        }

        public static void Set(string key, bool value)
        {
            Config.Set(ResolveKey(key), value);
        }

        public static void Set(string key, int value)
        {
            Config.Set(ResolveKey(key), value);
        }

        public static void Set(string key, string value)
        {
            Config.Set(ResolveKey(key), value);
        }

        public static void Set(string key, double value, int fraction = -1)
        {
            Config.Set(ResolveKey(key), value, fraction);
        }

        public static void SetActiveScope(string key, double value, int fraction = -1)
        {
            Config.Set(ResolveActiveScopeKey(key), value, fraction);
        }

        public static void SetRaw(string key, string value)
        {
            Config.dicts[ResolveKey(key)] = value;
            Config.WriteConfig(3000);
        }

        public static void SetActiveScopeRaw(string key, string value)
        {
            Config.dicts[ResolveActiveScopeKey(key)] = value;
            Config.WriteConfig(3000);
        }

        public static IEnumerable<string> GetAllTrainerScopedKeys()
        {
            return ScopedKeys.Select(key => Prefix + key);
        }

        public static void ResetTrainerScopedValues()
        {
            foreach (string key in GetAllTrainerScopedKeys().ToList())
                Config.dicts.Remove(key);

            Config.WriteConfig(0);
        }
    }
}
