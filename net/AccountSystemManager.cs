using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace TypeSunny.Net
{
    /// <summary>
    /// 账号信息
    /// </summary>
    public class AccountInfo
    {
        /// <summary>
        /// 服务名称（如：文来、赛文1、赛文2）
        /// </summary>
        public string ServiceName { get; set; }

        /// <summary>
        /// 服务器域名
        /// </summary>
        public string Domain { get; set; }

        /// <summary>
        /// 用户名
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// 密码
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// 显示名称
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// 用户ID
        /// </summary>
        public int UserId { get; set; }

        /// <summary>
        /// Cookie（JSON格式的Cookie容器序列化）
        /// </summary>
        public string Cookies { get; set; }

        /// <summary>
        /// 客户端RSA密钥对（XML格式）
        /// </summary>
        public string ClientKeyXml { get; set; }

        /// <summary>
        /// 最后登录时间
        /// </summary>
        public DateTime LastLoginTime { get; set; }

        public AccountInfo()
        {
            ServiceName = "";
            Domain = "";
            Username = "";
            Password = "";
            DisplayName = "";
            UserId = -1;
            Cookies = "";
            ClientKeyXml = "";
            LastLoginTime = DateTime.MinValue;
        }
    }

    /// <summary>
    /// 账号体系管理器
    /// 管理多个服务的账号信息，支持同域名账号共享
    /// </summary>
    public class AccountSystemManager
    {
        private Dictionary<string, AccountInfo> accounts;
        private const string CONFIG_KEY = "账号体系配置";

        public AccountSystemManager()
        {
            accounts = new Dictionary<string, AccountInfo>();
            LoadFromConfig();
        }

        /// <summary>
        /// 从配置文件加载账号信息
        /// </summary>
        private void LoadFromConfig()
        {
            try
            {
                string json = Config.GetString(CONFIG_KEY);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var loaded = JsonConvert.DeserializeObject<Dictionary<string, AccountInfo>>(json);
                    if (loaded != null)
                    {
                        accounts = loaded;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载账号配置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 保存账号信息到配置文件
        /// </summary>
        private void SaveToConfig()
        {
            try
            {
                string json = JsonConvert.SerializeObject(accounts, Formatting.None);
                Config.Set(CONFIG_KEY, json);
                Config.WriteConfig(0);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存账号配置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从URL提取域名（协议+主机+端口）
        /// </summary>
        private string ExtractDomain(string url)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(url))
                    return "";

                // 移除路径部分，只保留协议+主机+端口
                Uri uri = new Uri(url.TrimEnd('/'));
                return uri.GetLeftPart(UriPartial.Authority);
            }
            catch
            {
                return url;
            }
        }

        /// <summary>
        /// 获取账号信息
        /// </summary>
        public AccountInfo GetAccount(string serviceName)
        {
            bool found = accounts.ContainsKey(serviceName);
            var account = found ? accounts[serviceName] : null;
            System.Diagnostics.Debug.WriteLine($"GetAccount: ServiceName={serviceName}, Found={found}, Username={account?.Username}, DisplayName={account?.DisplayName}");
            return account;
        }

        /// <summary>
        /// 保存或更新账号信息
        /// </summary>
        /// <param name="account">账号信息</param>
        /// <param name="skipDomainSharing">是否跳过域名共享（用于退出登录等场景）</param>
        public void SaveAccount(AccountInfo account, bool skipDomainSharing = false)
        {
            System.Diagnostics.Debug.WriteLine($"SaveAccount被调用: ServiceName={account?.ServiceName}, Domain={account?.Domain}, Username={account?.Username}, DisplayName={account?.DisplayName}, skipDomainSharing={skipDomainSharing}");

            if (string.IsNullOrWhiteSpace(account.ServiceName))
            {
                System.Diagnostics.Debug.WriteLine("⚠ ServiceName为空，跳过保存");
                return;
            }

            // 如果域名与已有账号相同且当前账号信息为空，则复制已有账号信息
            if (!skipDomainSharing && !string.IsNullOrWhiteSpace(account.Domain))
            {
                string domain = ExtractDomain(account.Domain);
                var existingAccount = FindAccountByDomain(domain);
                System.Diagnostics.Debug.WriteLine($"  域名共享检查: domain={domain}, existingAccount={existingAccount?.ServiceName ?? "null"}");

                if (existingAccount != null &&
                    existingAccount.ServiceName != account.ServiceName &&
                    string.IsNullOrWhiteSpace(account.Username))
                {
                    // 复制账号信息
                    account.Username = existingAccount.Username;
                    account.Password = existingAccount.Password;
                    account.DisplayName = existingAccount.DisplayName;
                    account.UserId = existingAccount.UserId;
                    account.Cookies = existingAccount.Cookies;
                    account.ClientKeyXml = existingAccount.ClientKeyXml;
                    System.Diagnostics.Debug.WriteLine($"✓ 检测到相同域名({domain})，自动复制账号信息从 {existingAccount.ServiceName} 到 {account.ServiceName}");
                }
            }

            accounts[account.ServiceName] = account;
            System.Diagnostics.Debug.WriteLine($"✓ 账号已保存到内存: ServiceName={account.ServiceName}, Username={account.Username}, DisplayName={account.DisplayName}");
            SaveToConfig();
            System.Diagnostics.Debug.WriteLine($"✓ 账号已保存到配置文件");
        }

        /// <summary>
        /// 根据域名查找账号
        /// </summary>
        private AccountInfo FindAccountByDomain(string domain)
        {
            domain = ExtractDomain(domain);
            foreach (var account in accounts.Values)
            {
                if (ExtractDomain(account.Domain) == domain)
                {
                    return account;
                }
            }
            return null;
        }

        /// <summary>
        /// 更新登录信息
        /// </summary>
        public void UpdateLoginInfo(string serviceName, string username, string password,
            string displayName, int userId, string cookies = null, string clientKeyXml = null)
        {
            var account = GetAccount(serviceName);
            bool isNewAccount = false;
            if (account == null)
            {
                account = new AccountInfo { ServiceName = serviceName };
                isNewAccount = true;
            }

            account.Username = username;
            account.Password = password;
            account.DisplayName = displayName;
            account.UserId = userId;
            account.LastLoginTime = DateTime.Now;

            if (cookies != null)
                account.Cookies = cookies;
            if (clientKeyXml != null)
                account.ClientKeyXml = clientKeyXml;

            SaveAccount(account);

            System.Diagnostics.Debug.WriteLine($"✓ 更新登录信息: ServiceName={serviceName}, Username={username}, DisplayName={displayName}, UserId={userId}, Domain={account.Domain}, IsNewAccount={isNewAccount}");

            // 如果域名相同，同步更新其他账号
            if (!string.IsNullOrWhiteSpace(account.Domain))
            {
                string domain = ExtractDomain(account.Domain);
                System.Diagnostics.Debug.WriteLine($"  检查同域名服务进行同步，域名={domain}");

                foreach (var otherAccount in accounts.Values)
                {
                    if (otherAccount.ServiceName != serviceName &&
                        ExtractDomain(otherAccount.Domain) == domain)
                    {
                        otherAccount.Username = username;
                        otherAccount.Password = password;
                        otherAccount.DisplayName = displayName;
                        otherAccount.UserId = userId;
                        otherAccount.Cookies = cookies ?? otherAccount.Cookies;
                        otherAccount.ClientKeyXml = clientKeyXml ?? otherAccount.ClientKeyXml;
                        otherAccount.LastLoginTime = DateTime.Now;
                        System.Diagnostics.Debug.WriteLine($"✓ 同步登录信息到相同域名的服务: {otherAccount.ServiceName}");
                    }
                }
                SaveToConfig();
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"⚠ 账号 {serviceName} 的Domain为空，跳过同域名同步");
            }
        }

        /// <summary>
        /// 更新域名
        /// </summary>
        public void UpdateDomain(string serviceName, string domain)
        {
            var account = GetAccount(serviceName);
            if (account == null)
            {
                account = new AccountInfo { ServiceName = serviceName };
            }
            account.Domain = domain;
            SaveAccount(account);
        }

        /// <summary>
        /// 清除账号登录信息（保留服务条目，但清空登录凭据）
        /// </summary>
        public void ClearAccount(string serviceName)
        {
            var account = GetAccount(serviceName);
            if (account != null)
            {
                // 清空登录凭据，但保留服务名和域名
                account.Username = "";
                account.Password = "";
                account.DisplayName = "";
                account.UserId = -1;
                account.Cookies = "";
                account.ClientKeyXml = "";
                account.LastLoginTime = DateTime.MinValue;

                // 跳过域名共享，直接保存清空的账号信息
                SaveAccount(account, skipDomainSharing: true);
                System.Diagnostics.Debug.WriteLine($"✓ 已清除 {serviceName} 的登录信息");
            }
        }

        /// <summary>
        /// 删除账号
        /// </summary>
        public void DeleteAccount(string serviceName)
        {
            if (accounts.ContainsKey(serviceName))
            {
                accounts.Remove(serviceName);
                SaveToConfig();
            }
        }

        /// <summary>
        /// 获取所有账号
        /// </summary>
        public List<AccountInfo> GetAllAccounts()
        {
            return accounts.Values.ToList();
        }

        /// <summary>
        /// 清空所有账号
        /// </summary>
        public void ClearAll()
        {
            accounts.Clear();
            SaveToConfig();
        }
    }
}
