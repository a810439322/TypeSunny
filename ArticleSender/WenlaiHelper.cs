using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TypeSunny;
using TypeSunny.Net;
using TypeSunny.Net.Http;
using Newtonsoft.Json.Linq;

namespace TypeSunny.ArticleSender
{
    /// <summary>
    /// 文来服务辅助类
    /// 提供文来服务的登录、注册、载文等功能
    /// </summary>
    public class WenlaiHelper
    {
        private ApiClient apiClient;
        private JwtAuthProvider jwtAuthProvider;
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
        /// 获取或创建 ApiClient 实例（使用 JwtAuthProvider）
        /// </summary>
        private async Task<ApiClient> GetInstanceAsync()
        {
            string serverUrl = Config.GetString("文来接口地址");

            if (string.IsNullOrWhiteSpace(serverUrl))
            {
                throw new Exception("文来接口地址未配置，请在设置中配置服务器地址");
            }

            serverUrl = serverUrl.TrimEnd('/');
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
                // 服务器地址变了，清掉旧 token，需要重新登录
                account.JwtToken = "";
                account.Cookies = "";
                accountManager.UpdateDomain(SERVICE_NAME, serverUrl);
                System.Diagnostics.Debug.WriteLine($"[文来] 服务器地址变更，已清除本地 token");
            }

            // 创建或更新 ApiClient 实例
            if (apiClient == null || apiClient.BaseUrl != serverUrl)
            {
                // 创建 JwtAuthProvider（地址变了 token 已清空，不会携带旧 token）
                jwtAuthProvider = new JwtAuthProvider();

                // 如果有保存的 JWT Token，恢复它
                if (!string.IsNullOrWhiteSpace(account.JwtToken))
                {
                    jwtAuthProvider.AccessToken = account.JwtToken;
                }

                apiClient = new ApiClient(serverUrl, jwtAuthProvider);

                // 初始化 ArticleFetcher（注入同一个 ApiClient）
                ArticleFetcher.Initialize(apiClient);
            }
            else
            {
                // 每次使用前检查最新的账号信息（支持多窗口同步）
                var latestAccount = accountManager.GetAccount(SERVICE_NAME);
                if (latestAccount != null)
                {
                    // 同步 JWT Token
                    if (!string.IsNullOrWhiteSpace(latestAccount.JwtToken) && jwtAuthProvider != null)
                    {
                        jwtAuthProvider.AccessToken = latestAccount.JwtToken;
                    }

                    // 同步用户ID
                    if (latestAccount.UserId > 0 && latestAccount.UserId != currentUserId)
                    {
                        currentUserId = latestAccount.UserId;
                        Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            loginStatusChangedCallback?.Invoke(true);
                            System.Diagnostics.Debug.WriteLine($"✓ 文来登录状态已更新: {latestAccount.DisplayName}");
                        }));
                    }
                }
            }

            return apiClient;
        }

        /// <summary>
        /// 检查是否已登录
        /// </summary>
        public bool IsLoggedIn()
        {
            accountManager.Reload();
            var account = accountManager.GetAccount(SERVICE_NAME);

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

            accountManager.Reload();
            var account = accountManager.GetAccount(SERVICE_NAME);

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
                Height = 350,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = owner,
                ResizeMode = ResizeMode.NoResize
            };

            var grid = new Grid();
            grid.Margin = new Thickness(20);

            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // 0: 用户名
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // 2: 密码
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // 4: 邮箱
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // 6: 验证码
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // 8: 自动登录
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // 10: 按钮

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

            var lblEmail = new Label { Content = "邮箱:" };
            Grid.SetRow(lblEmail, 4);
            grid.Children.Add(lblEmail);

            var txtEmail = new TextBox
            {
                Padding = new Thickness(5),
                Margin = new Thickness(70, 0, 0, 0)
            };
            Grid.SetRow(txtEmail, 4);
            grid.Children.Add(txtEmail);

            // 验证码行：输入框 + 发送按钮
            var lblCode = new Label { Content = "验证码:" };
            Grid.SetRow(lblCode, 6);
            grid.Children.Add(lblCode);

            var codePanel = new Grid();
            codePanel.Margin = new Thickness(70, 0, 0, 0);
            codePanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            codePanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var txtCode = new TextBox
            {
                Padding = new Thickness(5),
                Margin = new Thickness(0, 0, 5, 0)
            };
            Grid.SetColumn(txtCode, 0);
            codePanel.Children.Add(txtCode);

            var btnSendCode = new Button
            {
                Content = "发送",
                Width = 60,
                Height = 28
            };
            Grid.SetColumn(btnSendCode, 1);
            codePanel.Children.Add(btnSendCode);

            Grid.SetRow(codePanel, 6);
            grid.Children.Add(codePanel);

            var chkAutoLogin = new CheckBox
            {
                Content = "注册后自动登录",
                IsChecked = true,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(chkAutoLogin, 8);
            grid.Children.Add(chkAutoLogin);

            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetRow(btnPanel, 10);

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

            // 发送验证码
            btnSendCode.Click += async (s, args) =>
            {
                if (string.IsNullOrWhiteSpace(txtEmail.Text))
                {
                    MessageBox.Show("请输入邮箱", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                btnSendCode.IsEnabled = false;
                btnSendCode.Content = "发送中...";

                try
                {
                    var client = await GetInstanceAsync();
                    var response = await client.PostAsync("/api/auth/register/sendCode", new { email = txtEmail.Text.Trim() });

                    if (response.IsSuccess)
                    {
                        MessageBox.Show("验证码已发送，请查收邮箱", "提示", MessageBoxButton.OK, MessageBoxImage.Information);

                        // 倒计时 60 秒
                        int countdown = 60;
                        var timer = new System.Windows.Threading.DispatcherTimer();
                        timer.Interval = TimeSpan.FromSeconds(1);
                        timer.Tick += (ts, te) =>
                        {
                            countdown--;
                            if (countdown <= 0)
                            {
                                timer.Stop();
                                btnSendCode.Content = "发送";
                                btnSendCode.IsEnabled = true;
                            }
                            else
                            {
                                btnSendCode.Content = $"{countdown}s";
                            }
                        };
                        timer.Start();
                    }
                    else
                    {
                        MessageBox.Show(response.Msg ?? "发送验证码失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        btnSendCode.IsEnabled = true;
                        btnSendCode.Content = "发送";
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"发送验证码失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    btnSendCode.IsEnabled = true;
                    btnSendCode.Content = "发送";
                }
            };

            // 注册
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
                    var client = await GetInstanceAsync();

                    // 生成 RSA 密钥对（用于赛文加密）
                    var cryptoClient = new TypeSunny.Net.RaceCryptoClient();
                    string clientKeyXml = cryptoClient.GetClientKeyXml();
                    string publicKeyBase64 = cryptoClient.GetClientPublicKeyBase64();

                    var registerRequest = new RegisterRequest
                    {
                        Username = txtUsername.Text.Trim(),
                        Password = txtPassword.Password,
                        Email = txtEmail.Text.Trim(),
                        VerifyCode = txtCode.Text.Trim(),
                        PublicKey = publicKeyBase64
                    };

                    var response = await client.PostAsync<LoginResponse>("/api/auth/register", registerRequest);

                    if (response.IsSuccess)
                    {
                        // 如果选择自动登录
                        if (chkAutoLogin.IsChecked == true)
                        {
                            // 注册成功后直接登录
                            var loginRequest = new LoginRequest
                            {
                                Username = txtUsername.Text.Trim(),
                                Password = txtPassword.Password
                            };
                            var loginResponse = await client.PostAsync<LoginResponse>("/api/auth/login", loginRequest);

                            if (loginResponse.IsSuccess && loginResponse.Data != null)
                            {
                                string token = loginResponse.Data.Token;
                                int userId = loginResponse.Data.UserId;
                                string displayName = loginResponse.Data.DisplayName ?? loginResponse.Data.Username ?? txtUsername.Text;

                                // 设置 JWT Token
                                if (jwtAuthProvider != null)
                                    jwtAuthProvider.AccessToken = token;

                                string serverUrl = Config.GetString("文来接口地址");

                                accountManager.UpdateLoginInfo(SERVICE_NAME, txtUsername.Text.Trim(), txtPassword.Password,
                                    displayName, userId, null, clientKeyXml, serverUrl, token);

                                MessageBox.Show($"注册成功并已自动登录！欢迎 {displayName}", "提示",
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                            else
                            {
                                MessageBox.Show($"注册成功！但自动登录失败: {loginResponse.Msg}", "提示",
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
                        MessageBox.Show(response.Msg ?? "注册失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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

            // 清除 JWT Token
            if (jwtAuthProvider != null)
                jwtAuthProvider.ClearAuth();

            // 清空 ApiClient 实例，下次使用时会重新创建
            apiClient = null;
            jwtAuthProvider = null;
            ArticleFetcher.Initialize(null);  // 清掉 ArticleFetcher 的旧客户端，避免退出后复用失效连接

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

                    // 地址变了，清掉本地 token，需要重新登录
                    var account = accountManager.GetAccount(SERVICE_NAME);
                    if (account != null)
                    {
                        account.JwtToken = "";
                        account.Cookies = "";
                    }
                    accountManager.UpdateDomain(SERVICE_NAME, newServerUrl);

                    // 清空 ApiClient 实例，下次使用时会使用新地址
                    apiClient = null;
                    jwtAuthProvider = null;
                    ArticleFetcher.Initialize(null);  // 清掉 ArticleFetcher 的旧客户端

                    MessageBox.Show("服务器地址已更新，请重新登录", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
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
        public bool? ShowLoginDialog(Window owner)
        {
            var account = accountManager.GetAccount(SERVICE_NAME);

            var loginDialog = new Window
            {
                Title = "文来登录",
                Width = 350,
                Height = 230,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = owner,
                ResizeMode = ResizeMode.NoResize
            };

            var grid = new Grid();
            grid.Margin = new Thickness(20);

            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // 0: 用户名
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // 2: 密码
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // 4: 按钮
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // 6: 注册链接

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
                Margin = new Thickness(0, 0, 10, 0),
                IsDefault = true  // Enter 键自动触发登录
            };

            var btnCancel = new Button
            {
                Content = "取消",
                Width = 80,
                Height = 30,
                IsCancel = true  // Esc 键自动取消
            };

            // 用户名框按回车跳到密码框
            txtUsername.KeyDown += (s, args) =>
            {
                if (args.Key == System.Windows.Input.Key.Enter)
                {
                    args.Handled = true;
                    txtPassword.Focus();
                }
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
                    var client = await GetInstanceAsync();

                    // 使用新 API（JWT）登录
                    var loginRequest = new LoginRequest
                    {
                        Username = txtUsername.Text.Trim(),
                        Password = txtPassword.Password
                    };
                    var loginResponse = await client.PostAsync<LoginResponse>("/api/auth/login", loginRequest);

                    if (loginResponse.IsSuccess && loginResponse.Data != null)
                    {
                        // JWT 登录成功
                        string token = loginResponse.Data.Token;
                        int userId = loginResponse.Data.UserId;
                        string displayName = loginResponse.Data.DisplayName ?? loginResponse.Data.Username ?? txtUsername.Text;

                        // 设置 JWT Token
                        if (jwtAuthProvider != null)
                            jwtAuthProvider.AccessToken = token;

                        string serverUrl = Config.GetString("文来接口地址");

                        // 确保本地有客户端密钥对，没有则生成新的
                        var existingAccount = accountManager.GetAccount(SERVICE_NAME);
                        string clientKeyXml = existingAccount?.ClientKeyXml;
                        if (string.IsNullOrWhiteSpace(clientKeyXml))
                        {
                            var cryptoClient = new TypeSunny.Net.RaceCryptoClient();
                            clientKeyXml = cryptoClient.GetClientKeyXml();
                        }

                        accountManager.UpdateLoginInfo(SERVICE_NAME, txtUsername.Text.Trim(), txtPassword.Password,
                            displayName, userId, null, clientKeyXml, serverUrl, token);

                        // 登录后同步公钥到服务端（补录或更新）
                        try
                        {
                            var jwtAuth = new TypeSunny.Net.Http.JwtAuthProvider(token);
                            var tempApiClient = new TypeSunny.Net.Http.ApiClient(serverUrl, jwtAuth);
                            var raceApi = new TypeSunny.Net.RaceAPI(tempApiClient, clientKeyXml);
                            await raceApi.UpdatePublicKeyAsync();
                            System.Diagnostics.Debug.WriteLine("[文来] 登录后公钥同步完成");
                        }
                        catch (Exception pkEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"[文来] 登录后公钥同步失败（不影响登录）: {pkEx.Message}");
                        }

                        loginDialog.DialogResult = true;
                        loginDialog.Close();
                    }
                    else
                    {
                        string errorMsg = loginResponse.Msg ?? "登录失败";
                        MessageBox.Show(errorMsg, "登录失败", MessageBoxButton.OK, MessageBoxImage.Error);
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

            var btnRegisterLink = new Button
            {
                Content = "没有账号？去注册",
                HorizontalAlignment = HorizontalAlignment.Center,
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = System.Windows.Media.Brushes.Gray,
                Cursor = System.Windows.Input.Cursors.Hand,
                FontSize = 12,
                Padding = new Thickness(0)
            };
            Grid.SetRow(btnRegisterLink, 6);
            btnRegisterLink.Click += (s, args) =>
            {
                loginDialog.DialogResult = false;
                loginDialog.Close();
                ShowRegisterDialog(owner);
            };
            grid.Children.Add(btnRegisterLink);

            loginDialog.Content = grid;

            // 窗口加载后设置焦点：有已保存的账号密码则聚焦登录按钮，否则聚焦用户名输入框
            loginDialog.Loaded += (s, args) =>
            {
                if (!string.IsNullOrEmpty(txtUsername.Text) && !string.IsNullOrEmpty(txtPassword.Password))
                {
                    btnLogin.Focus();
                }
                else
                {
                    txtUsername.Focus();
                }
            };

            return loginDialog.ShowDialog();
        }
    }
}
