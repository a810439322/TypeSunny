using Newtonsoft.Json;

namespace TypeSunny.Net.Http
{
    // ==================== 认证相关 ====================

    /// <summary>
    /// 登录请求
    /// </summary>
    public class LoginRequest
    {
        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("password")]
        public string Password { get; set; }
    }

    /// <summary>
    /// 登录响应
    /// </summary>
    public class LoginResponse
    {
        [JsonProperty("token")]
        public string Token { get; set; }

        [JsonProperty("userId")]
        public int UserId { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("displayName")]
        public string DisplayName { get; set; }
    }

    /// <summary>
    /// 注册请求
    /// </summary>
    public class RegisterRequest
    {
        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("password")]
        public string Password { get; set; }

        [JsonProperty("email")]
        public string Email { get; set; }

        [JsonProperty("code")]
        public string VerifyCode { get; set; }

        [JsonProperty("publicKey", NullValueHandling = NullValueHandling.Ignore)]
        public string PublicKey { get; set; }
    }

    // ==================== 文章/段落相关 ====================

    /// <summary>
    /// 段落响应（新 API 的 SegmentResponse）
    /// </summary>
    public class SegmentResponse
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("bookId")]
        public int BookId { get; set; }

        [JsonProperty("sortNum")]
        public int SortNum { get; set; }

        [JsonProperty("bookName")]
        public string BookName { get; set; }

        [JsonProperty("content")]
        public string Content { get; set; }

        [JsonProperty("charCount")]
        public int CharCount { get; set; }

        [JsonProperty("difficultyLevel")]
        public int DifficultyLevel { get; set; }

        [JsonProperty("difficultyLabel")]
        public string DifficultyLabel { get; set; }

        [JsonProperty("difficultyScore")]
        public double DifficultyScore { get; set; }

        [JsonProperty("mark")]
        public string Mark { get; set; }

        [JsonProperty("endSortNum")]
        public int EndSortNum { get; set; }

        [JsonProperty("endChars")]
        public string EndChars { get; set; }

        [JsonProperty("startChars")]
        public string StartChars { get; set; }

        [JsonProperty("category")]
        public string Category { get; set; }
    }

    /// <summary>
    /// 难度计算结果
    /// </summary>
    public class DifficultyResult
    {
        [JsonProperty("difficultyLevel")]
        public int DifficultyLevel { get; set; }

        [JsonProperty("difficultyLabel")]
        public string DifficultyLabel { get; set; }

        [JsonProperty("difficultyScore")]
        public double DifficultyScore { get; set; }
    }

    // ==================== 用户信息 ====================

    /// <summary>
    /// 用户信息响应
    /// </summary>
    public class UserInfoResponse
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("displayName")]
        public string DisplayName { get; set; }

        [JsonProperty("email")]
        public string Email { get; set; }
    }

    // ==================== 赛文相关 ====================

    /// <summary>
    /// 赛文配置（新 API）
    /// </summary>
    public class RaceConfigResponse
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("difficultyGroup")]
        public int DifficultyGroup { get; set; }

        [JsonProperty("allowResubmit")]
        public bool AllowResubmit { get; set; }

        [JsonProperty("isActive")]
        public bool IsActive { get; set; }

        [JsonProperty("charCount")]
        public int CharCount { get; set; }

        [JsonProperty("strictLength")]
        public bool StrictLength { get; set; }
    }

    /// <summary>
    /// 赛文初始化响应（包含加密数据）
    /// </summary>
    public class RaceInitResponse
    {
        [JsonProperty("encryptedData")]
        public string EncryptedData { get; set; }
    }

    /// <summary>
    /// 赛文提交成绩请求（新 API）
    /// </summary>
    public class RaceSubmitRequest
    {
        [JsonProperty("raceId")]
        public int RaceId { get; set; }

        [JsonProperty("encryptedData")]
        public string EncryptedData { get; set; }

        [JsonProperty("signature")]
        public string Signature { get; set; }

        [JsonProperty("clientTs")]
        public long ClientTs { get; set; }

        [JsonProperty("keyId")]
        public string KeyId { get; set; }
    }

    // ==================== 难度统计 ====================

    /// <summary>
    /// 难度统计项
    /// </summary>
    public class DifficultyStatItem
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("count")]
        public int Count { get; set; }
    }
}
