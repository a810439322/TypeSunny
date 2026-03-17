using System.Net.Http;

namespace TypeSunny.Net.Http
{
    /// <summary>
    /// 认证策略接口
    /// </summary>
    public interface IAuthProvider
    {
        /// <summary>
        /// 将认证信息应用到 HTTP 请求
        /// </summary>
        void ApplyAuth(HttpRequestMessage request);

        /// <summary>
        /// 是否已认证
        /// </summary>
        bool IsAuthenticated { get; }

        /// <summary>
        /// 清除认证信息
        /// </summary>
        void ClearAuth();
    }
}
