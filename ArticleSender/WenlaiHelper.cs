using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TypeSunny;
using TypeSunny.Net;
using Newtonsoft.Json.Linq;

namespace TypeSunny.ArticleSender
{
    /// <summary>
    /// 文来服务辅助类
    /// 提供文来服务的登录、注册、载文等功能
    /// </summary>
    public class WenlaiHelper
    {
        private RaceAPI raceAPI;
        private AccountSystemManager accountManager;
        private const string SERVICE_NAME = "文来";
        private int currentUserId = -1;

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

        private Action<bool> loginStatusChangedCallback;

        public WenlaiHelper()
        {
            accountManager = new AccountSystemManager();
        }

        /// <summary>
        /// 设置登录状态变化回调
        /// </summary>
        public void SetLoginStatusChangedCallback(Action<bool> callback)
        {
            loginStatusChangedCallback = callback;
        }

        /// <summary>
        /// 获取或创建RaceAPI实例
        /// </summary>
        private async Task<RaceAPI> GetInstanceAsync()
        {
            string serverUrl = Config.GetString("文来接口地址");

            if (string.IsNullOrWhiteSpace(serverUrl))
            {
                throw new Exception("文来接口地址未配置，请在设置中配置服务器地址");
            }

            System.Diagnostics.Debug.WriteLine($"[文来] 使用服务器地址: {serverUrl}");

            // 从账号管理器获取账号信息
            var account = accountManager.GetAccount(SERVICE_NAME);
            if (account == null)
            {
                account = new AccountInfo { ServiceName = SERVICE_NAME, Domain = serverUrl };
                accountManager.SaveAccount(account);
            }
            else if (account.Domain != serverUrl)
            {
                // 更新域名
                accountManager.UpdateDomain(SERVICE_NAME, serverUrl);
            }

            // 创建或更新RaceAPI实例
            if (raceAPI == null || raceAPI.GetType().GetField("serverUrl",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.GetValue(raceAPI)?.ToString() != serverUrl)
            {
                raceAPI = new RaceAPI(serverUrl, account.ClientKeyXml);

                // 设置密钥不匹配时的自动重新登录回调
                raceAPI.OnKeyMismatchCallback = async () =>
                {
                    System.Diagnostics.Debug.WriteLine($"[文来] 密钥不匹配，触发自动重新登录");
                    var (success, cookies, keyXml) = await accountManager.ReloginAsync(SERVICE_NAME, serverUrl);
                    if (success)
                    {
                        // 更新本地状态
                        currentUserId = accountManager.GetAccount(SERVICE_NAME)?.UserId ?? currentUserId;
                        // 触发UI更新
                        System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            loginStatusChangedCallback?.Invoke(true);
                        }));
                    }
                    return (cookies, keyXml);
                };

                // 加载Cookie
                if (!string.IsNullOrWhiteSpace(account.Cookies))
                {
                    raceAPI.LoadCookiesFromString(account.Cookies);
                }
            }
            else
            {
                // ✅ 每次使用前重新加载Cookie（以防其他服务已登录并同步了Cookie）
                var latestAccount = accountManager.GetAccount(SERVICE_NAME);
                if (latestAccount != null && !string.IsNullOrWhiteSpace(latestAccount.Cookies))
                {
                    // ✅ 检查域名是否匹配（只有同域名才重新加载Cookie）
                    if (IsSameDomain(latestAccount.Domain, serverUrl))
                    {
                        raceAPI.LoadCookiesFromString(latestAccount.Cookies);
                        System.Diagnostics.Debug.WriteLine($"✓ 文来重新加载Cookie（域名匹配，可能是赛文登录后同步的）");

                        // ✅ 如果账号信息有更新（比如用户ID变了），触发回调通知UI更新
                        if (latestAccount.UserId != currentUserId && latestAccount.UserId > 0)
                        {
                            currentUserId = latestAccount.UserId;
                            // 在UI线程更新登录状态显示
                            System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                loginStatusChangedCallback?.Invoke(true);
                                System.Diagnostics.Debug.WriteLine($"✓ 文来登录状态已更新: {latestAccount.DisplayName}");
                            }));
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠ 文来跳过Cookie加载（域名不匹配: {latestAccount.Domain} vs {serverUrl}）");
                    }
                }
            }

            // 确保已初始化
            await raceAPI.InitializeAsync();
            return raceAPI;
        }

        /// <summary>
        /// 检查是否已登录
        /// </summary>
        public bool IsLoggedIn()
        {
            // ✅ 重新加载配置以获取最新的登录状态（支持多窗口同步）
            accountManager.Reload();
            var account = accountManager.GetAccount(SERVICE_NAME);

            // ✅ 同步更新 currentUserId（如果账号信息有变化）
            if (account != null && account.UserId > 0 && account.UserId != currentUserId)
            {
                currentUserId = account.UserId;
                System.Diagnostics.Debug.WriteLine($"✓ WenlaiHelper.IsLoggedIn() 同步更新 currentUserId: {currentUserId}");
            }

            bool result = account != null && !string.IsNullOrWhiteSpace(account.Username);
            System.Diagnostics.Debug.WriteLine($"WenlaiHelper.IsLoggedIn(): account={account != null}, Username={account?.Username}, Result={result}");
            return result;
        }

        /// <summary>
        /// 获取当前登录用户名
        /// </summary>
        public string GetCurrentUsername()
        {
            System.Diagnostics.Debug.WriteLine($"WenlaiHelper.GetCurrentUsername() 被调用");

            // ✅ 重新加载配置以获取最新的登录状态（支持多窗口同步）
            accountManager.Reload();
            var account = accountManager.GetAccount(SERVICE_NAME);

            // ✅ 同步更新 currentUserId（如果账号信息有变化）
            if (account != null && account.UserId > 0 && account.UserId != currentUserId)
            {
                currentUserId = account.UserId;
                System.Diagnostics.Debug.WriteLine($"✓ WenlaiHelper.GetCurrentUsername() 同步更新 currentUserId: {currentUserId}");
            }

            System.Diagnostics.Debug.WriteLine($"  GetAccount 返回: account={(account != null ? "不为null" : "null")}");
            if (account != null)
            {
                System.Diagnostics.Debug.WriteLine($"  account.DisplayName='{account.DisplayName}', account.Username='{account.Username}'");
            }
            string result = account?.DisplayName ?? account?.Username ?? "";
            System.Diagnostics.Debug.WriteLine($"  最终返回值: '{result}'");
            return result;
        }

        /// <summary>
        /// 显示注册对话框
        /// </summary>
        public void ShowRegisterDialog(Window owner)
        {
            var registerDialog = new Window
            {
                Title = "文来注册",
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
                        // 保存客户端密钥
                        string clientKeyXml = api.GetClientKeyXml();
                        string cookies = api.GetCookiesAsString();

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

                                // 获取文来服务器地址（用于同域名同步到赛文）
                                string serverUrl = Config.GetString("文来接口地址");

                                // 更新账号信息（传入serverUrl以确保Domain正确设置，从而实现同域名同步）
                                accountManager.UpdateLoginInfo(SERVICE_NAME, txtUsername.Text, txtPassword.Password,
                                    username, userId, api.GetCookiesAsString(), api.GetClientKeyXml(), serverUrl);

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

        /// <summary>
        /// 退出登录
        /// </summary>
        public void Logout()
        {
            // 清除账号信息
            accountManager.ClearAccount(SERVICE_NAME);

            // 清空ArticleFetcher的Cookie
            string serverUrl = Config.GetString("文来接口地址");
            if (!string.IsNullOrWhiteSpace(serverUrl))
            {
                ArticleFetcher.ClearCookies(serverUrl);
            }

            // 清空RaceAPI实例，下次使用时会重新创建
            raceAPI = null;

            System.Diagnostics.Debug.WriteLine("✓ 文来已退出登录");
        }

        /// <summary>
        /// 显示服务器设置对话框
        /// </summary>
        public void ShowServerSettingsDialog(Window owner)
        {
            var settingsDialog = new Window
            {
                Title = "文来服务器设置",
                Width = 400,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = owner,
                ResizeMode = ResizeMode.NoResize
            };

            var grid = new Grid();
            grid.Margin = new Thickness(20);

            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var lblServerUrl = new Label { Content = "服务器地址:" };
            Grid.SetRow(lblServerUrl, 0);
            grid.Children.Add(lblServerUrl);

            var txtServerUrl = new TextBox
            {
                Text = Config.GetString("文来接口地址"),
                Padding = new Thickness(5),
                Margin = new Thickness(90, 0, 0, 0)
            };
            Grid.SetRow(txtServerUrl, 0);
            grid.Children.Add(txtServerUrl);

            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetRow(btnPanel, 2);

            var btnSave = new Button
            {
                Content = "保存",
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

            btnSave.Click += (s, args) =>
            {
                string newServerUrl = txtServerUrl.Text.Trim();

                if (string.IsNullOrWhiteSpace(newServerUrl))
                {
                    MessageBox.Show("请输入服务器地址", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 验证URL格式
                try
                {
                    var uri = new Uri(newServerUrl);
                    if (uri.Scheme != "http" && uri.Scheme != "https")
                    {
                        MessageBox.Show("服务器地址必须以 http:// 或 https:// 开头", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }
                catch
                {
                    MessageBox.Show("服务器地址格式不正确", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string oldServerUrl = Config.GetString("文来接口地址");
                if (newServerUrl != oldServerUrl)
                {
                    Config.Set("文来接口地址", newServerUrl);
                    Config.WriteConfig(0);

                    // 更新账号域名
                    accountManager.UpdateDomain(SERVICE_NAME, newServerUrl);

                    // 清空RaceAPI实例，下次使用时会使用新地址
                    raceAPI = null;

                    MessageBox.Show("服务器地址已更新", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                settingsDialog.DialogResult = true;
                settingsDialog.Close();
            };

            btnCancel.Click += (s, args) =>
            {
                settingsDialog.DialogResult = false;
                settingsDialog.Close();
            };

            btnPanel.Children.Add(btnSave);
            btnPanel.Children.Add(btnCancel);
            grid.Children.Add(btnPanel);

            settingsDialog.Content = grid;
            settingsDialog.ShowDialog();
        }

        /// <summary>
        /// 显示登录对话框
        /// </summary>
        /// <returns>登录是否成功</returns>
        public bool? ShowLoginDialog(Window owner)
        {
            var account = accountManager.GetAccount(SERVICE_NAME);

            var loginDialog = new Window
            {
                Title = "文来登录",
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
                Text = account?.Username ?? "",
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
                Password = account?.Password ?? "",
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

                        // 获取文来服务器地址（用于同域名同步到赛文）
                        string serverUrl = Config.GetString("文来接口地址");

                        // 保存登录信息（传入serverUrl以确保Domain正确设置，从而实现同域名同步）
                        accountManager.UpdateLoginInfo(SERVICE_NAME, txtUsername.Text, txtPassword.Password,
                            username, userId, api.GetCookiesAsString(), api.GetClientKeyXml(), serverUrl);

                        MessageBox.Show($"登录成功！欢迎 {username}", "提示",
                            MessageBoxButton.OK, MessageBoxImage.Information);
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
            return loginDialog.ShowDialog();
        }
    }
}
