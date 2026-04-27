namespace TypeSunny.Versioning
{
    internal enum VersionCheckTrigger
    {
        Startup,
        Timer,
        Manual
    }

    internal static class VersionCheckPolicy
    {
        public static bool ShouldCheck(
            VersionCheckTrigger trigger,
            bool dismissedToday,
            bool intervalElapsed)
        {
            if (trigger == VersionCheckTrigger.Manual)
                return true;

            if (dismissedToday)
                return false;

            if (trigger == VersionCheckTrigger.Startup)
                return true;

            return intervalElapsed;
        }

        public static bool ShouldForceRefresh(VersionCheckTrigger trigger)
        {
            return trigger == VersionCheckTrigger.Startup ||
                   trigger == VersionCheckTrigger.Manual;
        }
    }
}
