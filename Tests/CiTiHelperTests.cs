using System;
using TypeSunny.Utils;

namespace System.Windows.Media
{
    public abstract class Brush
    {
    }

    public class SolidColorBrush : Brush
    {
        public SolidColorBrush(Color color)
        {
            Color = color;
        }

        public Color Color { get; private set; }
    }

    public struct Color
    {
        public byte A;
        public byte R;
        public byte G;
        public byte B;

        public static Color FromRgb(byte r, byte g, byte b)
        {
            return FromArgb(255, r, g, b);
        }

        public static Color FromArgb(byte a, byte r, byte g, byte b)
        {
            return new Color { A = a, R = r, G = g, B = b };
        }
    }
}

namespace TypeSunny.Core
{
    internal static class TextInfo
    {
        public static System.Collections.Generic.List<CiTiSegment> CiTiSegments =
            new System.Collections.Generic.List<CiTiSegment>();
        public static System.Collections.Generic.List<int> CiTiSegmentIndices =
            new System.Collections.Generic.List<int>();
    }
}

namespace TypeSunny
{
    internal static class Config
    {
        public static System.Collections.Generic.Dictionary<string, string> dicts =
            new System.Collections.Generic.Dictionary<string, string>
            {
                { "词提1简色", "#112233" },
                { "词提2简色", "#223344" },
                { "词提3简色", "#334455" },
                { "词提4码色", "#445566" },
                { "词提选重色", "#556677" }
            };

        public static string GetString(string key)
        {
            return dicts.ContainsKey(key) ? dicts[key] : "";
        }
    }
}

namespace TypeSunny.Tests
{
    internal static class CiTiHelperTests
    {
        private static int _failures;

        private static int Main()
        {
            Run("available schemes strip 词提 suffix", AvailableSchemesStripCiTiSuffix);
            Run("split prefers word segments and trims punctuation top-screen key", SplitPrefersWordSegmentsAndTrimsPunctuationTopScreenKey);
            Run("segment index utilities expose code on first character only", SegmentIndexUtilitiesExposeCodeOnFirstCharacterOnly);
            Run("missing single-character code displays blank instead of original character", MissingSingleCharacterCodeDisplaysBlank);
            Run("word entry can still cover characters without single-character code", WordEntryCanStillCoverCharactersWithoutSingleCharacterCode);
            Run("color mapping reads configurable CiTi colors", ColorMappingReadsConfigurableCiTiColors);
            Run("alternate bolding applies only to continuous multi-character words", AlternateBoldingAppliesOnlyToContinuousMultiCharacterWords);

            if (_failures == 0)
            {
                Console.WriteLine("All CiTiHelper tests passed.");
                return 0;
            }

            Console.WriteLine(_failures + " CiTiHelper test(s) failed.");
            return 1;
        }

        private static void AvailableSchemesStripCiTiSuffix()
        {
            var schemes = CiTiHelper.GetAvailableSchemes();

            AssertTrue(schemes.Contains("测试"));
        }

        private static void SplitPrefersWordSegmentsAndTrimsPunctuationTopScreenKey()
        {
            CiTiHelper.Initialize("测试");

            var segments = CiTiHelper.SplitText("中国，人民");

            AssertEqual(3, segments.Count);
            AssertSegment("中国", "zg", CiTiType.Jian2, true, segments[0]);
            AssertSegment("，", ",", CiTiType.Punctuation, true, segments[1]);
            AssertSegment("人民", "rm2", CiTiType.Jian2, false, segments[2]);
        }

        private static void SegmentIndexUtilitiesExposeCodeOnFirstCharacterOnly()
        {
            CiTiHelper.Initialize("测试");
            TypeSunny.Core.TextInfo.CiTiSegments = CiTiHelper.SplitText("中国，人民");

            CiTiHelper.ComputeSegmentIndices();

            AssertEqual(5, TypeSunny.Core.TextInfo.CiTiSegmentIndices.Count);
            AssertEqual("zg", CiTiHelper.GetCodeForChar(0));
            AssertEqual("", CiTiHelper.GetCodeForChar(1));
            AssertEqual(",", CiTiHelper.GetCodeForChar(2));
            AssertEqual("rm2", CiTiHelper.GetCodeForChar(3));
            AssertEqual("", CiTiHelper.GetCodeForChar(4));
        }

        private static void MissingSingleCharacterCodeDisplaysBlank()
        {
            CiTiHelper.Initialize("测试");
            TypeSunny.Core.TextInfo.CiTiSegments = CiTiHelper.SplitText("天");

            CiTiHelper.ComputeSegmentIndices();

            AssertEqual(1, TypeSunny.Core.TextInfo.CiTiSegments.Count);
            AssertSegment("天", "", CiTiType.SingleChar, true, TypeSunny.Core.TextInfo.CiTiSegments[0]);
            AssertEqual("", CiTiHelper.GetCodeForChar(0));
        }

        private static void WordEntryCanStillCoverCharactersWithoutSingleCharacterCode()
        {
            CiTiHelper.Initialize("测试");

            var segments = CiTiHelper.SplitText("天地");

            AssertEqual(1, segments.Count);
            AssertSegment("天地", "td_", CiTiType.Jian2, true, segments[0]);
        }

        private static void ColorMappingReadsConfigurableCiTiColors()
        {
            AssertColor(0x11, 0x22, 0x33, CiTiHelper.GetCiTiColor(CiTiType.Jian1));
            AssertColor(0x22, 0x33, 0x44, CiTiHelper.GetCiTiColor(CiTiType.Jian2));
            AssertColor(0x33, 0x44, 0x55, CiTiHelper.GetCiTiColor(CiTiType.Jian3));
            AssertColor(0x44, 0x55, 0x66, CiTiHelper.GetCiTiColor(CiTiType.Ma4));
            AssertColor(0x44, 0x55, 0x66, CiTiHelper.GetCiTiColor(CiTiType.Normal));
            AssertColor(0x00, 0x00, 0x00, CiTiHelper.GetCodeColor(true));
            AssertColor(0x55, 0x66, 0x77, CiTiHelper.GetCodeColor(false));
        }

        private static void AlternateBoldingAppliesOnlyToContinuousMultiCharacterWords()
        {
            TypeSunny.Core.TextInfo.CiTiSegments = new System.Collections.Generic.List<CiTiSegment>
            {
                new CiTiSegment { Word = "中国", Type = CiTiType.Jian2 },
                new CiTiSegment { Word = "人民", Type = CiTiType.Jian2 },
                new CiTiSegment { Word = "中", Type = CiTiType.SingleChar },
                new CiTiSegment { Word = "国家", Type = CiTiType.Jian2 },
                new CiTiSegment { Word = "社会", Type = CiTiType.Jian2 },
                new CiTiSegment { Word = "。", Type = CiTiType.Punctuation },
                new CiTiSegment { Word = "发展", Type = CiTiType.Jian2 }
            };

            AssertEqual(false, CiTiHelper.ShouldBold(0));
            AssertEqual(true, CiTiHelper.ShouldBold(1));
            AssertEqual(false, CiTiHelper.ShouldBold(2));
            AssertEqual(false, CiTiHelper.ShouldBold(3));
            AssertEqual(true, CiTiHelper.ShouldBold(4));
            AssertEqual(false, CiTiHelper.ShouldBold(5));
            AssertEqual(false, CiTiHelper.ShouldBold(6));
        }

        private static void AssertSegment(string word, string code, CiTiType type, bool isPreferred, CiTiSegment segment)
        {
            AssertEqual(word, segment.Word);
            AssertEqual(code, segment.Code);
            AssertEqual(type, segment.Type);
            AssertEqual(isPreferred, segment.IsPreferred);
        }

        private static void AssertColor(byte r, byte g, byte b, System.Windows.Media.Brush brush)
        {
            var solid = brush as System.Windows.Media.SolidColorBrush;
            AssertTrue(solid != null);
            AssertEqual(r, solid.Color.R);
            AssertEqual(g, solid.Color.G);
            AssertEqual(b, solid.Color.B);
        }

        private static void Run(string name, Action test)
        {
            try
            {
                test();
                Console.WriteLine("PASS: " + name);
            }
            catch (Exception ex)
            {
                _failures++;
                Console.WriteLine("FAIL: " + name);
                Console.WriteLine(ex.Message);
            }
        }

        private static void AssertTrue(bool condition)
        {
            if (!condition)
                throw new Exception("Expected true, got false.");
        }

        private static void AssertEqual<T>(T expected, T actual)
        {
            if (!object.Equals(expected, actual))
                throw new Exception("Expected " + expected + ", got " + actual + ".");
        }
    }
}
