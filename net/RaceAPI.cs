using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TypeSunny.Net
{
    /// <summary>
    /// 赛文API客户端
    /// 提供注册、登录、获取赛文、提交成绩等功能
    /// </summary>
    public class RaceAPI
    {
        private readonly string serverUrl;
        private readonly HttpClient httpClient;
        private readonly CookieContainer cookieContainer;
        private RaceCryptoClient cryptoClient;
        private string clientKeyXml;  // 客户端密钥对（用于持久化）

        /// <summary>
        /// 密钥不匹配时需要重新登录的回调（返回新的Cookie和ClientKeyXml）
        /// </summary>
        public Func<Task<(string cookies, string clientKeyXml)>> OnKeyMismatchCallback { get; set; }

        /// <summary>
        /// 是否正在重试（防止递归）
        /// </summary>
        private bool isRetrying = false;

        /// <summary>
        /// 初始化赛文API客户端
        /// </summary>
        /// <param name="serverUrl">服务器地址，例如 http://localhost:8000</param>
        /// <param name="clientKeyXml">客户端RSA密钥对（XML格式，可选）</param>
        public RaceAPI(string serverUrl, string clientKeyXml = null)
        {
            // 启用所有TLS版本（包括 TLS 1.3）
            System.Net.ServicePointManager.SecurityProtocol = (System.Net.SecurityProtocolType)0x3000 | // TLS 1.3
                                                               System.Net.SecurityProtocolType.Tls12 |
                                                               System.Net.SecurityProtocolType.Tls11 |
                                                               System.Net.SecurityProtocolType.Tls;

            this.serverUrl = serverUrl.TrimEnd('/');
            this.clientKeyXml = clientKeyXml;

            // 创建支持Cookie的HttpClient
            this.cookieContainer = new CookieContainer();
            var handler = new HttpClientHandler
            {
                CookieContainer = this.cookieContainer,
                UseCookies = true,
                // 关键：直接在 Handler 上设置 SSL/TLS 协议
                SslProtocols = System.Security.Authentication.SslProtocols.Tls12 |
                               System.Security.Authentication.SslProtocols.Tls11 |
                               System.Security.Authentication.SslProtocols.Tls |
                               (System.Security.Authentication.SslProtocols)0x3000, // TLS 1.3
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
            };
            this.httpClient = new HttpClient(handler);
            this.httpClient.Timeout = TimeSpan.FromSeconds(30);

            // 添加浏览器User-Agent
            this.httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            this.httpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");
            this.httpClient.DefaultRequestHeaders.Add("Accept-Language", "zh-CN,zh;q=0.9,en;q=0.8");
        }

        /// <summary>
        /// 获取客户端密钥对（用于持久化保存）
        /// </summary>
        public string GetClientKeyXml()
        {
            return cryptoClient?.GetClientKeyXml() ?? clientKeyXml ?? "";
        }

        /// <summary>
        /// 获取Cookie容器（用于持久化保存）
        /// </summary>
        public CookieContainer GetCookieContainer()
        {
            return cookieContainer;
        }

        /// <summary>
        /// 从Cookie字符串加载Cookie（格式：name=value; name2=value2）
        /// </summary>
        public void LoadCookiesFromString(string cookieString)
        {
            if (string.IsNullOrWhiteSpace(cookieString))
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
        /// 获取Cookie字符串（格式：name=value; name2=value2）
        /// </summary>
        public string GetCookiesAsString()
        {
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
        /// 解密认证数据，当密钥不匹配时自动触发重新登录
        /// </summary>
        private async Task<JObject> DecryptAuthenticatedWithRetry(string encryptedData)
        {
            try
            {
                return cryptoClient.DecryptAuthenticated(encryptedData);
            }
            catch (Exception ex)
            {
                // 检查是否是 OAEP 填充解码错误（密钥不匹配）
                if ((ex.Message.Contains("OAEP") || ex.Message.Contains("填充")) && !isRetrying)
                {
                    System.Diagnostics.Debug.WriteLine($"[赛文] 检测到密钥不匹配，尝试自动重新登录: {ex.Message}");

                    if (OnKeyMismatchCallback != null)
                    {
                        isRetrying = true;
                        try
                        {
                            // 触发重新登录回调
                            var (newCookies, newKeyXml) = await OnKeyMismatchCallback();

                            // 重新初始化加密客户端
                            if (!string.IsNullOrWhiteSpace(newKeyXml))
                            {
                                clientKeyXml = newKeyXml;
                                // 重新获取服务器公钥并初始化
                                string publicKey = await GetPublicKeyAsync();
                                cryptoClient = new RaceCryptoClient(publicKey, clientKeyXml);
                            }

                            // 重新加载Cookie
                            if (!string.IsNullOrWhiteSpace(newCookies))
                            {
                                LoadCookiesFromString(newCookies);
                            }

                            System.Diagnostics.Debug.WriteLine($"[赛文] 重新登录成功，重试解密");

                            // 重试解密
                            return cryptoClient.DecryptAuthenticated(encryptedData);
                        }
                        finally
                        {
                            isRetrying = false;
                        }
                    }
                }

                // 如果无法自动恢复，抛出原异常
                throw;
            }
        }

        /// <summary>
        /// 初始化加密客户端（获取服务器公钥）
        /// </summary>
        public async Task<bool> InitializeAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[赛文] 开始初始化加密客户端，服务器地址: {serverUrl}");

                // 获取服务器公钥
                string publicKey = await GetPublicKeyAsync();
                if (string.IsNullOrEmpty(publicKey))
                {
                    System.Diagnostics.Debug.WriteLine($"[赛文] ✗ 获取服务器公钥失败：公钥为空");
                    System.Windows.MessageBox.Show($"获取服务器公钥失败：公钥为空\n服务器地址: {serverUrl}", "赛文初始化失败", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    return false;
                }

                System.Diagnostics.Debug.WriteLine($"[赛文] ✓ 成功获取服务器公钥");

                // 初始化加密客户端（传入已保存的客户端密钥对）
                cryptoClient = new RaceCryptoClient(publicKey, clientKeyXml);

                // 更新 clientKeyXml（可能是新生成的）
                clientKeyXml = cryptoClient.GetClientKeyXml();

                System.Diagnostics.Debug.WriteLine($"[赛文] ✓ 加密客户端初始化成功");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[赛文] ✗ 初始化赛文API失败: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[赛文] 详细错误: {ex}");
                System.Windows.MessageBox.Show($"初始化赛文API失败\n\n错误信息: {ex.Message}\n\n服务器地址: {serverUrl}\n\n完整错误:\n{ex}", "赛文初始化失败", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// 获取服务器RSA公钥
        /// </summary>
        private async Task<string> GetPublicKeyAsync()
        {
            try
            {
                string url = $"{serverUrl}/api/race/public_key";
                System.Diagnostics.Debug.WriteLine($"[赛文] 正在请求公钥接口: {url}");

                HttpResponseMessage response = await httpClient.GetAsync(url);
                System.Diagnostics.Debug.WriteLine($"[赛文] HTTP状态码: {(int)response.StatusCode} {response.StatusCode}");

                // 先读取响应体（无论状态码是什么）
                string jsonResponse = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"[赛文] 服务器响应: {jsonResponse}");

                // 尝试解析JSON
                JObject result = null;
                try
                {
                    result = JObject.Parse(jsonResponse);
                }
                catch
                {
                    // 无法解析JSON
                }

                // 如果HTTP状态码不成功，尝试提取错误消息
                if (!response.IsSuccessStatusCode)
                {
                    string errorMsg = result?["msg"]?.ToString();
                    if (string.IsNullOrEmpty(errorMsg))
                    {
                        errorMsg = $"HTTP {(int)response.StatusCode} {response.StatusCode}";
                    }
                    throw new Exception($"获取公钥失败: {errorMsg}");
                }

                // 检查JSON中的error字段
                if (result != null)
                {
                    int errorCode = result["error"]?.ToObject<int>() ?? 0;
                    if (errorCode != 0)
                    {
                        string errorMsg = result["msg"]?.ToString() ?? "未知错误";
                        throw new Exception($"获取公钥失败: {errorMsg}");
                    }
                }

                return result?["public_key"]?.ToString();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[赛文] ✗ 获取公钥时发生异常: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"[赛文] 异常详情: {ex.Message}");
                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[赛文] 内部异常: {ex.InnerException.Message}");
                }
                throw new Exception($"获取公钥失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 用户注册
        /// </summary>
        /// <param name="username">用户名</param>
        /// <param name="password">密码</param>
        /// <returns>注册结果</returns>
        public async Task<RaceApiResult> RegisterAsync(string username, string password)
        {
            try
            {
                if (cryptoClient == null)
                {
                    bool initialized = await InitializeAsync();
                    if (!initialized)
                    {
                        return new RaceApiResult { Success = false, Message = "初始化加密客户端失败" };
                    }
                }

                // 准备数据（包含客户端公钥）
                string clientPublicKeyPem = cryptoClient.GetClientPublicKeyPem();
                System.Diagnostics.Debug.WriteLine("=== 客户端公钥（将上传到服务器）===");
                System.Diagnostics.Debug.WriteLine(clientPublicKeyPem);
                System.Diagnostics.Debug.WriteLine("=== 客户端公钥结束 ===");

                var registerData = new
                {
                    username = username,
                    password = password,
                    client_public_key = clientPublicKeyPem  // 上传客户端公钥
                };

                // 加密数据
                string encryptedData = cryptoClient.Encrypt(registerData);

                // 发送请求
                string url = $"{serverUrl}/api/race/register";
                var requestData = new { encrypted_data = encryptedData };
                string jsonRequest = JsonConvert.SerializeObject(requestData);

                HttpResponseMessage response = await httpClient.PostAsync(url,
                    new StringContent(jsonRequest, Encoding.UTF8, "application/json"));

                // 先读取响应体
                string jsonResponse = await response.Content.ReadAsStringAsync();

                // 尝试解析JSON
                JObject result = null;
                try
                {
                    result = JObject.Parse(jsonResponse);
                }
                catch
                {
                    // 无法解析JSON
                }

                // 如果HTTP状态码不成功，尝试提取错误消息
                if (!response.IsSuccessStatusCode)
                {
                    string errorMsg = result?["msg"]?.ToString();
                    if (string.IsNullOrEmpty(errorMsg))
                    {
                        errorMsg = $"HTTP {(int)response.StatusCode} {response.StatusCode}";
                    }

                    return new RaceApiResult
                    {
                        Success = false,
                        Message = $"注册失败: {errorMsg}"
                    };
                }

                if (result == null)
                {
                    return new RaceApiResult
                    {
                        Success = false,
                        Message = "注册失败: 服务器返回内容无法解析"
                    };
                }

                // 检查是否是加密数据
                JObject responseData;
                if (result["encrypted_data"] != null && result["encrypted_data"].Type == JTokenType.String)
                {
                    // 解密数据（使用客户端私钥，因为服务器用客户端公钥加密）
                    string encryptedResponse = result["encrypted_data"].ToString();
                    responseData = await DecryptAuthenticatedWithRetry(encryptedResponse);
                }
                else
                {
                    // 直接使用明文数据
                    responseData = result;
                }

                // 检查服务器返回的 success 字段
                bool serverSuccess = responseData["success"]?.ToObject<bool>() ?? true;
                string serverMessage = responseData["msg"]?.ToString() ?? "";

                if (!serverSuccess)
                {
                    return new RaceApiResult
                    {
                        Success = false,
                        Message = string.IsNullOrEmpty(serverMessage) ? "注册失败" : serverMessage,
                        Data = responseData
                    };
                }

                return new RaceApiResult
                {
                    Success = true,
                    Message = string.IsNullOrEmpty(serverMessage) ? "注册成功" : serverMessage,
                    Data = responseData
                };
            }
            catch (Exception ex)
            {
                return new RaceApiResult
                {
                    Success = false,
                    Message = $"注册失败: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 用户登录
        /// </summary>
        /// <param name="username">用户名</param>
        /// <param name="password">密码</param>
        /// <returns>登录结果</returns>
        public async Task<RaceApiResult> LoginAsync(string username, string password)
        {
            try
            {
                if (cryptoClient == null)
                {
                    bool initialized = await InitializeAsync();
                    if (!initialized)
                    {
                        return new RaceApiResult { Success = false, Message = "初始化加密客户端失败" };
                    }
                }

                // 准备数据（包含客户端公钥，可选，用于更新）
                var loginData = new
                {
                    username = username,
                    password = password,
                    client_public_key = cryptoClient.GetClientPublicKeyPem()  // 可选，用于更新公钥
                };

                // 加密数据
                string encryptedData = cryptoClient.Encrypt(loginData);

                // 发送请求
                string url = $"{serverUrl}/api/race/login";
                var requestData = new { encrypted_data = encryptedData };
                string jsonRequest = JsonConvert.SerializeObject(requestData);

                HttpResponseMessage response = await httpClient.PostAsync(url,
                    new StringContent(jsonRequest, Encoding.UTF8, "application/json"));

                // 先读取响应体
                string jsonResponse = await response.Content.ReadAsStringAsync();

                // 尝试解析JSON
                JObject result = null;
                try
                {
                    result = JObject.Parse(jsonResponse);
                }
                catch
                {
                    // 无法解析JSON
                }

                // 如果HTTP状态码不成功，尝试提取错误消息
                if (!response.IsSuccessStatusCode)
                {
                    string errorMsg = result?["msg"]?.ToString();
                    if (string.IsNullOrEmpty(errorMsg))
                    {
                        errorMsg = $"HTTP {(int)response.StatusCode} {response.StatusCode}";
                    }

                    return new RaceApiResult
                    {
                        Success = false,
                        Message = $"登录失败: {errorMsg}"
                    };
                }

                if (result == null)
                {
                    return new RaceApiResult
                    {
                        Success = false,
                        Message = "登录失败: 服务器返回内容无法解析"
                    };
                }

                // 检查是否是加密数据
                JObject responseData;
                if (result["encrypted_data"] != null && result["encrypted_data"].Type == JTokenType.String)
                {
                    // 解密数据（使用客户端私钥，因为服务器用客户端公钥加密）
                    string encryptedResponse = result["encrypted_data"].ToString();
                    responseData = await DecryptAuthenticatedWithRetry(encryptedResponse);
                }
                else
                {
                    // 直接使用明文数据
                    responseData = result;
                }

                // 检查服务器返回的 success 字段
                bool serverSuccess = responseData["success"]?.ToObject<bool>() ?? true;
                string serverMessage = responseData["msg"]?.ToString() ?? "";

                if (!serverSuccess)
                {
                    return new RaceApiResult
                    {
                        Success = false,
                        Message = string.IsNullOrEmpty(serverMessage) ? "登录失败" : serverMessage,
                        Data = responseData
                    };
                }

                return new RaceApiResult
                {
                    Success = true,
                    Message = string.IsNullOrEmpty(serverMessage) ? "登录成功" : serverMessage,
                    Data = responseData
                };
            }
            catch (Exception ex)
            {
                return new RaceApiResult
                {
                    Success = false,
                    Message = $"登录失败: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 获取赛文列表
        /// </summary>
        /// <returns>赛文列表</returns>
        public async Task<RaceApiResult> GetRaceListAsync()
        {
            try
            {
                if (cryptoClient == null)
                {
                    bool initialized = await InitializeAsync();
                    if (!initialized)
                    {
                        return new RaceApiResult { Success = false, Message = "初始化加密客户端失败" };
                    }
                }

                string url = $"{serverUrl}/api/race/list";
                System.Diagnostics.Debug.WriteLine($"正在请求: {url}");

                HttpResponseMessage response = await httpClient.GetAsync(url);

                // 先读取响应体
                string jsonResponse = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"API原始返回: {jsonResponse.Substring(0, Math.Min(200, jsonResponse.Length))}...");

                // 尝试解析JSON
                JObject result = null;
                try
                {
                    result = JObject.Parse(jsonResponse);
                }
                catch
                {
                    // 无法解析JSON
                }

                // 如果HTTP状态码不成功，尝试提取错误消息
                if (!response.IsSuccessStatusCode)
                {
                    string errorMsg = result?["msg"]?.ToString();
                    if (string.IsNullOrEmpty(errorMsg))
                    {
                        errorMsg = $"HTTP {(int)response.StatusCode} {response.StatusCode}";
                    }

                    return new RaceApiResult
                    {
                        Success = false,
                        Message = $"获取赛文列表失败: {errorMsg}"
                    };
                }

                // 检查JSON中的error字段
                if (result != null)
                {
                    int errorCode = result["error"]?.ToObject<int>() ?? 0;
                    if (errorCode != 0)
                    {
                        string errorMsg = result["msg"]?.ToString() ?? "未知错误";
                        return new RaceApiResult
                        {
                            Success = false,
                            Message = $"获取赛文列表失败: {errorMsg}"
                        };
                    }
                }

                // 公开接口：直接返回明文JSON，不加密
                return new RaceApiResult
                {
                    Success = true,
                    Message = "获取赛文列表成功",
                    Data = result
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取赛文列表异常: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"异常类型: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"堆栈: {ex.StackTrace}");

                return new RaceApiResult
                {
                    Success = false,
                    Message = $"获取赛文列表失败: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 获取赛文信息
        /// </summary>
        /// <param name="raceId">赛文ID</param>
        /// <returns>赛文信息</returns>
        public async Task<RaceApiResult> GetRaceInfoAsync(int raceId)
        {
            try
            {
                if (cryptoClient == null)
                {
                    bool initialized = await InitializeAsync();
                    if (!initialized)
                    {
                        return new RaceApiResult { Success = false, Message = "初始化加密客户端失败" };
                    }
                }

                string url = $"{serverUrl}/api/race/info?race_id={raceId}";
                HttpResponseMessage response = await httpClient.GetAsync(url);

                // 先读取响应体
                string jsonResponse = await response.Content.ReadAsStringAsync();

                // 尝试解析JSON
                JObject result = null;
                try
                {
                    result = JObject.Parse(jsonResponse);
                }
                catch
                {
                    // 无法解析JSON
                }

                // 如果HTTP状态码不成功，尝试提取错误消息
                if (!response.IsSuccessStatusCode)
                {
                    string errorMsg = result?["msg"]?.ToString();
                    if (string.IsNullOrEmpty(errorMsg))
                    {
                        errorMsg = $"HTTP {(int)response.StatusCode} {response.StatusCode}";
                    }

                    return new RaceApiResult
                    {
                        Success = false,
                        Message = $"获取赛文信息失败: {errorMsg}"
                    };
                }

                // 检查JSON中的error字段
                if (result != null)
                {
                    int errorCode = result["error"]?.ToObject<int>() ?? 0;
                    if (errorCode != 0)
                    {
                        string errorMsg = result["msg"]?.ToString() ?? "未知错误";
                        return new RaceApiResult
                        {
                            Success = false,
                            Message = $"获取赛文信息失败: {errorMsg}"
                        };
                    }
                }

                // 公开接口：直接返回明文JSON，不加密
                return new RaceApiResult
                {
                    Success = true,
                    Message = "获取赛文信息成功",
                    Data = result
                };
            }
            catch (Exception ex)
            {
                return new RaceApiResult
                {
                    Success = false,
                    Message = $"获取赛文信息失败: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 获取每日赛文
        /// </summary>
        /// <param name="raceId">赛文ID</param>
        /// <param name="userId">用户ID</param>
        /// <returns>赛文内容</returns>
        public async Task<RaceApiResult> GetDailyArticleAsync(int raceId, int userId)
        {
            try
            {
                if (cryptoClient == null)
                {
                    bool initialized = await InitializeAsync();
                    if (!initialized)
                    {
                        return new RaceApiResult { Success = false, Message = "初始化加密客户端失败" };
                    }
                }

                string url = $"{serverUrl}/api/race/daily_article?race_id={raceId}&user_id={userId}";
                HttpResponseMessage response = await httpClient.GetAsync(url);

                // 先读取响应体
                string jsonResponse = await response.Content.ReadAsStringAsync();

                // 尝试解析JSON
                JObject result = null;
                try
                {
                    result = JObject.Parse(jsonResponse);
                }
                catch
                {
                    // 无法解析JSON
                }

                // 如果HTTP状态码不成功，尝试提取错误消息
                if (!response.IsSuccessStatusCode)
                {
                    string errorMsg = result?["msg"]?.ToString();
                    if (string.IsNullOrEmpty(errorMsg))
                    {
                        errorMsg = $"HTTP {(int)response.StatusCode} {response.StatusCode}";
                    }

                    return new RaceApiResult
                    {
                        Success = false,
                        Message = $"获取赛文失败: {errorMsg}"
                    };
                }

                if (result == null)
                {
                    return new RaceApiResult
                    {
                        Success = false,
                        Message = "获取赛文失败: 服务器返回内容无法解析"
                    };
                }

                // 检查是否是加密数据
                JObject responseData;
                if (result["encrypted_data"] != null && result["encrypted_data"].Type == JTokenType.String)
                {
                    // 解密数据（使用客户端私钥，因为这是需要认证的接口）
                    string encryptedData = result["encrypted_data"].ToString();
                    responseData = await DecryptAuthenticatedWithRetry(encryptedData);
                }
                else
                {
                    // 直接使用明文数据
                    responseData = result;
                }

                // 检查服务器返回的 success 字段
                bool serverSuccess = responseData["success"]?.ToObject<bool>() ?? true;
                string serverMessage = responseData["msg"]?.ToString() ?? "";

                if (!serverSuccess)
                {
                    return new RaceApiResult
                    {
                        Success = false,
                        Message = string.IsNullOrEmpty(serverMessage) ? "获取赛文失败" : serverMessage,
                        Data = responseData
                    };
                }

                return new RaceApiResult
                {
                    Success = true,
                    Message = string.IsNullOrEmpty(serverMessage) ? "获取赛文成功" : serverMessage,
                    Data = responseData
                };
            }
            catch (Exception ex)
            {
                return new RaceApiResult
                {
                    Success = false,
                    Message = $"获取赛文失败: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 提交成绩
        /// </summary>
        /// <param name="scoreData">成绩数据</param>
        /// <returns>提交结果</returns>
        public async Task<RaceApiResult> SubmitScoreAsync(RaceScoreData scoreData)
        {
            try
            {
                if (cryptoClient == null)
                {
                    bool initialized = await InitializeAsync();
                    if (!initialized)
                    {
                        return new RaceApiResult { Success = false, Message = "初始化加密客户端失败" };
                    }
                }

                // 生成签名 - 使用JObject确保字段顺序和格式
                // 签名所有字段（除了signature本身），浮点数保留5位小数避免精度问题
                var signData = new JObject();
                signData["race_id"] = scoreData.RaceId;
                signData["user_id"] = scoreData.UserId;
                signData["username"] = scoreData.Username;
                signData["article_id"] = scoreData.ArticleId;
                signData["date"] = scoreData.Date;
                signData["speed"] = Math.Round(scoreData.Speed, 5);
                signData["time_cost"] = scoreData.TimeCost;
                signData["char_count"] = scoreData.CharCount;
                signData["keystroke"] = Math.Round(scoreData.Keystroke, 5);
                signData["code_length"] = Math.Round(scoreData.CodeLength, 5);
                signData["backspace_count"] = scoreData.BackspaceCount;
                signData["key_count"] = scoreData.KeyCount;
                signData["key_accuracy"] = Math.Round(scoreData.KeyAccuracy, 5);
                signData["word_rate"] = Math.Round(scoreData.WordRate, 5);
                signData["input_method"] = scoreData.InputMethod;

                string signature = RaceCryptoClient.GenerateSignature(signData);

                // 准备提交数据（所有字段都是必填）
                // 浮点数使用相同的5位小数值，确保与签名一致
                var submitData = new
                {
                    race_id = scoreData.RaceId,
                    user_id = scoreData.UserId,
                    username = scoreData.Username,
                    article_id = scoreData.ArticleId,
                    date = scoreData.Date,
                    speed = Math.Round(scoreData.Speed, 5),
                    time_cost = scoreData.TimeCost,
                    char_count = scoreData.CharCount,
                    signature = signature,
                    keystroke = Math.Round(scoreData.Keystroke, 5),
                    code_length = Math.Round(scoreData.CodeLength, 5),
                    backspace_count = scoreData.BackspaceCount,
                    key_count = scoreData.KeyCount,
                    key_accuracy = Math.Round(scoreData.KeyAccuracy, 5),
                    word_rate = Math.Round(scoreData.WordRate, 5),
                    input_method = scoreData.InputMethod
                };

                // 调试：打印提交的数据
                System.Diagnostics.Debug.WriteLine("=== 提交成绩数据 ===");
                System.Diagnostics.Debug.WriteLine($"race_id: {submitData.race_id}");
                System.Diagnostics.Debug.WriteLine($"user_id: {submitData.user_id}");
                System.Diagnostics.Debug.WriteLine($"username: {submitData.username}");
                System.Diagnostics.Debug.WriteLine($"article_id: {submitData.article_id}");
                System.Diagnostics.Debug.WriteLine($"date: {submitData.date}");
                System.Diagnostics.Debug.WriteLine($"speed: {submitData.speed} (原始值: {scoreData.Speed})");
                System.Diagnostics.Debug.WriteLine($"time_cost: {submitData.time_cost}");
                System.Diagnostics.Debug.WriteLine($"char_count: {submitData.char_count}");
                System.Diagnostics.Debug.WriteLine($"signature: {submitData.signature}");
                System.Diagnostics.Debug.WriteLine($"keystroke: {submitData.keystroke}");
                System.Diagnostics.Debug.WriteLine($"code_length: {submitData.code_length}");
                System.Diagnostics.Debug.WriteLine($"backspace_count: {submitData.backspace_count}");
                System.Diagnostics.Debug.WriteLine($"key_count: {submitData.key_count}");
                System.Diagnostics.Debug.WriteLine($"key_accuracy: {submitData.key_accuracy}");
                System.Diagnostics.Debug.WriteLine($"word_rate: {submitData.word_rate}");
                System.Diagnostics.Debug.WriteLine($"input_method: {submitData.input_method}");
                System.Diagnostics.Debug.WriteLine("===================");

                // 加密数据
                string encryptedData = cryptoClient.Encrypt(submitData);

                // 发送请求
                string url = $"{serverUrl}/api/race/submit";
                var requestData = new { encrypted_data = encryptedData };
                string jsonRequest = JsonConvert.SerializeObject(requestData);

                HttpResponseMessage response = await httpClient.PostAsync(url,
                    new StringContent(jsonRequest, Encoding.UTF8, "application/json"));

                // 先读取响应体
                string jsonResponse = await response.Content.ReadAsStringAsync();

                // 尝试解析JSON
                JObject result = null;
                try
                {
                    result = JObject.Parse(jsonResponse);
                }
                catch
                {
                    // 无法解析JSON
                }

                // 如果HTTP状态码不成功，尝试提取错误消息
                if (!response.IsSuccessStatusCode)
                {
                    string errorMsg = result?["msg"]?.ToString();
                    if (string.IsNullOrEmpty(errorMsg))
                    {
                        errorMsg = $"HTTP {(int)response.StatusCode} {response.StatusCode}";
                    }

                    return new RaceApiResult
                    {
                        Success = false,
                        Message = $"提交成绩失败: {errorMsg}"
                    };
                }

                if (result == null)
                {
                    return new RaceApiResult
                    {
                        Success = false,
                        Message = "提交成绩失败: 服务器返回内容无法解析"
                    };
                }

                // 检查是否是加密数据
                JObject responseData;
                if (result["encrypted_data"] != null && result["encrypted_data"].Type == JTokenType.String)
                {
                    // 解密数据（使用客户端私钥，因为这是需要认证的接口）
                    string encryptedResponse = result["encrypted_data"].ToString();
                    responseData = await DecryptAuthenticatedWithRetry(encryptedResponse);
                }
                else
                {
                    // 直接使用明文数据
                    responseData = result;
                }

                // 检查服务器返回的 success 字段
                bool serverSuccess = responseData["success"]?.ToObject<bool>() ?? true;
                string serverMessage = responseData["msg"]?.ToString() ?? "";

                if (!serverSuccess)
                {
                    return new RaceApiResult
                    {
                        Success = false,
                        Message = string.IsNullOrEmpty(serverMessage) ? "提交成绩失败" : serverMessage,
                        Data = responseData
                    };
                }

                return new RaceApiResult
                {
                    Success = true,
                    Message = string.IsNullOrEmpty(serverMessage) ? "提交成绩成功" : serverMessage,
                    Data = responseData
                };
            }
            catch (Exception ex)
            {
                return new RaceApiResult
                {
                    Success = false,
                    Message = $"提交成绩失败: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 获取历史数据
        /// </summary>
        /// <param name="raceId">赛文ID</param>
        /// <param name="username">用户名</param>
        /// <param name="limit">返回记录数</param>
        /// <returns>历史数据</returns>
        public async Task<RaceApiResult> GetHistoryAsync(int raceId, string username, int limit = 30)
        {
            try
            {
                if (cryptoClient == null)
                {
                    bool initialized = await InitializeAsync();
                    if (!initialized)
                    {
                        return new RaceApiResult { Success = false, Message = "初始化加密客户端失败" };
                    }
                }

                string url = $"{serverUrl}/api/race/history?race_id={raceId}&username={username}&limit={limit}";
                HttpResponseMessage response = await httpClient.GetAsync(url);

                // 先读取响应体
                string jsonResponse = await response.Content.ReadAsStringAsync();

                // 尝试解析JSON
                JObject result = null;
                try
                {
                    result = JObject.Parse(jsonResponse);
                }
                catch
                {
                    // 无法解析JSON
                }

                // 如果HTTP状态码不成功，尝试提取错误消息
                if (!response.IsSuccessStatusCode)
                {
                    string errorMsg = result?["msg"]?.ToString();
                    if (string.IsNullOrEmpty(errorMsg))
                    {
                        errorMsg = $"HTTP {(int)response.StatusCode} {response.StatusCode}";
                    }

                    return new RaceApiResult
                    {
                        Success = false,
                        Message = $"获取历史数据失败: {errorMsg}"
                    };
                }

                // 检查JSON中的error字段
                if (result != null)
                {
                    int errorCode = result["error"]?.ToObject<int>() ?? 0;
                    if (errorCode != 0)
                    {
                        string errorMsg = result["msg"]?.ToString() ?? "未知错误";
                        return new RaceApiResult
                        {
                            Success = false,
                            Message = $"获取历史数据失败: {errorMsg}"
                        };
                    }
                }

                // 公开接口：直接返回明文JSON，不加密
                return new RaceApiResult
                {
                    Success = true,
                    Message = "获取历史数据成功",
                    Data = result
                };
            }
            catch (Exception ex)
            {
                return new RaceApiResult
                {
                    Success = false,
                    Message = $"获取历史数据失败: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 获取排行榜
        /// </summary>
        /// <param name="raceId">赛文ID</param>
        /// <param name="date">日期（可选）</param>
        /// <param name="limit">返回记录数</param>
        /// <returns>排行榜数据</returns>
        public async Task<RaceApiResult> GetLeaderboardAsync(int raceId, string date = null, int limit = 100)
        {
            try
            {
                if (cryptoClient == null)
                {
                    bool initialized = await InitializeAsync();
                    if (!initialized)
                    {
                        return new RaceApiResult { Success = false, Message = "初始化加密客户端失败" };
                    }
                }

                string url = $"{serverUrl}/api/race/leaderboard?race_id={raceId}&limit={limit}";
                if (!string.IsNullOrEmpty(date))
                {
                    url += $"&date_str={date}";
                }

                HttpResponseMessage response = await httpClient.GetAsync(url);

                // 先读取响应体
                string jsonResponse = await response.Content.ReadAsStringAsync();

                // 尝试解析JSON
                JObject result = null;
                try
                {
                    result = JObject.Parse(jsonResponse);
                }
                catch
                {
                    // 无法解析JSON
                }

                // 如果HTTP状态码不成功，尝试提取错误消息
                if (!response.IsSuccessStatusCode)
                {
                    string errorMsg = result?["msg"]?.ToString();
                    if (string.IsNullOrEmpty(errorMsg))
                    {
                        errorMsg = $"HTTP {(int)response.StatusCode} {response.StatusCode}";
                    }

                    return new RaceApiResult
                    {
                        Success = false,
                        Message = $"获取排行榜失败: {errorMsg}"
                    };
                }

                // 检查JSON中的error字段
                if (result != null)
                {
                    int errorCode = result["error"]?.ToObject<int>() ?? 0;
                    if (errorCode != 0)
                    {
                        string errorMsg = result["msg"]?.ToString() ?? "未知错误";
                        return new RaceApiResult
                        {
                            Success = false,
                            Message = $"获取排行榜失败: {errorMsg}"
                        };
                    }
                }

                // 公开接口：直接返回明文JSON，不加密
                return new RaceApiResult
                {
                    Success = true,
                    Message = "获取排行榜成功",
                    Data = result
                };
            }
            catch (Exception ex)
            {
                return new RaceApiResult
                {
                    Success = false,
                    Message = $"获取排行榜失败: {ex.Message}"
                };
            }
        }
    }

    /// <summary>
    /// API调用结果
    /// </summary>
    public class RaceApiResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public JObject Data { get; set; }
    }

    /// <summary>
    /// 赛文成绩数据
    /// </summary>
    public class RaceScoreData
    {
        public int RaceId { get; set; }
        public int UserId { get; set; }
        public string Username { get; set; }
        public int ArticleId { get; set; }
        public string Date { get; set; }
        public double Speed { get; set; }
        public int TimeCost { get; set; }
        public int CharCount { get; set; }
        public double Keystroke { get; set; }
        public double CodeLength { get; set; }
        public int BackspaceCount { get; set; }
        public int KeyCount { get; set; }
        public double KeyAccuracy { get; set; }
        public double WordRate { get; set; }
        public string InputMethod { get; set; }
    }
}
