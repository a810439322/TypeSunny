using System;

namespace TypeSunny.Tests
{
    internal static class ArticleTitleFormatterTests
    {
        private static int Main()
        {
            try
            {
                AssertEqual(
                    "formats local article title stats",
                    "雅舍谈吃 - 第 3/20 段 | 当前 400/3921 字 | 本段 198 字",
                    ArticleTitleFormatter.Format("雅舍谈吃.txt", 3, 20, 400, 198, 3921));

                AssertEqual(
                    "formats epub title without extension",
                    "Alice - 第 1/5 段 | 当前 0/600 字 | 本段 120 字",
                    ArticleTitleFormatter.Format("Alice.epub", 1, 5, 0, 120, 600));

                AssertEqual(
                    "falls back when no article is selected",
                    "本地文章管理器",
                    ArticleTitleFormatter.Format("", 1, 1, 0, 0, 0));

                Console.WriteLine("All ArticleTitleFormatter tests passed.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        private static void AssertEqual(string name, string expected, string actual)
        {
            if (actual != expected)
                throw new Exception(name + ": expected [" + expected + "], got [" + actual + "].");
        }
    }
}
