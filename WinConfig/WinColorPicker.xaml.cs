using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;

namespace TypeSunny
{
    public partial class WinColorPicker : Window
    {
        public string SelectedColor { get; private set; }
        private bool isUpdatingText = false;

        public WinColorPicker(string initialColor)
        {
            InitializeComponent();

            // 设置初始颜色
            SetColor(initialColor);
        }

        /// <summary>
        /// 设置颜色（支持 #F7F7F7 或 F7F7F7 格式）
        /// </summary>
        private void SetColor(string colorValue)
        {
            try
            {
                // 移除可能的#前缀
                string hexColor = colorValue.TrimStart('#');

                // 验证格式（6位十六进制）
                if (Regex.IsMatch(hexColor, "^[0-9A-Fa-f]{6}$"))
                {
                    isUpdatingText = true;
                    TbxHexColor.Text = hexColor.ToUpper();
                    isUpdatingText = false;

                    var color = (Color)ColorConverter.ConvertFromString("#" + hexColor);
                    PreviewBrush.Color = color;
                    SelectedColor = hexColor.ToUpper();
                }
            }
            catch
            {
                // 如果转换失败，不做处理
            }
        }

        /// <summary>
        /// 十六进制输入框文本变化事件
        /// </summary>
        private void TbxHexColor_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (isUpdatingText)
                return;

            string input = TbxHexColor.Text.TrimStart('#');

            // 自动转换为大写
            if (input != input.ToUpper())
            {
                int caretIndex = TbxHexColor.CaretIndex;
                isUpdatingText = true;
                TbxHexColor.Text = input.ToUpper();
                TbxHexColor.CaretIndex = caretIndex;
                isUpdatingText = false;
                input = input.ToUpper();
            }

            // 验证格式并更新预览
            if (Regex.IsMatch(input, "^[0-9A-Fa-f]{6}$"))
            {
                try
                {
                    var color = (Color)ColorConverter.ConvertFromString("#" + input);
                    PreviewBrush.Color = color;
                    SelectedColor = input.ToUpper();
                }
                catch
                {
                    // 转换失败，保持之前的颜色
                }
            }
        }

        /// <summary>
        /// 打开系统颜色拾色器
        /// </summary>
        private void BtnColorPicker_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Windows.Forms.ColorDialog colorDialog = new System.Windows.Forms.ColorDialog();

                // 设置当前颜色
                if (!string.IsNullOrEmpty(SelectedColor))
                {
                    var wpfColor = (Color)ColorConverter.ConvertFromString("#" + SelectedColor);
                    colorDialog.Color = System.Drawing.Color.FromArgb(wpfColor.A, wpfColor.R, wpfColor.G, wpfColor.B);
                }

                // 显示对话框
                if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    var selectedColor = colorDialog.Color;
                    string colorHex = selectedColor.R.ToString("X2") + selectedColor.G.ToString("X2") + selectedColor.B.ToString("X2");
                    SetColor(colorHex);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"颜色选择失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 确定按钮点击事件
        /// </summary>
        private void BtnOK_Click(object sender, RoutedEventArgs e)
        {
            string input = TbxHexColor.Text.TrimStart('#');

            // 验证格式
            if (!Regex.IsMatch(input, "^[0-9A-Fa-f]{6}$"))
            {
                MessageBox.Show("请输入有效的十六进制颜色值（如 F7F7F7）", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                TbxHexColor.Focus();
                return;
            }

            SelectedColor = input.ToUpper();
            DialogResult = true;
            Close();
        }

        /// <summary>
        /// 取消按钮点击事件
        /// </summary>
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
