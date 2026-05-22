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
    /// 旧版赛文助手（保留兼容，内部委托给 RaceHelperV2）
    /// </summary>
    public class RaceHelper
    {
        private AccountSystemManager accountManager;
        private MenuItem menuItemLogin;
        private MenuItem menuItemLoadArticle;
        private MenuItem menuItemRegister;
        private MenuItem menuItemHistory;
        private MenuItem menuItemLeaderboard;
        private int currentUserId = -1;
        private string currentUsername = "";
        private const string SERVICE_NAME = "赛文";

        public RaceHelper(MenuItem loginMenuItem, MenuItem loadArticleMenuItem, MenuItem registerMenuItem, MenuItem historyMenuItem, MenuItem leaderboardMenuItem)
        {
            this.menuItemLogin = loginMenuItem;
            this.menuItemLoadArticle = loadArticleMenuItem;
            this.menuItemRegister = registerMenuItem;
            this.menuItemHistory = historyMenuItem;
            this.menuItemLeaderboard = leaderboardMenuItem;
            this.accountManager = new AccountSystemManager();
        }

        public void UpdateLoginStatus()
        {
            if (menuItemLogin == null) return;

            var account = accountManager.GetAccount(SERVICE_NAME);
            if (account == null) account = accountManager.GetAccount("文来");

            if (account != null && !string.IsNullOrWhiteSpace(account.DisplayName))
            {
                menuItemLogin.Header = $"已登录: {account.DisplayName}";
                currentUsername = account.DisplayName;
                currentUserId = account.UserId;
            }
            else
            {
                menuItemLogin.Header = "登录（请先登录文来）";
                currentUsername = "";
                currentUserId = -1;
            }
        }

        public void UpdateArticleButtonStatus()
        {
            if (menuItemLoadArticle == null) return;

            string lastDate = Config.GetString("赛文最后载文日期");
            string today = DateTime.Now.ToString("yyyy-MM-dd");

            if (lastDate == today)
            {
                menuItemLoadArticle.Header = "今日已完成";
                menuItemLoadArticle.IsEnabled = false;
            }
            else
            {
                menuItemLoadArticle.Header = "载文";
                menuItemLoadArticle.IsEnabled = true;
            }
        }

        public void MarkArticleLoaded()
        {
            string today = DateTime.Now.ToString("yyyy-MM-dd");
            Config.Set("赛文最后载文日期", today);
            Config.WriteConfig(0);
            UpdateArticleButtonStatus();
        }

        public void ShowLoginDialog(Window owner)
        {
            MessageBox.Show(
                "赛文功能现在共用文来账号登录。\n请先通过「文来」按钮登录您的账号。",
                "赛文登录",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        public void ShowRegisterDialog(Window owner)
        {
            MessageBox.Show(
                "赛文功能现在共用文来账号。\n请先通过「文来」按钮注册您的账号。",
                "赛文注册",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        public async Task<string> LoadDailyArticle()
        {
            accountManager.Reload();
            var account = accountManager.GetAccount("文来") ?? accountManager.GetAccount(SERVICE_NAME);

            if (account == null || string.IsNullOrWhiteSpace(account.JwtToken))
                return "请先登录文来账号";

            try
            {
                string serverUrl = Config.GetString("赛文服务器地址");
                if (string.IsNullOrWhiteSpace(serverUrl))
                    return "赛文服务器地址未配置";

                var jwtAuth = new JwtAuthProvider(account.JwtToken);
                var apiClient = new ApiClient(serverUrl, jwtAuth);
                var api = new RaceAPI(apiClient, account.ClientKeyXml);

                var result = await api.GetDailyArticleAsync(1);

                if (result.Success)
                {
                    JObject data = result.Data;
                    JObject articleData = data["article"] as JObject;
                    string article = articleData?["content"]?.ToString() ?? "";
                    int articleId = articleData?["id"]?.ToObject<int>() ?? -1;

                    Config.Set("赛文当前文章ID", articleId.ToString());
                    Config.WriteConfig(0);
                    MarkArticleLoaded();

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

        public void OpenHistory()
        {
            if (string.IsNullOrWhiteSpace(currentUsername))
            {
                MessageBox.Show("请先登录", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                string serverUrl = Config.GetString("赛文服务器地址");
                if (string.IsNullOrWhiteSpace(serverUrl)) return;
                string url = $"{serverUrl}/history?race_id=1&username={Uri.EscapeDataString(currentUsername)}";
                System.Diagnostics.Process.Start(url);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法打开浏览器: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void OpenLeaderboard()
        {
            try
            {
                string serverUrl = Config.GetString("赛文服务器地址");
                if (string.IsNullOrWhiteSpace(serverUrl)) return;
                string url = $"{serverUrl}/leaderboard?race_id=1";
                System.Diagnostics.Process.Start(url);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法打开浏览器: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public Task<string> SubmitScore(
            double speed, TimeSpan time, int charCount,
            double keystroke, double codeLength, int backspaceCount,
            int keyCount, double keyAccuracy, double wordRate, string inputMethod)
        {
            accountManager.Reload();
            var account = accountManager.GetAccount("文来") ?? accountManager.GetAccount(SERVICE_NAME);

            if (account == null || string.IsNullOrWhiteSpace(account.JwtToken))
                return Task.FromResult("请先登录文来账号");

            string articleIdStr = Config.GetString("赛文当前文章ID");
            if (!int.TryParse(articleIdStr, out int articleId) || articleId < 0)
                return Task.FromResult("未载文");

            try
            {
                string serverUrl = Config.GetString("赛文服务器地址");
                if (string.IsNullOrWhiteSpace(serverUrl))
                    return Task.FromResult("赛文服务器地址未配置");

                var jwtAuth = new JwtAuthProvider(account.JwtToken);
                var apiClient = new ApiClient(serverUrl, jwtAuth);
                var api = new RaceAPI(apiClient, account.ClientKeyXml);

                // 旧版 Helper 没有 serverPublicKey/keyId，无法使用新加密提交
                // 需要先 init 获取这些信息
                return Task.FromResult("旧版赛文助手不支持新加密提交，请使用赛文菜单中的服务器载文");
            }
            catch (Exception ex)
            {
                return Task.FromResult($"提交失败: {ex.Message}");
            }
        }
    }
}
