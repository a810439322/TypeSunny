namespace TypeSunny
{
    internal sealed class LocalArticleDifficultyPolicy
    {
        private bool remoteDifficultyDisabled;

        public bool ShouldRequestRemoteDifficulty
        {
            get { return !remoteDifficultyDisabled; }
        }

        public void RecordRemoteDifficultyResult(bool disableFutureRequests)
        {
            if (disableFutureRequests)
                remoteDifficultyDisabled = true;
        }
    }
}
