using System;

namespace TypeSunny.Tests
{
    internal static class TrainerAutoSendPolicyTests
    {
        private static int Main()
        {
            try
            {
                InitialOpenSuppressesOneGroupSend();
                ProgrammaticRefreshSuppressesEveryGroupSendInsideRefresh();
                ProgrammaticRefreshDoesNotConsumeInitialOpenSuppression();

                Console.WriteLine("All TrainerAutoSendPolicy tests passed.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("FAIL: " + ex.Message);
                return 1;
            }
        }

        private static void InitialOpenSuppressesOneGroupSend()
        {
            var policy = new TrainerAutoSendPolicy();

            policy.SuppressNextGroupSend();

            AssertFalse("initial group send", policy.ConsumeShouldSendToMainWindow());
            AssertTrue("later user-driven group send", policy.ConsumeShouldSendToMainWindow());
        }

        private static void ProgrammaticRefreshSuppressesEveryGroupSendInsideRefresh()
        {
            var policy = new TrainerAutoSendPolicy();

            policy.BeginProgrammaticRefresh();

            AssertFalse("first refresh-triggered group send", policy.ConsumeShouldSendToMainWindow());
            AssertFalse("second refresh-triggered group send", policy.ConsumeShouldSendToMainWindow());

            policy.EndProgrammaticRefresh();

            AssertTrue("user-driven group send after refresh", policy.ConsumeShouldSendToMainWindow());
        }

        private static void ProgrammaticRefreshDoesNotConsumeInitialOpenSuppression()
        {
            var policy = new TrainerAutoSendPolicy();

            policy.SuppressNextGroupSend();
            policy.BeginProgrammaticRefresh();
            AssertFalse("refresh-triggered group send", policy.ConsumeShouldSendToMainWindow());
            policy.EndProgrammaticRefresh();

            AssertFalse("initial group send after refresh", policy.ConsumeShouldSendToMainWindow());
            AssertTrue("subsequent user-driven group send", policy.ConsumeShouldSendToMainWindow());
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
