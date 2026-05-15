using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Media;
using TypeSunny.Core;
using CoreTextInfo = TypeSunny.Core.TextInfo;

namespace TypeSunny.Utils
{
    internal enum CiTiType
    {
        Jian1,
        Jian2,
        Jian3,
        Ma4,
        Normal,
        SingleChar,
        Punctuation
    }

    internal class CiTiSegment
    {
        public string Word;
        public string Code;
        public CiTiType Type;
        public bool IsPreferred;
    }

    internal static class CiTiHelper
    {
        private static readonly char[] SelectKeys = { '_', '2', '3', '4', '5', '6', '7', '8', '9', '0' };

        private static readonly Dictionary<string, string> PunctuationMap = new Dictionary<string, string>
        {
            { "，", "," },
            { "。", "." },
            { "、", "/" }
        };

        private static Dictionary<string, (string codeKey, string selectKey)> _wordLib =
            new Dictionary<string, (string, string)>();
        private static Dictionary<string, (string codeKey, string selectKey)> _singleCharLib =
            new Dictionary<string, (string, string)>();
        private static int _maxWordLen;
        private static string _currentScheme = "";

        public static List<string> GetAvailableSchemes()
        {
            var schemes = new List<string>();
            string dir = FindCiTiDirectory();
            if (dir == null)
                return schemes;

            foreach (var file in Directory.GetFiles(dir, "*.txt"))
            {
                string name = Path.GetFileNameWithoutExtension(file);
                if (name.EndsWith("词提"))
                    name = name.Substring(0, name.Length - 2);
                schemes.Add(name);
            }

            return schemes;
        }

        public static void Initialize(string scheme)
        {
            if (string.IsNullOrEmpty(scheme) || scheme == _currentScheme)
                return;

            _wordLib.Clear();
            _singleCharLib.Clear();
            _maxWordLen = 0;
            _currentScheme = "";

            string dir = FindCiTiDirectory();
            if (dir == null)
                return;

            string filePath = Path.Combine(dir, scheme + "词提.txt");
            if (!File.Exists(filePath))
                filePath = Path.Combine(dir, scheme + ".txt");
            if (!File.Exists(filePath))
                return;

            foreach (var line in File.ReadLines(filePath, Encoding.UTF8))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                string trimmedLine = line.TrimStart('\uFEFF');
                var parts = trimmedLine.Split('\t');
                if (parts.Length < 2)
                    continue;

                string word = parts[0];
                string fullCode = parts[1];
                if (string.IsNullOrEmpty(word) || string.IsNullOrEmpty(fullCode))
                    continue;

                string codeKey = fullCode.TrimEnd(SelectKeys);
                string selectKey = fullCode.Substring(codeKey.Length);

                var si = new StringInfo(word);
                int charCount = si.LengthInTextElements;

                if (charCount == 1)
                {
                    if (!_singleCharLib.ContainsKey(word))
                        _singleCharLib[word] = (codeKey, selectKey);
                }
                else
                {
                    if (!_wordLib.ContainsKey(word))
                        _wordLib[word] = (codeKey, selectKey);
                }

                if (charCount > _maxWordLen)
                    _maxWordLen = charCount;
            }

            _currentScheme = scheme;
        }

        public static bool IsLoaded => _currentScheme != "" && (_wordLib.Count > 0 || _singleCharLib.Count > 0);

        public static List<CiTiSegment> SplitText(string fullText)
        {
            if (!IsLoaded || string.IsNullOrEmpty(fullText))
                return new List<CiTiSegment>();

            var si = new StringInfo(fullText);
            int n = si.LengthInTextElements;
            var chars = new string[n];
            for (int i = 0; i < n; i++)
                chars[i] = si.SubstringByTextElements(i, 1);

            double[] dp = new double[n + 1];
            List<(string word, string code)>[] choices = new List<(string, string)>[n + 1];
            for (int i = 0; i <= n; i++)
            {
                dp[i] = double.MaxValue;
                choices[i] = new List<(string, string)>();
            }
            dp[0] = 0;

            for (int i = 1; i <= n; i++)
            {
                string ch = chars[i - 1];

                if (PunctuationMap.ContainsKey(ch))
                {
                    var prevChoice = choices[i - 1];
                    double costReduction = 0;
                    double prevCost = 0;
                    foreach (var choice in prevChoice)
                        prevCost += choice.code.Length;

                    List<(string, string)> newChoice;
                    if (prevChoice.Count > 0 && prevChoice[prevChoice.Count - 1].code.EndsWith("_"))
                    {
                        var last = prevChoice[prevChoice.Count - 1];
                        string newLastCode = last.code.TrimEnd('_');
                        costReduction = last.code.Length - newLastCode.Length;
                        newChoice = new List<(string, string)>(prevChoice);
                        newChoice[newChoice.Count - 1] = (last.word, newLastCode);
                        newChoice.Add((ch, PunctuationMap[ch]));
                    }
                    else
                    {
                        newChoice = new List<(string, string)>(prevChoice);
                        newChoice.Add((ch, PunctuationMap[ch]));
                    }

                    double punctCost = PunctuationMap[ch].Length;
                    dp[i] = prevCost - costReduction + punctCost;
                    choices[i] = newChoice;
                    continue;
                }

                int start = Math.Max(0, i - _maxWordLen);
                for (int j = start; j < i; j++)
                {
                    string currentWord = Concat(chars, j, i - j);
                    string code = GetCode(currentWord, i - j);
                    if (code == null)
                        continue;

                    double cost = code.Length;
                    if (i - j == 1 && !_singleCharLib.ContainsKey(currentWord))
                        cost = 500;

                    string nextChar = i < n ? chars[i] : null;
                    if (code.Length > 4 && code.EndsWith("_") && !code.StartsWith("'"))
                    {
                        if (nextChar != null && (IsHanzi(nextChar) || PunctuationMap.ContainsKey(nextChar)))
                        {
                            string oldCode = code;
                            code = code.TrimEnd('_');
                            double reduction = oldCode.Length - code.Length - 0.1;
                            cost -= reduction;
                        }
                    }

                    double newCost = dp[j] + cost;
                    if (newCost < dp[i])
                    {
                        dp[i] = newCost;
                        choices[i] = new List<(string, string)>(choices[j]);
                        choices[i].Add((currentWord, code));
                    }
                    else if (Math.Abs(newCost - dp[i]) < 0.001
                             && code.EndsWith("_")
                             && choices[i].Count > 0
                             && !choices[i][choices[i].Count - 1].code.EndsWith("_")
                             && nextChar != null
                             && PunctuationMap.ContainsKey(nextChar))
                    {
                        dp[i] = newCost;
                        choices[i] = new List<(string, string)>(choices[j]);
                        choices[i].Add((currentWord, code));
                    }
                }
            }

            return BuildSegments(choices[n]);
        }

        private static Brush ParseColor(string hex)
        {
            if (string.IsNullOrEmpty(hex))
                return null;

            hex = hex.TrimStart('#');
            if (hex.Length < 6)
                return null;

            try
            {
                byte r = Convert.ToByte(hex.Substring(0, 2), 16);
                byte g = Convert.ToByte(hex.Substring(2, 2), 16);
                byte b = Convert.ToByte(hex.Substring(4, 2), 16);
                return new SolidColorBrush(Color.FromRgb(r, g, b));
            }
            catch
            {
                return null;
            }
        }

        public static Brush GetCiTiColor(CiTiType type)
        {
            switch (type)
            {
                case CiTiType.Jian1:
                    return ParseColor(Config.GetString("词提1简色"));
                case CiTiType.Jian2:
                    return ParseColor(Config.GetString("词提2简色"));
                case CiTiType.Jian3:
                    return ParseColor(Config.GetString("词提3简色"));
                case CiTiType.Ma4:
                case CiTiType.Normal:
                    return ParseColor(Config.GetString("词提4码色"));
                default:
                    return null;
            }
        }

        public static Brush GetCodeColor(bool isPreferred)
        {
            return isPreferred
                ? new SolidColorBrush(Color.FromRgb(0x00, 0x00, 0x00))
                : ParseColor(Config.GetString("词提选重色"));
        }

        public static void ComputeSegmentIndices()
        {
            CoreTextInfo.CiTiSegmentIndices.Clear();
            for (int s = 0; s < CoreTextInfo.CiTiSegments.Count; s++)
            {
                var si = new StringInfo(CoreTextInfo.CiTiSegments[s].Word);
                int charCount = si.LengthInTextElements;
                for (int c = 0; c < charCount; c++)
                    CoreTextInfo.CiTiSegmentIndices.Add(s);
            }
        }

        public static bool IsFirstCharOfSegment(int globalCharIndex)
        {
            if (globalCharIndex <= 0)
                return true;
            if (globalCharIndex >= CoreTextInfo.CiTiSegmentIndices.Count)
                return false;
            return CoreTextInfo.CiTiSegmentIndices[globalCharIndex] != CoreTextInfo.CiTiSegmentIndices[globalCharIndex - 1];
        }

        public static bool ShouldBold(int segIdx)
        {
            if (segIdx < 0 || segIdx >= CoreTextInfo.CiTiSegments.Count)
                return false;

            var seg = CoreTextInfo.CiTiSegments[segIdx];
            if (seg.Type == CiTiType.SingleChar || seg.Type == CiTiType.Punctuation)
                return false;

            int consecutivePos = 0;
            for (int i = segIdx - 1; i >= 0; i--)
            {
                var prev = CoreTextInfo.CiTiSegments[i];
                if (prev.Type == CiTiType.SingleChar || prev.Type == CiTiType.Punctuation)
                    break;
                consecutivePos++;
            }

            return consecutivePos % 2 == 1;
        }

        public static string GetCodeForChar(int globalCharIndex)
        {
            if (globalCharIndex < 0 || globalCharIndex >= CoreTextInfo.CiTiSegmentIndices.Count)
                return "";

            int segIdx = CoreTextInfo.CiTiSegmentIndices[globalCharIndex];
            if (segIdx < 0 || segIdx >= CoreTextInfo.CiTiSegments.Count)
                return "";

            if (!IsFirstCharOfSegment(globalCharIndex))
                return "";

            return CoreTextInfo.CiTiSegments[segIdx].Code;
        }

        private static string FindCiTiDirectory()
        {
            string[] possibleDirs =
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "词提"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "Resources", "词提"),
                Path.Combine("Resources", "词提")
            };

            foreach (string dir in possibleDirs)
            {
                string fullPath = Path.GetFullPath(dir);
                if (Directory.Exists(fullPath))
                    return fullPath;
            }

            return null;
        }

        private static string Concat(string[] chars, int start, int count)
        {
            if (count == 1)
                return chars[start];

            var sb = new StringBuilder(count * 2);
            for (int i = start; i < start + count; i++)
                sb.Append(chars[i]);
            return sb.ToString();
        }

        private static string GetCode(string word, int charCount)
        {
            if (charCount == 1)
            {
                if (_singleCharLib.TryGetValue(word, out var result))
                    return result.codeKey + result.selectKey;
                return "";
            }

            if (_wordLib.TryGetValue(word, out var wordResult))
                return wordResult.codeKey + wordResult.selectKey;
            return null;
        }

        private static bool IsHanzi(string ch)
        {
            if (string.IsNullOrEmpty(ch))
                return false;

            int code = char.ConvertToUtf32(ch, 0);
            return code >= 0x4E00 && code <= 0x9FFF;
        }

        private static List<CiTiSegment> BuildSegments(List<(string word, string code)> splitResult)
        {
            var segments = new List<CiTiSegment>();
            var nonPreferredSelectKeys = new HashSet<char> { '2', '3', '4', '5', '6', '7', '8', '9', '0' };

            foreach (var item in splitResult)
            {
                var seg = new CiTiSegment { Word = item.word, Code = item.code };

                if (PunctuationMap.ContainsKey(item.word))
                {
                    seg.Type = CiTiType.Punctuation;
                    seg.IsPreferred = true;
                }
                else
                {
                    var si = new StringInfo(item.word);
                    if (si.LengthInTextElements == 1)
                    {
                        seg.Type = CiTiType.SingleChar;
                    }
                    else
                    {
                        string codeKey = item.code.TrimEnd(SelectKeys);
                        switch (codeKey.Length)
                        {
                            case 1:
                                seg.Type = CiTiType.Jian1;
                                break;
                            case 2:
                                seg.Type = CiTiType.Jian2;
                                break;
                            case 3:
                                seg.Type = CiTiType.Jian3;
                                break;
                            case 4:
                                seg.Type = CiTiType.Ma4;
                                break;
                            default:
                                seg.Type = CiTiType.Normal;
                                break;
                        }
                    }

                    int wordLen = new StringInfo(item.word).LengthInTextElements;
                    bool isInLib = (wordLen == 1 && _singleCharLib.ContainsKey(item.word))
                                   || (wordLen > 1 && _wordLib.ContainsKey(item.word));
                    seg.IsPreferred = !isInLib || !item.code.Any(c => nonPreferredSelectKeys.Contains(c));
                }

                segments.Add(seg);
            }

            return segments;
        }
    }
}
