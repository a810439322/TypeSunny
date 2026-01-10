using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using TypeSunny;

namespace Net
{
    public class JiSuCupHelper
    {
        private JiSuCup jiSuCup;
        private MenuItem menuItemLogin;
        private MenuItem menuItemLoadArticle;
        private JbsHelper jbsHelper;

        public JiSuCupHelper(MenuItem loginMenuItem, MenuItem loadArticleMenuItem)
        {
            this.menuItemLogin = loginMenuItem;
            this.menuItemLoadArticle = loadArticleMenuItem;
        }

        /// <summary>
        /// 更新菜单项引用（用于菜单重新创建后）
        /// </summary>
        public void SetMenuItems(MenuItem loginMenuItem, MenuItem loadArticleMenuItem)
        {
            this.menuItemLogin = loginMenuItem;
            this.menuItemLoadArticle = loadArticleMenuItem;
        }

        public void SetJbsHelper(JbsHelper helper)
        {
            this.jbsHelper = helper;
        }

        public JiSuCup GetInstance()
        {
            if (jiSuCup == null)
            {
                jiSuCup = new JiSuCup(Config.GetString("极速杯用户名"), Config.GetString("极速杯密码"));
            }
            return jiSuCup;
        }

        public void UpdateLoginStatus()
        {
            if (menuItemLogin == null)
            {
                return;
            }

            string displayName = Config.GetString("极速杯显示名称");
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                menuItemLogin.Header = $"已登录: {displayName}";
            }
            else
            {
                menuItemLogin.Header = "登录";
            }

            // 同时更新载文按钮状态
            UpdateLoadArticleButtonStatus();
        }

        /// <summary>
        /// 更新载文按钮的启用/禁用状态
        /// </summary>
        public void UpdateLoadArticleButtonStatus()
        {
            if (menuItemLoadArticle == null)
            {
                return;
            }

            string displayName = Config.GetString("极速杯显示名称");
            if (string.IsNullOrWhiteSpace(displayName))
            {
                // 未登录，禁用载文按钮
                menuItemLoadArticle.Header = "载文(请先登录)";
                menuItemLoadArticle.IsEnabled = false;
            }
            else
            {
                // 已登录，启用载文按钮
                menuItemLoadArticle.Header = "载文";
                menuItemLoadArticle.IsEnabled = true;
            }
        }

        public void ShowLoginDialog(Window owner)
        {
            var loginDialog = new Window
            {
                Title = "极速杯登录",
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
                Text = Config.GetString("极速杯用户名"),
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
                Password = Config.GetString("极速杯密码"),
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

            btnLogin.Click += (s, args) =>
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

                if (jiSuCup == null)
                {
                    jiSuCup = new JiSuCup();
                }

                var response = jiSuCup.Login(txtUsername.Text, txtPassword.Password);

                if (response.ContainsKey("error") && response["error"].ToString() == "0")
                {
                    // 保存极速杯配置
                    Config.Set("极速杯用户名", txtUsername.Text);
                    Config.Set("极速杯密码", txtPassword.Password);
                    Config.Set("极速杯显示名称", jiSuCup.Username);

                    // 同步锦标赛配置（账号一体）
                    Config.Set("极速用户名", txtUsername.Text);
                    Config.Set("极速密码", txtPassword.Password);
                    Config.Set("极速显示名称", jiSuCup.Username);

                    Config.WriteConfig(0);

                    UpdateLoginStatus();

                    // 同步更新锦标赛登录状态
                    if (jbsHelper != null)
                    {
                        jbsHelper.UpdateLoginStatus();
                        jbsHelper.UpdateArticleButtonStatus();
                    }

                    MessageBox.Show($"登录成功！欢迎 {jiSuCup.Username}", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    loginDialog.DialogResult = true;
                    loginDialog.Close();
                }
                else
                {
                    string errorMsg = "登录失败";
                    if (response.ContainsKey("msg"))
                    {
                        errorMsg = response["msg"].ToString();
                    }
                    MessageBox.Show(errorMsg, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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

        public void OpenRanking()
        {
            try
            {
                System.Diagnostics.Process.Start("https://www.52dazi.cn/competitionRank/0");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法打开浏览器: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public string SubmitScore(
            double speed,
            double hitRate,
            double kpw,
            TimeSpan time,
            int corrections,
            int backspace,
            int keyCount,
            double accuracy,
            double ciRatio,
            int wrong,
            string inputMethod)
        {
            if (jiSuCup == null)
            {
                return "未登录";
            }

            return jiSuCup.SendScore(speed, hitRate, kpw, time, corrections, backspace, keyCount, accuracy, ciRatio, wrong, inputMethod);
        }
    }
}
