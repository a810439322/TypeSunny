using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TypeSunny.Net.Http
{
    /// <summary>
    /// 统一 HTTP 客户端
    /// 全项目唯一的 TLS/UA/超时 配置点
    /// </summary>
    public class ApiClient
    {
        private readonly string baseUrl;
        private readonly HttpClient httpClient;
        private readonly IAuthProvider authProvider;
        private readonly CookieContainer cookieContainer;

        /// <summary>
        /// 初始化 API 客户端
        /// </summary>
        /// <param name="baseUrl">服务器基础地址，例如 http://localhost:8080</param>
        /// <param name="authProvider">认证提供者（JWT 或 Cookie）</param>
        /// <param name="cookieContainer">Cookie 容器（可选，CookieAuthProvider 时自动使用）</param>
        public ApiClient(string baseUrl, IAuthProvider authProvider = null, CookieContainer cookieContainer = null)
        {
            this.baseUrl = baseUrl.TrimEnd('/');
            this.authProvider = authProvider;

            // 如果 authProvider 是 CookieAuthProvider 且未传入 cookieContainer，自动使用其内部容器
            if (cookieContainer == null && authProvider is CookieAuthProvider cookieAuth)
            {
                this.cookieContainer = cookieAuth.GetCookieContainer();
            }
            else
            {
                this.cookieContainer = cookieContainer ?? new CookieContainer();
            }

            // ========== 全项目统一的 HTTP 配置 ==========

            // 启用所有 TLS 版本（包括 TLS 1.3）
            ServicePointManager.SecurityProtocol =
                (SecurityProtocolType)0x3000 |  // TLS 1.3
                SecurityProtocolType.Tls12 |
                SecurityProtocolType.Tls11 |
                SecurityProtocolType.Tls;

            var handler = new HttpClientHandler
            {
                CookieContainer = this.cookieContainer,
                UseCookies = true,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                AllowAutoRedirect = true
            };

            this.httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(30)
            };

            // 防止 .NET Framework 连接池复用已被服务端关闭的 TCP 连接（"基础连接已经关闭"）
            // ConnectionLeaseTimeout 让连接池定期丢弃旧连接，避免用死连接发请求
            try
            {
                var sp = ServicePointManager.FindServicePoint(new Uri(this.baseUrl));
                sp.ConnectionLeaseTimeout = 60 * 1000;  // 60秒
            }
            catch { }

            // 浏览器 User-Agent
            this.httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            this.httpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");
            this.httpClient.DefaultRequestHeaders.Add("Accept-Language", "zh-CN,zh;q=0.9,en;q=0.8");
        }

        /// <summary>
        /// 获取基础 URL
        /// </summary>
        public string BaseUrl => baseUrl;

        /// <summary>
        /// 获取认证提供者
        /// </summary>
        public IAuthProvider AuthProvider => authProvider;

        /// <summary>
        /// 获取 Cookie 容器
        /// </summary>
        public CookieContainer GetCookieContainer()
        {
            return cookieContainer;
        }

        /// <summary>
        /// GET 请求（泛型，data 反序列化为 T）
        /// </summary>
        public async Task<ApiResponse<T>> GetAsync<T>(string path, Dictionary<string, string> queryParams = null)
        {
            string url = BuildUrl(path, queryParams);
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            return await SendAsync<T>(request);
        }

        /// <summary>
        /// GET 请求（非泛型，返回原始 JToken data）
        /// </summary>
        public async Task<ApiResponse> GetAsync(string path, Dictionary<string, string> queryParams = null)
        {
            string url = BuildUrl(path, queryParams);
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            return await SendAsync(request);
        }

        /// <summary>
        /// POST 请求（泛型）
        /// </summary>
        public async Task<ApiResponse<T>> PostAsync<T>(string path, object body)
        {
            string url = BuildUrl(path);
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            if (body != null)
            {
                string json = body is string s ? s : JsonConvert.SerializeObject(body);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }
            return await SendAsync<T>(request);
        }

        /// <summary>
        /// POST 请求（非泛型）
        /// </summary>
        public async Task<ApiResponse> PostAsync(string path, object body)
        {
            string url = BuildUrl(path);
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            if (body != null)
            {
                string json = body is string s ? s : JsonConvert.SerializeObject(body);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }
            return await SendAsync(request);
        }

        /// <summary>
        /// PUT 请求（泛型）
        /// </summary>
        public async Task<ApiResponse<T>> PutAsync<T>(string path, object body)
        {
            string url = BuildUrl(path);
            var request = new HttpRequestMessage(HttpMethod.Put, url);
            if (body != null)
            {
                string json = body is string s ? s : JsonConvert.SerializeObject(body);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }
            return await SendAsync<T>(request);
        }

        /// <summary>
        /// DELETE 请求（非泛型）
        /// </summary>
        public async Task<ApiResponse> DeleteAsync(string path, Dictionary<string, string> queryParams = null)
        {
            string url = BuildUrl(path, queryParams);
            var request = new HttpRequestMessage(HttpMethod.Delete, url);
            return await SendAsync(request);
        }

        // =============== 内部方法 ===============

        /// <summary>
        /// 构建完整 URL（baseUrl + path + queryParams）
        /// </summary>
        private string BuildUrl(string path, Dictionary<string, string> queryParams = null)
        {
            string url = baseUrl;
            if (!string.IsNullOrWhiteSpace(path))
            {
                if (!path.StartsWith("/"))
                    path = "/" + path;
                url += path;
            }

            if (queryParams != null && queryParams.Count > 0)
            {
                var parts = new List<string>();
                foreach (var kvp in queryParams)
                {
                    if (kvp.Value != null)
                    {
                        parts.Add($"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}");
                    }
                }
                if (parts.Count > 0)
                {
                    url += (url.Contains("?") ? "&" : "?") + string.Join("&", parts);
                }
            }

            return url;
        }

        /// <summary>
        /// 解析响应 JSON，自动检测新旧格式
        /// </summary>
        private ApiResponse ParseResponse(string jsonString, int httpStatusCode)
        {
            if (string.IsNullOrWhiteSpace(jsonString))
            {
                return new ApiResponse
                {
                    Code = httpStatusCode >= 200 && httpStatusCode < 300 ? 200 : httpStatusCode,
                    Msg = httpStatusCode >= 200 && httpStatusCode < 300 ? "" : $"HTTP {httpStatusCode}"
                };
            }

            JObject json;
            try
            {
                json = JObject.Parse(jsonString);
            }
            catch
            {
                // JSON 解析失败，根据 HTTP 状态码给出友好提示
                string friendlyMsg;
                switch (httpStatusCode)
                {
                    case 502: friendlyMsg = "服务器网关错误，后端服务可能未启动"; break;
                    case 503: friendlyMsg = "服务器暂时不可用，请稍后再试"; break;
                    case 504: friendlyMsg = "服务器网关超时，后端服务响应过慢"; break;
                    default:
                        friendlyMsg = httpStatusCode >= 500
                            ? $"服务器错误 (HTTP {httpStatusCode})"
                            : httpStatusCode >= 400
                                ? $"请求错误 (HTTP {httpStatusCode})"
                                : $"服务器返回了非JSON内容 (HTTP {httpStatusCode})";
                        break;
                }
                return new ApiResponse
                {
                    Code = httpStatusCode,
                    Msg = friendlyMsg
                };
            }

            // 如果 HTTP 状态码不成功，优先使用 JSON 中的错误信息
            if (httpStatusCode < 200 || httpStatusCode >= 300)
            {
                // 尝试从 JSON 提取错误消息
                string errorMsg = json["msg"]?.ToString()
                    ?? json["message"]?.ToString()
                    ?? $"HTTP {httpStatusCode}";

                int code = json["code"]?.Type == JTokenType.Integer
                    ? json["code"].ToObject<int>()
                    : httpStatusCode;

                return new ApiResponse
                {
                    Code = code,
                    Msg = errorMsg,
                    RawJson = json,
                    RawData = json["data"]
                };
            }

            // HTTP 成功，使用统一格式转换
            return ApiResponse.FromLegacy(json);
        }

        /// <summary>
        /// 发送请求、解析响应（非泛型）
        /// </summary>
        private async Task<ApiResponse> SendAsync(HttpRequestMessage request)
        {
            // 保存请求信息用于重试（HttpRequestMessage 发送后会被 dispose，不能直接复用）
            var method = request.Method;
            var uri = request.RequestUri;
            string bodyContent = null;
            if (request.Content != null)
            {
                bodyContent = await request.Content.ReadAsStringAsync();
            }

            for (int attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    HttpRequestMessage req;
                    HttpClient client;

                    if (attempt == 0)
                    {
                        req = request;
                        client = httpClient;
                    }
                    else
                    {
                        // 重试：用全新的 Handler + HttpClient，彻底绕过旧连接池
                        var retryHandler = new HttpClientHandler
                        {
                            CookieContainer = this.cookieContainer,
                            UseCookies = true,
                            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                            AllowAutoRedirect = true
                        };
                        client = new HttpClient(retryHandler) { Timeout = TimeSpan.FromSeconds(30) };

                        req = new HttpRequestMessage(method, uri);
                        if (bodyContent != null)
                            req.Content = new StringContent(bodyContent, Encoding.UTF8, "application/json");
                    }

                    // 应用认证
                    authProvider?.ApplyAuth(req);

                    var response = await client.SendAsync(req);
                    string body = await response.Content.ReadAsStringAsync();

                    System.Diagnostics.Debug.WriteLine($"[ApiClient] {req.Method} {req.RequestUri} → {(int)response.StatusCode}");

                    // 重试用的临时 client 用完即弃
                    if (attempt > 0) client.Dispose();

                    return ParseResponse(body, (int)response.StatusCode);
                }
                catch (HttpRequestException ex) when (
                    attempt == 0 && IsConnectionClosedError(ex))
                {
                    System.Diagnostics.Debug.WriteLine($"[ApiClient] 连接已关闭，正在重试: {ex.InnerException?.Message}");
                    continue;
                }
                catch (TaskCanceledException)
                {
                    return new ApiResponse { Code = 408, Msg = "请求超时，请检查网络连接" };
                }
                catch (HttpRequestException ex)
                {
                    string detail = ex.InnerException != null
                        ? $"{ex.Message}，{ex.InnerException.GetType().Name}： {ex.InnerException.Message}"
                        : ex.Message;
                    return new ApiResponse { Code = 0, Msg = $"网络请求失败: {detail}" };
                }
                catch (Exception ex)
                {
                    string detail = ex.InnerException != null
                        ? $"{ex.Message} → {ex.InnerException.GetType().Name}: {ex.InnerException.Message}"
                        : ex.Message;
                    return new ApiResponse { Code = 0, Msg = $"请求异常: {detail}" };
                }
            }

            return new ApiResponse { Code = 0, Msg = "网络请求失败: 重试后仍无法连接" };
        }

        /// <summary>
        /// 判断是否为"基础连接已关闭"类错误（服务端关闭了 TCP 连接）
        /// </summary>
        private static bool IsConnectionClosedError(HttpRequestException ex)
        {
            var inner = ex.InnerException;
            if (inner == null) return false;
            string msg = inner.Message ?? "";
            // 中文系统："基础连接已经关闭"  英文系统："The underlying connection was closed"
            return msg.Contains("基础连接已经关闭")
                || msg.Contains("基础连接已关闭")
                || msg.Contains("underlying connection was closed")
                || inner is System.IO.IOException;
        }

        /// <summary>
        /// 发送请求、解析响应（泛型，data 反序列化为 T）
        /// </summary>
        private async Task<ApiResponse<T>> SendAsync<T>(HttpRequestMessage request)
        {
            var raw = await SendAsync(request);
            return ApiResponse<T>.From(raw);
        }
    }
}
