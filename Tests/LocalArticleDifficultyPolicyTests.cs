using System;

namespace TypeSunny.Tests
{
    internal static class LocalArticleDifficultyPolicyTests
    {
        private static int Main()
        {
            try
            {
                InitialStateAllowsRemoteDifficulty();
                SuccessfulResultDoesNotDisableRemoteDifficulty();
                FailedResultDisablesRemoteDifficultyForPolicyLifetime();

                Console.WriteLine("All LocalArticleDifficultyPolicy tests passed.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("FAIL: " + ex.Message);
                return 1;
            }
        }

        private static void InitialStateAllowsRemoteDifficulty()
        {
            var policy = new LocalArticleDifficultyPolicy();

            AssertTrue("initial remote difficulty request", policy.ShouldRequestRemoteDifficulty);
        }

        private static void SuccessfulResultDoesNotDisableRemoteDifficulty()
        {
            var policy = new LocalArticleDifficultyPolicy();

            policy.RecordRemoteDifficultyResult(disableFutureRequests: false);

            AssertTrue("remote difficulty after success result", policy.ShouldRequestRemoteDifficulty);
        }

        private static void FailedResultDisablesRemoteDifficultyForPolicyLifetime()
        {
            var policy = new LocalArticleDifficultyPolicy();

            policy.RecordRemoteDifficultyResult(disableFutureRequests: true);

            AssertFalse("remote difficulty after failure", policy.ShouldRequestRemoteDifficulty);
        }

        private static void AssertTrue(string name, bool condition)
        {
            if (!condition)
                throw new Exception(name + " expected true, got false.");
        }

        private static void AssertFalse(string name, bool condition)
        {
            if (condition)
                throw new Exception(name + " expected false, got true.");
        }
    }
}
