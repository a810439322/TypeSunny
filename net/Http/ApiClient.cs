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

        /// <summary>
        /// 发送原始 HttpRequestMessage（用于需要自定义请求的场景）
        /// </summary>
        public async Task<HttpResponseMessage> SendRawAsync(HttpRequestMessage request)
        {
            authProvider?.ApplyAuth(request);
            return await httpClient.SendAsync(request);
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
                return new ApiResponse
                {
                    Code = 500,
                    Msg = "服务器返回内容无法解析为JSON"
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
            try
            {
                // 应用认证
                authProvider?.ApplyAuth(request);

                var response = await httpClient.SendAsync(request);
                string body = await response.Content.ReadAsStringAsync();

                System.Diagnostics.Debug.WriteLine($"[ApiClient] {request.Method} {request.RequestUri} → {(int)response.StatusCode}");

                return ParseResponse(body, (int)response.StatusCode);
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
