using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TypeSunny;
using TypeSunny.Net;
using TypeSunny.Net.Http;
using Newtonsoft.Json.Linq;

namespace TypeSunny.Net
{
    /// <summary>
    /// 赛文助手V2 — 共用文来 JWT 认证，支持多服务器
    /// </summary>
    public class RaceHelperV2
    {
        private RaceServerManager serverManager;
        private AccountSystemManager accountManager;

        public RaceHelperV2()
        {
            serverManager = new RaceServerManager();
            accountManager = new AccountSystemManager();
        }

        public RaceServerManager GetServerManager()
        {
            return serverManager;
        }

        /// <summary>
        /// 创建 RaceAPI 实例（使用文来的 JWT 认证）
        /// </summary>
        private RaceAPI CreateRaceAPI(RaceServer server)
        {
            // 从 AccountSystemManager 查找已认证的账号（优先文来，再按域名匹配）
            accountManager.Reload();
            AccountInfo account = FindAuthenticatedAccount(server);

            if (account == null || string.IsNullOrWhiteSpace(account.JwtToken))
            {
                throw new Exception("请先登录文来账号");
            }

            // 同步登录信息到 server 对象
            server.UserId = account.UserId;
            server.DisplayName = account.DisplayName;
            server.Username = account.Username;

            // 确定客户端密钥
            string keyXml = !string.IsNullOrWhiteSpace(account.ClientKeyXml)
                ? account.ClientKeyXml
                : server.ClientKeyXml;

            // 创建带 JWT 认证的 ApiClient
            var jwtAuth = new JwtAuthProvider(account.JwtToken);
            var apiClient = new ApiClient(server.Url, jwtAuth);

            return new RaceAPI(apiClient, keyXml);
        }

        /// <summary>
        /// 查找已认证的账号（优先直接匹配，再按域名匹配）
        /// </summary>
        private AccountInfo FindAuthenticatedAccount(RaceServer server)
        {
            // 1. 直接按 serverId 查找
            var directAccount = accountManager.GetAccount(server.Id);
            if (directAccount != null && !string.IsNullOrWhiteSpace(directAccount.JwtToken))
                return directAccount;

            // 2. 按 "文来" 查找
            var wenlaiAccount = accountManager.GetAccount("文来");
            if (wenlaiAccount != null && !string.IsNullOrWhiteSpace(wenlaiAccount.JwtToken))
            {
                // 检查域名是否匹配
                if (IsSameDomain(server.Url, wenlaiAccount.Domain))
                    return wenlaiAccount;
            }

            // 3. 遍历所有账号按域名匹配
            foreach (var acc in accountManager.GetAllAccounts())
            {
                if (acc != null && !string.IsNullOrWhiteSpace(acc.JwtToken) && !string.IsNullOrWhiteSpace(acc.Domain))
                {
                    if (IsSameDomain(server.Url, acc.Domain))
                        return acc;
                }
            }

            return null;
        }

        private bool IsSameDomain(string url1, string url2)
        {
            try
            {
                Uri uri1 = new Uri(url1.TrimEnd('/'));
                Uri uri2 = new Uri(url2.TrimEnd('/'));
                return uri1.Host.Equals(uri2.Host, StringComparison.OrdinalIgnoreCase)
                    && uri1.Port == uri2.Port;
            }
            catch { return false; }
        }

        /// <summary>
        /// 检查赛文服务器是否已通过文来登录
        /// </summary>
        public bool IsLoggedIn(string serverId)
        {
            var server = serverManager.GetAllServers().Find(s => s.Id == serverId);
            if (server == null) return false;

            accountManager.Reload();
            var account = FindAuthenticatedAccount(server);
            return account != null && !string.IsNullOrWhiteSpace(account.JwtToken);
        }

        /// <summary>
        /// 显示登录对话框（引导用户去文来登录）
        /// </summary>
        public void ShowLoginDialog(Window owner, string serverId)
        {
            MessageBox.Show(
                "赛文功能现在共用文来账号登录。\n请先通过「文来」按钮登录您的账号。",
                "赛文登录",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        /// <summary>
        /// 显示注册对话框（引导用户去文来注册）
        /// </summary>
        public void ShowRegisterDialog(Window owner, string serverId)
        {
            MessageBox.Show(
                "赛文功能现在共用文来账号。\n请先通过「文来」按钮注册您的账号。\n注册时会自动生成赛文所需的密钥。",
                "赛文注册",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        /// <summary>
        /// 载入每日文章
        /// </summary>
        public async Task<string> LoadDailyArticle(string serverId, int raceId)
        {
            var server = serverManager.GetAllServers().Find(s => s.Id == serverId);
            if (server == null)
                return "服务器不存在";

            // 同步账号信息
            accountManager.Reload();
            var account = FindAuthenticatedAccount(server);
            if (account == null || string.IsNullOrWhiteSpace(account.JwtToken))
                return "请先登录文来账号";

            // 同步到 server
            server.UserId = account.UserId;
            server.DisplayName = account.DisplayName;
            server.Username = account.Username;
            if (!string.IsNullOrWhiteSpace(account.ClientKeyXml))
                server.ClientKeyXml = account.ClientKeyXml;

            try
            {
                // 确保本地有客户端密钥对
                if (string.IsNullOrWhiteSpace(server.ClientKeyXml) && string.IsNullOrWhiteSpace(account.ClientKeyXml))
                {
                    var tempCrypto = new RaceCryptoClient();
                    string newKeyXml = tempCrypto.GetClientKeyXml();
                    server.ClientKeyXml = newKeyXml;
                    account.ClientKeyXml = newKeyXml;
                    accountManager.SaveAccount(account);
                    System.Diagnostics.Debug.WriteLine("[赛文V2] 本地无密钥对，已自动生成");
                }

                var api = CreateRaceAPI(server);
                var result = await api.GetDailyArticleAsync(raceId);

                // 如果因为密钥问题失败，重新生成密钥对并上传后重试
                if (!result.Success && result.NeedKeyReupload)
                {
                    System.Diagnostics.Debug.WriteLine("[赛文V2] 载文失败（密钥问题），重新生成密钥对并上传...");

                    var freshCrypto = new RaceCryptoClient();
                    string freshKeyXml = freshCrypto.GetClientKeyXml();
                    server.ClientKeyXml = freshKeyXml;
                    account.ClientKeyXml = freshKeyXml;
                    accountManager.SaveAccount(account);

                    api = CreateRaceAPI(server);
                    var uploadResult = await api.UpdatePublicKeyAsync();
                    if (uploadResult.Success)
                    {
                        System.Diagnostics.Debug.WriteLine("[赛文V2] 公钥补录成功，重试载文");
                        result = await api.GetDailyArticleAsync(raceId);
                    }
                    else
                    {
                        return $"载文失败: 补录公钥失败 - {uploadResult.Message}";
                    }
                }

                if (result.Success)
                {
                    JObject data = result.Data;

                    // 解密后的 RaceInitResponse 字段在根级别
                    string article = data["content"]?.ToString() ?? "";
                    int articleId = data["articleId"]?.ToObject<int>() ?? -1;

                    // 保存 init 响应中的关键信息
                    server.ServerPublicKey = result.ServerPublicKey;
                    server.KeyId = result.KeyId;
                    server.SessionNonce = result.SessionNonce;
                    server.ClientKeyXml = api.GetClientKeyXml();

                    serverManager.SetCurrentRace(serverId, raceId);
                    serverManager.SetCurrentArticle(serverId, articleId);

                    System.Diagnostics.Debug.WriteLine($"[赛文V2] 载文成功: articleId={articleId}, keyId={result.KeyId}");
                    return article;
                }
                else
                {
                    return $"载文失败: {result.Message}";
                }
            }
            catch (Exception ex)
            {
                return $"载文失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 提交成绩
        /// </summary>
        public async Task<string> SubmitScore(
            string serverId,
            int raceId,
            double speed,
            TimeSpan time,
            int charCount,
            double keystroke,
            double codeLength,
            int backspaceCount,
            int keyCount,
            double keyAccuracy,
            double wordRate,
            string inputMethod)
        {
            var server = serverManager.GetAllServers().Find(s => s.Id == serverId);
            if (server == null) return "服务器不存在";

            if (string.IsNullOrWhiteSpace(server.ServerPublicKey))
                return "未载文或服务器公钥丢失，请重新载文";

            if (server.CurrentArticleId < 0)
                return "未载文";

            try
            {
                var api = CreateRaceAPI(server);

                var submitData = new RaceSubmitData
                {
                    RaceId = raceId,
                    ArticleId = server.CurrentArticleId,
                    Speed = speed,
                    TimeCost = (int)time.TotalMilliseconds,
                    CharCount = charCount,
                    Keystroke = keystroke,
                    CodeLength = codeLength,
                    BackspaceCount = backspaceCount,
                    KeyCount = keyCount,
                    KeyAccuracy = keyAccuracy,
                    WordRate = wordRate,
                    InputMethod = inputMethod,
                    SessionNonce = server.SessionNonce,
                    ClientTs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };

                var result = await api.SubmitScoreAsync(submitData, server.ServerPublicKey, server.KeyId);

                if (result.Success)
                    return "提交成功";
                else
                    return $"提交失败: {result.Message}";
            }
            catch (Exception ex)
            {
                return $"提交失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 打开历史成绩页面
        /// </summary>
        public void OpenHistory(string serverId, int raceId)
        {
            var server = serverManager.GetAllServers().Find(s => s.Id == serverId);
            if (server == null)
            {
                MessageBox.Show("服务器不存在", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                string url = $"{server.Url}/api/race/history?raceId={raceId}";
                System.Diagnostics.Process.Start(url);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法打开浏览器: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 打开排行榜页面
        /// </summary>
        public void OpenLeaderboard(string serverId, int raceId)
        {
            var server = serverManager.GetAllServers().Find(s => s.Id == serverId);
            if (server == null)
            {
                MessageBox.Show("服务器不存在", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                string url = $"{server.Url}/api/race/leaderboard?raceId={raceId}";
                System.Diagnostics.Process.Start(url);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法打开浏览器: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}