using System;
using System.IO;
using System.Linq;
using System.Text;
using TypeSunny.Difficulty;

namespace TypeSunny.Tests
{
    internal static class QingDifficultyScorerTests
    {
        private static int Main()
        {
            try
            {
                DifficultyDictUsesQingfawenTrieDpScoring();
                DifficultyDictExposesQingfawenSegmentation();
                InvalidTextDoesNotProduceDifficultyText();
                LocalResourceScorerCanLoadPackagedVocabulary();

                Console.WriteLine("All QingDifficultyScorer tests passed.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("FAIL: " + ex.Message);
                return 1;
            }
        }

        private static void DifficultyDictUsesQingfawenTrieDpScoring()
        {
            string path = WriteFixtureVocabulary();
            try
            {
                var dict = new DifficultyDict(path);

                double score = dict.Calc("\u4e2d\u56fd\u4eba");

                AssertEqual("qingfawen score", 6.0, score, 0.001);
                AssertEqual("qingfawen label", "\u96be(6.00)", dict.DiffText(score));
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        private static void InvalidTextDoesNotProduceDifficultyText()
        {
            string path = WriteFixtureVocabulary();
            try
            {
                var dict = new DifficultyDict(path);

                double score = dict.Calc("???");

                AssertEqual("invalid score", -1.0, score, 0.001);
                AssertEqual("invalid label", "", dict.DiffText(score));
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        private static void DifficultyDictExposesQingfawenSegmentation()
        {
            string path = WriteFixtureVocabulary();
            try
            {
                var dict = new DifficultyDict(path);

                string[] segments = dict.SegmentText("\u4e2d\u56fd\u4eba").ToArray();

                AssertEqual("segment count", 2.0, segments.Length, 0.001);
                AssertEqual("first segment", "\u4e2d\u56fd", segments[0]);
                AssertEqual("second segment", "\u4eba", segments[1]);
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        private static void LocalResourceScorerCanLoadPackagedVocabulary()
        {
            var dict = new DifficultyDict();

            string text = "\u4e2d\u56fd\u4eba";
            double score = dict.Calc(text);

            AssertTrue("packaged vocabulary score should be valid", score >= 0);
            AssertTrue("packaged vocabulary label should be present", dict.DiffText(score).Length > 0);
        }

        private static string WriteFixtureVocabulary()
        {
            var json = new StringBuilder();
            json.Append("{\"validChar\":[\"\u4e2d\",\"\u56fd\",\"\u4eba\"");
            for (int i = 0; i < 120; i++)
                json.Append(",\"").Append((char)(0x4e10 + i)).Append("\"");

            json.Append("],\"words\":{");
            json.Append("\"\u4e2d\":[1000,1],");
            json.Append("\"\u56fd\":[1000,1],");
            json.Append("\"\u4eba\":[10000,1],");
            json.Append("\"\u4e2d\u56fd\":[1000,2]");
            for (int i = 0; i < 1002; i++)
                json.Append(",\"x").Append(i).Append("\":[").Append(2000 + i).Append(",1]");
            json.Append("}}");

            string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(path, json.ToString(), Encoding.UTF8);
            return path;
        }

        private static void AssertTrue(string name, bool condition)
        {
            if (!condition)
                throw new Exception(name + " expected true.");
        }

        private static void AssertEqual(string name, string expected, string actual)
        {
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
                throw new Exception(name + " expected [" + expected + "], got [" + actual + "].");
        }

        private static void AssertEqual(string name, double expected, double actual, double tolerance)
        {
            if (Math.Abs(expected - actual) > tolerance)
                throw new Exception(name + " expected [" + expected + "], got [" + actual + "].");
        }
    }
}
