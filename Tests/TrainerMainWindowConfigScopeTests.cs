using System;
using TypeSunny;
using TypeSunny.UI;

namespace TypeSunny.Tests
{
    internal static class TrainerMainWindowConfigScopeTests
    {
        private static int Main()
        {
            try
            {
                TrainerScopeMapsAndFallsBackAsExpected();
                TrainerScopedValuesCanBeReadBackFromConfigFile();
                Console.WriteLine("All TrainerMainWindowConfigScope tests passed.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        private static void TrainerScopeMapsAndFallsBackAsExpected()
        {
            Config.Path = "";
            Config.dicts.Clear();
            Config.SetDefault(
                "练单主窗口单独记忆", "是",
                "窗口宽度", "966.4",
                "练单场景_窗口宽度", "620",
                "练单场景_其它设置", "保留",
                "一键极简", "否",
                "练单场景_一键极简", "是",
                "首页功能按钮顺序", "文来,晴练单,晴双拼,赛文");

            AssertEqual("normal key outside trainer scope", "窗口宽度",
                TrainerMainWindowConfigScope.ResolveKey("窗口宽度"));

            TrainerMainWindowConfigScope.EnterTrainerScope();
            AssertEqual("trainer key in trainer scope", "练单场景_窗口宽度",
                TrainerMainWindowConfigScope.ResolveKey("窗口宽度"));
            AssertEqual("trainer scoped value", "620",
                TrainerMainWindowConfigScope.GetString("窗口宽度"));
            AssertEqual("trainer scoped bool", true,
                TrainerMainWindowConfigScope.GetBool("一键极简"));

            Config.dicts["练单主窗口单独记忆"] = "否";
            AssertEqual("disabled maps normal key", "窗口宽度",
                TrainerMainWindowConfigScope.ResolveKey("窗口宽度"));
            AssertEqual("disabled reads normal value", "966.4",
                TrainerMainWindowConfigScope.GetString("窗口宽度"));

            Config.dicts["练单主窗口单独记忆"] = "是";
            TrainerMainWindowConfigScope.ResetTrainerScopedValues();
            AssertEqual("reset removes trainer value", false,
                Config.dicts.ContainsKey("练单场景_窗口宽度"));
            AssertEqual("reset keeps switch", "是",
                Config.GetString("练单主窗口单独记忆"));
            AssertEqual("reset keeps unrelated trainer prefix value", "保留",
                Config.GetString("练单场景_其它设置"));
            AssertEqual("missing trainer value falls back normal", "966.4",
                TrainerMainWindowConfigScope.GetString("窗口宽度"));
            AssertEqual("missing trainer scoped value is not owned by current scope", false,
                TrainerMainWindowConfigScope.HasCurrentScopeValue("首页功能按钮顺序"));

            TrainerMainWindowConfigScope.SetRaw("首页功能按钮顺序", "晴练单,文来");
            AssertEqual("created trainer scoped value is owned by current scope", true,
                TrainerMainWindowConfigScope.HasCurrentScopeValue("首页功能按钮顺序"));
        }

        private static void TrainerScopedValuesCanBeReadBackFromConfigFile()
        {
            string configPath = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "typesunny-trainer-main-window-memory-" + System.Guid.NewGuid().ToString("N") + ".txt");

            try
            {
                Config.Path = "";
                Config.dicts.Clear();
                Config.SetDefault(
                    "练单主窗口单独记忆", "是",
                    "窗口宽度", "966.4",
                    "一键极简", "否");

                System.IO.File.WriteAllText(
                    configPath,
                    "练单主窗口单独记忆\t是\n练单场景_窗口宽度\t620\n练单场景_一键极简\t是\n练单场景_其它设置\t保留\n");

                Config.Path = configPath;
                Config.ReadConfig();

                AssertEqual("read known trainer scoped value", "620",
                    Config.GetString("练单场景_窗口宽度"));
                AssertEqual("read another known trainer scoped value", "是",
                    Config.GetString("练单场景_一键极简"));
                AssertEqual("does not import unrelated trainer prefix value", "",
                    Config.GetString("练单场景_其它设置"));
            }
            finally
            {
                Config.Path = "";
                try { System.IO.File.Delete(configPath); } catch { }
            }
        }

        private static void AssertEqual<T>(string name, T expected, T actual)
        {
            if (!object.Equals(expected, actual))
                throw new Exception(name + ": expected [" + expected + "] got [" + actual + "]");
        }
    }
}
