using System;
using System.Collections.Generic;
using TypeSunny;

namespace TypeSunny.Tests
{
    internal static class TrainerActualWordCounterTests
    {
        private static int Main()
        {
            try
            {
                LatinMistypeOverChineseTargetCountsOneTargetWord();
                SeparateLatinMistypeCommitsOverChineseTargetCountOneTargetWord();
                DirectModeLatinMistypeOverChineseTargetCountsOneTargetWord();
                DirectModeSeparateLatinMistypeOverChineseTargetCountsOneTargetWord();
                FallbackLatinMistypeOverChineseTargetCountsOneTargetWord();
                ChinesePhraseCommitCountsTextElements();
                LatinCommitOverLatinTargetKeepsTextElementCount();
                EmptyCommitListFallsBackToInputSnapshotWithTargetCap();
                TypedCounterReturnsOnlyNewTargetWordDelta();
                TypedCounterFallsBackWhenNoCommitTextExists();
                TypedCounterTreatsSeparateDirectModeLatinLettersAsOneChineseTargetWord();

                Console.WriteLine("All TrainerActualWordCounter tests passed.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        private static void LatinMistypeOverChineseTargetCountsOneTargetWord()
        {
            int actual = TrainerActualWordCounter.CountPartialWords(
                new[] { "abc" },
                new List<string> { "你", "好", "人" },
                fallbackInputTextElements: 3);

            AssertEqual("latin mistype over Chinese target", 1, actual);
        }

        private static void SeparateLatinMistypeCommitsOverChineseTargetCountOneTargetWord()
        {
            int actual = TrainerActualWordCounter.CountPartialWords(
                new[] { "a", "b", "c" },
                new List<string> { "你", "好", "人" },
                fallbackInputTextElements: 3);

            AssertEqual("separate latin mistype commits over Chinese target", 1, actual);
        }

        private static void DirectModeLatinMistypeOverChineseTargetCountsOneTargetWord()
        {
            int actual = TrainerActualWordCounter.CountCommittedWords(
                new[] { "abc" },
                new List<string> { "你", "好", "人" },
                targetStartIndex: 0);

            AssertEqual("direct mode latin mistype over Chinese target", 1, actual);
        }

        private static void DirectModeSeparateLatinMistypeOverChineseTargetCountsOneTargetWord()
        {
            int actual = TrainerActualWordCounter.CountCommittedWords(
                new[] { "a", "b", "c" },
                new List<string> { "你", "好", "人" },
                targetStartIndex: 0);

            AssertEqual("direct mode separate latin mistype over Chinese target", 1, actual);
        }

        private static void FallbackLatinMistypeOverChineseTargetCountsOneTargetWord()
        {
            int actual = TrainerActualWordCounter.CountPartialWords(
                Array.Empty<string>(),
                new List<string> { "你", "好", "人" },
                "abc",
                fallbackInputTextElements: 3);

            AssertEqual("fallback latin mistype over Chinese target", 1, actual);
        }

        private static void ChinesePhraseCommitCountsTextElements()
        {
            int actual = TrainerActualWordCounter.CountPartialWords(
                new[] { "中国" },
                new List<string> { "中", "国", "人" },
                fallbackInputTextElements: 2);

            AssertEqual("Chinese phrase commit", 2, actual);
        }

        private static void LatinCommitOverLatinTargetKeepsTextElementCount()
        {
            int actual = TrainerActualWordCounter.CountPartialWords(
                new[] { "abc" },
                new List<string> { "a", "b", "c", "d" },
                fallbackInputTextElements: 3);

            AssertEqual("latin commit over latin target", 3, actual);
        }

        private static void EmptyCommitListFallsBackToInputSnapshotWithTargetCap()
        {
            int actual = TrainerActualWordCounter.CountPartialWords(
                Array.Empty<string>(),
                new List<string> { "一", "二" },
                fallbackInputTextElements: 5);

            AssertEqual("fallback caps to target words", 2, actual);
        }

        private static void TypedCounterReturnsOnlyNewTargetWordDelta()
        {
            var counter = new TrainerTypedWordCounter();
            var target = new List<string> { "你", "好", "人" };

            int first = counter.AddFrom(new[] { "a", "b", "c" }, target, "abc", 3);
            int repeated = counter.AddFrom(new[] { "a", "b", "c" }, target, "abc", 3);

            counter.Reset();
            int afterReset = counter.AddFrom(new[] { "你" }, target, "你", 1);

            AssertEqual("typed counter first delta", 1, first);
            AssertEqual("typed counter repeated delta", 0, repeated);
            AssertEqual("typed counter reset delta", 1, afterReset);
        }

        private static void TypedCounterFallsBackWhenNoCommitTextExists()
        {
            var counter = new TrainerTypedWordCounter();
            var target = new List<string> { "你", "好" };

            int first = counter.AddFrom(Array.Empty<string>(), target, "abc", 3);
            int repeated = counter.AddFrom(Array.Empty<string>(), target, "abc", 3);

            AssertEqual("typed counter fallback first delta", 1, first);
            AssertEqual("typed counter fallback repeated delta", 0, repeated);
        }

        private static void TypedCounterTreatsSeparateDirectModeLatinLettersAsOneChineseTargetWord()
        {
            var counter = new TrainerTypedWordCounter();
            var target = new List<string> { "你", "好" };

            int first = counter.AddFrom(new[] { "a" }, target, "", 0);
            int second = counter.AddFrom(new[] { "a", "b" }, target, "", 0);
            int third = counter.AddFrom(new[] { "a", "b", "c" }, target, "", 0);

            AssertEqual("typed counter direct latin first delta", 1, first);
            AssertEqual("typed counter direct latin second delta", 0, second);
            AssertEqual("typed counter direct latin third delta", 0, third);
        }

        private static void AssertEqual(string name, int expected, int actual)
        {
            if (actual != expected)
                throw new Exception(name + ": expected " + expected + ", got " + actual + ".");
        }
    }
}
