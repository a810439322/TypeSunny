using System;
using System.Collections.Generic;
using System.Linq;

namespace TypeSunny.UI
{
    internal struct ScoreLabelDisplaySegment
    {
        public ScoreLabelDisplaySegment(string text, bool isLabel)
        {
            Text = text;
            IsLabel = isLabel;
        }

        public string Text { get; private set; }
        public bool IsLabel { get; private set; }
    }

    internal static class ScoreLabelDisplayFormatter
    {
        private static readonly string[] Prefixes =
        {
            "盲打正确率", "看打正确率", "禁用回改",
            "盲打模式", "看打模式",
            "打词率", "总键数",
            "速度", "击键", "码长", "字数", "难度", "重打", "键法", "回改",
            "退格", "键准", "废码", "选重", "标顶", "用时", "错字", "签名"
        };

        private static readonly string[] HeaderPrefixes =
        {
            "今日字数：", "总字数：", "▼ 点击加载更多", "✓ 已复制到剪贴板"
        };

        public static IEnumerable<ScoreLabelDisplaySegment> SplitLine(string line)
        {
            line = line ?? "";

            if (ShouldKeepLineVisible(line))
            {
                yield return new ScoreLabelDisplaySegment(line, false);
                yield break;
            }

            int index = 0;
            while (index < line.Length)
            {
                string prefix = FindPrefixAt(line, index);
                if (prefix == null)
                {
                    int next = FindNextPrefixIndex(line, index + 1);
                    if (next < 0)
                        next = line.Length;

                    yield return new ScoreLabelDisplaySegment(line.Substring(index, next - index), false);
                    index = next;
                    continue;
                }

                yield return new ScoreLabelDisplaySegment(prefix, true);
                index += prefix.Length;
            }
        }

        private static bool ShouldKeepLineVisible(string line)
        {
            string trimmed = line.TrimStart();
            return HeaderPrefixes.Any(prefix => trimmed.StartsWith(prefix, StringComparison.Ordinal));
        }

        private static int FindNextPrefixIndex(string line, int start)
        {
            int best = -1;
            for (int i = start; i < line.Length; i++)
            {
                if (FindPrefixAt(line, i) == null)
                    continue;

                best = i;
                break;
            }

            return best;
        }

        private static string FindPrefixAt(string line, int index)
        {
            foreach (string prefix in Prefixes)
            {
                if (!line.Substring(index).StartsWith(prefix, StringComparison.Ordinal))
                    continue;

                if (!IsTokenBoundary(line, index))
                    continue;

                return prefix;
            }

            return null;
        }

        private static bool IsTokenBoundary(string line, int index)
        {
            if (index <= 0)
                return true;

            char previous = line[index - 1];
            return char.IsWhiteSpace(previous) || previous == '[' || previous == '(' || previous == '（';
        }
    }
}
