using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using TypeSunny;

namespace Net
{
    public class JbsHelper
    {
        private JBS jbs;
        private MenuItem menuItemLogin;
        private MenuItem menuItemLoadArticle;
        private JiSuCupHelper jiSuCupHelper;

        public JbsHelper(MenuItem loginMenuItem, MenuItem loadArticleMenuItem)
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

        public void SetJiSuCupHelper(JiSuCupHelper helper)
        {
            this.jiSuCupHelper = helper;
        }

        public JBS GetInstance()
        {
            if (jbs == null)
            {
                jbs = new JBS(Config.GetString("极速用户名"), Config.GetString("极速密码"));
            }
            return jbs;
        }

        public void UpdateLoginStatus()
        {
            if (menuItemLogin == null)
            {
                return;
            }

            string displayName = Config.GetString("极速显示名称");
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                menuItemLogin.Header = $"已登录: {displayName}";
            }
            else
            {
                menuItemLogin.Header = "登录";
            }
        }

        public void UpdateArticleButtonStatus()
        {
            if (menuItemLoadArticle == null)
            {
                return;
            }

            // 先检查是否登录
            string displayName = Config.GetString("极速显示名称");
            if (string.IsNullOrWhiteSpace(displayName))
            {
                // 未登录，禁用载文按钮
                menuItemLoadArticle.Header = "载文(请先登录)";
                menuItemLoadArticle.IsEnabled = false;
                return;
            }

            // 已登录，再检查今日是否已完成
            string lastDate = Config.GetString("极速最后载文日期");
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
            Config.Set("极速最后载文日期", today);
            Config.WriteConfig(0);
            UpdateArticleButtonStatus();
        }

        public void ShowLoginDialog(Window owner)
        {
            var loginDialog = new Window
            {
                Title = "极速登录",
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
                Text = Config.GetString("极速用户名"),
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
                Password = Config.GetString("极速密码"),
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

                if (jbs == null)
                {
                    jbs = new JBS();
                }

                var response = jbs.Login(txtUsername.Text, txtPassword.Password);

                if (response.ContainsKey("error") && response["error"].ToString() == "0")
                {
                    // 保存锦标赛配置
                    Config.Set("极速用户名", txtUsername.Text);
                    Config.Set("极速密码", txtPassword.Password);
                    Config.Set("极速显示名称", jbs.Username);

                    // 同步极速杯配置（账号一体）
                    Config.Set("极速杯用户名", txtUsername.Text);
                    Config.Set("极速杯密码", txtPassword.Password);
                    Config.Set("极速杯显示名称", jbs.Username);

                    Config.WriteConfig(0);

                    UpdateLoginStatus();

                    // 同步更新极速杯登录状态
                    if (jiSuCupHelper != null)
                    {
                        jiSuCupHelper.UpdateLoginStatus();
                    }

                    MessageBox.Show($"登录成功！欢迎 {jbs.Username}", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
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
                System.Diagnostics.Process.Start("https://www.52dazi.cn/competitionRank/2");
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
            if (jbs == null)
            {
                return "未登录";
            }

            return jbs.SendScore(speed, hitRate, kpw, time, corrections, backspace, keyCount, accuracy, ciRatio, wrong, inputMethod);
        }
    }
}
