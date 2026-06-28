using System;
using System.Collections.Generic;
using TypeSunny.Core;

namespace TypeSunny.Tests
{
    internal static class SlowRetypeDetectorTests
    {
        private static int _failures;

        private static int Main()
        {
            Run("corrected commit keeps slow record on original target", CorrectedCommitKeepsSlowRecordOnOriginalTarget);
            Run("corrected commit accumulates time on original target", CorrectedCommitAccumulatesTimeOnOriginalTarget);
            Run("slow later commit records its own target", SlowLaterCommitRecordsItsOwnTarget);

            if (_failures == 0)
            {
                Console.WriteLine("All SlowRetypeDetector tests passed.");
                return 0;
            }

            Console.WriteLine(_failures + " SlowRetypeDetector test(s) failed.");
            return 1;
        }

        private static void CorrectedCommitKeepsSlowRecordOnOriginalTarget()
        {
            var records = SlowRetypeDetector.BuildSlowRecords(
                new List<string> { "A", "B" },
                new long[] { 100, 1200 },
                new string[] { "X", "A" },
                new int[] { 0, 0 },
                slowThresholdMilliseconds: 1000,
                excludePuncts: "",
                wrongExclude: "");

            AssertEqual("record count", 1, records.Count);
            AssertTrue("records original target A", records.ContainsKey(0));
            AssertFalse("does not record next target B", records.ContainsKey(1));
            AssertEqual("record value", "A", records[0]);
        }

        private static void CorrectedCommitAccumulatesTimeOnOriginalTarget()
        {
            var records = SlowRetypeDetector.BuildSlowRecords(
                new List<string> { "A", "B" },
                new long[] { 600, 1200 },
                new string[] { "X", "A" },
                new int[] { 0, 0 },
                slowThresholdMilliseconds: 1000,
                excludePuncts: "",
                wrongExclude: "");

            AssertEqual("record count", 1, records.Count);
            AssertTrue("records corrected target A", records.ContainsKey(0));
            AssertFalse("does not record next target B", records.ContainsKey(1));
            AssertEqual("record value", "A", records[0]);
        }

        private static void SlowLaterCommitRecordsItsOwnTarget()
        {
            var records = SlowRetypeDetector.BuildSlowRecords(
                new List<string> { "A", "B" },
                new long[] { 100, 1300 },
                new string[] { "A", "B" },
                new int[] { 0, 1 },
                slowThresholdMilliseconds: 1000,
                excludePuncts: "",
                wrongExclude: "");

            AssertEqual("record count", 1, records.Count);
            AssertFalse("does not record A", records.ContainsKey(0));
            AssertTrue("records B", records.ContainsKey(1));
            AssertEqual("record value", "B", records[1]);
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
