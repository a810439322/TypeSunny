using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TypeSunny;
using TypeSunny.Net;
using Newtonsoft.Json.Linq;

namespace TypeSunny.Net
{
    public class RaceHelper
    {
        private RaceAPI raceAPI;
        private AccountSystemManager accountManager;
        private MenuItem menuItemLogin;
        private MenuItem menuItemLoadArticle;
        private MenuItem menuItemRegister;
        private MenuItem menuItemHistory;
        private MenuItem menuItemLeaderboard;
        private int currentUserId = -1;
        private string currentUsername = "";
        private const string SERVICE_NAME = "赛文";

        /// <summary>
        /// 比较两个URL的域名是否相同（协议+主机+端口）
        /// </summary>
        private bool IsSameDomain(string url1, string url2)
        {
            if (string.IsNullOrWhiteSpace(url1) || string.IsNullOrWhiteSpace(url2))
                return false;

            try
            {
                Uri uri1 = new Uri(url1.TrimEnd('/'));
                Uri uri2 = new Uri(url2.TrimEnd('/'));
                return uri1.GetLeftPart(UriPartial.Authority) == uri2.GetLeftPart(UriPartial.Authority);
            }
            catch
            {
                return false;
            }
        }

        public RaceHelper(MenuItem loginMenuItem, MenuItem loadArticleMenuItem, MenuItem registerMenuItem, MenuItem historyMenuItem, MenuItem leaderboardMenuItem)
        {
            this.menuItemLogin = loginMenuItem;
            this.menuItemLoadArticle = loadArticleMenuItem;
            this.menuItemRegister = registerMenuItem;
            this.menuItemHistory = historyMenuItem;
            this.menuItemLeaderboard = leaderboardMenuItem;

            // 初始化账号管理器
            this.accountManager = new AccountSystemManager();

            // 初始化RaceAPI
            string serverUrl = Config.GetString("赛文服务器地址");

            if (string.IsNullOrWhiteSpace(serverUrl))
            {
                throw new Exception("赛文服务器地址未配置，请在设置中配置服务器地址");
            }

            System.Diagnostics.Debug.WriteLine($"[赛文] 构造函数使用服务器地址: {serverUrl}");

            var account = accountManager.GetAccount(SERVICE_NAME);
            if (account == null)
            {
                account = new AccountInfo { ServiceName = SERVICE_NAME, Domain = serverUrl };
                accountManager.SaveAccount(account);
            }

            raceAPI = new RaceAPI(serverUrl, account.ClientKeyXml);

            // 加载Cookie
            if (!string.IsNullOrWhiteSpace(account.Cookies))
            {
                raceAPI.LoadCookiesFromString(account.Cookies);
            }
        }

        public async Task<RaceAPI> GetInstanceAsync()
        {
            if (raceAPI == null)
            {
                string serverUrl = Config.GetString("赛文服务器地址");

                if (string.IsNullOrWhiteSpace(serverUrl))
                {
                    throw new Exception("赛文服务器地址未配置，请在设置中配置服务器地址");
                }

                System.Diagnostics.Debug.WriteLine($"[赛文] GetInstanceAsync使用服务器地址: {serverUrl}");

                var account = accountManager.GetAccount(SERVICE_NAME);
                if (account == null)
                {
                    account = new AccountInfo { ServiceName = SERVICE_NAME, Domain = serverUrl };
                    accountManager.SaveAccount(account);
                }
                else if (account.Domain != serverUrl)
                {
                    accountManager.UpdateDomain(SERVICE_NAME, serverUrl);
                }

                raceAPI = new RaceAPI(serverUrl, account.ClientKeyXml);

                // 加载Cookie
                if (!string.IsNullOrWhiteSpace(account.Cookies))
                {
                    raceAPI.LoadCookiesFromString(account.Cookies);
                }
            }
            else
            {
                // ✅ 每次使用前重新加载Cookie（以防其他服务已登录并同步了Cookie）
                var account = accountManager.GetAccount(SERVICE_NAME);
                if (account != null && !string.IsNullOrWhiteSpace(account.Cookies))
                {
                    string serverUrl = Config.GetString("赛文服务器地址");

                    if (string.IsNullOrWhiteSpace(serverUrl))
                    {
                        throw new Exception("赛文服务器地址未配置");
                    }

                    // ✅ 检查域名是否匹配（只有同域名才重新加载Cookie）
                    if (IsSameDomain(account.Domain, serverUrl))
                    {
                        raceAPI.LoadCookiesFromString(account.Cookies);
                        System.Diagnostics.Debug.WriteLine($"✓ 赛文重新加载Cookie（域名匹配，可能是文来登录后同步的）");

                        // ✅ 如果账号信息有更新（比如用户ID变了），更新登录状态显示
                        if (account.UserId != currentUserId && account.UserId > 0)
                        {
                            currentUserId = account.UserId;
                            currentUsername = account.DisplayName;
                            // 在UI线程更新菜单显示
                            System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                UpdateLoginStatus();
                                System.Diagnostics.Debug.WriteLine($"✓ 赛文登录状态已更新: {currentUsername}");
                            }));
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠ 赛文跳过Cookie加载（域名不匹配: {account.Domain} vs {serverUrl}）");
                    }
                }
            }

            // 确保已初始化
            await raceAPI.InitializeAsync();
            return raceAPI;
        }

        public void UpdateLoginStatus()
        {
            if (menuItemLogin == null)
            {
                return;
            }

            var account = accountManager.GetAccount(SERVICE_NAME);
            if (account != null && !string.IsNullOrWhiteSpace(account.DisplayName))
            {
                menuItemLogin.Header = $"已登录: {account.DisplayName}";
                currentUsername = account.DisplayName;
                currentUserId = account.UserId;
            }
            else
            {
                // 兼容旧版配置
                string displayName = Config.GetString("赛文显示名称");
                if (!string.IsNullOrWhiteSpace(displayName))
                {
                    menuItemLogin.Header = $"已登录: {displayName}";
                    currentUsername = displayName;

                    // 尝试获取用户ID
                    string userIdStr = Config.GetString("赛文用户ID");
                    if (int.TryParse(userIdStr, out int userId))
                    {
                        currentUserId = userId;
                    }
                }
                else
                {
                    menuItemLogin.Header = "登录";
                    currentUsername = "";
                    currentUserId = -1;
                }
            }
        }

        public void UpdateArticleButtonStatus()
        {
            if (menuItemLoadArticle == null)
            {
                return;
            }

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

        public void ShowRegisterDialog(Window owner)
        {
            var registerDialog = new Window
            {
                Title = "赛文注册",
                Width = 350,
                Height = 250,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = owner,
                ResizeMode = ResizeMode.NoResize
            };

            var grid = new Grid();
            grid.Margin = new Thickness(20);

            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
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

            var chkAutoLogin = new CheckBox
            {
                Content = "注册后自动登录",
                IsChecked = true,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(chkAutoLogin, 4);
            grid.Children.Add(chkAutoLogin);

            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetRow(btnPanel, 6);

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
                    var api = await GetInstanceAsync();
                    var result = await api.RegisterAsync(txtUsername.Text, txtPassword.Password);

                    if (result.Success)
                    {
                        // 如果选择自动登录，则直接登录
                        if (chkAutoLogin.IsChecked == true)
                        {
                            var loginResult = await api.LoginAsync(txtUsername.Text, txtPassword.Password);
                            if (loginResult.Success)
                            {
                                // 解析用户信息
                                JObject data = loginResult.Data;
                                int userId = data["user"]?["id"]?.ToObject<int>() ?? -1;
                                string username = data["user"]?["username"]?.ToString() ?? txtUsername.Text;

                                // 获取赛文服务器地址（用于同域名同步到文来）
                                string serverUrl = Config.GetString("赛文服务器地址");

                                // 保存到账号管理器（传入serverUrl以确保Domain正确设置，从而实现同域名同步）
                                accountManager.UpdateLoginInfo(SERVICE_NAME, txtUsername.Text, txtPassword.Password,
                                    username, userId, api.GetCookiesAsString(), api.GetClientKeyXml(), serverUrl);

                                // 兼容旧版配置
                                Config.Set("赛文用户名", txtUsername.Text);
                                Config.Set("赛文密码", txtPassword.Password);
                                Config.Set("赛文显示名称", username);
                                Config.Set("赛文用户ID", userId.ToString());
                                Config.WriteConfig(0);

                                currentUserId = userId;
                                currentUsername = username;
                                UpdateLoginStatus();

                                MessageBox.Show($"注册成功并已自动登录！欢迎 {username}", "提示",
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                            else
                            {
                                MessageBox.Show($"注册成功！但自动登录失败: {loginResult.Message}", "提示",
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                        }
                        else
                        {
                            MessageBox.Show($"注册成功！请使用用户名和密码登录", "提示",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                        }

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

        public void ShowLoginDialog(Window owner)
        {
            var account = accountManager.GetAccount(SERVICE_NAME);

            var loginDialog = new Window
            {
                Title = "赛文登录",
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
                Text = account?.Username ?? Config.GetString("赛文用户名"),
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
                Password = account?.Password ?? Config.GetString("赛文密码"),
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
                    var api = await GetInstanceAsync();
                    var result = await api.LoginAsync(txtUsername.Text, txtPassword.Password);

                    if (result.Success)
                    {
                        // 解析返回的用户信息
                        JObject data = result.Data;
                        int userId = data["user"]?["id"]?.ToObject<int>() ?? -1;
                        string username = data["user"]?["username"]?.ToString() ?? txtUsername.Text;

                        // 保存到账号管理器
                        accountManager.UpdateLoginInfo(SERVICE_NAME, txtUsername.Text, txtPassword.Password,
                            username, userId, api.GetCookiesAsString(), api.GetClientKeyXml());

                        // 兼容旧版配置
                        Config.Set("赛文用户名", txtUsername.Text);
                        Config.Set("赛文密码", txtPassword.Password);
                        Config.Set("赛文显示名称", username);
                        Config.Set("赛文用户ID", userId.ToString());
                        Config.WriteConfig(0);

                        currentUserId = userId;
                        currentUsername = username;

                        UpdateLoginStatus();

                        MessageBox.Show($"登录成功！欢迎 {username}", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                        loginDialog.DialogResult = true;
                        loginDialog.Close();
                    }
                    else
                    {
                        // 检查是否是服务器端未实现的错误
                        string errorMsg = result.Message;
                        if (errorMsg.Contains("create_web_session") || errorMsg.Contains("has no attribute"))
                        {
                            MessageBox.Show(
                                "登录失败：服务器端尚未实现Cookie会话功能\n\n" +
                                "请联系服务器管理员更新服务器代码，添加 create_web_session 方法。\n\n" +
                                "技术说明：登录接口需要在成功后调用 db.create_web_session(user_id) 来创建会话Cookie。",
                                "服务器功能缺失",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning
                            );
                        }
                        else
                        {
                            MessageBox.Show(errorMsg, "登录失败", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                        btnLogin.IsEnabled = true;
                        btnLogin.Content = "登录";
                    }
                }
                catch (Exception ex)
                {
                    string errorMsg = ex.Message;
                    if (errorMsg.Contains("create_web_session") || errorMsg.Contains("has no attribute"))
                    {
                        MessageBox.Show(
                            "登录失败：服务器端尚未实现Cookie会话功能\n\n" +
                            "请联系服务器管理员更新服务器代码，添加 create_web_session 方法。\n\n" +
                            "技术说明：登录接口需要在成功后调用 db.create_web_session(user_id) 来创建会话Cookie。",
                            "服务器功能缺失",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning
                        );
                    }
                    else
                    {
                        MessageBox.Show($"登录失败: {errorMsg}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
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

        public async Task<string> LoadDailyArticle()
        {
            // 每次载文前重新获取最新的账号信息（确保currentUserId是最新的）
            var account = accountManager.GetAccount(SERVICE_NAME);
            if (account != null)
            {
                if (account.UserId > 0)
                {
                    currentUserId = account.UserId;
                    currentUsername = account.DisplayName ?? account.Username ?? "";
                }
            }

            // 如果还是没有有效的UserId，尝试从旧版配置读取
            if (currentUserId < 0)
            {
                string userIdStr = Config.GetString("赛文用户ID");
                if (int.TryParse(userIdStr, out int userId) && userId > 0)
                {
                    currentUserId = userId;
                    currentUsername = Config.GetString("赛文显示名称") ?? Config.GetString("赛文用户名") ?? "";
                }
            }

            if (currentUserId < 0)
            {
                return "未登录";
            }

            try
            {
                var api = await GetInstanceAsync();
                // 使用默认raceId=1（兼容旧版单赛文模式）
                var result = await api.GetDailyArticleAsync(1, currentUserId);

                if (result.Success)
                {
                    JObject data = result.Data;
                    JObject articleData = data["article"] as JObject;

                    string article = articleData?["content"]?.ToString() ?? "";
                    int articleId = articleData?["id"]?.ToObject<int>() ?? -1;

                    // 保存当前文章ID
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

                if (string.IsNullOrWhiteSpace(serverUrl))
                {
                    MessageBox.Show("赛文服务器地址未配置，请在设置中配置", "配置错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 添加race_id参数（默认为1）
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

                if (string.IsNullOrWhiteSpace(serverUrl))
                {
                    MessageBox.Show("赛文服务器地址未配置，请在设置中配置", "配置错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 添加race_id参数（默认为1）
                string url = $"{serverUrl}/leaderboard?race_id=1";
                System.Diagnostics.Process.Start(url);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法打开浏览器: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public async Task<string> SubmitScore(
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
            // 每次提交前重新获取最新的账号信息（确保currentUserId是最新的）
            var account = accountManager.GetAccount(SERVICE_NAME);
            if (account != null)
            {
                if (account.UserId > 0)
                {
                    currentUserId = account.UserId;
                    currentUsername = account.DisplayName ?? account.Username ?? "";
                }
            }

            // 如果还是没有有效的UserId，尝试从旧版配置读取
            if (currentUserId < 0)
            {
                string userIdStr = Config.GetString("赛文用户ID");
                if (int.TryParse(userIdStr, out int userId) && userId > 0)
                {
                    currentUserId = userId;
                    currentUsername = Config.GetString("赛文显示名称") ?? Config.GetString("赛文用户名") ?? "";
                }
            }

            if (currentUserId < 0 || string.IsNullOrWhiteSpace(currentUsername))
            {
                return "未登录";
            }

            string articleIdStr = Config.GetString("赛文当前文章ID");
            if (!int.TryParse(articleIdStr, out int articleId) || articleId < 0)
            {
                return "未载文";
            }

            try
            {
                var scoreData = new RaceScoreData
                {
                    RaceId = 1,  // 默认raceId=1（兼容旧版单赛文模式）
                    UserId = currentUserId,
                    Username = currentUsername,
                    ArticleId = articleId,
                    Date = DateTime.Now.ToString("yyyy-MM-dd"),
                    Speed = speed,
                    TimeCost = (int)time.TotalMilliseconds,  // 注意：使用毫秒
                    CharCount = charCount,
                    Keystroke = keystroke,
                    CodeLength = codeLength,
                    BackspaceCount = backspaceCount,
                    KeyCount = keyCount,
                    KeyAccuracy = keyAccuracy,
                    WordRate = wordRate,
                    InputMethod = inputMethod
                };

                var api = await GetInstanceAsync();
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
