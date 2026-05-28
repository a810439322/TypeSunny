using System;
using TypeSunny.UI.Modes;

namespace TypeSunny.Tests
{
    internal static class CopybookNavigationTests
    {
        private static int _failures;

        private static int Main()
        {
            Run("End stops at typed end within current visual line", EndStopsAtTypedEndWithinCurrentVisualLine);
            Run("End ignores typed text from later visual lines", EndIgnoresTypedTextFromLaterVisualLines);
            Run("End stays put when current visual line has no typed text", EndStaysPutWhenCurrentLineHasNoTypedText);
            Run("End stays on current visual line when typed text reaches line edge", EndStaysOnCurrentLineWhenTypedTextReachesLineEdge);

            if (_failures == 0)
            {
                Console.WriteLine("All CopybookNavigation tests passed.");
                return 0;
            }

            Console.WriteLine(_failures + " CopybookNavigation test(s) failed.");
            return 1;
        }

        private static void EndStopsAtTypedEndWithinCurrentVisualLine()
        {
            int[] currentLine = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };

            int target = CopybookNavigation.FindEndTargetWithinTypedLine(
                currentIndex: 2,
                totalCount: 10,
                lineIndexes: currentLine,
                isTyped: index => index <= 5);

            AssertEqual(6, target);
        }

        private static void EndIgnoresTypedTextFromLaterVisualLines()
        {
            int[] currentLine = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };

            int target = CopybookNavigation.FindEndTargetWithinTypedLine(
                currentIndex: 2,
                totalCount: 20,
                lineIndexes: currentLine,
                isTyped: index => index <= 5 || index >= 10);

            AssertEqual(6, target);
        }

        private static void EndStaysPutWhenCurrentLineHasNoTypedText()
        {
            int[] currentLine = { 0, 1, 2, 3, 4 };

            int target = CopybookNavigation.FindEndTargetWithinTypedLine(
                currentIndex: 2,
                totalCount: 10,
                lineIndexes: currentLine,
                isTyped: index => index >= 5);

            AssertEqual(2, target);
        }

        private static void EndStaysOnCurrentLineWhenTypedTextReachesLineEdge()
        {
            int[] currentLine = { 0, 1, 2, 3, 4 };

            int target = CopybookNavigation.FindEndTargetWithinTypedLine(
                currentIndex: 2,
                totalCount: 10,
                lineIndexes: currentLine,
                isTyped: index => index <= 4);

            AssertEqual(4, target);
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

        private static void AssertEqual<T>(T expected, T actual)
        {
            if (!object.Equals(expected, actual))
                throw new Exception("Expected " + expected + ", got " + actual + ".");
        }
    }
}
