using System;
using System.Collections.Generic;
using TypeSunny.Core;

namespace TypeSunny.Tests
{
    internal static class RetypeTextBuilderTests
    {
        private static int _failures;

        private static int Main()
        {
            Run("final wrong records replace transient correction records", FinalWrongRecordsReplaceTransientCorrectionRecords);
            Run("all right final state clears transient wrong records", AllRightFinalStateClearsTransientWrongRecords);
            Run("wrong retype text is ordered by source position", WrongRetypeTextIsOrderedBySourcePosition);
            Run("combined retype text keeps wrong records before slow records", CombinedRetypeTextKeepsWrongRecordsBeforeSlowRecords);
            Run("look typing deletion records deleted source word", LookTypingDeletionRecordsDeletedSourceWord);
            Run("blind IME backspace does not record next pending word", BlindImeBackspaceDoesNotRecordNextPendingWord);

            if (_failures == 0)
            {
                Console.WriteLine("All RetypeTextBuilder tests passed.");
                return 0;
            }

            Console.WriteLine(_failures + " RetypeTextBuilder test(s) failed.");
            return 1;
        }

        private static void FinalWrongRecordsReplaceTransientCorrectionRecords()
        {
            var records = new Dictionary<int, string>
            {
                { 1, "B" }
            };
            var finalRecords = new[]
            {
                new KeyValuePair<int, string>(2, "C")
            };

            RetypeTextBuilder.ReplaceWithFinalWrongRecords(records, finalRecords, "");

            AssertEqual("record count", 1, records.Count);
            AssertFalse("transient correction record removed", records.ContainsKey(1));
            AssertTrue("final wrong record kept", records.ContainsKey(2));
            AssertEqual("final wrong value", "C", records[2]);
        }

        private static void AllRightFinalStateClearsTransientWrongRecords()
        {
            var records = new Dictionary<int, string>
            {
                { 0, "A" }
            };

            RetypeTextBuilder.ReplaceWithFinalWrongRecords(records, new KeyValuePair<int, string>[0], "");

            AssertEqual("record count", 0, records.Count);
        }

        private static void WrongRetypeTextIsOrderedBySourcePosition()
        {
            var wrong = new Dictionary<int, string>
            {
                { 2, "C" },
                { 0, "A" }
            };

            string actual = RetypeTextBuilder.BuildCombinedRetypeText(wrong, 2, null, 0);

            AssertEqual("ordered wrong text", "ACAC", actual);
        }

        private static void CombinedRetypeTextKeepsWrongRecordsBeforeSlowRecords()
        {
            var wrong = new Dictionary<int, string>
            {
                { 1, "B" }
            };
            var slow = new Dictionary<int, string>
            {
                { 3, "D" },
                { 2, "C" }
            };

            string actual = RetypeTextBuilder.BuildCombinedRetypeText(wrong, 1, slow, 1);

            AssertEqual("combined text", "BCD", actual);
        }

        private static void LookTypingDeletionRecordsDeletedSourceWord()
        {
            var diffs = new[]
            {
                new Diff.DiffRes(Diff.DiffType.Delete, 2, -1)
            };
            var records = RetypeTextBuilder.BuildLookTypingDeletedWordRecords("ABC", diffs, "");

            AssertEqual("record count", 1, records.Count);
            AssertTrue("deleted B record kept", records.ContainsKey(1));
            AssertEqual("deleted source word", "B", records[1]);
        }

        private static void BlindImeBackspaceDoesNotRecordNextPendingWord()
        {
            int position;
            string word;
            bool actual = RetypeTextBuilder.TryResolveBlindImeBackspaceWrongRecord(
                new List<string> { "A", "B" },
                nextPendingWordIndex: 1,
                wrongExclude: "",
                position: out position,
                word: out word);

            AssertFalse("next pending word should not be recorded", actual);
            AssertEqual("position", -1, position);
            AssertEqual("word", null, word);
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

        private static void AssertEqual<T>(string name, T expected, T actual)
        {
            if (!object.Equals(expected, actual))
                throw new Exception(name + ": expected " + expected + ", got " + actual + ".");
        }

        private static void AssertTrue(string name, bool actual)
        {
            if (!actual)
                throw new Exception(name + ": expected true, got false.");
        }

        private static void AssertFalse(string name, bool actual)
        {
            if (actual)
                throw new Exception(name + ": expected false, got true.");
        }
    }
}
