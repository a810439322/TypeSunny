using System;
using TypeSunny.UI.Modes;

namespace TypeSunny.Tests
{
    internal static class CopybookInputBufferTests
    {
        private static int _failures;

        private static int Main()
        {
            Run("backspace shifts later typed text forward", BackspaceShiftsLaterTypedTextForward);
            Run("delete shifts later typed text forward", DeleteShiftsLaterTypedTextForward);
            Run("insert shifts later typed text backward", InsertShiftsLaterTypedTextBackward);
            Run("caret movement is clamped to typed text bounds", CaretMovementIsClampedToTypedTextBounds);

            if (_failures == 0)
            {
                Console.WriteLine("All CopybookInputBuffer tests passed.");
                return 0;
            }

            Console.WriteLine(_failures + " CopybookInputBuffer test(s) failed.");
            return 1;
        }

        private static void BackspaceShiftsLaterTypedTextForward()
        {
            var buffer = new CopybookInputBuffer();
            buffer.Insert("axbcd");

            buffer.MoveCaret(2);
            bool deleted = buffer.Backspace();

            AssertTrue(deleted);
            AssertEqual("abcd", buffer.Text);
            AssertEqual(1, buffer.CaretIndex);
            AssertStates(
                buffer.BuildStates(new[] { "a", "b", "c", "d" }, lookingType: false),
                CopybookInputState.Right,
                CopybookInputState.Right,
                CopybookInputState.Right,
                CopybookInputState.Right);
        }

        private static void InsertShiftsLaterTypedTextBackward()
        {
            var buffer = new CopybookInputBuffer();
            buffer.Insert("acd");

            buffer.MoveCaret(1);
            buffer.Insert("b");

            AssertEqual("abcd", buffer.Text);
            AssertEqual(2, buffer.CaretIndex);
            AssertStates(
                buffer.BuildStates(new[] { "a", "b", "c", "d" }, lookingType: false),
                CopybookInputState.Right,
                CopybookInputState.Right,
                CopybookInputState.Right,
                CopybookInputState.Right);
        }

        private static void DeleteShiftsLaterTypedTextForward()
        {
            var buffer = new CopybookInputBuffer();
            buffer.Insert("axbcd");

            buffer.MoveCaret(1);
            bool deleted = buffer.Delete();

            AssertTrue(deleted);
            AssertEqual("abcd", buffer.Text);
            AssertEqual(1, buffer.CaretIndex);
            AssertStates(
                buffer.BuildStates(new[] { "a", "b", "c", "d" }, lookingType: false),
                CopybookInputState.Right,
                CopybookInputState.Right,
                CopybookInputState.Right,
                CopybookInputState.Right);
        }

        private static void CaretMovementIsClampedToTypedTextBounds()
        {
            var buffer = new CopybookInputBuffer();
            buffer.Insert("ab");

            buffer.MoveCaret(99);
            AssertEqual(2, buffer.CaretIndex);

            buffer.MoveCaret(-99);
            AssertEqual(0, buffer.CaretIndex);
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

        private static void AssertStates(CopybookInputState[] actual, params CopybookInputState[] expected)
        {
            AssertEqual(expected.Length, actual.Length);
            for (int i = 0; i < expected.Length; i++)
                AssertEqual(expected[i], actual[i]);
        }

        private static void AssertTrue(bool value)
        {
            if (!value)
                throw new Exception("Expected true, got false.");
        }

        private static void AssertEqual<T>(T expected, T actual)
        {
            if (!object.Equals(expected, actual))
                throw new Exception("Expected " + expected + ", got " + actual + ".");
        }
    }
}
