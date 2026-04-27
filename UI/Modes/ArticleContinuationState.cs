namespace TypeSunny.UI.Modes
{
    internal enum ArticleContinuationAction
    {
        None,
        WenlaiRandom,
        WenlaiNext,
        WenlaiPrevious,
        LocalNext,
        LocalPrevious
    }

    internal sealed class ArticleContinuationState
    {
        private ArticleContinuationAction _action;
        private bool _hasPendingRequest;

        public bool HasAction
        {
            get { return _action != ArticleContinuationAction.None; }
        }

        public bool HasPending
        {
            get { return _hasPendingRequest && HasAction; }
        }

        public void Record(ArticleContinuationAction action)
        {
            _action = action;
            if (!HasAction)
                _hasPendingRequest = false;
        }

        public bool RequestPending()
        {
            _hasPendingRequest = HasAction;
            return _hasPendingRequest;
        }

        public void ClearPending()
        {
            _hasPendingRequest = false;
        }

        public bool TryConsume(bool shouldStart, out ArticleContinuationAction action)
        {
            if (!shouldStart || !HasPending)
            {
                action = ArticleContinuationAction.None;
                return false;
            }

            action = _action;
            _hasPendingRequest = false;
            return true;
        }
    }
}
