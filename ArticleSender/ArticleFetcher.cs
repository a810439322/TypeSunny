using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using TypeSunny.Net.Http;

namespace TypeSunny.ArticleSender
{
    /// <summary>
    /// 难度信息类
    /// </summary>
    public class DifficultyInfo
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Count { get; set; }
    }

    /// <summary>
    /// 分类信息类
    /// </summary>
    public class CategoryInfo
    {
        public string Code { get; set; }
        public string Name { get; set; }
    }

    /// <summary>
    /// 文章获取器，通过 ApiClient 获取文章
    /// </summary>
    public class ArticleFetcher
    {
        private static ApiClient apiClient;
        private static List<CategoryInfo> cachedCategories = null;
        private static DateTime categoryCacheTime = DateTime.MinValue;
        private static readonly object difficultyCacheLock = new object();
        private static readonly Dictionary<string, DifficultyCacheEntry> difficultyCacheByCategory =
            new Dictionary<string, DifficultyCacheEntry>();
        private static readonly Dictionary<string, Task<List<DifficultyInfo>>> difficultyRequestsByCategory =
            new Dictionary<string, Task<List<DifficultyInfo>>>();
        private static readonly TimeSpan CACHE_EXPIRATION = TimeSpan.FromMinutes(5);

        private class DifficultyCacheEntry
        {
            public List<DifficultyInfo> Difficulties { get; set; }
            public DateTime CacheTime { get; set; }
        }

        public static List<DifficultyInfo> CreateDefaultDifficulties()
        {
            return new List<DifficultyInfo>
            {
                new DifficultyInfo { Id = 1, Name = "淼", Count = 0 },
                new DifficultyInfo { Id = 2, Name = "水", Count = 0 },
                new DifficultyInfo { Id = 3, Name = "易", Count = 0 },
                new DifficultyInfo { Id = 4, Name = "普", Count = 0 },
                new DifficultyInfo { Id = 5, Name = "难", Count = 0 },
                new DifficultyInfo { Id = 6, Name = "虐", Count = 0 }
            };
        }

        public static bool HasCachedDifficulties(string categoryOverride = null)
        {
            string configuredCategory = Config.GetString("文来分类");
            string configCategory = ((categoryOverride ?? configuredCategory) ?? "").Trim();
            return TryGetCachedDifficulties(configCategory, out _);
        }

        // ========== 安全取值工具方法 ==========
        // JToken?.ToObject<int>() 对 JSON null 无效（JToken 不是 C# null，?.不会短路）
        // 必须先检查 Type != JTokenType.Null

        private static int SafeInt(JToken token, int defaultValue = 0)
        {
            if (token == null || token.Type == JTokenType.Null)
                return defaultValue;
            try { return token.ToObject<int>(); }
            catch { return defaultValue; }
        }

        private static double SafeDouble(JToken token, double defaultValue = 0)
        {
            if (token == null || token.Type == JTokenType.Null)
                return defaultValue;
            try { return token.ToObject<double>(); }
            catch { return defaultValue; }
        }

        private static string SafeString(JToken token, string defaultValue = "")
        {
            if (token == null || token.Type == JTokenType.Null)
                return defaultValue;
            return token.ToString();
        }

        private static string ReadCategoryCode(JToken item)
        {
            foreach (var field in new[] { "code", "category", "value", "slug", "key", "categoryCode" })
            {
                string value = SafeString(item?[field]).Trim();
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return "";
        }

        private static string ReadCategoryName(JToken item, string fallback)
        {
            foreach (var field in new[] { "name", "label", "title" })
            {
                string value = SafeString(item?[field]).Trim();
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return fallback ?? "";
        }

        private static int ReadDifficultyLevel(string labelOrLevel, JToken item, Dictionary<string, int> labelToLevel)
        {
            string key = (labelOrLevel ?? "").Trim();
            if (labelToLevel.ContainsKey(key))
                return labelToLevel[key];

            int parsed;
            if (int.TryParse(key, out parsed))
                return parsed;

            if (item == null || item.Type != JTokenType.Object)
                return 0;

            foreach (var field in new[] { "difficultyLevel", "level", "id", "difficultyId", "levelId" })
            {
                int level = SafeInt(item?[field]);
                if (level > 0)
                    return level;
            }

            foreach (var field in new[] { "difficultyLabel", "label", "name", "levelName", "difficulty", "level" })
            {
                string label = SafeString(item?[field]).Trim();
                if (labelToLevel.ContainsKey(label))
                    return labelToLevel[label];
            }

            return 0;
        }

        private static string ReadDifficultyName(int level, string labelOrLevel, JToken item, Dictionary<int, string> levelToLabel)
        {
            string key = (labelOrLevel ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(key) && !int.TryParse(key, out _))
                return key;

            if (item == null || item.Type != JTokenType.Object)
                return levelToLabel.ContainsKey(level) ? levelToLabel[level] : level.ToString();

            foreach (var field in new[] { "difficultyLabel", "label", "name", "levelName", "difficulty" })
            {
                string value = SafeString(item?[field]).Trim();
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return levelToLabel.ContainsKey(level) ? levelToLabel[level] : level.ToString();
        }

        private static int ReadDifficultyCount(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
                return 0;
            if (token.Type != JTokenType.Object)
                return SafeInt(token);

            foreach (var field in new[] { "count", "segmentCount", "segments", "total", "totalSegments", "totalCount" })
            {
                int count = SafeInt(token[field]);
                if (count > 0)
                    return count;
            }

            return 0;
        }

        private static void AddDifficultyStat(List<DifficultyInfo> difficulties, string labelOrLevel, JToken statToken,
            Dictionary<string, int> labelToLevel, Dictionary<int, string> levelToLabel)
        {
            int level = ReadDifficultyLevel(labelOrLevel, statToken, labelToLevel);
            if (level <= 0)
                return;

            difficulties.Add(new DifficultyInfo
            {
                Id = level,
                Name = ReadDifficultyName(level, labelOrLevel, statToken, levelToLabel),
                Count = ReadDifficultyCount(statToken)
            });
        }

        private static JToken UnwrapStatsData(JToken token)
        {
            if (token == null || token.Type != JTokenType.Object)
                return token;

            var data = token["data"];
            if (LooksLikeDifficultyStats(data))
                return data;

            var msg = token["msg"];
            if (LooksLikeDifficultyStats(msg))
                return msg;

            return token;
        }

        private static bool LooksLikeDifficultyStats(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
                return false;

            if (token.Type == JTokenType.Array)
                return true;

            if (token.Type != JTokenType.Object)
                return false;

            foreach (var field in new[] { "levelStats", "difficultyStats", "stats", "levels", "totalSegments", "totalCount" })
            {
                if (token[field] != null)
                    return true;
            }

            return false;
        }

        private static bool TryGetCachedDifficulties(string category, out List<DifficultyInfo> difficulties)
        {
            category = (category ?? "").Trim();
            lock (difficultyCacheLock)
            {
                if (difficultyCacheByCategory.TryGetValue(category, out var entry))
                {
                    if (DateTime.Now - entry.CacheTime <= CACHE_EXPIRATION)
                    {
                        difficulties = entry.Difficulties;
                        return true;
                    }

                    difficultyCacheByCategory.Remove(category);
                }
            }

            difficulties = null;
            return false;
        }

        private static void StoreDifficulties(string category, List<DifficultyInfo> difficulties)
        {
            category = (category ?? "").Trim();
            lock (difficultyCacheLock)
            {
                difficultyCacheByCategory[category] = new DifficultyCacheEntry
                {
                    Difficulties = difficulties ?? new List<DifficultyInfo>(),
                    CacheTime = DateTime.Now
                };
            }
        }

        private static List<DifficultyInfo> CloneDifficulties(List<DifficultyInfo> difficulties)
        {
            return (difficulties ?? new List<DifficultyInfo>())
                .Select(d => new DifficultyInfo { Id = d.Id, Name = d.Name, Count = d.Count })
                .ToList();
        }

        private static List<DifficultyInfo> NormalizeDifficulties(List<DifficultyInfo> parsedDifficulties)
        {
            var normalized = CreateDefaultDifficulties()
                .ToDictionary(d => d.Id, d => d);

            foreach (var difficulty in parsedDifficulties ?? new List<DifficultyInfo>())
            {
                if (difficulty == null || difficulty.Id <= 0)
                    continue;

                if (normalized.TryGetValue(difficulty.Id, out var existing))
                {
                    if (!string.IsNullOrWhiteSpace(difficulty.Name))
                        existing.Name = difficulty.Name;
                    existing.Count = difficulty.Count;
                }
                else
                {
                    normalized[difficulty.Id] = new DifficultyInfo
                    {
                        Id = difficulty.Id,
                        Name = string.IsNullOrWhiteSpace(difficulty.Name) ? difficulty.Id.ToString() : difficulty.Name,
                        Count = difficulty.Count
                    };
                }
            }

            return normalized.Values.OrderBy(d => d.Id).ToList();
        }

        /// <summary>
        /// 注入 ApiClient 实例（由 WenlaiHelper 在初始化时调用）
        /// </summary>
        public static void Initialize(ApiClient client)
        {
            apiClient = client;
            System.Diagnostics.Debug.WriteLine($"[ArticleFetcher] 已初始化 ApiClient，baseUrl: {client?.BaseUrl ?? "(null)"}");
        }

        /// <summary>
        /// 获取或创建 ApiClient（如果未通过 Initialize 注入，则从配置创建）
        /// </summary>
        private static ApiClient EnsureClient()
        {
            if (apiClient != null)
            {
                // 检查配置中的地址是否已变更
                string currentUrl = Config.GetString("文来接口地址");
                if (!string.IsNullOrWhiteSpace(currentUrl))
                {
                    currentUrl = currentUrl.TrimEnd('/');
                    if (currentUrl.EndsWith("/api/get_text"))
                        currentUrl = currentUrl.Substring(0, currentUrl.Length - "/api/get_text".Length);

                    if (apiClient.BaseUrl != currentUrl)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ArticleFetcher] 检测到地址变更: {apiClient.BaseUrl} -> {currentUrl}");
                        apiClient = null;
                    }
                }

                if (apiClient != null)
                {
                    // 如果已有 client 但没有认证，尝试补上 JWT
                    if (apiClient.AuthProvider == null || string.IsNullOrWhiteSpace((apiClient.AuthProvider as JwtAuthProvider)?.AccessToken))
                    {
                        var acctMgr = new TypeSunny.Net.AccountSystemManager();
                        var acct = acctMgr.GetAccount("文来");
                        if (acct != null && !string.IsNullOrWhiteSpace(acct.JwtToken))
                        {
                            var jwtAuth = new JwtAuthProvider(acct.JwtToken);
                            apiClient = new ApiClient(apiClient.BaseUrl, jwtAuth);
                            System.Diagnostics.Debug.WriteLine("[ArticleFetcher] 已补充 JWT 认证");
                        }
                    }
                    return apiClient;
                }
            }

            string apiUrl = Config.GetString("文来接口地址");
            if (string.IsNullOrWhiteSpace(apiUrl))
                return null;

            apiUrl = apiUrl.TrimEnd('/');
            if (apiUrl.EndsWith("/api/get_text"))
                apiUrl = apiUrl.Substring(0, apiUrl.Length - "/api/get_text".Length);

            // 从账号管理器获取 JWT token，确保请求带认证
            var accountManager = new TypeSunny.Net.AccountSystemManager();
            var account = accountManager.GetAccount("文来");
            if (account != null && !string.IsNullOrWhiteSpace(account.JwtToken))
            {
                var jwtAuth = new JwtAuthProvider(account.JwtToken);
                apiClient = new ApiClient(apiUrl, jwtAuth);
            }
            else
            {
                apiClient = new ApiClient(apiUrl);
            }
            System.Diagnostics.Debug.WriteLine($"[ArticleFetcher] 自动创建 ApiClient，baseUrl: {apiUrl}");
            return apiClient;
        }

        /// <summary>
        /// 获取难度列表（只返回缓存，不会触发网络请求）
        /// </summary>
        public static List<DifficultyInfo> GetDifficulties(string categoryOverride = null)
        {
            string configuredCategory = Config.GetString("文来分类");
            string configCategory = ((categoryOverride ?? configuredCategory) ?? "").Trim();
            if (TryGetCachedDifficulties(configCategory, out var difficulties))
                return CloneDifficulties(difficulties);

            return CreateDefaultDifficulties();
        }

        /// <summary>
        /// 异步获取难度列表
        /// </summary>
        public static async Task<List<DifficultyInfo>> GetDifficultiesAsync(string categoryOverride = null, bool forceRefresh = false)
        {
            string configuredCategory = Config.GetString("文来分类");
            string configCategory = ((categoryOverride ?? configuredCategory) ?? "").Trim();
            if (!forceRefresh && TryGetCachedDifficulties(configCategory, out var cachedForCategory))
                return CloneDifficulties(cachedForCategory);

            Task<List<DifficultyInfo>> requestTask;
            bool isOwnerRequest = false;
            lock (difficultyCacheLock)
            {
                if (!difficultyRequestsByCategory.TryGetValue(configCategory, out requestTask))
                {
                    requestTask = FetchDifficultiesFromServerAsync(configCategory);
                    difficultyRequestsByCategory[configCategory] = requestTask;
                    isOwnerRequest = true;
                }
            }

            try
            {
                return CloneDifficulties(await requestTask);
            }
            finally
            {
                if (isOwnerRequest)
                {
                    lock (difficultyCacheLock)
                    {
                        if (difficultyRequestsByCategory.TryGetValue(configCategory, out var currentTask) && currentTask == requestTask)
                            difficultyRequestsByCategory.Remove(configCategory);
                    }
                }
            }
        }

        private static async Task<List<DifficultyInfo>> FetchDifficultiesFromServerAsync(string configCategory)
        {
            try
            {
                var client = EnsureClient();
                if (client == null)
                    return CreateDefaultDifficulties();

                var queryParams = new Dictionary<string, string>();
                if (!string.IsNullOrWhiteSpace(configCategory))
                    queryParams["category"] = configCategory;

                // 新 API 路径：/api/segments/stats
                var response = await client.GetAsync("/api/segments/stats", queryParams);

                if (!response.IsSuccess || response.RawData == null)
                    return CreateDefaultDifficulties();

                var difficulties = new List<DifficultyInfo>();
                var statsData = UnwrapStatsData(response.RawData);

                // 难度标签 → 等级ID 映射（服务端 DifficultyConstant）
                var labelToLevel = new Dictionary<string, int>
                {
                    ["淼"] = 1, ["水"] = 2, ["易"] = 3,
                    ["普"] = 4, ["难"] = 5, ["虐"] = 6,
                    ["一级"] = 1, ["二级"] = 2, ["三级"] = 3,
                    ["四级"] = 4, ["五级"] = 5, ["六级"] = 6
                };
                var levelToLabel = new Dictionary<int, string>
                {
                    [1] = "淼", [2] = "水", [3] = "易",
                    [4] = "普", [5] = "难", [6] = "虐"
                };

                // 新格式：data = { totalSegments, levelStats: { "淼": 10000, "水": 20000, ... }, totalChars }
                var statsContainer = statsData as JObject;
                var statsObject = statsContainer?["levelStats"] as JObject
                    ?? statsContainer?["difficultyStats"] as JObject
                    ?? statsContainer?["stats"] as JObject
                    ?? statsContainer?["levels"] as JObject;
                if (statsObject != null)
                {
                    foreach (var item in statsObject)
                        AddDifficultyStat(difficulties, item.Key, item.Value, labelToLevel, levelToLabel);
                }

                var statsArray = statsData.Type == JTokenType.Array
                    ? statsData as JArray
                    : statsContainer?["levelStats"] as JArray
                        ?? statsContainer?["difficultyStats"] as JArray
                        ?? statsContainer?["stats"] as JArray
                        ?? statsContainer?["levels"] as JArray;
                if (statsArray != null)
                {
                    foreach (var item in statsArray)
                        AddDifficultyStat(difficulties, null, item, labelToLevel, levelToLabel);
                }

                difficulties = NormalizeDifficulties(difficulties);

                StoreDifficulties(configCategory, difficulties);
                return difficulties;
            }
            catch (Exception)
            {
                return CreateDefaultDifficulties();
            }
        }

        /// <summary>
        /// 清除难度缓存（用于刷新数据）
        /// </summary>
        public static void ClearDifficultyCache()
        {
            lock (difficultyCacheLock)
            {
                difficultyCacheByCategory.Clear();
            }
        }

        /// <summary>
        /// 获取分类列表（只返回缓存）
        /// </summary>
        public static List<CategoryInfo> GetCategories()
        {
            if (cachedCategories != null && DateTime.Now - categoryCacheTime > CACHE_EXPIRATION)
            {
                cachedCategories = null;
                categoryCacheTime = DateTime.MinValue;
            }
            return cachedCategories ?? new List<CategoryInfo>();
        }

        /// <summary>
        /// 异步获取分类列表
        /// </summary>
        public static async Task<List<CategoryInfo>> GetCategoriesAsync()
        {
            if (cachedCategories != null)
                return cachedCategories;

            try
            {
                var client = EnsureClient();
                if (client == null)
                    return new List<CategoryInfo>();

                var response = await client.GetAsync("/api/categories");
                if (!response.IsSuccess || response.RawData == null)
                    return new List<CategoryInfo>();

                var categories = new List<CategoryInfo>();
                if (response.RawData.Type == JTokenType.Array)
                {
                    foreach (var item in response.RawData)
                    {
                        bool isActive = item["isActive"]?.ToObject<bool>() ?? true;
                        if (!isActive) continue;

                        string code = ReadCategoryCode(item);
                        if (string.IsNullOrWhiteSpace(code))
                            continue;

                        categories.Add(new CategoryInfo
                        {
                            Code = code,
                            Name = ReadCategoryName(item, code)
                        });
                    }
                }

                cachedCategories = categories;
                categoryCacheTime = DateTime.Now;
                return categories;
            }
            catch (Exception)
            {
                return new List<CategoryInfo>();
            }
        }

        /// <summary>
        /// 清除分类缓存
        /// </summary>
        public static void ClearCategoryCache()
        {
            cachedCategories = null;
            categoryCacheTime = DateTime.MinValue;
        }

        // ========== 兼容性方法（过渡期保留） ==========

        /// <summary>
        /// 加载 Cookie（兼容旧代码调用，新 API 使用 JWT 无需此操作）
        /// </summary>
        public static void LoadCookiesFromString(string serverUrl, string cookieString)
        {
            // 新架构使用 JWT 认证，Cookie 由 ApiClient 内部管理
            // 此方法保留以兼容 MainWindow.xaml.cs 和 WinConfig.xaml.cs 中的调用
            System.Diagnostics.Debug.WriteLine($"[ArticleFetcher] LoadCookiesFromString 已弃用（JWT模式），忽略Cookie加载");
        }

        /// <summary>
        /// 清除 Cookie（兼容旧代码调用）
        /// </summary>
        public static void ClearCookies(string serverUrl)
        {
            System.Diagnostics.Debug.WriteLine($"[ArticleFetcher] ClearCookies 已弃用（JWT模式）");
        }

        /// <summary>
        /// 从响应数据中解析文章（支持新旧两种字段命名）
        /// </summary>
        private static ArticleData ParseArticleFromData(JToken data)
        {
            if (data == null || data.Type != JTokenType.Object)
            {
                return new ArticleData
                {
                    Title = "数据错误",
                    Content = "API返回的数据字段不是对象",
                    FullContent = "",
                    Mark = ""
                };
            }

            var dataObj = data as JObject;

            // 获取标题（新: bookName, 旧: name）
            string title = SafeString(dataObj["bookName"])
                ?? SafeString(dataObj["name"])
                ?? "未知标题";
            if (string.IsNullOrEmpty(title)) title = "未知标题";

            // 去掉标题中#及后面的内容
            int hashIndex = title.IndexOf('#');
            if (hashIndex >= 0)
                title = title.Substring(0, hashIndex);

            // 获取文章内容
            string content = SafeString(dataObj["content"]);
            if (string.IsNullOrWhiteSpace(content))
            {
                return new ArticleData
                {
                    Title = "数据错误",
                    Content = "API返回的文章内容为空",
                    FullContent = "",
                    Mark = ""
                };
            }

            // 获取段落标记
            string mark = SafeString(dataObj["mark"]);

            // 获取难度信息（新: difficultyLevel/difficultyLabel/difficultyScore, 旧: difficulty/custom_difficulty）
            string difficultyText = "";
            string difficultyName = "";
            int difficultyId = 0;
            int difficultyLevel = 0;
            string difficultyLabel = "";
            double difficultyScore = 0;

            if (dataObj["difficultyLabel"] != null && dataObj["difficultyLabel"].Type != JTokenType.Null)
            {
                // 新格式
                difficultyLevel = SafeInt(dataObj["difficultyLevel"]);
                difficultyLabel = SafeString(dataObj["difficultyLabel"]);
                difficultyScore = SafeDouble(dataObj["difficultyScore"]);
                difficultyText = $"{difficultyLabel}({difficultyScore:F2})";
                difficultyName = difficultyLabel;
                difficultyId = difficultyLevel;
            }
            else
            {
                // 旧格式
                difficultyText = SafeString(dataObj["difficulty"]);
                difficultyId = SafeInt(dataObj["custom_difficulty"]);

                var difficulties = GetDifficulties();
                if (difficultyId > 0 && difficulties.Count > 0)
                {
                    var diffInfo = difficulties.FirstOrDefault(d => d.Id == difficultyId);
                    if (diffInfo != null)
                        difficultyName = diffInfo.Name;
                }
            }

            // 获取书籍ID和段号（新: bookId/sortNum, 旧: book_id/sort_num）
            int bookId = SafeInt(dataObj["bookId"]) != 0
                ? SafeInt(dataObj["bookId"])
                : SafeInt(dataObj["book_id"]);
            int sortNum = SafeInt(dataObj["sortNum"]) != 0
                ? SafeInt(dataObj["sortNum"])
                : SafeInt(dataObj["sort_num"]);

            // 新字段
            int endSortNum = SafeInt(dataObj["endSortNum"]);
            string endChars = SafeString(dataObj["endChars"]);
            string startChars = SafeString(dataObj["startChars"]);
            string category = SafeString(dataObj["category"]);

            // 应用字符过滤规则
            content = Filter.ProcFilter(content);
            string fullContent = content;

            return new ArticleData
            {
                Title = title,
                Content = content,
                FullContent = fullContent,
                Mark = mark,
                Difficulty = difficultyText,
                DifficultyName = difficultyName,
                BookId = bookId,
                SortNum = sortNum,
                DifficultyId = difficultyId,
                EndSortNum = endSortNum,
                EndChars = endChars,
                StartChars = startChars,
                Category = category,
                DifficultyLevel = difficultyLevel,
                DifficultyLabel = difficultyLabel,
                DifficultyScore = difficultyScore
            };
        }

        /// <summary>
        /// 异步获取随机文章
        /// </summary>
        public static async Task<ArticleData> FetchArticleAsync(int difficulty, int maxLength)
        {
            try
            {
                var client = EnsureClient();
                if (client == null)
                {
                    return new ArticleData
                    {
                        Title = "配置错误",
                        Content = "请在设置中配置[文来接口地址]",
                        FullContent = "",
                        Mark = ""
                    };
                }

                // 从配置读取参数
                int configDifficulty = Config.GetInt("文来难度");
                int configLength = Config.GetInt("文来字数");
                if (configDifficulty > 0) difficulty = configDifficulty;
                if (configLength > 0) maxLength = configLength;

                // 构建查询参数（新参数名）
                var queryParams = new Dictionary<string, string>();
                if (difficulty > 0)
                    queryParams["difficultyLevel"] = difficulty.ToString();
                if (maxLength > 0)
                {
                    queryParams["length"] = maxLength.ToString();
                    string lengthMode = Config.GetString("字数模式");
                    bool strictLength = (lengthMode == "精确字数");
                    queryParams["strictLength"] = strictLength.ToString().ToLower();
                }

                // 分类
                string configCategory = Config.GetString("文来分类");
                if (!string.IsNullOrWhiteSpace(configCategory))
                    queryParams["category"] = configCategory;

                System.Diagnostics.Debug.WriteLine($"[文来] 正在请求随机文章，参数: {string.Join(", ", queryParams)}");

                // 新 API 路径：/api/texts/random
                var response = await client.GetAsync("/api/texts/random", queryParams);

                if (!response.IsSuccess)
                {
                    string errorMsg = response.Msg ?? "未知错误";
                    System.Diagnostics.Debug.WriteLine($"[文来] 请求失败: {errorMsg}");

                    if (response.Code == 401)
                    {
                        return new ArticleData
                        {
                            Title = "接口错误",
                            Content = "请先登录文来服务",
                            FullContent = "",
                            Mark = ""
                        };
                    }

                    return new ArticleData
                    {
                        Title = "接口错误",
                        Content = errorMsg,
                        FullContent = "",
                        Mark = ""
                    };
                }

                return ParseArticleFromData(response.RawData);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[文来] ✗ 获取文章失败: {ex.Message}");
                return new ArticleData
                {
                    Title = "获取失败",
                    Content = $"获取文章失败: {ex.Message}",
                    FullContent = "",
                    Mark = ""
                };
            }
        }

        /// <summary>
        /// 异步获取下一段/上一段
        /// </summary>
        /// <param name="bookId">书籍ID</param>
        /// <param name="sortNum">当前段号</param>
        /// <param name="pageType">页面类型：1=下一段(next)，0=上一段(prev)</param>
        /// <param name="category">分类代码（必需）</param>
        /// <param name="endSortNum">上一段结果的最后段序号</param>
        /// <param name="endChars">上一段结果的尾部字符（后翻时使用）</param>
        /// <param name="startChars">上一段结果的开头字符（前翻时使用）</param>
        public static async Task<ArticleData> FetchSegmentAsync(int bookId, int sortNum, int pageType,
            string category, int endSortNum = 0, string endChars = null, string startChars = null)
        {
            try
            {
                var client = EnsureClient();
                if (client == null)
                {
                    return new ArticleData
                    {
                        Title = "配置错误",
                        Content = "请在设置中配置[文来接口地址]",
                        FullContent = "",
                        Mark = ""
                    };
                }

                // 构建查询参数（按服务端 TextController.getAdjacent 要求）
                string direction = pageType == 1 ? "next" : "prev";
                var queryParams = new Dictionary<string, string>
                {
                    ["bookId"] = bookId.ToString(),
                    ["sortNum"] = sortNum.ToString(),
                    ["direction"] = direction,
                    ["category"] = string.IsNullOrEmpty(category) ? "wangwen" : category
                };
                if (endSortNum > 0)
                    queryParams["endSortNum"] = endSortNum.ToString();
                if (!string.IsNullOrWhiteSpace(endChars))
                    queryParams["endChars"] = endChars;
                if (!string.IsNullOrWhiteSpace(startChars))
                    queryParams["startChars"] = startChars;

                // 读取字数和模式配置
                int configLength = Config.GetInt("文来字数");
                if (configLength > 0)
                {
                    queryParams["length"] = configLength.ToString();
                    string lengthMode = Config.GetString("字数模式");
                    bool strictLength = (lengthMode == "精确字数");
                    queryParams["strictLength"] = strictLength.ToString().ToLower();
                }

                System.Diagnostics.Debug.WriteLine($"[文来] 正在请求段落接口，参数: {string.Join(", ", queryParams)}");

                // 新 API 路径：/api/texts/adjacent
                var response = await client.GetAsync("/api/texts/adjacent", queryParams);

                if (!response.IsSuccess)
                {
                    string errorMsg = response.Msg ?? "未知错误";
                    return new ArticleData
                    {
                        Title = "获取失败",
                        Content = errorMsg,
                        FullContent = "",
                        Mark = ""
                    };
                }

                return ParseArticleFromData(response.RawData);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[文来] ✗ 获取段落失败: {ex.Message}");
                return new ArticleData
                {
                    Title = "获取失败",
                    Content = $"获取段落失败: {ex.Message}",
                    FullContent = "",
                    Mark = ""
                };
            }
        }

        /// <summary>
        /// 获取下一段/上一段（同步版本）
        /// </summary>
        public static ArticleData FetchSegment(int bookId, int sortNum, int pageType,
            string category, int endSortNum = 0, string endChars = null, string startChars = null)
        {
            return FetchSegmentAsync(bookId, sortNum, pageType, category, endSortNum, endChars, startChars).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 获取随机文章（同步版本）
        /// </summary>
        public static ArticleData FetchArticle(int difficulty, int maxLength)
        {
            return FetchArticleAsync(difficulty, maxLength).GetAwaiter().GetResult();
        }

    }

    /// <summary>
    /// 文章数据类
    /// </summary>
    public class ArticleData
    {
        public string Title { get; set; }
        public string Content { get; set; }
        public string FullContent { get; set; }
        public string Mark { get; set; }
        public string Difficulty { get; set; }
        public string DifficultyName { get; set; }
        public int BookId { get; set; }
        public int SortNum { get; set; }
        public int DifficultyId { get; set; }
        // 新字段
        public int EndSortNum { get; set; }
        public string EndChars { get; set; }
        public string StartChars { get; set; }
        public string Category { get; set; }
        public int DifficultyLevel { get; set; }
        public string DifficultyLabel { get; set; }
        public double DifficultyScore { get; set; }
    }
}
