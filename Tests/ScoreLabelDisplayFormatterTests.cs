using System;
using System.Linq;
using TypeSunny.UI;

namespace TypeSunny.Tests
{
    internal static class ScoreLabelDisplayFormatterTests
    {
        private static int Main()
        {
            try
            {
                SplitsScoreItemLabelsFromValues();
                LeavesHeaderAndUtilityLinesVisible();

                Console.WriteLine("All ScoreLabelDisplayFormatter tests passed.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        private static void SplitsScoreItemLabelsFromValues()
        {
            var segments = ScoreLabelDisplayFormatter.SplitLine("05-26 16:30  速度120.00 击键5.20 键准98.00% 码长3.10").ToList();

            AssertSegment("speed label", segments, "速度", true);
            AssertSegment("speed value", segments, "120.00 ", false);
            AssertSegment("hit label", segments, "击键", true);
            AssertSegment("accuracy label", segments, "键准", true);
            AssertSegment("kpw label", segments, "码长", true);
        }

        private static void LeavesHeaderAndUtilityLinesVisible()
        {
            AssertNoLabels("今日字数：120   总字数：900");
            AssertNoLabels("▼ 点击加载更多 (12 条)");
            AssertNoLabels("✓ 已复制到剪贴板");
        }

        private static void AssertNoLabels(string line)
        {
            var labels = ScoreLabelDisplayFormatter.SplitLine(line).Where(s => s.IsLabel).ToList();
            if (labels.Count > 0)
                throw new Exception("Expected no hidden labels in '" + line + "'.");
        }

        private static void AssertSegment(string name, System.Collections.Generic.List<ScoreLabelDisplaySegment> segments, string text, bool isLabel)
        {
            if (!segments.Any(s => s.Text == text && s.IsLabel == isLabel))
                throw new Exception(name + " segment not found.");
        }
    }
}
