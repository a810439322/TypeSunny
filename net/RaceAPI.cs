using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TypeSunny.Net.Http;

namespace TypeSunny.Net
{
    /// <summary>
    /// 赛文API客户端（新版）
    /// 使用 JWT 认证（共用文来登录），AES-256-GCM + RSA-OAEP-SHA256 加密
    /// </summary>
    public class RaceAPI
    {
        private readonly string serverUrl;
        private readonly ApiClient apiClient;
        private readonly RaceCryptoClient cryptoClient;

        /// <summary>
        /// 初始化赛文API客户端
        /// </summary>
        /// <param name="apiClient">已带 JWT 认证的 ApiClient（从 WenlaiHelper 获取）</param>
        /// <param name="clientKeyXml">客户端RSA密钥对（XML格式）</param>
        public RaceAPI(ApiClient apiClient, string clientKeyXml = null)
        {
            this.serverUrl = apiClient.BaseUrl;
            this.apiClient = apiClient;
            this.cryptoClient = new RaceCryptoClient(clientKeyXml);
        }

        /// <summary>
        /// 获取客户端密钥对 XML
        /// </summary>
        public string GetClientKeyXml()
        {
            return cryptoClient.GetClientKeyXml();
        }

        /// <summary>
        /// 获取客户端公钥 PEM
        /// </summary>
        public string GetClientPublicKeyPem()
        {
            return cryptoClient.GetClientPublicKeyPem();
        }

        /// <summary>
        /// 获取客户端公钥 Base64
        /// </summary>
        public string GetClientPublicKeyBase64()
        {
            return cryptoClient.GetClientPublicKeyBase64();
        }

        // ==================== 公钥管理 ====================

        /// <summary>
        /// 更新服务端存储的客户端公钥（POST /api/auth/updatePublicKey）
        /// 适用于登录后补录、或密钥对更换场景
        /// </summary>
        public async Task<RaceApiResult> UpdatePublicKeyAsync()
        {
            try
            {
                string publicKeyBase64 = cryptoClient.GetClientPublicKeyBase64();
                var body = new { publicKey = publicKeyBase64 };
                var response = await apiClient.PostAsync("/api/auth/updatePublicKey", body);

                if (!response.IsSuccess)
                    return new RaceApiResult { Success = false, Message = $"更新公钥失败: {response.Msg}" };

                System.Diagnostics.Debug.WriteLine("[赛文] 公钥已更新到服务端");
                return new RaceApiResult { Success = true, Message = "公钥已更新" };
            }
            catch (Exception ex)
            {
                return new RaceApiResult { Success = false, Message = $"更新公钥失败: {ex.Message}" };
            }
        }

        // ==================== 赛文列表 ====================

        /// <summary>
        /// 获取赛文列表
        /// </summary>
        public async Task<RaceApiResult> GetRaceListAsync()
        {
            try
            {
                var response = await apiClient.GetAsync("/api/race/configs");
                if (response.Code == 404)
                    response = await apiClient.GetAsync("/api/race/list");

                if (!response.IsSuccess)
                    return new RaceApiResult { Success = false, Message = $"获取赛文列表失败: {response.Msg}" };

                JObject resultData = response.RawJson ?? new JObject();
                if (response.RawData != null && resultData["data"] == null)
                    resultData["data"] = response.RawData;

                return new RaceApiResult { Success = true, Message = "获取赛文列表成功", Data = resultData };
            }
            catch (Exception ex)
            {
                return new RaceApiResult { Success = false, Message = $"获取赛文列表失败: {ex.Message}" };
            }
        }

        // ==================== 载文（init） ====================

        /// <summary>
        /// 获取每日赛文（新版：解密加密 envelope，返回文章 + serverPublicKey + keyId + sessionNonce）
        /// </summary>
        public async Task<RaceInitResult> GetDailyArticleAsync(int raceId)
        {
            try
            {
                var queryParams = new Dictionary<string, string>
                {
                    ["raceId"] = raceId.ToString()
                };

                var response = await apiClient.GetAsync("/api/race/init", queryParams);

                if (!response.IsSuccess)
                    return new RaceInitResult { Success = false, Message = $"获取赛文失败: {response.Msg}" };

                // 检查 data 是否是加密 envelope
                JObject data = response.RawData as JObject;
                if (data == null)
                    return new RaceInitResult { Success = false, Message = "获取赛文失败: 服务器返回数据为空" };

                JObject decrypted;
                if (data["encryptedKey"] != null && data["iv"] != null && data["encryptedData"] != null)
                {
                    // 新格式：加密 envelope，需要解密
                    decrypted = cryptoClient.DecryptInitResponse(data);
                }
                else
                {
                    // 未加密（兼容）
                    decrypted = data;
                }

                // 提取关键字段
                string serverPublicKey = decrypted["serverPublicKey"]?.ToString() ?? "";
                string keyId = decrypted["keyId"]?.ToString() ?? "";
                string sessionNonce = decrypted["sessionNonce"]?.ToString() ?? "";

                return new RaceInitResult
                {
                    Success = true,
                    Message = "获取赛文成功",
                    Data = decrypted,
                    ServerPublicKey = serverPublicKey,
                    KeyId = keyId,
                    SessionNonce = sessionNonce
                };
            }
            catch (Exception ex)
            {
                return new RaceInitResult { Success = false, Message = $"获取赛文失败: {ex.Message}" };
            }
        }

        // ==================== 提交成绩 ====================

        /// <summary>
        /// 提交成绩（新版：RSA-OAEP-SHA256 加密 + RSA-SHA256 签名）
        /// </summary>
        public async Task<RaceApiResult> SubmitScoreAsync(RaceSubmitData submitData, string serverPublicKey, string keyId)
        {
            try
            {
                // 1. 构造要加密和签名的 payload JSON
                var payloadObj = new JObject
                {
                    ["raceId"] = submitData.RaceId,
                    ["articleId"] = submitData.ArticleId,
                    ["speed"] = Math.Round(submitData.Speed, 5),
                    ["timeCost"] = submitData.TimeCost,
                    ["charCount"] = submitData.CharCount,
                    ["keystroke"] = Math.Round(submitData.Keystroke, 5),
                    ["codeLength"] = Math.Round(submitData.CodeLength, 5),
                    ["backspaceCount"] = submitData.BackspaceCount,
                    ["keyCount"] = submitData.KeyCount,
                    ["keyAccuracy"] = Math.Round(submitData.KeyAccuracy, 5),
                    ["wordRate"] = Math.Round(submitData.WordRate, 5),
                    ["inputMethod"] = submitData.InputMethod,
                    ["sessionNonce"] = submitData.SessionNonce,
                    ["clientTs"] = submitData.ClientTs
                };

                string payloadJson = payloadObj.ToString(Formatting.None);

                // 2. 用服务器公钥加密
                JObject encryptedEnvelope = cryptoClient.EncryptForServer(payloadJson, serverPublicKey);

                // 3. 用客户端私钥签名
                string signature = cryptoClient.SignPayload(payloadJson);

                // 4. 构造提交请求
                var submitRequest = new
                {
                    raceId = submitData.RaceId,
                    encryptedData = encryptedEnvelope.ToString(Formatting.None),
                    signature = signature,
                    clientTs = submitData.ClientTs,
                    keyId = keyId
                };

                System.Diagnostics.Debug.WriteLine($"[赛文] 提交成绩: raceId={submitData.RaceId}, keyId={keyId}");

                var response = await apiClient.PostAsync("/api/race/submit", submitRequest);

                if (!response.IsSuccess)
                    return new RaceApiResult { Success = false, Message = $"提交成绩失败: {response.Msg}" };

                return new RaceApiResult
                {
                    Success = true,
                    Message = response.RawData?["msg"]?.ToString() ?? "提交成绩成功",
                    Data = response.RawJson ?? new JObject()
                };
            }
            catch (Exception ex)
            {
                return new RaceApiResult { Success = false, Message = $"提交成绩失败: {ex.Message}" };
            }
        }

        // ==================== 历史 / 排行榜 ====================

        public async Task<RaceApiResult> GetHistoryAsync(int raceId, int limit = 30)
        {
            try
            {
                var queryParams = new Dictionary<string, string>
                {
                    ["raceId"] = raceId.ToString(),
                    ["limit"] = limit.ToString()
                };

                var response = await apiClient.GetAsync("/api/race/history", queryParams);
                if (!response.IsSuccess)
                    return new RaceApiResult { Success = false, Message = $"获取历史数据失败: {response.Msg}" };

                return new RaceApiResult { Success = true, Message = "获取历史数据成功", Data = response.RawJson ?? new JObject() };
            }
            catch (Exception ex)
            {
                return new RaceApiResult { Success = false, Message = $"获取历史数据失败: {ex.Message}" };
            }
        }

        public async Task<RaceApiResult> GetLeaderboardAsync(int raceId, string date = null, int limit = 100)
        {
            try
            {
                var queryParams = new Dictionary<string, string>
                {
                    ["raceId"] = raceId.ToString(),
                    ["limit"] = limit.ToString()
                };
                if (!string.IsNullOrEmpty(date))
                    queryParams["dateStr"] = date;

                var response = await apiClient.GetAsync("/api/race/leaderboard", queryParams);
                if (!response.IsSuccess)
                    return new RaceApiResult { Success = false, Message = $"获取排行榜失败: {response.Msg}" };

                return new RaceApiResult { Success = true, Message = "获取排行榜成功", Data = response.RawJson ?? new JObject() };
            }
            catch (Exception ex)
            {
                return new RaceApiResult { Success = false, Message = $"获取排行榜失败: {ex.Message}" };
            }
        }
    }

    // ==================== 数据类 ====================

    public class RaceApiResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public JObject Data { get; set; }
    }

    /// <summary>
    /// 赛文 init 结果（包含解密后的数据 + 服务器公钥等元信息）
    /// </summary>
    public class RaceInitResult : RaceApiResult
    {
        public string ServerPublicKey { get; set; }
        public string KeyId { get; set; }
        public string SessionNonce { get; set; }
    }

    /// <summary>
    /// 赛文提交数据（新版）
    /// </summary>
    public class RaceSubmitData
    {
        public int RaceId { get; set; }
        public int ArticleId { get; set; }
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
        public string SessionNonce { get; set; }
        public long ClientTs { get; set; }
    }

    /// <summary>
    /// 旧版赛文成绩数据（保留兼容）
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