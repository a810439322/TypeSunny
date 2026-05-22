using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace TypeSunny.Difficulty
{
    internal sealed class QingDifficultyResult
    {
        public double Score { get; set; }
        public string Rank { get; set; }
        public string Label { get; set; }
        public bool IsInvalid { get; set; }
    }

    internal sealed class QingDifficultyScorer
    {
        private const string LetterDigit = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        private static readonly bool[] AsciiLetterDigit = new bool[128];

        private readonly string vocabPath;
        private HashSet<char> validChars = new HashSet<char>();
        private Dictionary<string, WordInfo> words = new Dictionary<string, WordInfo>();
        private Dictionary<char, int> singleCharFrequency = new Dictionary<char, int>();
        private TrieNode trieRoot = new TrieNode();

        static QingDifficultyScorer()
        {
            foreach (char c in LetterDigit)
            {
                if (c < AsciiLetterDigit.Length)
                    AsciiLetterDigit[c] = true;
            }
        }

        public QingDifficultyScorer(string vocabPath = null)
        {
            this.vocabPath = string.IsNullOrWhiteSpace(vocabPath) ? ResolveDefaultVocabularyPath() : vocabPath;
            LoadVocabulary();
        }

        public QingDifficultyResult Compute(string text)
        {
            string filtered = FilterText(text);
            if (filtered.Length == 0)
                return new QingDifficultyResult { Score = -1.0, IsInvalid = true };

            MatchBucket[] matchesByStart = CollectMatches(filtered);
            DpNode[] dp = SegmentDp(filtered, matchesByStart);
            ScoreParts parts = ComputeScoreFromDp(filtered, dp);
            double score = Math.Round(parts.Hard / parts.Water, 2);
            string[] rank = GetRank(score);

            return new QingDifficultyResult
            {
                Score = score,
                Rank = rank[0],
                Label = rank[1],
                IsInvalid = false
            };
        }

        public IReadOnlyList<string> SegmentText(string text)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(text))
                return result;

            var run = new StringBuilder();
            foreach (char ch in text)
            {
                if (validChars.Contains(ch) || IsAsciiLetterDigit(ch))
                {
                    run.Append(ch);
                    continue;
                }

                AddSegmentsForRun(run.ToString(), result);
                run.Clear();
            }

            AddSegmentsForRun(run.ToString(), result);
            return result;
        }

        private static string ResolveDefaultVocabularyPath()
        {
            string[] candidates =
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Difficulty", "valid_and_weight.json"),
                Path.Combine(Environment.CurrentDirectory, "Resources", "Difficulty", "valid_and_weight.json"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "valid_and_weight.json"),
                Path.Combine(Environment.CurrentDirectory, "valid_and_weight.json")
            };

            return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
        }

        private void LoadVocabulary()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(vocabPath) || !File.Exists(vocabPath))
                {
                    InitializeDefaultChars();
                    return;
                }

                JObject root = JObject.Parse(File.ReadAllText(vocabPath, Encoding.UTF8));
                var parsedValidChars = new HashSet<char>();
                var parsedWords = new Dictionary<string, WordInfo>();

                JArray validCharNode = root["validChar"] as JArray;
                if (validCharNode != null)
                {
                    foreach (JToken node in validCharNode)
                    {
                        string value = node.ToString();
                        if (!string.IsNullOrEmpty(value))
                            parsedValidChars.Add(value[0]);
                    }
                }

                JObject wordsNode = root["words"] as JObject;
                if (wordsNode != null)
                {
                    foreach (JProperty property in wordsNode.Properties())
                    {
                        JArray value = property.Value as JArray;
                        if (value == null || value.Count < 2)
                            continue;

                        parsedWords[property.Name] = new WordInfo(
                            value[0].ToObject<int>(),
                            value[1].ToObject<int>());
                    }
                }

                if (parsedValidChars.Count < 100 || parsedWords.Count < 1000)
                {
                    InitializeDefaultChars();
                    return;
                }

                validChars = parsedValidChars;
                words = parsedWords;
                RebuildRuntimeCaches();
            }
            catch
            {
                InitializeDefaultChars();
            }
        }

        private void InitializeDefaultChars()
        {
            validChars = new HashSet<char>();
            words = new Dictionary<string, WordInfo>();
            singleCharFrequency = new Dictionary<char, int>();
            trieRoot = new TrieNode();

            foreach (char c in "的一了是在有人这他来个上说")
                validChars.Add(c);

            foreach (char c in LetterDigit)
                validChars.Add(c);
        }

        private void RebuildRuntimeCaches()
        {
            singleCharFrequency = new Dictionary<char, int>();
            trieRoot = new TrieNode();

            foreach (KeyValuePair<string, WordInfo> entry in words)
            {
                string word = entry.Key;
                WordInfo info = entry.Value;
                if (string.IsNullOrEmpty(word) || info == null)
                    continue;

                if (word.Length == 1)
                {
                    singleCharFrequency[word[0]] = info.Frequency;
                    continue;
                }

                TrieNode node = trieRoot;
                foreach (char c in word)
                {
                    TrieNode next;
                    if (!node.Children.TryGetValue(c, out next))
                    {
                        next = new TrieNode();
                        node.Children[c] = next;
                    }
                    node = next;
                }
                node.WordInfo = info;
            }
        }

        private string FilterText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";

            var builder = new StringBuilder(text.Length);
            foreach (char ch in text)
            {
                if (validChars.Contains(ch) || IsAsciiLetterDigit(ch))
                    builder.Append(ch);
            }
            return builder.ToString();
        }

        private static bool IsAsciiLetterDigit(char ch)
        {
            return ch < AsciiLetterDigit.Length && AsciiLetterDigit[ch];
        }

        private MatchBucket[] CollectMatches(string text)
        {
            int n = text.Length;
            var matchesByStart = new MatchBucket[n];

            for (int start = 0; start < n; start++)
            {
                TrieNode node = trieRoot;
                MatchBucket bucket = null;

                for (int end = start; end < n; end++)
                {
                    if (!node.Children.TryGetValue(text[end], out node))
                        break;

                    if (node.WordInfo != null)
                    {
                        if (bucket == null)
                            bucket = new MatchBucket();
                        bucket.Add(end + 1, node.WordInfo.CodeLength, node.WordInfo.Frequency);
                    }
                }

                matchesByStart[start] = bucket;
            }

            return matchesByStart;
        }

        private DpNode[] SegmentDp(string text, MatchBucket[] matchesByStart)
        {
            int n = text.Length;
            var dp = new DpNode[n + 1];
            for (int i = 0; i < dp.Length; i++)
                dp[i] = DpNode.Invalid();

            dp[0] = new DpNode(0, 0, -1, 0, 0);

            for (int start = 0; start < n; start++)
            {
                DpNode current = dp[start];
                if (!current.IsValid)
                    continue;

                int singleFreq;
                singleCharFrequency.TryGetValue(text[start], out singleFreq);
                UpdateDp(dp, start + 1, current, 1, start, singleFreq, 1);

                MatchBucket bucket = matchesByStart[start];
                if (bucket == null || bucket.Size == 0)
                    continue;

                for (int i = 0; i < bucket.Size; i++)
                    UpdateDp(dp, bucket.Ends[i], current, bucket.CodeLengths[i], start, bucket.Frequencies[i], bucket.Ends[i] - start);
            }

            return dp;
        }

        private void AddSegmentsForRun(string text, List<string> result)
        {
            if (string.IsNullOrEmpty(text))
                return;

            MatchBucket[] matchesByStart = CollectMatches(text);
            DpNode[] dp = SegmentDp(text, matchesByStart);
            int pos = text.Length;
            var reversed = new List<string>();

            while (pos > 0)
            {
                DpNode node = dp[pos];
                if (!node.IsValid || node.Prev < 0 || node.Prev > pos)
                {
                    foreach (char c in text)
                        result.Add(c.ToString());
                    return;
                }

                reversed.Add(text.Substring(node.Prev, pos - node.Prev));
                pos = node.Prev;
            }

            reversed.Reverse();
            result.AddRange(reversed);
        }

        private static void UpdateDp(
            DpNode[] dp,
            int end,
            DpNode current,
            int codeLength,
            int start,
            int frequency,
            int length)
        {
            int candidateSumCodeLength = current.SumCodeLength + codeLength;
            int candidateWordCount = current.WordCount + 1;
            DpNode endNode = dp[end];

            if (!endNode.IsValid
                || candidateSumCodeLength < endNode.SumCodeLength
                || (candidateSumCodeLength == endNode.SumCodeLength && candidateWordCount < endNode.WordCount))
            {
                dp[end] = new DpNode(candidateSumCodeLength, candidateWordCount, start, frequency, length);
            }
        }

        private ScoreParts ComputeScoreFromDp(string text, DpNode[] dp)
        {
            int pos = text.Length;
            double hard = 0.0;
            double water = 1.0;

            while (pos > 0)
            {
                DpNode node = dp[pos];
                if (!node.IsValid || node.Prev < 0 || node.Prev > pos)
                {
                    hard = 0.0;
                    water = 1.0;
                    foreach (char c in text)
                    {
                        int freq;
                        singleCharFrequency.TryGetValue(c, out freq);
                        hard += ScoreChar(freq);
                    }
                    return new ScoreParts(hard, water);
                }

                if (node.Length == 1)
                    hard += ScoreChar(node.Frequency);
                else
                    water += 2000.0 / (node.Frequency + 2000.0);

                pos = node.Prev;
            }

            return new ScoreParts(hard, water);
        }

        private static double ScoreChar(int freq)
        {
            if (freq <= 0)
                return 0.0;

            return Math.Min(10.0, Math.Pow(freq, 1.5) / 100000.0);
        }

        private static string[] GetRank(double score)
        {
            if (score < 0.1)
                return new[] { "miao", "淼" };
            if (score < 0.3)
                return new[] { "shui", "水" };
            if (score < 0.8)
                return new[] { "yi", "易" };
            if (score < 5.0)
                return new[] { "pu", "普" };
            if (score < 15.0)
                return new[] { "nan", "难" };
            return new[] { "nue", "虐" };
        }

        private sealed class WordInfo
        {
            public int Frequency { get; private set; }
            public int CodeLength { get; private set; }

            public WordInfo(int frequency, int codeLength)
            {
                Frequency = frequency;
                CodeLength = codeLength;
            }
        }

        private sealed class DpNode
        {
            public int SumCodeLength { get; private set; }
            public int WordCount { get; private set; }
            public int Prev { get; private set; }
            public int Frequency { get; private set; }
            public int Length { get; private set; }

            public DpNode(int sumCodeLength, int wordCount, int prev, int frequency, int length)
            {
                SumCodeLength = sumCodeLength;
                WordCount = wordCount;
                Prev = prev;
                Frequency = frequency;
                Length = length;
            }

            public bool IsValid
            {
                get { return SumCodeLength >= 0; }
            }

            public static DpNode Invalid()
            {
                return new DpNode(-1, -1, -1, 0, 0);
            }
        }

        private sealed class TrieNode
        {
            public Dictionary<char, TrieNode> Children { get; private set; }
            public WordInfo WordInfo { get; set; }

            public TrieNode()
            {
                Children = new Dictionary<char, TrieNode>();
            }
        }

        private sealed class MatchBucket
        {
            public List<int> Ends { get; private set; }
            public List<int> CodeLengths { get; private set; }
            public List<int> Frequencies { get; private set; }

            public MatchBucket()
            {
                Ends = new List<int>();
                CodeLengths = new List<int>();
                Frequencies = new List<int>();
            }

            public int Size
            {
                get { return Ends.Count; }
            }

            public void Add(int end, int codeLength, int frequency)
            {
                Ends.Add(end);
                CodeLengths.Add(codeLength);
                Frequencies.Add(frequency);
            }
        }

        private sealed class ScoreParts
        {
            public double Hard { get; private set; }
            public double Water { get; private set; }

            public ScoreParts(double hard, double water)
            {
                Hard = hard;
                Water = water;
            }
        }
    }
}
