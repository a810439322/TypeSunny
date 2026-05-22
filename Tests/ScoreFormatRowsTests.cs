using System;
using System.Collections.Generic;
using TypeSunny.Core;

namespace TypeSunny.Tests
{
    internal static class ScoreFormatRowsTests
    {
        private static int Main()
        {
            try
            {
                TrainerRoundSummaryDoesNotSetFirstColumnWidth();
                TrainerRoundSummaryPrefixIsShared();

                Console.WriteLine("All ScoreFormatRows tests passed.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        private static void TrainerRoundSummaryDoesNotSetFirstColumnWidth()
        {
            string trainerSummary = Score.TrainerSummaryPrefix + " very-long-trainer-file-name 均击5.00 均速60.00 均准98.00% 字数100 实际120 用时00:01:40";
            var rows = new List<List<string>>
            {
                Score.ParseReportLine(trainerSummary),
                Score.ParseReportLine("速度60.00  击键5.00  键准98.00%  字数100"),
                Score.ParseReportLine("速度7.00  击键4.00  键准97.00%  字数50")
            };

            string formatted = Score.FormatRows(rows);
            string[] lines = formatted.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            AssertTrue(
                "trainer summary line remains visible",
                lines[0].StartsWith("[晴练单] very-long-trainer-file-name 均击5.00", StringComparison.Ordinal));
            AssertTrue(
                "normal score row should not be padded to trainer summary width",
                lines[1].StartsWith("速度60.00  击键5.00", StringComparison.Ordinal));
        }

        private static void TrainerRoundSummaryPrefixIsShared()
        {
            AssertTrue(
                "trainer summary prefix",
                Score.TrainerSummaryPrefix == "[晴练单]");
        }

        private static void AssertTrue(string name, bool condition)
        {
            if (!condition)
                throw new Exception(name + " expected true, got false.");
        }
    }
}
