using System;
using TypeSunny.Core;
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
            Run("backspace release ends empty composition protection", BackspaceReleaseEndsEmptyCompositionProtection);
            Run("empty composition without backspace does not protect raw backspace", EmptyCompositionWithoutBackspaceDoesNotProtect);
            Run("finish gate begins once until reset", FinishGateBeginsOnceUntilReset);
            Run("pending retype starts only on trigger and consumes once", PendingRetypeStartsOnlyOnTriggerAndConsumesOnce);
            Run("finished keydown without pending retype bubbles to window commands", FinishedKeyDownWithoutPendingRetypeBubblesToWindowCommands);
            Run("article continuation requires an explicit pending request", ArticleContinuationRequiresExplicitPendingRequest);
            Run("article continuation consumes last action only on trigger", ArticleContinuationConsumesLastActionOnlyOnTrigger);

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

        private static void BackspaceReleaseEndsEmptyCompositionProtection()
        {
            var policy = new ImeBackspacePolicy();
            policy.NotifyCompositionText("a", false);
            policy.NotifyCompositionText("", true);
            policy.NotifyPhysicalBackspaceReleased();

            AssertTrue(policy.ShouldDeletePreviousWord(false, false));
        }

        private static void FinishGateBeginsOnceUntilReset()
        {
            var gate = new FinishOnceGate();

            AssertTrue(gate.TryBegin());
            AssertFalse(gate.TryBegin());
            AssertTrue(gate.IsPending);

            gate.Reset();

            AssertFalse(gate.IsPending);
            AssertTrue(gate.TryBegin());
        }

        private static void PendingRetypeStartsOnlyOnTriggerAndConsumesOnce()
        {
            var pending = new PendingRetypeRequest();
            pending.Set("wrong", RetypeType.wrongRetype);
            string text;
            RetypeType type;

            AssertFalse(pending.TryConsume(false, out text, out type));
            AssertTrue(pending.HasPending);
            AssertTrue(pending.TryConsume(true, out text, out type));
            AssertEqual("wrong", text);
            AssertEqual(RetypeType.wrongRetype, type);
            AssertFalse(pending.HasPending);
            AssertFalse(pending.TryConsume(true, out text, out type));
        }

        private static void FinishedKeyDownWithoutPendingRetypeBubblesToWindowCommands()
        {
            AssertFalse(FinishedInputPolicy.ShouldHandlePreviewKeyDown(false));
            AssertTrue(FinishedInputPolicy.ShouldHandlePreviewKeyDown(true));
        }

        private static void ArticleContinuationRequiresExplicitPendingRequest()
        {
            var state = new ArticleContinuationState();
            state.Record(ArticleContinuationAction.WenlaiRandom);
            ArticleContinuationAction action;

            AssertTrue(state.HasAction);
            AssertFalse(state.HasPending);
            AssertFalse(state.TryConsume(true, out action));

            AssertTrue(state.RequestPending());
            AssertTrue(state.HasPending);
            AssertFalse(state.TryConsume(false, out action));
            AssertTrue(state.HasPending);

            state.Record(ArticleContinuationAction.LocalNext);
            AssertTrue(state.TryConsume(true, out action));
            AssertEqual(ArticleContinuationAction.LocalNext, action);
            AssertTrue(state.HasAction);
            AssertFalse(state.HasPending);
            AssertFalse(state.TryConsume(true, out action));
        }

        private static void ArticleContinuationConsumesLastActionOnlyOnTrigger()
        {
            var state = new ArticleContinuationState();
            state.Record(ArticleContinuationAction.WenlaiRandom);
            state.RequestPending();
            ArticleContinuationAction action;

            AssertFalse(state.TryConsume(false, out action));
            AssertTrue(state.HasAction);
            AssertTrue(state.TryConsume(true, out action));
            AssertEqual(ArticleContinuationAction.WenlaiRandom, action);
            AssertTrue(state.HasAction);
            AssertFalse(state.HasPending);
            AssertFalse(state.TryConsume(true, out action));
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

        private static void AssertEqual<T>(T expected, T actual)
        {
            if (!object.Equals(expected, actual))
                throw new Exception("Expected " + expected + ", got " + actual + ".");
        }
    }
}
