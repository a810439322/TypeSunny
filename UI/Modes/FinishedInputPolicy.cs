namespace TypeSunny.UI.Modes
{
    internal static class FinishedInputPolicy
    {
        public static bool ShouldHandlePreviewKeyDown(bool pendingRetypeStarted)
        {
            return pendingRetypeStarted;
        }
    }
}
