using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using TypeSunny.Net.Http;

namespace TypeSunny.Net
{
    public sealed class WenlaiDailyWordsService : IDailyWordsService
    {
        private const string ServiceName = "文来";
        private readonly AccountSystemManager accountManager;

        public WenlaiDailyWordsService()
            : this(new AccountSystemManager())
        {
        }

        public WenlaiDailyWordsService(AccountSystemManager accountManager)
        {
            this.accountManager = accountManager ?? new AccountSystemManager();
        }

        public async Task<DailyWordsReportResult> ReportAsync(
            DailyWordsReport report,
            CancellationToken cancellationToken)
        {
            if (report == null || report.Count <= 0)
                return DailyWordsReportResult.Success();

            ApiClient client = CreateClient(requireAuth: true, out string error);
            if (client == null)
                return DailyWordsReportResult.Failure(error);

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                ApiResponse response = await client.PostAsync("/api/dailyWords/report", new
                {
                    count = report.Count,
                    singleWordCount = report.SingleWordCount,
                    articleWordCount = report.ArticleWordCount,
                    articleAvgSpeed = report.ArticleAvgSpeed,
                    singleAvgKeystroke = report.SingleAvgKeystroke,
                    date = report.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                });
                cancellationToken.ThrowIfCancellationRequested();

                return response != null && response.IsSuccess
                    ? DailyWordsReportResult.Success(response.Msg)
                    : DailyWordsReportResult.Failure(response?.Msg ?? "上报字数失败");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                return DailyWordsReportResult.Failure("上报字数失败: " + ex.Message);
            }
        }

        public async Task<DailyWordsRankResult> GetCurrentRankAsync(
            DailyWordsLeaderboardType type,
            DateTime? date,
            CancellationToken cancellationToken)
        {
            ApiClient client = CreateClient(requireAuth: true, out string error);
            if (client == null)
                return DailyWordsRankResult.Failure(error);

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                ApiResponse response = await client.GetAsync("/api/dailyWords/rank", BuildQuery(type, date, null));
                cancellationToken.ThrowIfCancellationRequested();
                if (response == null || !response.IsSuccess)
                    return DailyWordsRankResult.Failure(response?.Msg ?? "查询排名失败");

                DailyWordsRank rank = ParseRank(response.RawData, type, date);
                return DailyWordsRankResult.Success(rank);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                return DailyWordsRankResult.Failure("查询排名失败: " + ex.Message);
            }
        }

        public async Task<DailyWordsLeaderboardResult> GetLeaderboardAsync(
            DailyWordsLeaderboardType type,
            DateTime? date,
            int limit,
            CancellationToken cancellationToken)
        {
            ApiClient client = CreateClient(requireAuth: false, out string error);
            if (client == null)
                return DailyWordsLeaderboardResult.Failure(error);

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                ApiResponse response = await client.GetAsync(
                    "/api/dailyWords/leaderboard",
                    BuildQuery(type, date, limit <= 0 ? 100 : limit));
                cancellationToken.ThrowIfCancellationRequested();

                if (response == null || !response.IsSuccess)
                    return DailyWordsLeaderboardResult.Failure(response?.Msg ?? "查询排行榜失败");

                return ParseLeaderboard(response.RawData, type, date);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                return DailyWordsLeaderboardResult.Failure("查询排行榜失败: " + ex.Message);
            }
        }

        private ApiClient CreateClient(bool requireAuth, out string error)
        {
            error = "";
            string baseUrl = NormalizeBaseUrl(Config.GetString("文来接口地址"));
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                error = "请在设置中配置[文来接口地址]";
                return null;
            }

            accountManager.Reload();
            AccountInfo account = accountManager.GetAccount(ServiceName);
            if (requireAuth && !HasValidLogin(account, baseUrl))
            {
                error = "请先登录文来账号";
                return null;
            }

            IAuthProvider authProvider = null;
            if (HasValidLogin(account, baseUrl) && !string.IsNullOrWhiteSpace(account.JwtToken))
            {
                authProvider = new JwtAuthProvider(account.JwtToken);
            }
            else if (HasValidLogin(account, baseUrl) && !string.IsNullOrWhiteSpace(account.Cookies))
            {
                var cookieAuthProvider = new CookieAuthProvider(baseUrl: baseUrl);
                cookieAuthProvider.LoadCookies(account.Cookies, baseUrl);
                authProvider = cookieAuthProvider;
            }

            return new ApiClient(baseUrl, authProvider);
        }

        private static bool HasValidLogin(AccountInfo account, string baseUrl)
        {
            return account != null
                && !string.IsNullOrWhiteSpace(account.Username)
                && (!string.IsNullOrWhiteSpace(account.JwtToken) || !string.IsNullOrWhiteSpace(account.Cookies))
                && IsSameServer(account.Domain, baseUrl);
        }

        private static Dictionary<string, string> BuildQuery(
            DailyWordsLeaderboardType type,
            DateTime? date,
            int? limit)
        {
            var query = new Dictionary<string, string>
            {
                ["type"] = ToApiType(type)
            };

            if (type == DailyWordsLeaderboardType.Daily && date.HasValue)
                query["date"] = date.Value.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (limit.HasValue)
                query["limit"] = limit.Value.ToString(CultureInfo.InvariantCulture);

            return query;
        }

        private static DailyWordsRank ParseRank(JToken data, DailyWordsLeaderboardType fallbackType, DateTime? fallbackDate)
        {
            JObject obj = data as JObject;
            JObject rankObj = obj?["rank"] as JObject;
            if (rankObj == null)
                return null;

            DailyWordsLeaderboardEntry entry = ParseEntry(
                rankObj,
                ParseType(obj.Value<string>("type"), fallbackType),
                ParseDate(obj.Value<string>("date"), fallbackDate));
            if (entry == null)
                return null;

            return new DailyWordsRank(
                entry.Rank,
                entry.WordCount,
                entry.Type,
                entry.Date,
                entry.SingleWordCount,
                entry.ArticleWordCount,
                entry.ArticleAvgSpeed,
                entry.SingleAvgKeystroke);
        }

        private static DailyWordsLeaderboardResult ParseLeaderboard(
            JToken data,
            DailyWordsLeaderboardType fallbackType,
            DateTime? fallbackDate)
        {
            JObject obj = data as JObject;
            if (obj == null)
                return DailyWordsLeaderboardResult.Failure("API返回的数据字段不是对象");

            DailyWordsLeaderboardType type = ParseType(obj.Value<string>("type"), fallbackType);
            DateTime? date = ParseDate(obj.Value<string>("date"), fallbackDate);
            var entries = new List<DailyWordsLeaderboardEntry>();
            JArray leaderboard = obj["leaderboard"] as JArray;
            if (leaderboard != null)
            {
                foreach (JToken item in leaderboard)
                {
                    DailyWordsLeaderboardEntry entry = ParseEntry(item as JObject, type, date);
                    if (entry != null)
                        entries.Add(entry);
                }
            }

            return DailyWordsLeaderboardResult.Success(type, date, entries);
        }

        private static DailyWordsLeaderboardEntry ParseEntry(
            JObject obj,
            DailyWordsLeaderboardType fallbackType,
            DateTime? fallbackDate)
        {
            if (obj == null)
                return null;

            return new DailyWordsLeaderboardEntry(
                obj.Value<int?>("rank") ?? 0,
                obj.Value<long?>("userId") ?? 0,
                obj.Value<string>("username") ?? "",
                obj.Value<long?>("wordCount") ?? 0,
                ParseType(obj.Value<string>("type"), fallbackType),
                ParseDate(obj.Value<string>("rankingDate"), fallbackDate),
                obj.Value<long?>("singleWordCount") ?? 0,
                obj.Value<long?>("articleWordCount") ?? 0,
                obj.Value<double?>("articleAvgSpeed") ?? 0,
                obj.Value<double?>("singleAvgKeystroke") ?? 0);
        }

        private static string ToApiType(DailyWordsLeaderboardType type)
        {
            return type == DailyWordsLeaderboardType.Total ? "total" : "daily";
        }

        private static DailyWordsLeaderboardType ParseType(string value, DailyWordsLeaderboardType fallback)
        {
            if (string.Equals(value, "total", StringComparison.OrdinalIgnoreCase))
                return DailyWordsLeaderboardType.Total;
            if (string.Equals(value, "daily", StringComparison.OrdinalIgnoreCase))
                return DailyWordsLeaderboardType.Daily;
            return fallback;
        }

        private static DateTime? ParseDate(string value, DateTime? fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
                return fallback;

            if (DateTime.TryParseExact(
                value,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime exact))
                return exact.Date;

            return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed)
                ? parsed.Date
                : fallback;
        }

        private static string NormalizeBaseUrl(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "";

            string url = value.Trim().TrimEnd('/');
            const string oldPath = "/api/get_text";
            if (url.EndsWith(oldPath, StringComparison.OrdinalIgnoreCase))
                url = url.Substring(0, url.Length - oldPath.Length);

            return url.TrimEnd('/');
        }

        private static bool IsSameServer(string left, string right)
        {
            NormalizedUrl normalizedLeft = NormalizeForCompare(left);
            NormalizedUrl normalizedRight = NormalizeForCompare(right);
            return string.Equals(normalizedLeft.Authority, normalizedRight.Authority, StringComparison.OrdinalIgnoreCase)
                && string.Equals(normalizedLeft.Path, normalizedRight.Path, StringComparison.Ordinal);
        }

        private static NormalizedUrl NormalizeForCompare(string value)
        {
            string url = NormalizeBaseUrl(value);
            if (string.IsNullOrWhiteSpace(url))
                return NormalizedUrl.Empty;

            try
            {
                var uri = new Uri(url);
                var builder = new UriBuilder(uri)
                {
                    Path = uri.AbsolutePath.TrimEnd('/'),
                    Query = "",
                    Fragment = ""
                };
                if (builder.Uri.IsDefaultPort)
                    builder.Port = -1;
                Uri normalized = builder.Uri;
                return new NormalizedUrl(
                    normalized.GetLeftPart(UriPartial.Authority).TrimEnd('/'),
                    normalized.AbsolutePath.TrimEnd('/'));
            }
            catch
            {
                return new NormalizedUrl(url, "");
            }
        }

        private sealed class NormalizedUrl
        {
            public static readonly NormalizedUrl Empty = new NormalizedUrl("", "");

            public NormalizedUrl(string authority, string path)
            {
                Authority = authority ?? "";
                Path = path ?? "";
            }

            public string Authority { get; private set; }
            public string Path { get; private set; }
        }
    }
}
