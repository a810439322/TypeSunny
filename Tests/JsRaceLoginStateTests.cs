using System;
using System.Collections.Generic;
using Net;
using TypeSunny.Utils;

namespace TypeSunny
{
    internal static class Config
    {
        public static Dictionary<string, string> dicts = new Dictionary<string, string>();

        public static string GetString(string key)
        {
            return dicts.ContainsKey(key) ? dicts[key] : "";
        }

        public static void Set(string key, string value)
        {
            dicts[key] = value;
        }

        public static string GetPassword(string key)
        {
            string cipher = GetString(key);
            return string.IsNullOrWhiteSpace(cipher) ? "" : PasswordCrypto.Decrypt(cipher);
        }

        public static void SetPassword(string key, string value)
        {
            Set(key, string.IsNullOrWhiteSpace(value) ? "" : PasswordCrypto.Encrypt(value));
        }
    }
}

namespace TypeSunny.Tests
{
    internal static class JsRaceLoginStateTests
    {
        private static int Main()
        {
            try
            {
                LoginFromJbsWritesBothLegacyKeySets();
                LoginFromJiSuCupWritesBothLegacyKeySets();
                JbsReadsJiSuCupCredentialsWhenOnlyJiSuCupKeysExist();
                JiSuCupReadsJbsCredentialsWhenOnlyJbsKeysExist();

                Console.WriteLine("All JsRaceLoginState tests passed.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("FAIL: " + ex.Message);
                return 1;
            }
        }

        private static void LoginFromJbsWritesBothLegacyKeySets()
        {
            ResetConfig();

            JsRaceLoginState.SaveLogin("alice", "secret", "晴天");

            AssertEqual("jbs username", "alice", Config.GetString("极速用户名"));
            AssertEqual("jbs password", "secret", Config.GetPassword("极速密码"));
            AssertEqual("jbs display", "晴天", Config.GetString("极速显示名称"));
            AssertEqual("jisucup username", "alice", Config.GetString("极速杯用户名"));
            AssertEqual("jisucup password", "secret", Config.GetPassword("极速杯密码"));
            AssertEqual("jisucup display", "晴天", Config.GetString("极速杯显示名称"));
        }

        private static void LoginFromJiSuCupWritesBothLegacyKeySets()
        {
            ResetConfig();

            JsRaceLoginState.SaveLogin("bob", "pwd", "极速用户");

            AssertEqual("jbs username", "bob", Config.GetString("极速用户名"));
            AssertEqual("jbs password", "pwd", Config.GetPassword("极速密码"));
            AssertEqual("jbs display", "极速用户", Config.GetString("极速显示名称"));
            AssertEqual("jisucup username", "bob", Config.GetString("极速杯用户名"));
            AssertEqual("jisucup password", "pwd", Config.GetPassword("极速杯密码"));
            AssertEqual("jisucup display", "极速用户", Config.GetString("极速杯显示名称"));
        }

        private static void JbsReadsJiSuCupCredentialsWhenOnlyJiSuCupKeysExist()
        {
            ResetConfig();
            Config.Set("极速杯用户名", "cup-user");
            Config.SetPassword("极速杯密码", "cup-pass");
            Config.Set("极速杯显示名称", "杯用户");

            AssertEqual("shared username", "cup-user", JsRaceLoginState.Username);
            AssertEqual("shared password", "cup-pass", JsRaceLoginState.Password);
            AssertEqual("shared display", "杯用户", JsRaceLoginState.DisplayName);
        }

        private static void JiSuCupReadsJbsCredentialsWhenOnlyJbsKeysExist()
        {
            ResetConfig();
            Config.Set("极速用户名", "jbs-user");
            Config.SetPassword("极速密码", "jbs-pass");
            Config.Set("极速显示名称", "锦标用户");

            AssertEqual("shared username", "jbs-user", JsRaceLoginState.Username);
            AssertEqual("shared password", "jbs-pass", JsRaceLoginState.Password);
            AssertEqual("shared display", "锦标用户", JsRaceLoginState.DisplayName);
        }

        private static void ResetConfig()
        {
            Config.dicts = new Dictionary<string, string>();
        }

        private static void AssertEqual(string name, string expected, string actual)
        {
            if (!object.Equals(expected, actual))
                throw new Exception(name + " expected [" + expected + "], got [" + actual + "].");
        }
    }
}
