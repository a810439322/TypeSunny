using System.Collections.Generic;
using System.Globalization;

namespace TypeSunny.Personalization
{
    /// <summary>
    /// 文本中候选 unit 的提取工具。
    ///
    /// SQLite 改造后 Predictor / Trainer 不再持有"全部 Units"的内存字典，需要在查 DB 前
    /// 先列出"可能命中的候选 key"。本类的规则与 <see cref="PersonalScorePredictor"/> 内部
    /// 决定哪些 unit 会被用于 DP 计算的规则一致：
    ///
    /// - 单字（CJK）始终是候选；
    /// - 长度 2-4 的纯中文子串是候选；
    /// - 此外把外部 fallback 切分（难度分词器输出）按原样追加，以兼容词提模式下非纯中文段。
    /// </summary>
    internal static class PersonalUnitExtractor
    {
        public const int MaxUnitCharacters = 4;

        /// <summary>
        /// 扫描 <paramref name="text"/>，返回该文本可能命中的全部 unit key。
        /// 结果不去重；调用方按需用 HashSet 收口。
        /// </summary>
        public static IEnumerable<string> EnumerateCandidates(string text)
        {
            if (string.IsNullOrEmpty(text))
                yield break;

            string[] chars = SplitTextElements(text);
            int n = chars.Length;

            for (int i = 0; i < n; i++)
            {
                // 单字（无论是否中文）也作为候选；预测器内部会再做过滤。
                yield return chars[i];

                int maxLen = MaxUnitCharacters;
                if (i + maxLen > n)
                    maxLen = n - i;
                for (int len = 2; len <= maxLen; len++)
                {
                    string unit = Concat(chars, i, len);
                    if (IsPureChineseUnit(unit))
                        yield return unit;
                }
            }
        }

        /// <summary>
        /// 合并文本候选与外部 fallback 段，返回去重后的集合。
        /// </summary>
        public static HashSet<string> CollectAllKeys(string text, IEnumerable<string> fallbackSegments)
        {
            var set = new HashSet<string>();
            foreach (string candidate in EnumerateCandidates(text))
                set.Add(candidate);

            if (fallbackSegments != null)
            {
                foreach (string segment in fallbackSegments)
                {
                    if (!string.IsNullOrEmpty(segment))
                        set.Add(segment);
                }
            }
            return set;
        }

        public static bool IsPureChineseUnit(string text)
        {
            string[] elements = SplitTextElements(text);
            if (elements.Length == 0)
                return false;

            foreach (string element in elements)
            {
                if (element.Length != 1 || !IsCjkUnifiedIdeograph(element[0]))
                    return false;
            }

            return true;
        }

        public static bool IsCjkUnifiedIdeograph(char ch)
        {
            return (ch >= '㐀' && ch <= '鿿') || (ch >= '豈' && ch <= '﫿');
        }

        public static string[] SplitTextElements(string text)
        {
            if (string.IsNullOrEmpty(text))
                return new string[0];

            var si = new StringInfo(text);
            string[] result = new string[si.LengthInTextElements];
            for (int i = 0; i < result.Length; i++)
                result[i] = si.SubstringByTextElements(i, 1);
            return result;
        }

        private static string Concat(string[] chars, int start, int count)
        {
            if (count == 1)
                return chars[start];

            string result = "";
            for (int i = start; i < start + count; i++)
                result += chars[i];
            return result;
        }
    }
}
