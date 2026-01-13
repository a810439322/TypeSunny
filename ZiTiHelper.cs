using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace TypeSunny
{
    /// <summary>
    /// 字提功能帮助类，用于读取和查询字提数据
    /// </summary>
    internal static class ZiTiHelper
    {
        private static Dictionary<string, string> _ziTiDict = null;
        private static bool _initialized = false;
        private static int _loadedCount = 0;
        private static string _currentScheme = null;

        /// <summary>
        /// 获取所有可用的字提方案
        /// </summary>
        /// <returns>方案名称列表（不含扩展名）</returns>
        public static List<string> GetAvailableSchemes()
        {
            var schemes = new List<string>();

            // 从 Resources/字提 文件夹查找所有 txt 文件
            string[] possiblePaths = new string[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "字提"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "Resources", "字提"),
                Path.Combine("Resources", "字提")
            };

            string schemeDir = null;
            foreach (string path in possiblePaths)
            {
                string fullPath = Path.GetFullPath(path);
                if (Directory.Exists(fullPath))
                {
                    schemeDir = fullPath;
                    break;
                }
            }

            if (schemeDir != null)
            {
                foreach (string file in Directory.GetFiles(schemeDir, "*.txt"))
                {
                    schemes.Add(Path.GetFileNameWithoutExtension(file));
                }
            }

            // 兼容旧的单文件模式（Resources/字提.txt）
            if (schemes.Count == 0)
            {
                string[] legacyPaths = new string[]
                {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "字提.txt"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "Resources", "字提.txt"),
                    Path.Combine("Resources", "字提.txt")
                };

                foreach (string path in legacyPaths)
                {
                    if (File.Exists(Path.GetFullPath(path)))
                    {
                        schemes.Add("默认");
                        break;
                    }
                }
            }

            return schemes;
        }

        /// <summary>
        /// 初始化字提数据
        /// </summary>
        public static void Initialize()
        {
            Initialize(null);
        }

        /// <summary>
        /// 初始化字提数据（指定方案）
        /// </summary>
        /// <param name="scheme">方案名称，null表示使用默认</param>
        public static void Initialize(string scheme)
        {
            string schemeKey = scheme ?? "默认";

            // 如果已经初始化且方案未变，则不重新加载
            if (_initialized && _currentScheme == schemeKey)
                return;

            _ziTiDict = new Dictionary<string, string>();
            _loadedCount = 0;

            string filePath = null;

            // 如果指定了方案，从 Resources/字提 文件夹查找
            if (scheme != null)
            {
                string[] possibleDirs = new string[]
                {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "字提"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "Resources", "字提"),
                    Path.Combine("Resources", "字提")
                };

                foreach (string dir in possibleDirs)
                {
                    string fullPath = Path.GetFullPath(dir);
                    if (Directory.Exists(fullPath))
                    {
                        string schemeFile = Path.Combine(fullPath, scheme + ".txt");
                        if (File.Exists(schemeFile))
                        {
                            filePath = schemeFile;
                            break;
                        }
                    }
                }
            }
            else
            {
                // 兼容旧的单文件模式
                string[] possiblePaths = new string[]
                {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "字提.txt"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "Resources", "字提.txt"),
                    Path.Combine("Resources", "字提.txt"),
                    "字提.txt"
                };

                foreach (string path in possiblePaths)
                {
                    string fullPath = Path.GetFullPath(path);
                    if (File.Exists(fullPath))
                    {
                        filePath = fullPath;
                        break;
                    }
                }
            }

            if (filePath == null)
            {
                _initialized = true;
                return;
            }

            try
            {

                // 尝试多种编码读取
                string[] lines = null;
                Encoding[] encodings = new Encoding[]
                {
                    Encoding.UTF8,
                    Encoding.GetEncoding("GB2312"),
                    Encoding.GetEncoding("GBK"),
                    Encoding.Default
                };

                foreach (Encoding encoding in encodings)
                {
                    try
                    {
                        lines = File.ReadAllLines(filePath, encoding);
                        break;
                    }
                    catch { }
                }

                if (lines == null)
                    lines = File.ReadAllLines(filePath, Encoding.UTF8);

                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    string trimmedLine = line.TrimStart();

                    // 跳过BOM标记
                    if (trimmedLine.Length > 0 && trimmedLine[0] == '\uFEFF')
                        trimmedLine = trimmedLine.Substring(1);

                    int tabIndex = trimmedLine.IndexOf('\t');
                    if (tabIndex > 0 && tabIndex < trimmedLine.Length - 1)
                    {
                        string character = trimmedLine.Substring(0, tabIndex);
                        string hint = trimmedLine.Substring(tabIndex + 1);

                        if (!string.IsNullOrEmpty(character))
                        {
                            _ziTiDict[character] = hint;
                            _loadedCount++;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // 静默处理错误
            }

            _initialized = true;
            _currentScheme = schemeKey;
        }

        /// <summary>
        /// 获取指定字的字提
        /// </summary>
        /// <param name="character">要查询的字</param>
        /// <returns>字提内容，如果没有找到返回空字符串</returns>
        public static string GetZiTi(string character)
        {
            if (string.IsNullOrEmpty(character))
                return "";

            if (!_initialized)
                Initialize();

            if (_ziTiDict != null && _ziTiDict.TryGetValue(character, out string hint))
                return hint;

            return "";
        }

        /// <summary>
        /// 获取已加载的字数
        /// </summary>
        public static int LoadedCount => _loadedCount;

        /// <summary>
        /// 重新加载字提数据
        /// </summary>
        public static void Reload()
        {
            _initialized = false;
            _ziTiDict = null;
            _currentScheme = null;
            Initialize();
        }

        /// <summary>
        /// 重新加载字提数据（指定方案）
        /// </summary>
        /// <param name="scheme">方案名称，null表示使用默认</param>
        public static void Reload(string scheme)
        {
            _initialized = false;
            _ziTiDict = null;
            _currentScheme = null;
            Initialize(scheme);
        }
    }
}
