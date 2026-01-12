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

        /// <summary>
        /// 初始化字提数据
        /// </summary>
        public static void Initialize()
        {
            if (_initialized)
                return;

            _ziTiDict = new Dictionary<string, string>();
            _loadedCount = 0;

            // 尝试多个可能的路径
            string[] possiblePaths = new string[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "字提.txt"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "Resources", "字提.txt"),
                Path.Combine("Resources", "字提.txt"),
                "字提.txt"
            };

            string filePath = null;
            foreach (string path in possiblePaths)
            {
                string fullPath = Path.GetFullPath(path);
                if (File.Exists(fullPath))
                {
                    filePath = fullPath;
                    break;
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
            Initialize();
        }
    }
}
