using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace TypeSunny
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        // 静态构造函数：在类第一次使用前执行（比OnStartup更早）
        static App()
        {
            // 使用 SystemDefault 让操作系统自动选择最佳TLS版本（包括TLS 1.3）
            // 这比手动指定版本更好，会自动适配服务器要求
            System.Net.ServicePointManager.SecurityProtocol = (System.Net.SecurityProtocolType)0x3000 | // TLS 1.3 (0x3000)
                                                               System.Net.SecurityProtocolType.Tls12 |
                                                               System.Net.SecurityProtocolType.Tls11 |
                                                               System.Net.SecurityProtocolType.Tls;

            // 禁用 Expect: 100-Continue（这个经常导致HTTPS连接问题）
            System.Net.ServicePointManager.Expect100Continue = false;

            // 设置连接限制
            System.Net.ServicePointManager.DefaultConnectionLimit = 1024;

            // 禁用连接池的Nagle算法（可能导致延迟问题）
            System.Net.ServicePointManager.UseNagleAlgorithm = false;

            System.Diagnostics.Debug.WriteLine($"[App静态构造] TLS协议已设置: {System.Net.ServicePointManager.SecurityProtocol}");
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 再次确认 TLS 1.3/1.2 已启用
            System.Net.ServicePointManager.SecurityProtocol = (System.Net.SecurityProtocolType)0x3000 | // TLS 1.3
                                                               System.Net.SecurityProtocolType.Tls12 |
                                                               System.Net.SecurityProtocolType.Tls11 |
                                                               System.Net.SecurityProtocolType.Tls;

            // 临时禁用SSL证书验证（用于调试，生产环境应该移除）
            // 如果服务器SSL证书有问题，这个可以绕过验证
            System.Net.ServicePointManager.ServerCertificateValidationCallback =
                (sender, certificate, chain, sslPolicyErrors) =>
                {
                    // 记录证书验证问题
                    if (sslPolicyErrors != System.Net.Security.SslPolicyErrors.None)
                    {
                        System.Diagnostics.Debug.WriteLine($"SSL证书验证问题: {sslPolicyErrors}");
                        System.Diagnostics.Debug.WriteLine($"证书主题: {certificate?.Subject}");
                    }
                    return true; // 暂时接受所有证书
                };

            // 添加全局异常处理
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            DispatcherUnhandledException += App_DispatcherUnhandledException;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = e.ExceptionObject as Exception;
            string errorMessage = ex != null ? ex.ToString() : "未知错误";

            MessageBox.Show(
                $"程序发生严重错误：\n\n{errorMessage}\n\n请联系开发者反馈此问题。",
                "晴跟打 - 严重错误",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            // 构建详细的错误信息（包括内部异常）
            var errorDetails = new System.Text.StringBuilder();
            errorDetails.AppendLine("错误消息：");
            errorDetails.AppendLine(e.Exception.Message);
            errorDetails.AppendLine();

            // 添加内部异常信息
            if (e.Exception.InnerException != null)
            {
                errorDetails.AppendLine("内部错误：");
                errorDetails.AppendLine(e.Exception.InnerException.Message);
                errorDetails.AppendLine();
                errorDetails.AppendLine("内部错误堆栈：");
                errorDetails.AppendLine(e.Exception.InnerException.StackTrace);
                errorDetails.AppendLine();
            }

            errorDetails.AppendLine("错误堆栈：");
            errorDetails.AppendLine(e.Exception.StackTrace);

            // 同时输出到调试窗口
            System.Diagnostics.Debug.WriteLine("=== 程序异常 ===");
            System.Diagnostics.Debug.WriteLine(errorDetails.ToString());
            System.Diagnostics.Debug.WriteLine("================");

            MessageBox.Show(
                $"程序发生错误：\n\n{errorDetails}\n\n程序将尝试继续运行。",
                "晴跟打 - 错误",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            e.Handled = true; // 标记异常已处理，防止程序崩溃
        }
    }
}
