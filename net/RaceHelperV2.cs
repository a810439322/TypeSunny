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
    /// 赛文助手 - 支持多服务器、多赛文
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

        /// <summary>
        /// 获取服务器管理器
        /// </summary>
        public RaceServerManager GetServerManager()
        {
            return serverManager;
        }

        /// <summary>
        /// 创建 RaceAPI 实例并设置密钥不匹配回调
        /// </summary>
        private async Task<RaceAPI> CreateRaceAPIAsync(RaceServer server)
        {
            // 从 AccountSystemManager 查找账号（支持同域名自动登录）
            AccountInfo account = accountManager.GetAccount(server.Id);
            bool usedDomainMatch = false;

            if (account == null || string.IsNullOrWhiteSpace(account.Cookies))
            {
                // 备选：通过域名匹配查找账号
                var allAccounts = accountManager.GetAllAccounts();
                foreach (var acc in allAccounts)
                {
                    if (acc != null && acc.UserId > 0 && !string.IsNullOrWhiteSpace(acc.Domain))
                    {
                        // 检查是否有 JWT token 或 Cookie
                        bool hasAuth = !string.IsNullOrWhiteSpace(acc.Cookies) || !string.IsNullOrWhiteSpace(acc.JwtToken);
                        if (!hasAuth) continue;

                        try
                        {
                            Uri serverUri = new Uri(server.Url.TrimEnd('/'));
                            Uri accUri = new Uri(acc.Domain.TrimEnd('/'));
                            if (serverUri.Host.Equals(accUri.Host, StringComparison.OrdinalIgnoreCase))
                            {
                                account = acc;
                                usedDomainMatch = true;
                                System.Diagnostics.Debug.WriteLine($"[CreateRaceAPIAsync] 通过域名匹配找到账号: {acc.ServiceName}");
                                break;
                            }
                        }
                        catch { }
                    }
                }
            }

            // 确定要使用的密钥
            string keyXml = (account != null && !string.IsNullOrWhiteSpace(account.ClientKeyXml))
                ? account.ClientKeyXml
                : server.ClientKeyXml;

            var api = new RaceAPI(server.Url, keyXml);

            if (account != null && !string.IsNullOrWhiteSpace(account.Cookies))
            {
                api.LoadCookiesFromString(account.Cookies);
                System.Diagnostics.Debug.WriteLine($"[CreateRaceAPIAsync] 已加载 Cookie: {account.ServiceName} (域名匹配: {usedDomainMatch})");

                if (usedDomainMatch && !string.IsNullOrWhiteSpace(account.ClientKeyXml))
                {
                    server.ClientKeyXml = account.ClientKeyXml;
                    System.Diagnostics.Debug.WriteLine($"[CreateRaceAPIAsync] 已同步 ClientKeyXml 到 server 对象");
                }
            }

            // 设置密钥不匹配时的自动重新登录回调
            api.OnKeyMismatchCallback = async () =>
            {
                System.Diagnostics.Debug.WriteLine($"[赛文V2] 密钥不匹配，触发自动重新登录: {server.Name}");
                var (success, cookies, newKeyXml) = await accountManager.ReloginAsync(server.Id, server.Url);
                if (success)
                {
                    var updatedAccount = accountManager.GetAccount(server.Id);
                    if (updatedAccount != null)
                    {
                        server.ClientKeyXml = updatedAccount.ClientKeyXml;
                        server.UserId = updatedAccount.UserId;
                        server.Username = updatedAccount.DisplayName;
                    }
                }
                return (cookies, newKeyXml);
            };

            await api.InitializeAsync();
            return api;
        }

        /// <summary>
        /// 显示登录对话框
        /// </summary>
        public void ShowLoginDialog(Window owner, string serverId)
        {
            var server = serverManager.GetAllServers().Find(s => s.Id == serverId);
            if (server == null)
            {
                MessageBox.Show("服务器不存在", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var loginDialog = new Window
            {
                Title = $"登录 - {server.Name}",
                Width = 350,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = owner,
                ResizeMode = ResizeMode.NoResize
            };

            var grid = new Grid();
            grid.Margin = new Thickness(20);

            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var lblUsername = new Label { Content = "用户名:" };
            Grid.SetRow(lblUsername, 0);
            grid.Children.Add(lblUsername);

            var txtUsername = new TextBox
            {
                Text = server.Username ?? "",
                Padding = new Thickness(5),
                Margin = new Thickness(70, 0, 0, 0)
            };
            Grid.SetRow(txtUsername, 0);
            grid.Children.Add(txtUsername);

            var lblPassword = new Label { Content = "密码:" };
            Grid.SetRow(lblPassword, 2);
            grid.Children.Add(lblPassword);

            var txtPassword = new PasswordBox
            {
                Password = server.Password ?? "",
                Padding = new Thickness(5),
                Margin = new Thickness(70, 0, 0, 0)
            };
            Grid.SetRow(txtPassword, 2);
            grid.Children.Add(txtPassword);

            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetRow(btnPanel, 4);

            var btnLogin = new Button
            {
                Content = "登录",
                Width = 80,
                Height = 30,
                Margin = new Thickness(0, 0, 10, 0)
            };

            var btnCancel = new Button
            {
                Content = "取消",
                Width = 80,
                Height = 30
            };

            btnLogin.Click += async (s, args) =>
            {
                if (string.IsNullOrWhiteSpace(txtUsername.Text))
                {
                    MessageBox.Show("请输入用户名", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(txtPassword.Password))
                {
                    MessageBox.Show("请输入密码", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                btnLogin.IsEnabled = false;
                btnLogin.Content = "登录中...";

                try
                {
                    var api = await CreateRaceAPIAsync(server);
                    var result = await api.LoginAsync(txtUsername.Text, txtPassword.Password);

                    if (result.Success)
                    {
                        JObject data = result.Data;
                        JObject userData = data["user"] as JObject;

                        int userId = userData?["id"]?.ToObject<int>() ?? -1;
                        string username = userData?["username"]?.ToString() ?? txtUsername.Text;

                        server.ClientKeyXml = api.GetClientKeyXml();

                        server.Username = txtUsername.Text;
                        server.Password = txtPassword.Password;
                        serverManager.UpdateServerLogin(serverId, userId, txtUsername.Text, username);

                        // 保存到 AccountSystemManager
                        accountManager.UpdateLoginInfo(
                            serverId,
                            txtUsername.Text,
                            txtPassword.Password,
                            username,
                            userId,
                            api.GetCookiesAsString(),
                            api.GetClientKeyXml(),
                            server.Url
                        );

                        if (!string.IsNullOrWhiteSpace(server.Name) && server.Name != serverId)
                        {
                            accountManager.UpdateLoginInfo(
                                server.Name,
                                txtUsername.Text,
                                txtPassword.Password,
                                username,
                                userId,
                                api.GetCookiesAsString(),
                                api.GetClientKeyXml(),
                                server.Url
                            );
                            System.Diagnostics.Debug.WriteLine($"✓ 赛文登录已同时保存到AccountSystemManager: {username} (server.Name={server.Name})");
                        }

                        System.Diagnostics.Debug.WriteLine($"✓ 赛文登录已保存到AccountSystemManager: {username} (serverId={serverId})");

                        await serverManager.RefreshServerRaces(serverId);

                        MessageBox.Show($"登录成功！欢迎 {username}", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                        loginDialog.DialogResult = true;
                        loginDialog.Close();
                    }
                    else
                    {
                        MessageBox.Show(result.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        btnLogin.IsEnabled = true;
                        btnLogin.Content = "登录";
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"登录失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    btnLogin.IsEnabled = true;
                    btnLogin.Content = "登录";
                }
            };

            btnCancel.Click += (s, args) =>
            {
                loginDialog.DialogResult = false;
                loginDialog.Close();
            };

            btnPanel.Children.Add(btnLogin);
            btnPanel.Children.Add(btnCancel);
            grid.Children.Add(btnPanel);

            loginDialog.Content = grid;
            loginDialog.ShowDialog();
        }

        /// <summary>
        /// 显示注册对话框
        /// </summary>
        public void ShowRegisterDialog(Window owner, string serverId)
        {
            var server = serverManager.GetAllServers().Find(s => s.Id == serverId);
            if (server == null)
            {
                MessageBox.Show("服务器不存在", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var registerDialog = new Window
            {
                Title = $"注册 - {server.Name}",
                Width = 350,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = owner,
                ResizeMode = ResizeMode.NoResize
            };

            var grid = new Grid();
            grid.Margin = new Thickness(20);

            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var lblUsername = new Label { Content = "用户名:" };
            Grid.SetRow(lblUsername, 0);
            grid.Children.Add(lblUsername);

            var txtUsername = new TextBox
            {
                Padding = new Thickness(5),
                Margin = new Thickness(70, 0, 0, 0)
            };
            Grid.SetRow(txtUsername, 0);
            grid.Children.Add(txtUsername);

            var lblPassword = new Label { Content = "密码:" };
            Grid.SetRow(lblPassword, 2);
            grid.Children.Add(lblPassword);

            var txtPassword = new PasswordBox
            {
                Padding = new Thickness(5),
                Margin = new Thickness(70, 0, 0, 0)
            };
            Grid.SetRow(txtPassword, 2);
            grid.Children.Add(txtPassword);

            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetRow(btnPanel, 4);

            var btnRegister = new Button
            {
                Content = "注册",
                Width = 80,
                Height = 30,
                Margin = new Thickness(0, 0, 10, 0)
            };

            var btnCancel = new Button
            {
                Content = "取消",
                Width = 80,
                Height = 30
            };

            btnRegister.Click += async (s, args) =>
            {
                if (string.IsNullOrWhiteSpace(txtUsername.Text))
                {
                    MessageBox.Show("请输入用户名", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(txtPassword.Password))
                {
                    MessageBox.Show("请输入密码", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                btnRegister.IsEnabled = false;
                btnRegister.Content = "注册中...";

                try
                {
                    var api = await CreateRaceAPIAsync(server);
                    var result = await api.RegisterAsync(txtUsername.Text, txtPassword.Password);

                    if (result.Success)
                    {
                        server.ClientKeyXml = api.GetClientKeyXml();
                        serverManager.SaveToConfig();

                        MessageBox.Show($"注册成功！请使用用户名和密码登录", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                        registerDialog.DialogResult = true;
                        registerDialog.Close();
                    }
                    else
                    {
                        MessageBox.Show(result.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        btnRegister.IsEnabled = true;
                        btnRegister.Content = "注册";
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"注册失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    btnRegister.IsEnabled = true;
                    btnRegister.Content = "注册";
                }
            };

            btnCancel.Click += (s, args) =>
            {
                registerDialog.DialogResult = false;
                registerDialog.Close();
            };

            btnPanel.Children.Add(btnRegister);
            btnPanel.Children.Add(btnCancel);
            grid.Children.Add(btnPanel);

            registerDialog.Content = grid;
            registerDialog.ShowDialog();
        }

        /// <summary>
        /// 载入每日文章
        /// </summary>
        public async Task<string> LoadDailyArticle(string serverId, int raceId)
        {
            var server = serverManager.GetAllServers().Find(s => s.Id == serverId);
            if (server == null)
            {
                return "服务器不存在";
            }

            // 从 AccountSystemManager 同步登录信息
            accountManager.Reload();

            var allAccounts = accountManager.GetAllAccounts();
            AccountInfo matchedAccount = null;

            var directAccount = accountManager.GetAccount(serverId);
            if (directAccount != null && directAccount.UserId > 0)
            {
                matchedAccount = directAccount;
            }
            else
            {
                foreach (var account in allAccounts)
                {
                    if (account != null && account.UserId > 0 && !string.IsNullOrWhiteSpace(account.Domain))
                    {
                        try
                        {
                            Uri serverUri = new Uri(server.Url.TrimEnd('/'));
                            Uri accountUri = new Uri(account.Domain.TrimEnd('/'));
                            if (serverUri.Host.Equals(accountUri.Host, StringComparison.OrdinalIgnoreCase))
                            {
                                matchedAccount = account;
                                break;
                            }
                        }
                        catch { }
                    }
                }
            }

            if (matchedAccount != null)
            {
                server.UserId = matchedAccount.UserId;
                server.DisplayName = matchedAccount.DisplayName;
                server.Username = matchedAccount.Username;
                server.Password = matchedAccount.Password;
                server.ClientKeyXml = matchedAccount.ClientKeyXml;
            }

            if (!server.IsLoggedIn())
            {
                return "请先登录";
            }

            try
            {
                var api = await CreateRaceAPIAsync(server);
                var result = await api.GetDailyArticleAsync(raceId, server.UserId);

                if (result.Success)
                {
                    JObject data = result.Data;
                    JObject articleData = data["article"] as JObject;

                    string article = articleData?["content"]?.ToString() ?? "";
                    int articleId = articleData?["id"]?.ToObject<int>() ?? -1;

                    serverManager.SetCurrentRace(serverId, raceId);
                    serverManager.SetCurrentArticle(serverId, articleId);

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

            if (!server.IsLoggedIn())
            {
                MessageBox.Show("请先登录", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                string url = $"{server.Url}/api/race/history?raceId={raceId}&username={Uri.EscapeDataString(server.Username)}";
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
            if (server == null)
            {
                return "服务器不存在";
            }

            if (!server.IsLoggedIn())
            {
                return "未登录";
            }

            if (server.CurrentArticleId < 0)
            {
                return "未载文";
            }

            try
            {
                var scoreData = new RaceScoreData
                {
                    RaceId = raceId,
                    UserId = server.UserId,
                    Username = server.Username,
                    ArticleId = server.CurrentArticleId,
                    Date = DateTime.Now.ToString("yyyy-MM-dd"),
                    Speed = speed,
                    TimeCost = (int)time.TotalMilliseconds,
                    CharCount = charCount,
                    Keystroke = keystroke,
                    CodeLength = codeLength,
                    BackspaceCount = backspaceCount,
                    KeyCount = keyCount,
                    KeyAccuracy = keyAccuracy,
                    WordRate = wordRate,
                    InputMethod = inputMethod
                };

                var api = await CreateRaceAPIAsync(server);
                var result = await api.SubmitScoreAsync(scoreData);

                if (result.Success)
                {
                    return "提交成功";
                }
                else
                {
                    return $"提交失败: {result.Message}";
                }
            }
            catch (Exception ex)
            {
                return $"提交失败: {ex.Message}";
            }
        }
    }
}
