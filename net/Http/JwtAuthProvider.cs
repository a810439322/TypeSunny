using System.Net.Http;
using System.Net.Http.Headers;

namespace TypeSunny.Net.Http
{
    /// <summary>
    /// JWT Bearer Token 认证提供者
    /// </summary>
    public class JwtAuthProvider : IAuthProvider
    {
        /// <summary>
        /// JWT Access Token
        /// </summary>
        public string AccessToken { get; set; }

        public JwtAuthProvider(string accessToken = null)
        {
            AccessToken = accessToken;
        }

        /// <summary>
        /// 设置 Authorization: Bearer 请求头
        /// </summary>
        public void ApplyAuth(HttpRequestMessage request)
        {
            if (!string.IsNullOrWhiteSpace(AccessToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
            }
        }

        /// <summary>
        /// 是否已认证（有有效的 Token）
        /// </summary>
        public bool IsAuthenticated => !string.IsNullOrWhiteSpace(AccessToken);

        /// <summary>
        /// 清除 Token
        /// </summary>
        public void ClearAuth()
        {
            AccessToken = null;
        }
    }
}
