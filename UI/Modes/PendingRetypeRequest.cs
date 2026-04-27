using TypeSunny.Core;

namespace TypeSunny.UI.Modes
{
    internal sealed class PendingRetypeRequest
    {
        private string _text;
        private RetypeType _type;

        public bool HasPending
        {
            get { return !string.IsNullOrEmpty(_text); }
        }

        public void Set(string text, RetypeType type)
        {
            if (string.IsNullOrEmpty(text))
            {
                Clear();
                return;
            }

            _text = text;
            _type = type;
        }

        public bool TryConsume(bool shouldStart, out string text, out RetypeType type)
        {
            if (!shouldStart || !HasPending)
            {
                text = null;
                type = RetypeType.first;
                return false;
            }

            text = _text;
            type = _type;
            Clear();
            return true;
        }

        public void Clear()
        {
            _text = null;
            _type = RetypeType.first;
        }
    }
}
