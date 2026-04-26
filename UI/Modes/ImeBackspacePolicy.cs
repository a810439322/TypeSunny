namespace TypeSunny.UI.Modes
{
    internal sealed class ImeBackspacePolicy
    {
        private bool _hadComposition;
        private bool _protectNextRawBackspaceAfterEmptyComposition;

        public void Reset()
        {
            _hadComposition = false;
            _protectNextRawBackspaceAfterEmptyComposition = false;
        }

        public void NotifyCompositionText(string composition, bool isPhysicalBackspaceDown)
        {
            bool hasComposition = !string.IsNullOrEmpty(composition);
            if (_hadComposition && !hasComposition && isPhysicalBackspaceDown)
                _protectNextRawBackspaceAfterEmptyComposition = true;

            _hadComposition = hasComposition;
        }

        public void NotifyCompositionEnded()
        {
            _hadComposition = false;
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
