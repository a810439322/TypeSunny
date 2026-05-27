using System;

namespace TypeSunny
{
    internal static class ArticleTitleFormatter
    {
        private const string DefaultTitle = "本地文章管理器";

        public static string Format(string title, int currentSection, int totalSections, int currentProgressWords, int currentWords, int totalWords)
        {
            string displayTitle = GetDisplayTitle(title);
            if (string.IsNullOrWhiteSpace(displayTitle))
                return DefaultTitle;

            return string.Format(
                "{0} - 第 {1}/{2} 段 | 当前 {3}/{4} 字 | 本段 {5} 字",
                displayTitle,
                Math.Max(1, currentSection),
                Math.Max(1, totalSections),
                Math.Max(0, currentProgressWords),
                Math.Max(0, totalWords),
                Math.Max(0, currentWords));
        }

        private static string GetDisplayTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return "";

            string result = title.Trim();
            if (result.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                return result.Substring(0, result.Length - 4);

            if (result.EndsWith(".epub", StringComparison.OrdinalIgnoreCase))
                return result.Substring(0, result.Length - 5);

            return result;
        }
    }
}
