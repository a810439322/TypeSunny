using TypeSunny;

namespace Net
{
    internal static class JsRaceLoginState
    {
        private const string JbsUsernameKey = "极速用户名";
        private const string JbsPasswordKey = "极速密码";
        private const string JbsDisplayNameKey = "极速显示名称";
        private const string JiSuCupUsernameKey = "极速杯用户名";
        private const string JiSuCupPasswordKey = "极速杯密码";
        private const string JiSuCupDisplayNameKey = "极速杯显示名称";

        public static string Username => FirstNonEmpty(
            Config.GetString(JbsUsernameKey),
            Config.GetString(JiSuCupUsernameKey));

        public static string Password => FirstNonEmpty(
            Config.GetPassword(JbsPasswordKey),
            Config.GetPassword(JiSuCupPasswordKey));

        public static string DisplayName => FirstNonEmpty(
            Config.GetString(JbsDisplayNameKey),
            Config.GetString(JiSuCupDisplayNameKey));

        public static bool IsLoggedIn => !string.IsNullOrWhiteSpace(DisplayName);

        public static void SaveLogin(string username, string password, string displayName)
        {
            Config.Set(JbsUsernameKey, username);
            Config.SetPassword(JbsPasswordKey, password);
            Config.Set(JbsDisplayNameKey, displayName);
            Config.Set(JiSuCupUsernameKey, username);
            Config.SetPassword(JiSuCupPasswordKey, password);
            Config.Set(JiSuCupDisplayNameKey, displayName);
        }

        private static string FirstNonEmpty(string first, string second)
        {
            return !string.IsNullOrWhiteSpace(first) ? first : second;
        }
    }
}
