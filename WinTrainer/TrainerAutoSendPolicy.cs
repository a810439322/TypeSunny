namespace TypeSunny
{
    internal sealed class TrainerAutoSendPolicy
    {
        private bool _suppressNextGroupSend;
        private int _programmaticRefreshDepth;

        public void SuppressNextGroupSend()
        {
            _suppressNextGroupSend = true;
        }

        public void BeginProgrammaticRefresh()
        {
            _programmaticRefreshDepth++;
        }

        public void EndProgrammaticRefresh()
        {
            if (_programmaticRefreshDepth > 0)
                _programmaticRefreshDepth--;
        }

        public bool ConsumeShouldSendToMainWindow()
        {
            if (_programmaticRefreshDepth > 0)
                return false;

            if (_suppressNextGroupSend)
            {
                _suppressNextGroupSend = false;
                return false;
            }

            return true;
        }
    }
}
