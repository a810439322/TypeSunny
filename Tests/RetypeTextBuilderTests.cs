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
            Run("final wrong records merge with historical correction records", FinalWrongRecordsMergeWithHistoricalCorrectionRecords);
            Run("all right final state keeps historical wrong records", AllRightFinalStateKeepsHistoricalWrongRecords);
            Run("corrected wrong does not clear slow records", CorrectedWrongDoesNotClearSlowRecords);
            Run("wrong retype text is ordered by source position", WrongRetypeTextIsOrderedBySourcePosition);
            Run("combined retype text keeps wrong records before slow records", CombinedRetypeTextKeepsWrongRecordsBeforeSlowRecords);
            Run("look typing deletion records deleted source word", LookTypingDeletionRecordsDeletedSourceWord);
            Run("canceled composition records composition start target", CanceledCompositionRecordsCompositionStartTarget);
            Run("canceled composition ignores invalid or excluded target", CanceledCompositionIgnoresInvalidOrExcludedTarget);

            if (_failures == 0)
            {
                Console.WriteLine("All RetypeTextBuilder tests passed.");
                return 0;
            }

            Console.WriteLine(_failures + " RetypeTextBuilder test(s) failed.");
            return 1;
        }

        private static void FinalWrongRecordsMergeWithHistoricalCorrectionRecords()
        {
            var records = new Dictionary<int, string>
            {
                { 1, "B" }
            };
            var finalRecords = new[]
            {
                new KeyValuePair<int, string>(2, "C")
            };

            RetypeTextBuilder.MergeFinalWrongRecords(records, finalRecords, "");

            AssertEqual("record count", 2, records.Count);
            AssertTrue("historical correction record kept", records.ContainsKey(1));
            AssertTrue("final wrong record kept", records.ContainsKey(2));
            AssertEqual("historical value", "B", records[1]);
            AssertEqual("final wrong value", "C", records[2]);
        }

        private static void AllRightFinalStateKeepsHistoricalWrongRecords()
        {
            var records = new Dictionary<int, string>
            {
                { 0, "A" }
            };

            RetypeTextBuilder.MergeFinalWrongRecords(records, new KeyValuePair<int, string>[0], "");

            AssertEqual("record count", 1, records.Count);
            AssertEqual("historical value", "A", records[0]);
        }

        private static void CorrectedWrongDoesNotClearSlowRecords()
        {
            var wrong = new Dictionary<int, string>
            {
                { 0, "A" }
            };
            var slow = new Dictionary<int, string>
            {
                { 0, "A" }
            };

            RetypeTextBuilder.MergeFinalWrongRecords(wrong, new KeyValuePair<int, string>[0], "");
            string actual = RetypeTextBuilder.BuildCombinedRetypeText(wrong, 1, slow, 1);

            AssertEqual("wrong records stay available", 1, wrong.Count);
            AssertEqual("wrong and slow records stay available", "AA", actual);
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

        private static void CanceledCompositionRecordsCompositionStartTarget()
        {
            int position;
            string word;
            bool actual = RetypeTextBuilder.TryResolveCompositionTargetWrongRecord(
                new List<string> { "A", "B" },
                compositionStartTargetPosition: 0,
                wrongExclude: "",
                position: out position,
                word: out word);

            AssertTrue("canceled composition should record the target active when coding started", actual);
            AssertEqual("position", 0, position);
            AssertEqual("word", "A", word);
        }

        private static void CanceledCompositionIgnoresInvalidOrExcludedTarget()
        {
            int position;
            string word;
            bool invalid = RetypeTextBuilder.TryResolveCompositionTargetWrongRecord(
                new List<string> { "A" },
                compositionStartTargetPosition: 1,
                wrongExclude: "",
                position: out position,
                word: out word);

            AssertFalse("out-of-range start target should not record", invalid);
            AssertEqual("invalid position", -1, position);
            AssertEqual("invalid word", null, word);

            bool excluded = RetypeTextBuilder.TryResolveCompositionTargetWrongRecord(
                new List<string> { "A" },
                compositionStartTargetPosition: 0,
                wrongExclude: "A",
                position: out position,
                word: out word);

            AssertFalse("excluded target should not record", excluded);
            AssertEqual("excluded position", -1, position);
            AssertEqual("excluded word", null, word);
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
