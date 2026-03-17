using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TypeSunny.Net.Http;

namespace TypeSunny.Net
{
    /// <summary>
    /// 赛文API客户端
    /// 提供注册、登录、获取赛文、提交成绩等功能
    /// 内部使用 ApiClient 进行 HTTP 通信
    /// </summary>
    public class RaceAPI
    {
        private readonly string serverUrl;
        private readonly ApiClient apiClient;
        private readonly CookieAuthProvider cookieAuth;
        private RaceCryptoClient cryptoClient;
        private string clientKeyXml;

        /// <summary>
        /// 密钥不匹配时需要重新登录的回调
        /// </summary>
        public Func<Task<(string cookies, string clientKeyXml)>> OnKeyMismatchCallback { get; set; }

        private bool isRetrying = false;

        /// <summary>
        /// 初始化赛文API客户端
        /// </summary>
        /// <param name="serverUrl">服务器地址</param>
        /// <param name="clientKeyXml">客户端RSA密钥对（XML格式，可选）</param>
        public RaceAPI(string serverUrl, string clientKeyXml = null)
        {
            this.serverUrl = serverUrl.TrimEnd('/');
            this.clientKeyXml = clientKeyXml;

            // 使用 CookieAuthProvider 管理 Cookie
            this.cookieAuth = new CookieAuthProvider(null, this.serverUrl);

            // 创建 ApiClient（统一的 TLS/UA/超时 配置）
            this.apiClient = new ApiClient(this.serverUrl, this.cookieAuth, this.cookieAuth.GetCookieContainer());
        }

        /// <summary>
        /// 获取内部 ApiClient（供外部使用）
        /// </summary>
        public ApiClient GetApiClient()
        {
            return apiClient;
        }

        /// <summary>
        /// 获取客户端密钥对
        /// </summary>
        public string GetClientKeyXml()
        {
            return cryptoClient?.GetClientKeyXml() ?? clientKeyXml ?? "";
        }

        /// <summary>
        /// 获取Cookie容器
        /// </summary>
        public CookieContainer GetCookieContainer()
        {
            return cookieAuth.GetCookieContainer();
        }

        /// <summary>
        /// 从Cookie字符串加载Cookie
        /// </summary>
        public void LoadCookiesFromString(string cookieString)
        {
            cookieAuth.LoadCookies(cookieString, serverUrl);
        }

        /// <summary>
        /// 获取Cookie字符串
        /// </summary>
        public string GetCookiesAsString()
        {
            return cookieAuth.GetCookiesAsString(serverUrl);
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
                if ((ex.Message.Contains("OAEP") || ex.Message.Contains("填充")) && !isRetrying)
                {
                    System.Diagnostics.Debug.WriteLine($"[赛文] 检测到密钥不匹配，尝试自动重新登录: {ex.Message}");

                    if (OnKeyMismatchCallback != null)
                    {
                        isRetrying = true;
                        try
                        {
                            var (newCookies, newKeyXml) = await OnKeyMismatchCallback();

                            if (!string.IsNullOrWhiteSpace(newKeyXml))
                            {
                                clientKeyXml = newKeyXml;
                                string publicKey = await GetPublicKeyAsync();
                                cryptoClient = new RaceCryptoClient(publicKey, clientKeyXml);
                            }

                            if (!string.IsNullOrWhiteSpace(newCookies))
                            {
                                LoadCookiesFromString(newCookies);
                            }

                            System.Diagnostics.Debug.WriteLine($"[赛文] 重新登录成功，重试解密");
                            return cryptoClient.DecryptAuthenticated(encryptedData);
                        }
                        finally
                        {
                            isRetrying = false;
                        }
                    }
                }

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

                string publicKey = await GetPublicKeyAsync();
                if (string.IsNullOrEmpty(publicKey))
                {
                    System.Diagnostics.Debug.WriteLine($"[赛文] ✗ 获取服务器公钥失败：公钥为空");
                    System.Windows.MessageBox.Show($"获取服务器公钥失败：公钥为空\n服务器地址: {serverUrl}", "赛文初始化失败", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    return false;
                }

                System.Diagnostics.Debug.WriteLine($"[赛文] ✓ 成功获取服务器公钥");

                cryptoClient = new RaceCryptoClient(publicKey, clientKeyXml);
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
                System.Diagnostics.Debug.WriteLine($"[赛文] 正在请求公钥接口");

                var response = await apiClient.GetAsync("/api/race/public_key");

                System.Diagnostics.Debug.WriteLine($"[赛文] 公钥接口响应 code: {response.Code}");

                if (!response.IsSuccess)
                {
                    throw new Exception($"获取公钥失败: {response.Msg}");
                }

                // 新格式：data 中包含 publicKey
                if (response.RawData != null)
                {
                    string key = response.RawData["publicKey"]?.ToString()
                        ?? response.RawData.ToString();
                    if (!string.IsNullOrWhiteSpace(key))
                        return key;
                }

                // 旧格式：顶层有 public_key
                if (response.RawJson != null)
                {
                    return response.RawJson["public_key"]?.ToString();
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[赛文] ✗ 获取公钥时发生异常: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"[赛文] 异常详情: {ex.Message}");
                throw new Exception($"获取公钥失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 用户注册
        /// </summary>
        public async Task<RaceApiResult> RegisterAsync(string username, string password)
        {
            try
            {
                if (cryptoClient == null)
                {
                    bool initialized = await InitializeAsync();
                    if (!initialized)
                        return new RaceApiResult { Success = false, Message = "初始化加密客户端失败" };
                }

                string clientPublicKeyPem = cryptoClient.GetClientPublicKeyPem();

                var registerData = new
                {
                    username = username,
                    password = password,
                    client_public_key = clientPublicKeyPem
                };

                string encryptedData = cryptoClient.Encrypt(registerData);

                var response = await apiClient.PostAsync("/api/race/register", new { encrypted_data = encryptedData });

                if (!response.IsSuccess && string.IsNullOrWhiteSpace(response.EncryptedData))
                {
                    return new RaceApiResult
                    {
                        Success = false,
                        Message = $"注册失败: {response.Msg}"
                    };
                }

                // 处理加密响应
                JObject responseData;
                if (!string.IsNullOrWhiteSpace(response.EncryptedData))
                {
                    responseData = await DecryptAuthenticatedWithRetry(response.EncryptedData);
                }
                else if (response.RawJson != null)
                {
                    responseData = response.RawJson;
                }
                else
                {
                    return new RaceApiResult { Success = false, Message = "注册失败: 服务器返回内容无法解析" };
                }

                bool serverSuccess = responseData["success"]?.ToObject<bool>() ?? true;
                string serverMessage = responseData["msg"]?.ToString() ?? "";

                return new RaceApiResult
                {
                    Success = serverSuccess,
                    Message = string.IsNullOrEmpty(serverMessage) ? (serverSuccess ? "注册成功" : "注册失败") : serverMessage,
                    Data = responseData
                };
            }
            catch (Exception ex)
            {
                return new RaceApiResult { Success = false, Message = $"注册失败: {ex.Message}" };
            }
        }

        /// <summary>
        /// 用户登录
        /// </summary>
        public async Task<RaceApiResult> LoginAsync(string username, string password)
        {
            try
            {
                if (cryptoClient == null)
                {
                    bool initialized = await InitializeAsync();
                    if (!initialized)
                        return new RaceApiResult { Success = false, Message = "初始化加密客户端失败" };
                }

                var loginData = new
                {
                    username = username,
                    password = password,
                    client_public_key = cryptoClient.GetClientPublicKeyPem()
                };

                string encryptedData = cryptoClient.Encrypt(loginData);

                var response = await apiClient.PostAsync("/api/race/login", new { encrypted_data = encryptedData });

                if (!response.IsSuccess && string.IsNullOrWhiteSpace(response.EncryptedData))
                {
                    return new RaceApiResult
                    {
                        Success = false,
                        Message = $"登录失败: {response.Msg}"
                    };
                }

                // 处理加密响应
                JObject responseData;
                if (!string.IsNullOrWhiteSpace(response.EncryptedData))
                {
                    responseData = await DecryptAuthenticatedWithRetry(response.EncryptedData);
                }
                else if (response.RawJson != null)
                {
                    responseData = response.RawJson;
                }
                else
                {
                    return new RaceApiResult { Success = false, Message = "登录失败: 服务器返回内容无法解析" };
                }

                bool serverSuccess = responseData["success"]?.ToObject<bool>() ?? true;
                string serverMessage = responseData["msg"]?.ToString() ?? "";

                return new RaceApiResult
                {
                    Success = serverSuccess,
                    Message = string.IsNullOrEmpty(serverMessage) ? (serverSuccess ? "登录成功" : "登录失败") : serverMessage,
                    Data = responseData
                };
            }
            catch (Exception ex)
            {
                return new RaceApiResult { Success = false, Message = $"登录失败: {ex.Message}" };
            }
        }

        /// <summary>
        /// 获取赛文列表
        /// </summary>
        public async Task<RaceApiResult> GetRaceListAsync()
        {
            try
            {
                if (cryptoClient == null)
                {
                    bool initialized = await InitializeAsync();
                    if (!initialized)
                        return new RaceApiResult { Success = false, Message = "初始化加密客户端失败" };
                }

                // 新 API 路径：/api/race/configs
                var response = await apiClient.GetAsync("/api/race/configs");

                // 如果新路径返回 404，回退到旧路径
                if (response.Code == 404)
                {
                    response = await apiClient.GetAsync("/api/race/list");
                }

                if (!response.IsSuccess)
                {
                    return new RaceApiResult
                    {
                        Success = false,
                        Message = $"获取赛文列表失败: {response.Msg}"
                    };
                }

                // 构造兼容的返回格式
                JObject resultData = response.RawJson ?? new JObject();
                if (response.RawData != null && resultData["data"] == null)
                {
                    resultData["data"] = response.RawData;
                }

                return new RaceApiResult
                {
                    Success = true,
                    Message = "获取赛文列表成功",
                    Data = resultData
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取赛文列表异常: {ex.Message}");
                return new RaceApiResult { Success = false, Message = $"获取赛文列表失败: {ex.Message}" };
            }
        }

        /// <summary>
        /// 获取赛文信息
        /// </summary>
        public async Task<RaceApiResult> GetRaceInfoAsync(int raceId)
        {
            try
            {
                if (cryptoClient == null)
                {
                    bool initialized = await InitializeAsync();
                    if (!initialized)
                        return new RaceApiResult { Success = false, Message = "初始化加密客户端失败" };
                }

                var queryParams = new Dictionary<string, string>
                {
                    ["raceId"] = raceId.ToString()
                };

                var response = await apiClient.GetAsync("/api/race/info", queryParams);

                // 旧格式回退
                if (response.Code == 404)
                {
                    var oldParams = new Dictionary<string, string> { ["race_id"] = raceId.ToString() };
                    response = await apiClient.GetAsync("/api/race/info", oldParams);
                }

                if (!response.IsSuccess)
                {
                    return new RaceApiResult
                    {
                        Success = false,
                        Message = $"获取赛文信息失败: {response.Msg}"
                    };
                }

                return new RaceApiResult
                {
                    Success = true,
                    Message = "获取赛文信息成功",
                    Data = response.RawJson ?? new JObject()
                };
            }
            catch (Exception ex)
            {
                return new RaceApiResult { Success = false, Message = $"获取赛文信息失败: {ex.Message}" };
            }
        }

        /// <summary>
        /// 获取每日赛文
        /// </summary>
        public async Task<RaceApiResult> GetDailyArticleAsync(int raceId, int userId)
        {
            try
            {
                if (cryptoClient == null)
                {
                    bool initialized = await InitializeAsync();
                    if (!initialized)
                        return new RaceApiResult { Success = false, Message = "初始化加密客户端失败" };
                }

                // 新 API：/api/race/init?raceId=
                var queryParams = new Dictionary<string, string>
                {
                    ["raceId"] = raceId.ToString()
                };

                var response = await apiClient.GetAsync("/api/race/init", queryParams);

                // 旧路径回退
                if (response.Code == 404)
                {
                    var oldParams = new Dictionary<string, string>
                    {
                        ["race_id"] = raceId.ToString(),
                        ["user_id"] = userId.ToString()
                    };
                    response = await apiClient.GetAsync("/api/race/daily_article", oldParams);
                }

                if (!response.IsSuccess && string.IsNullOrWhiteSpace(response.EncryptedData))
                {
                    return new RaceApiResult
                    {
                        Success = false,
                        Message = $"获取赛文失败: {response.Msg}"
                    };
                }

                // 处理加密响应
                JObject responseData;
                if (!string.IsNullOrWhiteSpace(response.EncryptedData))
                {
                    responseData = await DecryptAuthenticatedWithRetry(response.EncryptedData);
                }
                else if (response.RawJson != null)
                {
                    responseData = response.RawJson;
                    // 如果 data 在 RawData 中，合并到 responseData
                    if (response.RawData != null && responseData["article"] == null && response.RawData["article"] != null)
                    {
                        responseData = response.RawData as JObject ?? responseData;
                    }
                }
                else
                {
                    return new RaceApiResult { Success = false, Message = "获取赛文失败: 服务器返回内容无法解析" };
                }

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
                return new RaceApiResult { Success = false, Message = $"获取赛文失败: {ex.Message}" };
            }
        }

        /// <summary>
        /// 提交成绩
        /// </summary>
        public async Task<RaceApiResult> SubmitScoreAsync(RaceScoreData scoreData)
        {
            try
            {
                if (cryptoClient == null)
                {
                    bool initialized = await InitializeAsync();
                    if (!initialized)
                        return new RaceApiResult { Success = false, Message = "初始化加密客户端失败" };
                }

                // 生成签名
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

                System.Diagnostics.Debug.WriteLine("=== 提交成绩数据 ===");
                System.Diagnostics.Debug.WriteLine($"race_id: {submitData.race_id}, user_id: {submitData.user_id}, username: {submitData.username}");
                System.Diagnostics.Debug.WriteLine("===================");

                string encryptedData = cryptoClient.Encrypt(submitData);

                var response = await apiClient.PostAsync("/api/race/submit", new { encrypted_data = encryptedData });

                if (!response.IsSuccess && string.IsNullOrWhiteSpace(response.EncryptedData))
                {
                    return new RaceApiResult
                    {
                        Success = false,
                        Message = $"提交成绩失败: {response.Msg}"
                    };
                }

                // 处理加密响应
                JObject responseData;
                if (!string.IsNullOrWhiteSpace(response.EncryptedData))
                {
                    responseData = await DecryptAuthenticatedWithRetry(response.EncryptedData);
                }
                else if (response.RawJson != null)
                {
                    responseData = response.RawJson;
                }
                else
                {
                    return new RaceApiResult { Success = false, Message = "提交成绩失败: 服务器返回内容无法解析" };
                }

                bool serverSuccess = responseData["success"]?.ToObject<bool>() ?? true;
                string serverMessage = responseData["msg"]?.ToString() ?? "";

                return new RaceApiResult
                {
                    Success = serverSuccess,
                    Message = string.IsNullOrEmpty(serverMessage) ? (serverSuccess ? "提交成绩成功" : "提交成绩失败") : serverMessage,
                    Data = responseData
                };
            }
            catch (Exception ex)
            {
                return new RaceApiResult { Success = false, Message = $"提交成绩失败: {ex.Message}" };
            }
        }

        /// <summary>
        /// 获取历史数据
        /// </summary>
        public async Task<RaceApiResult> GetHistoryAsync(int raceId, string username, int limit = 30)
        {
            try
            {
                if (cryptoClient == null)
                {
                    bool initialized = await InitializeAsync();
                    if (!initialized)
                        return new RaceApiResult { Success = false, Message = "初始化加密客户端失败" };
                }

                var queryParams = new Dictionary<string, string>
                {
                    ["raceId"] = raceId.ToString(),
                    ["limit"] = limit.ToString()
                };
                // 新 API 用 JWT 自动识别用户，但仍传 username 以兼容旧 API
                if (!string.IsNullOrWhiteSpace(username))
                    queryParams["username"] = username;

                var response = await apiClient.GetAsync("/api/race/history", queryParams);

                // 旧格式回退
                if (response.Code == 404)
                {
                    var oldParams = new Dictionary<string, string>
                    {
                        ["race_id"] = raceId.ToString(),
                        ["username"] = username,
                        ["limit"] = limit.ToString()
                    };
                    response = await apiClient.GetAsync("/api/race/history", oldParams);
                }

                if (!response.IsSuccess)
                {
                    return new RaceApiResult
                    {
                        Success = false,
                        Message = $"获取历史数据失败: {response.Msg}"
                    };
                }

                return new RaceApiResult
                {
                    Success = true,
                    Message = "获取历史数据成功",
                    Data = response.RawJson ?? new JObject()
                };
            }
            catch (Exception ex)
            {
                return new RaceApiResult { Success = false, Message = $"获取历史数据失败: {ex.Message}" };
            }
        }

        /// <summary>
        /// 获取排行榜
        /// </summary>
        public async Task<RaceApiResult> GetLeaderboardAsync(int raceId, string date = null, int limit = 100)
        {
            try
            {
                if (cryptoClient == null)
                {
                    bool initialized = await InitializeAsync();
                    if (!initialized)
                        return new RaceApiResult { Success = false, Message = "初始化加密客户端失败" };
                }

                var queryParams = new Dictionary<string, string>
                {
                    ["raceId"] = raceId.ToString(),
                    ["limit"] = limit.ToString()
                };
                if (!string.IsNullOrEmpty(date))
                    queryParams["dateStr"] = date;

                var response = await apiClient.GetAsync("/api/race/leaderboard", queryParams);

                // 旧格式回退
                if (response.Code == 404)
                {
                    var oldParams = new Dictionary<string, string>
                    {
                        ["race_id"] = raceId.ToString(),
                        ["limit"] = limit.ToString()
                    };
                    if (!string.IsNullOrEmpty(date))
                        oldParams["date_str"] = date;

                    response = await apiClient.GetAsync("/api/race/leaderboard", oldParams);
                }

                if (!response.IsSuccess)
                {
                    return new RaceApiResult
                    {
                        Success = false,
                        Message = $"获取排行榜失败: {response.Msg}"
                    };
                }

                return new RaceApiResult
                {
                    Success = true,
                    Message = "获取排行榜成功",
                    Data = response.RawJson ?? new JObject()
                };
            }
            catch (Exception ex)
            {
                return new RaceApiResult { Success = false, Message = $"获取排行榜失败: {ex.Message}" };
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
