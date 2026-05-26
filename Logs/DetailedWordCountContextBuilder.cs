using System;
using System.Linq;
using System.Text;
using TypeSunny.ArticleSender;
using TypeSunny.Core;
using TypeSunny.Difficulty;
using TypeSunny.Net;

namespace TypeSunny.Logs
{
    internal sealed class DetailedWordCountContextBuilder
    {
        private readonly DifficultyDict difficultyDict;
        private readonly Func<bool> hasWenlaiArticle;
        private readonly Func<string> getWenlaiDifficultyName;
        private readonly Func<string> getTrainerExerciseName;
        private readonly Func<string> getRaceServerId;
        private readonly Func<int> getRaceId;
        private readonly Func<RaceServerManager> getRaceServerManager;

        public DetailedWordCountContextBuilder(
            DifficultyDict difficultyDict,
            Func<bool> hasWenlaiArticle,
            Func<string> getWenlaiDifficultyName,
            Func<string> getTrainerExerciseName,
            Func<string> getRaceServerId,
            Func<int> getRaceId,
            Func<RaceServerManager> getRaceServerManager)
        {
            this.difficultyDict = difficultyDict;
            this.hasWenlaiArticle = hasWenlaiArticle;
            this.getWenlaiDifficultyName = getWenlaiDifficultyName;
            this.getTrainerExerciseName = getTrainerExerciseName;
            this.getRaceServerId = getRaceServerId;
            this.getRaceId = getRaceId;
            this.getRaceServerManager = getRaceServerManager;
        }

        public TypingWordCountContext Build(TxtSource source, string loadedText)
        {
            string difficultyLabel = DetailedWordCountLog.NormalizeDifficultyLabel(difficultyDict.CalcText(loadedText ?? ""));
            bool includeDifficulty = source != TxtSource.trainer;

            switch (source)
            {
                case TxtSource.articlesender:
                    {
                        string wenlaiDifficulty = hasWenlaiArticle() ? getWenlaiDifficultyName() : "";
                        if (string.IsNullOrWhiteSpace(wenlaiDifficulty))
                            wenlaiDifficulty = "未分类";
                        return new TypingWordCountContext(
                            source,
                            "category:wenlai:" + NormalizeKey(wenlaiDifficulty),
                            "文来 / " + wenlaiDifficulty,
                            true,
                            difficultyLabel);
                    }
                case TxtSource.book:
                    {
                        string title = StripArticleExtension(ArticleManager.Title);
                        if (string.IsNullOrWhiteSpace(title))
                            title = "未命名";
                        return new TypingWordCountContext(
                            source,
                            "category:book:" + NormalizeKey(title),
                            "本地文章 / " + title,
                            true,
                            difficultyLabel);
                    }
                case TxtSource.trainer:
                    {
                        string title = getTrainerExerciseName();
                        if (string.IsNullOrWhiteSpace(title))
                            title = "未命名";
                        return new TypingWordCountContext(
                            source,
                            "category:trainer:" + NormalizeKey(title),
                            DetailedWordCountLog.FormatTrainerCategoryDisplayName(title),
                            false,
                            "");
                    }
                case TxtSource.jbs:
                    return new TypingWordCountContext(source, "category:race:jbs", "赛文 / 锦标赛", true, difficultyLabel, true);
                case TxtSource.jisucup:
                    return new TypingWordCountContext(source, "category:race:jisucup", "赛文 / 极速杯", true, difficultyLabel, true);
                case TxtSource.raceApi:
                    {
                        string name = GetCurrentRaceDisplayName();
                        return new TypingWordCountContext(
                            source,
                            "category:raceapi:" + NormalizeKey(name),
                            "赛文 / " + name,
                            true,
                            difficultyLabel,
                            true);
                    }
                case TxtSource.clipboard:
                    return new TypingWordCountContext(source, "category:clipboard", "剪贴板载文", true, difficultyLabel);
                case TxtSource.qq:
                    return new TypingWordCountContext(source, "category:qq", "QQ群载文", true, difficultyLabel);
                default:
                    return new TypingWordCountContext(source, "category:other", "其他来源", includeDifficulty, difficultyLabel);
            }
        }

        private string GetCurrentRaceDisplayName()
        {
            try
            {
                var serverManager = getRaceServerManager();
                var server = serverManager?.GetAllServers()?.FirstOrDefault(s => s.Id == getRaceServerId());
                var race = server?.Races?.FirstOrDefault(r => r.Id == getRaceId());
                if (!string.IsNullOrWhiteSpace(race?.Name))
                    return race.Name;
                if (!string.IsNullOrWhiteSpace(server?.Name))
                    return server.Name;
            }
            catch
            {
            }

            return "赛文API";
        }

        private static string StripArticleExtension(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return "";

            string result = title;
            foreach (string extension in new[] { ".txt", ".Txt", ".TXT", ".epub", ".Epub", ".EPUB" })
            {
                if (result.EndsWith(extension, StringComparison.Ordinal))
                    return result.Substring(0, result.Length - extension.Length);
            }

            return result;
        }

        private static string NormalizeKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "unnamed";

            var sb = new StringBuilder();
            foreach (char c in value.Trim())
            {
                if (char.IsControl(c) || c == ':' || c == '\t' || c == '\r' || c == '\n')
                    sb.Append('_');
                else
                    sb.Append(c);
            }

            return sb.ToString();
        }
    }
}
