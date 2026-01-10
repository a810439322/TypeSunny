using System.Collections.Generic;

namespace TypeSunny
{
    /// <summary>
    /// 主题助手类，管理明暗主题的颜色配置
    /// </summary>
    internal static class ThemeHelper
    {
        // 明色主题预设
        private static Dictionary<string, string> LightTheme = new Dictionary<string, string>
        {
            { "窗体背景色", "F7F7F7" },
            { "窗体字体色", "333333" },
            { "跟打区背景色", "EDEDED" },
            { "跟打区字体色", "000000" },
            { "发文区字体色", "000000" },
            { "打对色", "A2CCD7" },
            { "打错色", "FF6347" },
            { "按钮背景色", "EBEBEB" },
            { "按钮字体色", "000000" },
            { "菜单背景色", "EBEBEB" },
            { "菜单字体色", "000000" }
        };

        // 暗色主题预设
        private static Dictionary<string, string> DarkTheme = new Dictionary<string, string>
        {
            { "窗体背景色", "1E1E1E" },
            { "窗体字体色", "CCCCCC" },
            { "跟打区背景色", "2D2D2D" },
            { "跟打区字体色", "E0E0E0" },
            { "发文区字体色", "E0E0E0" },
            { "打对色", "4EC9B0" },
            { "打错色", "F44747" },
            { "按钮背景色", "3C3C3C" },
            { "按钮字体色", "E0E0E0" },
            { "菜单背景色", "3C3C3C" },
            { "菜单字体色", "E0E0E0" }
        };

        /// <summary>
        /// 应用主题到配置
        /// </summary>
        /// <param name="themeName">主题名称：明/暗</param>
        public static void ApplyTheme(string themeName)
        {
            Dictionary<string, string> theme = null;

            if (themeName == "明")
            {
                theme = LightTheme;
            }
            else if (themeName == "暗")
            {
                theme = DarkTheme;
            }
            else
            {
                // 自定义模式，不做任何修改
                return;
            }

            // 应用主题颜色到配置
            if (theme != null)
            {
                foreach (var colorItem in theme)
                {
                    Config.dicts[colorItem.Key] = colorItem.Value;
                }
            }
        }

        /// <summary>
        /// 获取指定主题的颜色值
        /// </summary>
        public static string GetThemeColor(string themeName, string colorKey)
        {
            if (themeName == "明" && LightTheme.ContainsKey(colorKey))
            {
                return LightTheme[colorKey];
            }
            else if (themeName == "暗" && DarkTheme.ContainsKey(colorKey))
            {
                return DarkTheme[colorKey];
            }
            else
            {
                // 返回当前配置的值
                return Config.GetString(colorKey);
            }
        }
    }
}
