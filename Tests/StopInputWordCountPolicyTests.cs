using System;
using TypeSunny.Core;
using TypeSunny.UI;

namespace TypeSunny.Tests
{
    internal static class StopInputWordCountPolicyTests
    {
        private static int Main()
        {
            try
            {
                TrainerKeepsExistingScoreCountWhenInputSnapshotIsEmpty();
                TrainerUsesSnapshotCountWhenSnapshotHasText();
                NonTrainerUsesSnapshotCount();
                HiddenInputModeKeepsExistingScoreCountWhenSnapshotIsEmpty();
                HiddenInputModeIgnoresSnapshotText();

                Console.WriteLine("All StopInputWordCountPolicy tests passed.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        private static void TrainerKeepsExistingScoreCountWhenInputSnapshotIsEmpty()
        {
            int actual = StopInputWordCountPolicy.Resolve(TxtSource.trainer, 10, 0);

            AssertEqual("trainer empty snapshot keeps Score input count", 10, actual);
        }

        private static void TrainerUsesSnapshotCountWhenSnapshotHasText()
        {
            int actual = StopInputWordCountPolicy.Resolve(TxtSource.trainer, 10, 8);

            AssertEqual("trainer non-empty snapshot uses snapshot count", 8, actual);
        }

        private static void NonTrainerUsesSnapshotCount()
        {
            int actual = StopInputWordCountPolicy.Resolve(TxtSource.book, 10, 0);

            AssertEqual("non-trainer empty snapshot uses snapshot count", 0, actual);
        }

        private static void HiddenInputModeKeepsExistingScoreCountWhenSnapshotIsEmpty()
        {
            int actual = StopInputWordCountPolicy.Resolve(TxtSource.book, 10, 0, true);

            AssertEqual("hidden input mode empty snapshot keeps Score input count", 10, actual);
        }

        private static void HiddenInputModeIgnoresSnapshotText()
        {
            int actual = StopInputWordCountPolicy.Resolve(TxtSource.book, 10, 2, true);

            AssertEqual("hidden input mode ignores incomplete input snapshot", 10, actual);
        }

        private static void AssertEqual(string name, int expected, int actual)
        {
            if (actual != expected)
                throw new Exception(name + ": expected " + expected + ", got " + actual + ".");
        }
    }
}
