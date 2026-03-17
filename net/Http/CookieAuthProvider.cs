using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;

namespace TypeSunny.Net.Http
{
    /// <summary>
    /// Cookie 认证提供者（过渡期/JBS 兼容）
    /// </summary>
    public class CookieAuthProvider : IAuthProvider
    {
        private readonly CookieContainer cookieContainer;
        private string baseUrl;

        public CookieAuthProvider(CookieContainer container = null, string baseUrl = null)
        {
            this.cookieContainer = container ?? new CookieContainer();
            this.baseUrl = baseUrl;
        }

        /// <summary>
        /// 获取内部的 CookieContainer
        /// </summary>
        public CookieContainer GetCookieContainer()
        {
            return cookieContainer;
        }

        /// <summary>
        /// 设置 base URL（用于 Cookie 的域名关联）
        /// </summary>
        public void SetBaseUrl(string url)
        {
            baseUrl = url;
        }

        /// <summary>
        /// Cookie 认证通过 CookieContainer 自动处理，无需额外设置请求头
        /// </summary>
        public void ApplyAuth(HttpRequestMessage request)
        {
            // CookieContainer 由 HttpClientHandler 自动处理
        }

        /// <summary>
        /// 是否有 Cookie（已认证）
        /// </summary>
        public bool IsAuthenticated
        {
            get
            {
                if (string.IsNullOrWhiteSpace(baseUrl))
                    return false;

                try
                {
                    var cookies = cookieContainer.GetCookies(new Uri(baseUrl));
                    return cookies.Count > 0;
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// 清除所有 Cookie
        /// </summary>
        public void ClearAuth()
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                return;

            try
            {
                Uri uri = new Uri(baseUrl);
                var cookies = cookieContainer.GetCookies(uri);
                foreach (Cookie cookie in cookies)
                {
                    cookie.Expired = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"清除Cookie失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从 Cookie 字符串加载（格式：name=value; name2=value2）
        /// </summary>
        public void LoadCookies(string cookieString)
        {
            if (string.IsNullOrWhiteSpace(cookieString) || string.IsNullOrWhiteSpace(baseUrl))
                return;

            LoadCookies(cookieString, baseUrl);
        }

        /// <summary>
        /// 从 Cookie 字符串加载到指定 URL
        /// </summary>
        public void LoadCookies(string cookieString, string serverUrl)
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
        /// 获取 Cookie 字符串（格式：name=value; name2=value2）
        /// </summary>
        public string GetCookiesAsString()
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                return "";

            return GetCookiesAsString(baseUrl);
        }

        /// <summary>
        /// 获取指定 URL 的 Cookie 字符串
        /// </summary>
        public string GetCookiesAsString(string serverUrl)
        {
            if (string.IsNullOrWhiteSpace(serverUrl))
                return "";

            try
            {
                Uri uri = new Uri(serverUrl);
                var cookies = cookieContainer.GetCookies(uri);
                var cookieList = new List<string>();
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
    }
}
