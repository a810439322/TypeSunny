using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace TypeSunny
{
    public class FilterDiff
    {
        public int Position { get; set; }
        public string Original { get; set; }
        public string Replaced { get; set; }
    }

    public class FilterResult
    {
        public string Text { get; set; }
        public bool IsBlocked { get; set; }
        public string BlockReason { get; set; }
        public List<FilterDiff> Diffs { get; set; } = new List<FilterDiff>();
    }

    static internal class RegexFilter
    {
        private const string SEPARATOR = "=>";
        private const string LINE_ESCAPE = "\\n";

        public static bool IsEnabled(string source)
        {
            string key = "过滤_生效_" + source;
            if (!Config.dicts.ContainsKey(key)) return false;
            return Config.GetBool(key);
        }

        public static FilterResult Apply(string text)
        {
            var result = new FilterResult { Text = text, IsBlocked = false };
            if (string.IsNullOrEmpty(text)) return result;

            // 1. 替换：关键词
            var replaceKeywords = ParseLines(Config.GetString("过滤_替换关键词"));
            foreach (var line in replaceKeywords)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                ParseReplacePair(line, out string find, out string replace);
                if (string.IsNullOrEmpty(find)) continue;

                int idx;
                while ((idx = result.Text.IndexOf(find, StringComparison.Ordinal)) >= 0)
                {
                    result.Diffs.Add(new FilterDiff { Position = idx, Original = find, Replaced = replace });
                    result.Text = result.Text.Remove(idx, find.Length).Insert(idx, replace);
                }
            }

            // 2. 替换：正则
            var replaceRegexes = ParseLines(Config.GetString("过滤_替换正则"));
            foreach (var line in replaceRegexes)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                ParseReplacePair(line, out string pattern, out string replace);
                if (string.IsNullOrEmpty(pattern)) continue;

                try
                {
                    var regex = new Regex(pattern, RegexOptions.None, TimeSpan.FromSeconds(1));
                    var matches = regex.Matches(result.Text);
                    for (int i = matches.Count - 1; i >= 0; i--)
                    {
                        var m = matches[i];
                        string replaced = regex.Replace(m.Value, replace);
                        result.Diffs.Add(new FilterDiff { Position = m.Index, Original = m.Value, Replaced = replaced });
                        result.Text = result.Text.Remove(m.Index, m.Length).Insert(m.Index, replaced);
                    }
                }
                catch { }
            }

            // 3. 黑名单：关键词
            var blacklistKeywords = ParseLines(Config.GetString("过滤_黑名单关键词"));
            foreach (var kw in blacklistKeywords)
            {
                if (string.IsNullOrWhiteSpace(kw)) continue;
                if (result.Text.Contains(kw))
                {
                    result.IsBlocked = true;
                    result.BlockReason = $"包含屏蔽关键词：{kw}";
                    return result;
                }
            }

            // 4. 黑名单：正则
            var blacklistRegexes = ParseLines(Config.GetString("过滤_黑名单正则"));
            foreach (var pattern in blacklistRegexes)
            {
                if (string.IsNullOrWhiteSpace(pattern)) continue;
                try
                {
                    var regex = new Regex(pattern, RegexOptions.None, TimeSpan.FromSeconds(1));
                    if (regex.IsMatch(result.Text))
                    {
                        result.IsBlocked = true;
                        result.BlockReason = $"匹配屏蔽正则：{pattern}";
                        return result;
                    }
                }
                catch { }
            }

            return result;
        }

        public static FilterResult Preview(string text, string blacklistKw, string replaceKw, string blacklistRx, string replaceRx)
        {
            var result = new FilterResult { Text = text, IsBlocked = false };
            if (string.IsNullOrEmpty(text)) return result;

            var rkLines = ParseLinesRaw(replaceKw);
            foreach (var line in rkLines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                ParseReplacePair(line, out string find, out string replace);
                if (string.IsNullOrEmpty(find)) continue;
                int idx;
                while ((idx = result.Text.IndexOf(find, StringComparison.Ordinal)) >= 0)
                {
                    result.Diffs.Add(new FilterDiff { Position = idx, Original = find, Replaced = replace });
                    result.Text = result.Text.Remove(idx, find.Length).Insert(idx, replace);
                }
            }

            var rrLines = ParseLinesRaw(replaceRx);
            foreach (var line in rrLines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                ParseReplacePair(line, out string pattern, out string replace);
                if (string.IsNullOrEmpty(pattern)) continue;
                try
                {
                    var regex = new Regex(pattern, RegexOptions.None, TimeSpan.FromSeconds(1));
                    var matches = regex.Matches(result.Text);
                    for (int i = matches.Count - 1; i >= 0; i--)
                    {
                        var m = matches[i];
                        string replaced = regex.Replace(m.Value, replace);
                        result.Diffs.Add(new FilterDiff { Position = m.Index, Original = m.Value, Replaced = replaced });
                        result.Text = result.Text.Remove(m.Index, m.Length).Insert(m.Index, replaced);
                    }
                }
                catch { }
            }

            var bkLines = ParseLinesRaw(blacklistKw);
            foreach (var kw in bkLines)
            {
                if (string.IsNullOrWhiteSpace(kw)) continue;
                if (result.Text.Contains(kw))
                {
                    result.IsBlocked = true;
                    result.BlockReason = $"包含屏蔽关键词：{kw}";
                    return result;
                }
            }

            var brLines = ParseLinesRaw(blacklistRx);
            foreach (var pattern in brLines)
            {
                if (string.IsNullOrWhiteSpace(pattern)) continue;
                try
                {
                    var regex = new Regex(pattern, RegexOptions.None, TimeSpan.FromSeconds(1));
                    if (regex.IsMatch(result.Text))
                    {
                        result.IsBlocked = true;
                        result.BlockReason = $"匹配屏蔽正则：{pattern}";
                        return result;
                    }
                }
                catch { }
            }

            return result;
        }

        public static string EncodeMultiline(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Replace("\r\n", "\n").Replace("\n", LINE_ESCAPE);
        }

        public static string DecodeMultiline(string stored)
        {
            if (string.IsNullOrEmpty(stored)) return "";
            return stored.Replace(LINE_ESCAPE, "\n");
        }

        private static List<string> ParseLines(string configValue)
        {
            string decoded = DecodeMultiline(configValue);
            return ParseLinesRaw(decoded);
        }

        private static List<string> ParseLinesRaw(string text)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(text)) return lines;
            foreach (var line in text.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string trimmed = line.Trim('\r');
                if (!string.IsNullOrWhiteSpace(trimmed))
                    lines.Add(trimmed);
            }
            return lines;
        }

        private static void ParseReplacePair(string line, out string find, out string replace)
        {
            int idx = line.IndexOf(SEPARATOR, StringComparison.Ordinal);
            if (idx >= 0)
            {
                find = line.Substring(0, idx);
                replace = line.Substring(idx + SEPARATOR.Length);
            }
            else
            {
                find = line;
                replace = "";
            }
        }
    }
}
