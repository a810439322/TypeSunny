using System;
using TypeSunny.UI.Modes;

namespace TypeSunny.Tests
{
    internal static class ImeBackspacePolicyTests
    {
        private static int _failures;

        private static int Main()
        {
            Run("raw backspace without IME state deletes previous word", RawBackspaceWithoutImeStateDeletesPreviousWord);
            Run("active composition backspace stays with IME", ActiveCompositionBackspaceStaysWithIme);
            Run("IME processed backspace stays with IME when composition looks empty", ImeProcessedBackspaceStaysWithIme);
            Run("backspace that clears last composition char is protected once", EmptyCompositionByBackspaceProtectsOnce);
            Run("empty composition without backspace does not protect raw backspace", EmptyCompositionWithoutBackspaceDoesNotProtect);

            if (_failures == 0)
            {
                Console.WriteLine("All ImeBackspacePolicy tests passed.");
                return 0;
            }

            Console.WriteLine(_failures + " ImeBackspacePolicy test(s) failed.");
            return 1;
        }

        private static void RawBackspaceWithoutImeStateDeletesPreviousWord()
        {
            var policy = new ImeBackspacePolicy();

            AssertTrue(policy.ShouldDeletePreviousWord(false, false));
        }

        private static void ActiveCompositionBackspaceStaysWithIme()
        {
            var policy = new ImeBackspacePolicy();

            AssertFalse(policy.ShouldDeletePreviousWord(false, true));
        }

        private static void ImeProcessedBackspaceStaysWithIme()
        {
            var policy = new ImeBackspacePolicy();

            AssertFalse(policy.ShouldDeletePreviousWord(true, false));
        }

        private static void EmptyCompositionByBackspaceProtectsOnce()
        {
            var policy = new ImeBackspacePolicy();
            policy.NotifyCompositionText("a", false);
            policy.NotifyCompositionText("", true);

            AssertFalse(policy.ShouldDeletePreviousWord(false, false));
            AssertTrue(policy.ShouldDeletePreviousWord(false, false));
        }

        private static void EmptyCompositionWithoutBackspaceDoesNotProtect()
        {
            var policy = new ImeBackspacePolicy();
            policy.NotifyCompositionText("a", false);
            policy.NotifyCompositionText("", false);

            AssertTrue(policy.ShouldDeletePreviousWord(false, false));
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

        private static void AssertFalse(bool condition)
        {
            if (condition)
                throw new Exception("Expected false, got true.");
        }
    }
}
