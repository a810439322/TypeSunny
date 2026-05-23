namespace TypeSunny.UI.Modes
{
    internal sealed class ImeBackspacePolicy
    {
        private bool _hadComposition;
        private bool _imeBackspaceInProgress;
        private bool _protectNextRawBackspaceAfterEmptyComposition;

        public void Reset()
        {
            _hadComposition = false;
            _imeBackspaceInProgress = false;
            _protectNextRawBackspaceAfterEmptyComposition = false;
        }

        public void NotifyImeBackspaceStarted()
        {
            _imeBackspaceInProgress = true;
        }

        public void NotifyCompositionText(string composition)
        {
            bool hasComposition = !string.IsNullOrEmpty(composition);
            if (_hadComposition && !hasComposition && _imeBackspaceInProgress)
                _protectNextRawBackspaceAfterEmptyComposition = true;

            _hadComposition = hasComposition;
        }

        public void NotifyCompositionEnded()
        {
            _hadComposition = false;
        }

        public void NotifyPhysicalBackspaceReleased()
        {
            _protectNextRawBackspaceAfterEmptyComposition = false;
            _imeBackspaceInProgress = false;
        }

        public bool ShouldDeletePreviousWord(bool isImeProcessedBackspace, bool hasActiveComposition)
        {
            if (isImeProcessedBackspace || hasActiveComposition)
                return false;

            if (_protectNextRawBackspaceAfterEmptyComposition)
            {
                _protectNextRawBackspaceAfterEmptyComposition = false;
                return false;
            }

            return true;
        }
    }
}
