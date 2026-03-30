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
        private static List<DifficultyInfo> cachedDifficulties = null;
        private static List<CategoryInfo> cachedCategories = null;
        private static DateTime cacheTime = DateTime.MinValue;
        private static DateTime categoryCacheTime = DateTime.MinValue;
        private static readonly TimeSpan CACHE_EXPIRATION = TimeSpan.FromMinutes(5);

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
        public static List<DifficultyInfo> GetDifficulties()
        {
            if (cachedDifficulties != null && DateTime.Now - cacheTime > CACHE_EXPIRATION)
            {
                System.Diagnostics.Debug.WriteLine($"[难度] 缓存已过期（{(DateTime.Now - cacheTime).TotalMinutes:F1}分钟），清除缓存");
                cachedDifficulties = null;
                cacheTime = DateTime.MinValue;
            }

            if (cachedDifficulties != null)
                return cachedDifficulties;

            return new List<DifficultyInfo>();
        }

        /// <summary>
        /// 异步获取难度列表
        /// </summary>
        public static async Task<List<DifficultyInfo>> GetDifficultiesAsync()
        {
            if (cachedDifficulties != null)
                return cachedDifficulties;

            try
            {
                var client = EnsureClient();
                if (client == null)
                    return new List<DifficultyInfo>();

                // 新 API 路径：/api/segments/stats
                var response = await client.GetAsync("/api/segments/stats");

                if (!response.IsSuccess || response.RawData == null)
                    return new List<DifficultyInfo>();

                var difficulties = new List<DifficultyInfo>();

                // 难度标签 → 等级ID 映射（服务端 DifficultyConstant）
                var labelToLevel = new Dictionary<string, int>
                {
                    ["淼"] = 1, ["水"] = 2, ["易"] = 3,
                    ["普"] = 4, ["难"] = 5, ["虐"] = 6
                };

                // 新格式：data = { totalSegments, levelStats: { "淼": 10000, "水": 20000, ... }, totalChars }
                var levelStats = response.RawData["levelStats"] as JObject;
                if (levelStats != null)
                {
                    foreach (var item in levelStats)
                    {
                        string label = item.Key;
                        long count = item.Value?.Type == JTokenType.Integer ? (long)item.Value : 0;
                        int level = labelToLevel.ContainsKey(label) ? labelToLevel[label] : 0;
                        if (level > 0)
                        {
                            difficulties.Add(new DifficultyInfo
                            {
                                Id = level,
                                Name = label,
                                Count = (int)count
                            });
                        }
                    }
                }
                // 旧格式兼容：data 是数组 [{ id, name, count }, ...]
                else if (response.RawData.Type == JTokenType.Array)
                {
                    foreach (var item in response.RawData)
                    {
                        difficulties.Add(new DifficultyInfo
                        {
                            Id = SafeInt(item["id"]),
                            Name = SafeString(item["name"]),
                            Count = SafeInt(item["count"])
                        });
                    }
                }

                difficulties.Sort((a, b) => a.Id.CompareTo(b.Id));

                cachedDifficulties = difficulties;
                cacheTime = DateTime.Now;
                System.Diagnostics.Debug.WriteLine($"[难度] 已更新难度缓存，共{difficulties.Count}个难度");
                return difficulties;
            }
            catch (Exception)
            {
                return new List<DifficultyInfo>();
            }
        }

        /// <summary>
        /// 清除难度缓存（用于刷新数据）
        /// </summary>
        public static void ClearDifficultyCache()
        {
            cachedDifficulties = null;
            cacheTime = DateTime.MinValue;
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
                        categories.Add(new CategoryInfo
                        {
                            Code = item["code"]?.ToString() ?? "",
                            Name = item["name"]?.ToString() ?? ""
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

                if (difficultyId > 0 && cachedDifficulties != null)
                {
                    var diffInfo = cachedDifficulties.FirstOrDefault(d => d.Id == difficultyId);
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

        /// <summary>
        /// 从接口计算文本难度
        /// </summary>
        /// <param name="content">待计算难度的文本内容</param>
        /// <returns>格式化的难度文本，如 "普(1.23)"，失败返回空字符串</returns>
        public static async Task<string> CalcDifficultyFromApiAsync(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return "";

            try
            {
                var client = EnsureClient();
                if (client == null)
                    return "";

                var response = await client.PostAsync("/api/texts/calcDifficulty", new { text = content });

                if (response.IsSuccess && response.RawData != null)
                {
                    var data = response.RawData;
                    double score = data["difficultyScore"]?.ToObject<double>() ?? 0;
                    string label = data["difficultyLabel"]?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(label))
                        return $"{label}({score:F2})";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[难度] 接口调用失败: {ex.Message}");
            }
            return "";
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
