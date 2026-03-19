using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TypeSunny.Net.Http
{
    /// <summary>
    /// 统一 API 响应模型（非泛型版本）
    /// </summary>
    public class ApiResponse
    {
        /// <summary>
        /// 状态码（新 API 返回的 code 字段，旧 API 会被映射为 200 或错误码）
        /// </summary>
        [JsonProperty("code")]
        public int Code { get; set; }

        /// <summary>
        /// 消息
        /// </summary>
        [JsonProperty("msg")]
        public string Msg { get; set; }

        /// <summary>
        /// 原始数据（JToken 形式）
        /// </summary>
        [JsonProperty("data")]
        public JToken RawData { get; set; }

        /// <summary>
        /// 时间戳（新 API 返回）
        /// </summary>
        [JsonProperty("timestamp")]
        public long? Timestamp { get; set; }

        /// <summary>
        /// 加密响应数据（赛文加密接口透传）
        /// </summary>
        [JsonIgnore]
        public string EncryptedData { get; set; }

        /// <summary>
        /// 原始 JSON 响应（始终保留）
        /// </summary>
        [JsonIgnore]
        public JObject RawJson { get; set; }

        /// <summary>
        /// 是否成功
        /// </summary>
        [JsonIgnore]
        public bool IsSuccess => Code == 200;

        /// <summary>
        /// 从旧格式（文来）转换为统一格式
        /// 旧格式: { "error": 0, "msg": { ... } }
        /// </summary>
        public static ApiResponse FromLegacy(JObject json)
        {
            if (json == null)
                return new ApiResponse { Code = 500, Msg = "响应为空" };

            var response = new ApiResponse { RawJson = json };

            // 检测新格式：有 code 整数字段
            if (json["code"] != null && json["code"].Type == JTokenType.Integer)
            {
                response.Code = (int)json["code"];
                response.Msg = json["msg"]?.ToString() ?? "";
                response.RawData = json["data"];
                if (json["timestamp"] != null && json["timestamp"].Type != JTokenType.Null)
                    response.Timestamp = (long)json["timestamp"];
                return response;
            }

            // 检测加密响应（旧格式）
            if (json["encrypted_data"] != null && json["encrypted_data"].Type == JTokenType.String)
            {
                response.Code = 200;
                response.Msg = "";
                response.EncryptedData = json["encrypted_data"].ToString();
                return response;
            }

            // 检测新版加密 envelope（encryptedKey + iv + encryptedData）
            // 这种情况下 data 本身就是 envelope，不需要特殊处理，由调用方解密

            // 旧格式1（文来）: { "error": 0, "msg": { ... } }
            if (json["error"] != null && json["error"].Type == JTokenType.Integer)
            {
                int errorCode = (int)json["error"];
                if (errorCode == 0)
                {
                    response.Code = 200;
                    response.Msg = "";
                    response.RawData = json["msg"];
                }
                else
                {
                    response.Code = 400 + errorCode;
                    response.Msg = json["msg"]?.ToString() ?? "未知错误";
                }
                return response;
            }

            // 旧格式2（赛文加密解密后）: { "success": true/false, "msg": "...", ... }
            if (json["success"] != null && json["success"].Type == JTokenType.Boolean)
            {
                bool success = (bool)json["success"];
                response.Code = success ? 200 : 400;
                response.Msg = json["msg"]?.ToString() ?? "";
                response.RawData = json;
                return response;
            }

            // 无法识别的格式，按成功处理
            response.Code = 200;
            response.RawData = json;
            return response;
        }
    }

    /// <summary>
    /// 统一 API 响应模型（泛型版本，data 字段反序列化为 T）
    /// </summary>
    public class ApiResponse<T>
    {
        /// <summary>
        /// 状态码
        /// </summary>
        public int Code { get; set; }

        /// <summary>
        /// 消息
        /// </summary>
        public string Msg { get; set; }

        /// <summary>
        /// 强类型数据
        /// </summary>
        public T Data { get; set; }

        /// <summary>
        /// 时间戳
        /// </summary>
        public long? Timestamp { get; set; }

        /// <summary>
        /// 加密响应数据
        /// </summary>
        public string EncryptedData { get; set; }

        /// <summary>
        /// 原始 JSON 响应
        /// </summary>
        public JObject RawJson { get; set; }

        /// <summary>
        /// 是否成功
        /// </summary>
        public bool IsSuccess => Code == 200;

        /// <summary>
        /// 从非泛型 ApiResponse 转换
        /// </summary>
        public static ApiResponse<T> From(ApiResponse raw)
        {
            var result = new ApiResponse<T>
            {
                Code = raw.Code,
                Msg = raw.Msg,
                Timestamp = raw.Timestamp,
                EncryptedData = raw.EncryptedData,
                RawJson = raw.RawJson
            };

            // 尝试将 RawData 反序列化为 T
            if (raw.RawData != null && raw.IsSuccess)
            {
                try
                {
                    if (typeof(T) == typeof(JToken) || typeof(T) == typeof(JObject) || typeof(T) == typeof(JArray))
                    {
                        result.Data = (T)(object)raw.RawData;
                    }
                    else if (typeof(T) == typeof(string))
                    {
                        result.Data = (T)(object)raw.RawData.ToString();
                    }
                    else
                    {
                        result.Data = raw.RawData.ToObject<T>();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ApiResponse] 反序列化数据失败: {ex.Message}");
                }
            }

            return result;
        }
    }
}
