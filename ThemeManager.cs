using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TypeSunny
{
    /// <summary>
    /// 主题管理类，支持多主题配置，每个主题保存为单独文件
    /// </summary>
    public static class ThemeManager
    {
        private static readonly string ThemesFolder = "Resources/主题";
        private static readonly string ThemeIndexFile = "Resources/主题/index.txt";

        /// <summary>
        /// 主题颜色配置
        /// </summary>
        public class ThemeColors
        {
            public string WindowBackground { get; set; } = "f7f7f7";
            public string WindowForeground { get; set; } = "D3D3D3";
            public string DisplayBackground { get; set; } = "ededed";
            public string DisplayForeground { get; set; } = "000000";
            public string ArticleForeground { get; set; } = "000000";
            public string CorrectBackground { get; set; } = "A2CCD7";
            public string IncorrectBackground { get; set; } = "FF6347";
            public string ButtonBackground { get; set; } = "EBEBEB";
            public string ButtonForeground { get; set; } = "000000";
            public string MenuBackground { get; set; } = "EBEBEB";
            public string MenuForeground { get; set; } = "000000";
            public string ProgressBarColor { get; set; } = "007ACC";
        }

        /// <summary>
        /// 预定义主题
        /// </summary>
        private static readonly Dictionary<string, ThemeColors> BuiltInThemes = new Dictionary<string, ThemeColors>
        {
            ["明"] = new ThemeColors
            {
                WindowBackground = "f7f7f7",
                WindowForeground = "606060",
                DisplayBackground = "ededed",
                DisplayForeground = "000000",
                ArticleForeground = "000000",
                CorrectBackground = "A2CCD7",
                IncorrectBackground = "FF6347",
                ButtonBackground = "EBEBEB",
                ButtonForeground = "000000",
                MenuBackground = "EBEBEB",
                MenuForeground = "000000",
                ProgressBarColor = "007ACC"
            },
            ["暗"] = new ThemeColors
            {
                WindowBackground = "2d2d2d",
                WindowForeground = "D3D3D3",
                DisplayBackground = "1e1e1e",
                DisplayForeground = "D3D3D3",
                ArticleForeground = "D3D3D3",
                CorrectBackground = "005353",
                IncorrectBackground = "FF6347",
                ButtonBackground = "3d3d3d",
                ButtonForeground = "D3D3D3",
                MenuBackground = "3d3d3d",
                MenuForeground = "D3D3D3",
                ProgressBarColor = "007ACC"
            },
            ["pain"] = new ThemeColors
            {
                WindowBackground = "505050",
                WindowForeground = "D3D3D3",
                DisplayBackground = "C5B28F",
                DisplayForeground = "000000",
                ArticleForeground = "000000",
                CorrectBackground = "95b0e3",
                IncorrectBackground = "FF6347",
                ButtonBackground = "666666",
                ButtonForeground = "eeeeee",
                MenuBackground = "666666",
                MenuForeground = "eeeeee",
                ProgressBarColor = "95b0e3"
            }
        };

        /// <summary>
        /// 初始化主题管理器
        /// </summary>
        static ThemeManager()
        {
            // 确保主题文件夹存在
            if (!Directory.Exists(ThemesFolder))
            {
                Directory.CreateDirectory(ThemesFolder);
            }

            // 初始化内置主题文件
            InitializeBuiltInThemes();
        }

        /// <summary>
        /// 初始化内置主题文件
        /// </summary>
        private static void InitializeBuiltInThemes()
        {
            foreach (var theme in BuiltInThemes)
            {
                string themeFile = Path.Combine(ThemesFolder, $"{theme.Key}.txt");
                if (!File.Exists(themeFile))
                {
                    SaveTheme(theme.Key, theme.Value);
                }
            }
        }

        /// <summary>
        /// 获取所有可用主题列表
        /// </summary>
        public static string[] GetAvailableThemes()
        {
            var themes = new List<string>(BuiltInThemes.Keys);

            // 添加自定义主题
            if (Directory.Exists(ThemesFolder))
            {
                foreach (var file in Directory.GetFiles(ThemesFolder, "*.txt"))
                {
                    string themeName = Path.GetFileNameWithoutExtension(file);
                    if (!BuiltInThemes.ContainsKey(themeName))
                    {
                        themes.Add(themeName);
                    }
                }
            }

            return themes.OrderBy(t => t).ToArray();
        }

        /// <summary>
        /// 加载主题颜色配置
        /// </summary>
        public static ThemeColors LoadTheme(string themeName)
        {
            // 如果是内置主题，直接返回
            if (BuiltInThemes.ContainsKey(themeName))
            {
                return CloneTheme(BuiltInThemes[themeName]);
            }

            // 从文件加载自定义主题
            string themeFile = Path.Combine(ThemesFolder, $"{themeName}.txt");
            if (File.Exists(themeFile))
            {
                return LoadThemeFromFile(themeFile);
            }

            // 默认返回明亮主题
            return CloneTheme(BuiltInThemes["明"]);
        }

        /// <summary>
        /// 从文件加载主题
        /// </summary>
        private static ThemeColors LoadThemeFromFile(string filePath)
        {
            var colors = new ThemeColors();
            foreach (var line in File.ReadAllLines(filePath))
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    continue;

                string[] parts = line.Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    string key = parts[0];
                    string value = parts[1];

                    switch (key)
                    {
                        case "窗体背景色": colors.WindowBackground = value; break;
                        case "窗体字体色": colors.WindowForeground = value; break;
                        case "跟打区背景色": colors.DisplayBackground = value; break;
                        case "跟打区字体色": colors.DisplayForeground = value; break;
                        case "发文区字体色": colors.ArticleForeground = value; break;
                        case "打对色": colors.CorrectBackground = value; break;
                        case "打错色": colors.IncorrectBackground = value; break;
                        case "按钮背景色": colors.ButtonBackground = value; break;
                        case "按钮字体色": colors.ButtonForeground = value; break;
                        case "菜单背景色": colors.MenuBackground = value; break;
                        case "菜单字体色": colors.MenuForeground = value; break;
                        case "标题栏进度条颜色": colors.ProgressBarColor = value; break;
                    }
                }
            }
            return colors;
        }

        /// <summary>
        /// 保存主题到文件
        /// </summary>
        public static void SaveTheme(string themeName, ThemeColors colors)
        {
            string themeFile = Path.Combine(ThemesFolder, $"{themeName}.txt");
            var lines = new List<string>
            {
                "# 主题配置文件",
                $"窗体背景色\t{colors.WindowBackground}",
                $"窗体字体色\t{colors.WindowForeground}",
                $"跟打区背景色\t{colors.DisplayBackground}",
                $"跟打区字体色\t{colors.DisplayForeground}",
                $"发文区字体色\t{colors.ArticleForeground}",
                $"打对色\t{colors.CorrectBackground}",
                $"打错色\t{colors.IncorrectBackground}",
                $"按钮背景色\t{colors.ButtonBackground}",
                $"按钮字体色\t{colors.ButtonForeground}",
                $"菜单背景色\t{colors.MenuBackground}",
                $"菜单字体色\t{colors.MenuForeground}",
                $"标题栏进度条颜色\t{colors.ProgressBarColor}"
            };
            File.WriteAllLines(themeFile, lines);
        }

        /// <summary>
        /// 删除主题
        /// </summary>
        public static bool DeleteTheme(string themeName)
        {
            // 不能删除内置主题
            if (BuiltInThemes.ContainsKey(themeName))
            {
                return false;
            }

            string themeFile = Path.Combine(ThemesFolder, $"{themeName}.txt");
            if (File.Exists(themeFile))
            {
                File.Delete(themeFile);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 重命名主题
        /// </summary>
        public static bool RenameTheme(string oldName, string newName)
        {
            // 不能重命名内置主题
            if (BuiltInThemes.ContainsKey(oldName))
            {
                return false;
            }

            // 新名称不能是内置主题名称
            if (BuiltInThemes.ContainsKey(newName))
            {
                return false;
            }

            string oldFile = Path.Combine(ThemesFolder, $"{oldName}.txt");
            string newFile = Path.Combine(ThemesFolder, $"{newName}.txt");

            if (!File.Exists(oldFile))
            {
                return false;
            }

            // 如果新文件名已存在，不覆盖
            if (File.Exists(newFile))
            {
                return false;
            }

            File.Move(oldFile, newFile);
            return true;
        }

        /// <summary>
        /// 检查主题是否为内置主题
        /// </summary>
        public static bool IsBuiltInTheme(string themeName)
        {
            return BuiltInThemes.ContainsKey(themeName);
        }

        /// <summary>
        /// 克隆主题配置
        /// </summary>
        private static ThemeColors CloneTheme(ThemeColors source)
        {
            return new ThemeColors
            {
                WindowBackground = source.WindowBackground,
                WindowForeground = source.WindowForeground,
                DisplayBackground = source.DisplayBackground,
                DisplayForeground = source.DisplayForeground,
                ArticleForeground = source.ArticleForeground,
                CorrectBackground = source.CorrectBackground,
                IncorrectBackground = source.IncorrectBackground,
                ButtonBackground = source.ButtonBackground,
                ButtonForeground = source.ButtonForeground,
                MenuBackground = source.MenuBackground,
                MenuForeground = source.MenuForeground,
                ProgressBarColor = source.ProgressBarColor
            };
        }

        /// <summary>
        /// 从 Config 读取当前主题并应用
        /// </summary>
        public static void ApplyCurrentTheme()
        {
            string themeMode = Config.GetString("主题模式");
            ThemeColors colors;

            if (themeMode == "自定义")
            {
                // 从配置读取自定义主题颜色
                colors = new ThemeColors
                {
                    WindowBackground = Config.GetString("窗体背景色"),
                    WindowForeground = Config.GetString("窗体字体色"),
                    DisplayBackground = Config.GetString("跟打区背景色"),
                    DisplayForeground = Config.GetString("跟打区字体色"),
                    ArticleForeground = Config.GetString("发文区字体色"),
                    CorrectBackground = Config.GetString("打对色"),
                    IncorrectBackground = Config.GetString("打错色"),
                    ButtonBackground = Config.GetString("按钮背景色"),
                    ButtonForeground = Config.GetString("按钮字体色"),
                    MenuBackground = Config.GetString("菜单背景色"),
                    MenuForeground = Config.GetString("菜单字体色"),
                    ProgressBarColor = Config.GetString("标题栏进度条颜色")
                };
            }
            else
            {
                // 加载预定义或自定义主题文件
                colors = LoadTheme(themeMode);
            }

            // 应用颜色到 Config
            Config.dicts["窗体背景色"] = colors.WindowBackground;
            Config.dicts["窗体字体色"] = colors.WindowForeground;
            Config.dicts["跟打区背景色"] = colors.DisplayBackground;
            Config.dicts["跟打区字体色"] = colors.DisplayForeground;
            Config.dicts["发文区字体色"] = colors.ArticleForeground;
            Config.dicts["打对色"] = colors.CorrectBackground;
            Config.dicts["打错色"] = colors.IncorrectBackground;
            Config.dicts["按钮背景色"] = colors.ButtonBackground;
            Config.dicts["按钮字体色"] = colors.ButtonForeground;
            Config.dicts["菜单背景色"] = colors.MenuBackground;
            Config.dicts["菜单字体色"] = colors.MenuForeground;
            Config.dicts["标题栏进度条颜色"] = colors.ProgressBarColor;
        }
    }
}
