using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

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
    /// 文章获取器，通过HTTP GET请求获取文章
    /// </summary>
    public class ArticleFetcher
    {
        private static HttpClient httpClient;
        private static CookieContainer cookieContainer;
        private static List<DifficultyInfo> cachedDifficulties = null;

        static ArticleFetcher()
        {
            // 1. 启用所有TLS版本（包括 TLS 1.3）
            System.Net.ServicePointManager.SecurityProtocol = (System.Net.SecurityProtocolType)0x3000 | // TLS 1.3 (0x3000)
                                                               System.Net.SecurityProtocolType.Tls12 |
                                                               System.Net.SecurityProtocolType.Tls11 |
                                                               System.Net.SecurityProtocolType.Tls;

            // 2. 创建 HttpClientHandler 并配置
            var handler = new HttpClientHandler
            {
                CookieContainer = new CookieContainer(),
                UseCookies = true,
                // 忽略证书错误（避免证书验证问题）
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true,
                // 允许自动重定向
                AllowAutoRedirect = true,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };

            cookieContainer = handler.CookieContainer;

            // 3. 初始化 HttpClient
            httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(30)
            };

            // 4. 添加请求头（模拟浏览器）
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");
            httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
            httpClient.DefaultRequestHeaders.Add("Accept-Language", "zh-CN,zh;q=0.9");

            System.Diagnostics.Debug.WriteLine($"[ArticleFetcher] TLS协议已设置，支持TLS 1.0/1.1/1.2/1.3");
        }

        /// <summary>
        /// 从Cookie字符串加载Cookie
        /// </summary>
        public static void LoadCookiesFromString(string serverUrl, string cookieString)
        {
            if (string.IsNullOrWhiteSpace(cookieString) || string.IsNullOrWhiteSpace(serverUrl))
                return;

            try
            {
                Uri uri = new Uri(serverUrl);
                foreach (var cookiePair in cookieString.Split(';'))
                {
                    var parts = cookiePair.Trim().Split('=');
                    if (parts.Length == 2)
                    {
                        cookieContainer.Add(uri, new Cookie(parts[0].Trim(), parts[1].Trim()));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载Cookie失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取Cookie字符串
        /// </summary>
        public static string GetCookiesAsString(string serverUrl)
        {
            if (string.IsNullOrWhiteSpace(serverUrl))
                return "";

            try
            {
                Uri uri = new Uri(serverUrl);
                var cookies = cookieContainer.GetCookies(uri);
                var cookieList = new System.Collections.Generic.List<string>();
                foreach (Cookie cookie in cookies)
                {
                    cookieList.Add($"{cookie.Name}={cookie.Value}");
                }
                return string.Join("; ", cookieList);
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// 清除指定服务器的所有Cookie
        /// </summary>
        public static void ClearCookies(string serverUrl)
        {
            if (string.IsNullOrWhiteSpace(serverUrl))
                return;

            try
            {
                Uri uri = new Uri(serverUrl);
                var cookies = cookieContainer.GetCookies(uri);

                // 将所有cookie标记为过期
                foreach (Cookie cookie in cookies)
                {
                    cookie.Expired = true;
                }

                System.Diagnostics.Debug.WriteLine($"✓ 已清除 {serverUrl} 的所有Cookie");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"清除Cookie失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取难度列表（只返回缓存，不会触发网络请求）
        /// </summary>
        /// <returns>难度列表</returns>
        public static List<DifficultyInfo> GetDifficulties()
        {
            // 只返回缓存，避免同步阻塞
            if (cachedDifficulties != null)
            {
                return cachedDifficulties;
            }

            // 没有缓存，返回空列表
            return new List<DifficultyInfo>();
        }

        /// <summary>
        /// 异步获取难度列表
        /// </summary>
        /// <returns>难度列表</returns>
        public static async Task<List<DifficultyInfo>> GetDifficultiesAsync()
        {
            // 如果有缓存，直接返回
            if (cachedDifficulties != null)
            {
                return cachedDifficulties;
            }

            try
            {
                string apiUrl = Config.GetString("文来接口地址");

                if (string.IsNullOrWhiteSpace(apiUrl))
                {
                    return new List<DifficultyInfo>();
                }

                // 移除可能的尾部斜杠
                apiUrl = apiUrl.TrimEnd('/');

                // 构建难度API URL
                string difficultyUrl = apiUrl + "/api/stats_by_difficulty";

                // 发送GET请求（使用真正的异步）
                var response = await httpClient.GetAsync(difficultyUrl);

                // 读取响应体内容
                string responseBody = await response.Content.ReadAsStringAsync();

                // 尝试解析JSON响应
                JObject result;
                try
                {
                    result = JObject.Parse(responseBody);
                }
                catch
                {
                    // 无法解析JSON，返回空列表
                    return new List<DifficultyInfo>();
                }

                // 检查错误码
                int errorCode = result["error"]?.ToObject<int>() ?? -1;
                if (errorCode != 0)
                {
                    return new List<DifficultyInfo>();
                }

                // 解析msg对象
                var msgObj = result["msg"] as JObject;
                if (msgObj == null)
                {
                    return new List<DifficultyInfo>();
                }

                // 解析难度列表
                var difficulties = new List<DifficultyInfo>();
                foreach (var item in msgObj)
                {
                    int id = int.Parse(item.Key);
                    var value = item.Value as JObject;
                    if (value != null)
                    {
                        difficulties.Add(new DifficultyInfo
                        {
                            Id = id,
                            Name = value["name"]?.ToString() ?? "",
                            Count = value["count"]?.ToObject<int>() ?? 0
                        });
                    }
                }

                // 按ID排序
                difficulties.Sort((a, b) => a.Id.CompareTo(b.Id));

                // 缓存结果
                cachedDifficulties = difficulties;
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
        }

        /// <summary>
        /// 异步获取随机文章
        /// </summary>
        /// <param name="difficulty">难度ID（从GetDifficulties获取可用难度）</param>
        /// <param name="maxLength">最大字数</param>
        /// <returns>文章对象，包含标题和内容</returns>
        public static async Task<ArticleData> FetchArticleAsync(int difficulty, int maxLength)
        {
            try
            {
                string apiUrl = Config.GetString("文来接口地址");

                if (string.IsNullOrWhiteSpace(apiUrl))
                {
                    return new ArticleData
                    {
                        Title = "配置错误",
                        Content = "请在设置中配置[文来接口地址]",
                        FullContent = "",
                        Mark = ""
                    };
                }

                // 移除可能的尾部斜杠
                apiUrl = apiUrl.TrimEnd('/');

                // 移除可能已经存在的 /api/get_text 路径（兼容旧配置）
                if (apiUrl.EndsWith("/api/get_text"))
                {
                    apiUrl = apiUrl.Substring(0, apiUrl.Length - "/api/get_text".Length);
                }

                // 构建文章获取API URL
                string baseUrl = apiUrl + "/api/get_text";

                // 从配置读取参数
                int configDifficulty = Config.GetInt("文来难度");
                int configLength = Config.GetInt("文来字数");

                // 使用配置中的值，如果配置为0则使用传入的默认值
                if (configDifficulty > 0)
                    difficulty = configDifficulty;
                if (configLength > 0)
                    maxLength = configLength;

                // 构建URL参数
                var queryParams = new List<string>();
                if (difficulty > 0)
                {
                    queryParams.Add($"difficulty={difficulty}");
                }
                if (maxLength > 0)
                {
                    queryParams.Add($"length={maxLength}");
                    // 只有设置了字数时才添加 strict_length 参数
                    string lengthMode = Config.GetString("字数模式");
                    bool strictLength = (lengthMode == "精确字数");
                    queryParams.Add($"strict_length={strictLength.ToString().ToLower()}");
                }

                // 构建完整URL
                string requestUrl = baseUrl;
                if (queryParams.Count > 0)
                {
                    string separator = requestUrl.Contains("?") ? "&" : "?";
                    requestUrl = requestUrl + separator + string.Join("&", queryParams);
                }

                System.Diagnostics.Debug.WriteLine($"[文来] 正在请求文章接口: {requestUrl}");

                // 改回使用 HttpClient（更好的TLS支持）
                HttpResponseMessage response;
                string responseBody;
                JObject result = null;

                try
                {
                    response = await httpClient.GetAsync(requestUrl);
                    System.Diagnostics.Debug.WriteLine($"[文来] HTTP状态码: {(int)response.StatusCode} {response.StatusCode}");

                    // 先读取响应体（无论状态码是什么）
                    responseBody = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[文来] 服务器响应长度: {responseBody.Length} 字符");

                    // 尝试解析JSON响应（统一处理所有响应）
                    try
                    {
                        result = JObject.Parse(responseBody);
                    }
                    catch
                    {
                        // 无法解析JSON
                    }

                    // 如果响应不成功，尝试从JSON中提取错误信息
                    if (!response.IsSuccessStatusCode)
                    {
                        string errorMsg = null;

                        // 尝试从JSON中提取msg字段
                        if (result != null)
                        {
                            errorMsg = result["msg"]?.ToString();
                        }

                        // 如果没有msg字段，使用默认错误消息
                        if (string.IsNullOrEmpty(errorMsg))
                        {
                            errorMsg = $"HTTP {(int)response.StatusCode} {response.StatusCode}";
                        }

                        System.Diagnostics.Debug.WriteLine($"[文来] 请求失败: {errorMsg}");

                        // 特殊处理401未授权（触发自动登录）
                        if (response.StatusCode == HttpStatusCode.Unauthorized)
                        {
                            return new ArticleData
                            {
                                Title = "接口错误",
                                Content = "请先登录文来服务",
                                FullContent = "",
                                Mark = ""
                            };
                        }

                        // 其他错误统一返回
                        return new ArticleData
                        {
                            Title = "接口错误",
                            Content = errorMsg,
                            FullContent = "",
                            Mark = ""
                        };
                    }

                    // 响应成功，但可能JSON中包含error字段
                    if (result == null)
                    {
                        return new ArticleData
                        {
                            Title = "接口错误",
                            Content = "服务器返回内容无法解析为JSON",
                            FullContent = "",
                            Mark = ""
                        };
                    }

                    // 检查JSON中的error字段
                    int errorCode = result["error"]?.ToObject<int>() ?? 0;
                    if (errorCode != 0)
                    {
                        // 提取错误消息
                        string errorMsg = result["msg"]?.ToString() ?? "未知错误";
                        return new ArticleData
                        {
                            Title = "接口错误",
                            Content = errorMsg,
                            FullContent = "",
                            Mark = ""
                        };
                    }
                }
                catch (HttpRequestException httpEx)
                {
                    string errorMsg = $"发送请求失败\n\n请求地址: {requestUrl}\n\n错误: {httpEx.Message}";
                    System.Diagnostics.Debug.WriteLine($"[文来] ✗ {errorMsg}");
                    System.Diagnostics.Debug.WriteLine($"[文来] 完整异常: {httpEx}");

                    return new ArticleData
                    {
                        Title = "获取失败",
                        Content = errorMsg,
                        FullContent = "",
                        Mark = ""
                    };
                }
                catch (TaskCanceledException)
                {
                    return new ArticleData
                    {
                        Title = "获取失败",
                        Content = "请求超时，请检查网络连接",
                        FullContent = "",
                        Mark = ""
                    };
                }

                // result已在上面的try块中解析和验证

                // 解析msg对象
                var msgObj = result["msg"] as JObject;
                if (msgObj == null)
                {
                    return new ArticleData
                    {
                        Title = "数据错误",
                        Content = "API返回的msg字段不是对象",
                        FullContent = "",
                        Mark = ""
                    };
                }

                // 获取标题
                string title = msgObj["name"]?.ToString() ?? "未知标题";

                // 去掉标题中#及后面的内容
                int hashIndex = title.IndexOf('#');
                if (hashIndex >= 0)
                {
                    title = title.Substring(0, hashIndex);
                }

                // 获取文章内容
                string content = msgObj["content"]?.ToString() ?? "";

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

                // 获取段落标记（mark字段，格式如 "1-34112"）
                string mark = msgObj["mark"]?.ToString() ?? "";

                // 获取难度（difficulty字段，格式如 "一般(2.05)"）
                string difficultyText = msgObj["difficulty"]?.ToString() ?? "";

                // 获取书籍ID、段号、自定义难度ID（用于获取下一段/上一段和查找难度名称）
                int bookId = msgObj["book_id"]?.ToObject<int>() ?? 0;
                int sortNum = msgObj["sort_num"]?.ToObject<int>() ?? 0;
                int difficultyId = msgObj["custom_difficulty"]?.ToObject<int>() ?? 0;

                // 应用字符过滤规则（全角转半角、字符映射、白名单过滤）
                content = Filter.ProcFilter(content);

                // 保存完整内容（文来接口已由服务端根据length参数处理，不再本地截断）
                string fullContent = content;

                return new ArticleData
                {
                    Title = title,
                    Content = content,
                    FullContent = fullContent,
                    Mark = mark,
                    Difficulty = difficultyText,
                    BookId = bookId,
                    SortNum = sortNum,
                    DifficultyId = difficultyId
                };
            }
            catch (TaskCanceledException)
            {
                return new ArticleData
                {
                    Title = "获取失败",
                    Content = "请求超时，请检查网络连接或API服务器状态",
                    FullContent = "",
                    Mark = ""
                };
            }
            catch (HttpRequestException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[文来] ✗ 网络请求失败: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[文来] 完整异常: {ex}");
                return new ArticleData
                {
                    Title = "获取失败",
                    Content = $"网络请求失败: {ex.Message}",
                    FullContent = "",
                    Mark = ""
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[文来] ✗ 获取文章失败: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[文来] 完整异常: {ex}");
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
        /// <param name="pageType">页面类型：1=下一段，0=上一段</param>
        /// <param name="difficulty">难度ID</param>
        /// <returns>文章对象</returns>
        public static async Task<ArticleData> FetchSegmentAsync(int bookId, int sortNum, int pageType, int difficulty)
        {
            try
            {
                string apiUrl = Config.GetString("文来接口地址");

                if (string.IsNullOrWhiteSpace(apiUrl))
                {
                    return new ArticleData
                    {
                        Title = "配置错误",
                        Content = "请在设置中配置[文来接口地址]",
                        FullContent = "",
                        Mark = ""
                    };
                }

                // 移除可能的尾部斜杠
                apiUrl = apiUrl.TrimEnd('/');

                // 构建文章获取API URL
                string baseUrl = apiUrl + "/api/get_text";

                // 构建URL参数
                var queryParams = new List<string>
                {
                    $"book_id={bookId}",
                    $"sort_num={sortNum}",
                    $"page_type={pageType}"
                };
                if (difficulty > 0)
                {
                    queryParams.Add($"difficulty={difficulty}");
                }

                // 构建完整URL
                string requestUrl = baseUrl + "?" + string.Join("&", queryParams);

                System.Diagnostics.Debug.WriteLine($"[文来] 正在请求段落接口: {requestUrl}");

                HttpResponseMessage response = await httpClient.GetAsync(requestUrl);
                string responseBody = await response.Content.ReadAsStringAsync();

                JObject result = null;
                try
                {
                    result = JObject.Parse(responseBody);
                }
                catch
                {
                    return new ArticleData
                    {
                        Title = "接口错误",
                        Content = "服务器返回内容无法解析为JSON",
                        FullContent = "",
                        Mark = ""
                    };
                }

                // 检查错误
                int errorCode = result["error"]?.ToObject<int>() ?? -1;
                if (errorCode != 0)
                {
                    string errorMsg = result["msg"]?.ToString() ?? "未知错误";
                    return new ArticleData
                    {
                        Title = "获取失败",
                        Content = errorMsg,
                        FullContent = "",
                        Mark = ""
                    };
                }

                // 解析msg对象
                var msgObj = result["msg"] as JObject;
                if (msgObj == null)
                {
                    return new ArticleData
                    {
                        Title = "数据错误",
                        Content = "API返回的msg字段不是对象",
                        FullContent = "",
                        Mark = ""
                    };
                }

                // 获取标题
                string title = msgObj["name"]?.ToString() ?? "未知标题";
                int hashIndex = title.IndexOf('#');
                if (hashIndex >= 0)
                {
                    title = title.Substring(0, hashIndex);
                }

                // 获取文章内容
                string content = msgObj["content"]?.ToString() ?? "";

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
                string mark = msgObj["mark"]?.ToString() ?? "";

                // 获取难度
                string difficultyText = msgObj["difficulty"]?.ToString() ?? "";

                // 获取书籍ID、段号、自定义难度ID（用于继续翻页）
                int responseBookId = msgObj["book_id"]?.ToObject<int>() ?? 0;
                int responseSortNum = msgObj["sort_num"]?.ToObject<int>() ?? 0;
                int difficultyId = msgObj["custom_difficulty"]?.ToObject<int>() ?? 0;

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
                    BookId = responseBookId,
                    SortNum = responseSortNum,
                    DifficultyId = difficultyId
                };
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
        public static ArticleData FetchSegment(int bookId, int sortNum, int pageType, int difficulty)
        {
            return FetchSegmentAsync(bookId, sortNum, pageType, difficulty).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 获取随机文章（同步版本，内部调用异步版本）
        /// </summary>
        /// <param name="difficulty">难度ID（从GetDifficulties获取可用难度）</param>
        /// <param name="maxLength">最大字数</param>
        /// <returns>文章对象，包含标题和内容</returns>
        public static ArticleData FetchArticle(int difficulty, int maxLength)
        {
            // 同步版本直接调用异步版本（阻塞等待）
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
        public string Mark { get; set; }  // 段落标记，格式如 "1-34112" 表示第1段/共34112段
        public string Difficulty { get; set; }  // 难度描述，格式如 "一般(2.05)"
        public int BookId { get; set; }  // 书籍ID，用于获取下一段/上一段
        public int SortNum { get; set; }  // 当前段号，用于获取下一段/上一段
        public int DifficultyId { get; set; }  // 难度ID，来自custom_difficulty字段，对应/api/stats_by_difficulty中的id
    }
}
