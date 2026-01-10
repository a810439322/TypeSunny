using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TypeSunny;
using TypeSunny.Net;
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
                    // 使用服务器保存的客户端密钥对（如果有）
                    var api = new RaceAPI(server.Url, server.ClientKeyXml);
                    await api.InitializeAsync();
                    var result = await api.LoginAsync(txtUsername.Text, txtPassword.Password);

                    if (result.Success)
                    {
                        // 解析返回的用户信息 - 根据新API文档，结构是 {"user": {"id": ..., "username": ...}}
                        JObject data = result.Data;
                        JObject userData = data["user"] as JObject;

                        int userId = userData?["id"]?.ToObject<int>() ?? -1;
                        string username = userData?["username"]?.ToString() ?? txtUsername.Text;

                        // 保存客户端密钥对（可能是新生成的）
                        server.ClientKeyXml = api.GetClientKeyXml();

                        // 更新服务器登录信息
                        server.Username = txtUsername.Text;
                        server.Password = txtPassword.Password;
                        serverManager.UpdateServerLogin(serverId, userId, txtUsername.Text, username);

                        // 保存到 AccountSystemManager（支持同域名自动登录）
                        accountManager.UpdateLoginInfo(
                            serverId,  // 使用 serverId 作为 serviceName
                            txtUsername.Text,
                            txtPassword.Password,
                            username,
                            userId,
                            api.GetCookiesAsString(),
                            api.GetClientKeyXml()
                        );

                        System.Diagnostics.Debug.WriteLine($"✓ 赛文登录已保存到AccountSystemManager: {username} (serverId={serverId})");

                        // 登录成功后，刷新该服务器的赛文列表
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
                    // 使用服务器保存的客户端密钥对（如果有）
                    var api = new RaceAPI(server.Url, server.ClientKeyXml);
                    await api.InitializeAsync();
                    var result = await api.RegisterAsync(txtUsername.Text, txtPassword.Password);

                    if (result.Success)
                    {
                        // 保存客户端密钥对（注册时生成的）
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

            // 从 AccountSystemManager 同步登录信息（支持同域名自动登录）
            // 遍历所有账号，找到域名匹配的账号
            var allAccounts = accountManager.GetAllAccounts();
            AccountInfo matchedAccount = null;

            foreach (var account in allAccounts)
            {
                if (account != null && account.UserId > 0 && !string.IsNullOrWhiteSpace(account.Domain))
                {
                    // 检查域名是否匹配
                    try
                    {
                        Uri serverUri = new Uri(server.Url.TrimEnd('/'));
                        Uri accountUri = new Uri(account.Domain.TrimEnd('/'));
                        if (serverUri.Host == accountUri.Host)
                        {
                            matchedAccount = account;
                            break;
                        }
                    }
                    catch { }
                }
            }

            if (matchedAccount != null)
            {
                // 同步账号信息到 server 对象
                if (server.UserId != matchedAccount.UserId || server.DisplayName != matchedAccount.DisplayName)
                {
                    server.UserId = matchedAccount.UserId;
                    server.DisplayName = matchedAccount.DisplayName;
                    server.Username = matchedAccount.Username;
                    server.Password = matchedAccount.Password;
                    server.ClientKeyXml = matchedAccount.ClientKeyXml;

                    System.Diagnostics.Debug.WriteLine($"✓ 赛文API已同步登录信息: {matchedAccount.DisplayName} (UserId={matchedAccount.UserId})");
                }
            }

            if (!server.IsLoggedIn())
            {
                return "请先登录";
            }

            try
            {
                var api = new RaceAPI(server.Url, server.ClientKeyXml);
                await api.InitializeAsync();
                var result = await api.GetDailyArticleAsync(raceId, server.UserId);

                if (result.Success)
                {
                    JObject data = result.Data;
                    JObject articleData = data["article"] as JObject;

                    string article = articleData?["content"]?.ToString() ?? "";
                    int articleId = articleData?["id"]?.ToObject<int>() ?? -1;

                    // 保存当前文章ID和赛文ID
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
                string url = $"{server.Url}/api/race/history?race_id={raceId}&username={Uri.EscapeDataString(server.Username)}";
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
                string url = $"{server.Url}/api/race/leaderboard?race_id={raceId}";
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

                var api = new RaceAPI(server.Url, server.ClientKeyXml);
                await api.InitializeAsync();
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
