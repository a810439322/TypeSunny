namespace TypeSunny.UI.Modes
{
    internal sealed class FinishOnceGate
    {
        public bool IsPending { get; private set; }

        public bool TryBegin()
        {
            if (IsPending)
                return false;

            IsPending = true;
            return true;
        }

        public void Reset()
        {
            IsPending = false;
        }
    }
}
