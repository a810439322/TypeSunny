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
                "发文区字体大小", "40",
                "练单场景_发文区字体大小", "36",
                "跟打区字体大小", "40",
                "练单场景_跟打区字体大小", "38",
                "成绩区字体大小", "15",
                "练单场景_成绩区字体大小", "17",
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
            AssertEqual("trainer display font size key", "练单场景_发文区字体大小",
                TrainerMainWindowConfigScope.ResolveKey("发文区字体大小"));
            AssertEqual("trainer display font size value", 36d,
                TrainerMainWindowConfigScope.GetDouble("发文区字体大小"));
            AssertEqual("trainer input font size value", 38d,
                TrainerMainWindowConfigScope.GetDouble("跟打区字体大小"));
            AssertEqual("trainer results font size value", 17d,
                TrainerMainWindowConfigScope.GetDouble("成绩区字体大小"));

            Config.dicts["练单主窗口单独记忆"] = "否";
            AssertEqual("disabled maps normal key", "窗口宽度",
                TrainerMainWindowConfigScope.ResolveKey("窗口宽度"));
            AssertEqual("disabled reads normal value", "966.4",
                TrainerMainWindowConfigScope.GetString("窗口宽度"));
            AssertEqual("disabled reads normal display font size", 40d,
                TrainerMainWindowConfigScope.GetDouble("发文区字体大小"));

            Config.dicts["练单主窗口单独记忆"] = "是";
            TrainerMainWindowConfigScope.ResetTrainerScopedValues();
            AssertEqual("reset removes trainer value", false,
                Config.dicts.ContainsKey("练单场景_窗口宽度"));
            AssertEqual("reset removes trainer display font size", false,
                Config.dicts.ContainsKey("练单场景_发文区字体大小"));
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
                    "发文区字体大小", "40",
                    "跟打区字体大小", "40",
                    "成绩区字体大小", "15",
                    "一键极简", "否");

                System.IO.File.WriteAllText(
                    configPath,
                    "练单主窗口单独记忆\t是\n练单场景_窗口宽度\t620\n练单场景_发文区字体大小\t36\n练单场景_跟打区字体大小\t38\n练单场景_成绩区字体大小\t17\n练单场景_一键极简\t是\n练单场景_其它设置\t保留\n");

                Config.Path = configPath;
                Config.ReadConfig();

                AssertEqual("read known trainer scoped value", "620",
                    Config.GetString("练单场景_窗口宽度"));
                AssertEqual("read another known trainer scoped value", "是",
                    Config.GetString("练单场景_一键极简"));
                AssertEqual("read trainer display font size", "36",
                    Config.GetString("练单场景_发文区字体大小"));
                AssertEqual("read trainer input font size", "38",
                    Config.GetString("练单场景_跟打区字体大小"));
                AssertEqual("read trainer results font size", "17",
                    Config.GetString("练单场景_成绩区字体大小"));
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
