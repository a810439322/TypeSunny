using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TypeSunny.UI;
using TypeSunny.Utils;

namespace TypeSunny
{
    public partial class WinArticle : Window
    {
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_LEFT = 10;
        private const int HT_RIGHT = 11;
        private const int HT_TOP = 12;
        private const int HT_BOTTOM = 15;

        public WinArticle()
        {
            InitializeComponent();
        }

        private void InitTxtFiles()
        {
            CbFiles.ItemsSource = ArticleManager.Articles.Keys;

            string cur = ArticleManager.Title;
            if (CbFiles.Items.Contains(cur))
            {
                CbFiles.SelectedItem = cur;
            }
            CbFiles.Items.Refresh();
        }

        bool AllLoaded;
        private void InitControls()
        {
            TbSectionSize.Text = ArticleManager.SectionSize.ToString();
            CbFilter.IsChecked = ArticleManager.EnableFilter;
            CbRemoveSpace.IsChecked = ArticleManager.RemoveSpace;
            CbSegmentMode.SelectedIndex = ArticleManager.SegmentMode == ArticleManager.SegmentModeManual ? 1 : 0;
        }

        public void UpdateDisplay()
        {
            SldProgress.Maximum = ArticleManager.MaxIndex;

            if (SldProgress.Value != ArticleManager.Index)
                SldProgress.Value = ArticleManager.Index;

            TbTest.Text = ArticleManager.GetPreviewCurrentSection();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyThemeColors();
            ApplyCurrentLogo();
            InitTxtFiles();
            InitControls();
            UpdateDisplay();
            AllLoaded = true;
        }

        public void RefreshTheme()
        {
            ApplyThemeColors();
            ApplyCurrentLogo();
        }

        private void ApplyCurrentLogo()
        {
            try
            {
                string currentLogo = Config.GetString("当前Logo");
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "ico", $"{currentLogo}.ico");
                if (!File.Exists(iconPath))
                {
                    Debug.WriteLine($"文章管理器Logo文件不存在: {iconPath}");
                    return;
                }

                var iconUri = new Uri(iconPath, UriKind.Absolute);
                this.Icon = new BitmapImage(iconUri);
                TitleBarIcon.Source = new BitmapImage(iconUri);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"文章管理器应用Logo失败: {ex.Message}");
            }
        }

        private void ApplyThemeColors()
        {
            try
            {
                string windowBgColor = Config.GetString("窗体背景色");
                string windowFgColor = Config.GetString("窗体字体色");
                string displayBgColor = Config.GetString("跟打区背景色");
                string displayFgColor = Config.GetString("跟打区字体色");
                string accentColor = Config.GetString("标题栏进度条颜色");

                var bgBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#" + windowBgColor));
                var fgBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#" + windowFgColor));
                var displayBgBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#" + displayBgColor));
                var displayFgBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#" + displayFgColor));
                var accentColorBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#" + accentColor));

                var borderBrush = ThemeColorHelper.CreateSubtleBorderBrush(bgBrush);

                var toolbarBgBrush = new SolidColorBrush(Color.FromRgb(
                    (byte)Math.Max(0, bgBrush.Color.R - 15),
                    (byte)Math.Max(0, bgBrush.Color.G - 15),
                    (byte)Math.Max(0, bgBrush.Color.B - 15)
                ));

                var buttonBgBrush = new SolidColorBrush(Color.FromRgb(
                    (byte)Math.Min(255, bgBrush.Color.R + 20),
                    (byte)Math.Min(255, bgBrush.Color.G + 20),
                    (byte)Math.Min(255, bgBrush.Color.B + 20)
                ));

                var buttonHoverBrush = new SolidColorBrush(Color.FromRgb(
                    (byte)Math.Min(255, buttonBgBrush.Color.R + 15),
                    (byte)Math.Min(255, buttonBgBrush.Color.G + 15),
                    (byte)Math.Min(255, buttonBgBrush.Color.B + 15)
                ));

                this.Resources["WindowBackground"] = bgBrush;
                this.Resources["WindowBorderBrush"] = borderBrush;
                this.Resources["TextForeground"] = fgBrush;
                this.Resources["ToolbarBackground"] = toolbarBgBrush;
                this.Resources["ContentBackground"] = displayBgBrush;
                this.Resources["ContentForeground"] = displayFgBrush;
                this.Resources["BorderBrush"] = borderBrush;
                this.Resources["ButtonBackground"] = buttonBgBrush;
                this.Resources["ButtonHoverBackground"] = buttonHoverBrush;
                this.Resources["ButtonPressedBackground"] = buttonHoverBrush;
                this.Resources["AccentColor"] = accentColorBrush;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"文章管理器应用主题颜色失败: {ex.Message}");
            }
        }

        private void Reload()
        {
            ArticleManager.ReadFiles();
            AllLoaded = false;
            InitTxtFiles();
            InitControls();
            UpdateDisplay();
            AllLoaded = true;
        }

        // 标题栏拖动
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2) return;
            DragMove();
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        // 边框拖动调整大小
        private void ResizeBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var hwndSource = PresentationSource.FromVisual(this) as HwndSource;
            if (hwndSource == null) return;

            var border = sender as FrameworkElement;
            if (border == null) return;

            int direction = 0;
            switch (border.Name)
            {
                case "ResizeTop": direction = HT_TOP; break;
                case "ResizeBottom": direction = HT_BOTTOM; break;
                case "ResizeLeft": direction = HT_LEFT; break;
                case "ResizeRight": direction = HT_RIGHT; break;
            }

            if (direction != 0)
            {
                ReleaseCapture();
                SendMessage(hwndSource.Handle, WM_NCLBUTTONDOWN, (IntPtr)direction, IntPtr.Zero);
            }
        }

        // 每段字数控制
        private void TbSectionSize_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!AllLoaded) return;
            if (int.TryParse(TbSectionSize.Text, out int val) && val >= 10)
            {
                ArticleManager.SectionSize = val;
            }
        }

        private void SectionSizeUp(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(TbSectionSize.Text, out int val))
            {
                val += 50;
                if (val > 5000) val = 5000;
                TbSectionSize.Text = val.ToString();
            }
        }

        private void SectionSizeDown(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(TbSectionSize.Text, out int val))
            {
                val -= 50;
                if (val < 10) val = 10;
                TbSectionSize.Text = val.ToString();
            }
        }

        private void CbFiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AllLoaded)
            {
                ArticleManager.Title = CbFiles.SelectedItem.ToString();
            }
        }

        private void Search()
        {
            if (TbSearch.Text == "")
                return;

            int startindex = Math.Min(ArticleManager.Progress + ArticleManager.SectionSize, ArticleManager.TotalSize - 1);

            int s = ArticleManager.Search(TbSearch.Text, startindex);
            if (s >= 0)
            {
                ArticleManager.Progress = s;
            }
            else if (ArticleManager.Progress > 0)
            {
                if (MessageBox.Show("查找不到，是否从头开始查找？", "查找", MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK)
                {
                    int s0 = ArticleManager.Search(TbSearch.Text, 0);
                    if (s0 >= 0)
                    {
                        ArticleManager.Progress = s0;
                    }
                    else
                    {
                        MessageBox.Show("查找不到", "查找", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            else
            {
                MessageBox.Show("查找不到", "查找", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            Search();
        }

        public void Prev()
        {
            MainWindow.Current?.RecordLocalArticleContinuation(next: false);
            ArticleManager.PrevSection();
        }

        private void BtnPrev_Click(object sender, RoutedEventArgs e)
        {
            Prev();
        }

        public void Next()
        {
            MainWindow.Current?.RecordLocalArticleContinuation(next: true);
            ArticleManager.NextSection();
        }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            Next();
        }

        private void BtnReload_Click(object sender, RoutedEventArgs e)
        {
            Reload();
        }

        private void BtnOpen_Click(object sender, RoutedEventArgs e)
        {
            string folderPath = AppDomain.CurrentDomain.BaseDirectory + "文章";
            Process.Start(folderPath);
        }

        private async Task SendCurrentArticle()
        {
            var mainWindow = MainWindow.Current ?? App.Current.MainWindow as MainWindow;
            if (mainWindow != null)
                await mainWindow.SendArticle();
        }

        private async void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            await SendCurrentArticle();
        }

        private void CbFilter_Checked(object sender, RoutedEventArgs e)
        {
            if (AllLoaded)
            {
                ArticleManager.EnableFilter = (CbFilter.IsChecked == true);
                Reload();
            }
        }

        private void CbFilter_Unchecked(object sender, RoutedEventArgs e)
        {
            if (AllLoaded)
            {
                ArticleManager.EnableFilter = (CbFilter.IsChecked == true);
                Reload();
            }
        }

        private void CbRemoveSpace_Checked(object sender, RoutedEventArgs e)
        {
            if (AllLoaded)
            {
                ArticleManager.RemoveSpace = (CbRemoveSpace.IsChecked == true);
                Reload();
            }
        }

        private void CbRemoveSpace_Unchecked(object sender, RoutedEventArgs e)
        {
            if (AllLoaded)
            {
                ArticleManager.RemoveSpace = (CbRemoveSpace.IsChecked == true);
                Reload();
            }
        }

        private void CbSegmentMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!AllLoaded)
                return;

            string mode = CbSegmentMode.SelectedIndex == 1
                ? ArticleManager.SegmentModeManual
                : ArticleManager.SegmentModeAuto;

            ArticleManager.SegmentMode = mode;

            if (mode == ArticleManager.SegmentModeManual)
            {
                MessageBox.Show(
                    "本地文章手动换段模式：\n\n" +
                    "打完当前段后不会自动发送下一段。\n" +
                    "可按 Ctrl+P 发下一段，Ctrl+O 发上一段。\n" +
                    "也可以点击底部 < / > 按钮翻页。",
                    "本地文章换段模式",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private void TbSearch_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Search();
                e.Handled = true;
            }
        }

        private async void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (IsModifiedShortcut(e))
                return;

            switch (e.Key)
            {
                case Key.Escape:
                    if (WindowEscapeBehavior.HandleEscape(this))
                        e.Handled = true;
                    break;

                case Key.Enter:
                    if (Keyboard.FocusedElement == TbSearch)
                        return;

                    e.Handled = true;
                    await SendCurrentArticle();
                    break;

                case Key.Left:
                    if (IsTextInputFocused())
                        return;

                    e.Handled = true;
                    Prev();
                    break;

                case Key.Right:
                    if (IsTextInputFocused())
                        return;

                    e.Handled = true;
                    Next();
                    break;

                case Key.Tab:
                    e.Handled = true;
                    MoveArticleMenuFocus();
                    break;
            }
        }

        private static bool IsModifiedShortcut(KeyEventArgs e)
        {
            var modifiers = Keyboard.Modifiers;
            if (e.Key == Key.Tab)
                modifiers &= ~ModifierKeys.Shift;

            return (modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Windows)) != 0;
        }

        private bool IsTextInputFocused()
        {
            return Keyboard.FocusedElement == TbSearch || Keyboard.FocusedElement == TbSectionSize;
        }

        private void MoveArticleMenuFocus()
        {
            if (Keyboard.FocusedElement is UIElement focusedElement && focusedElement.IsKeyboardFocusWithin)
            {
                if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                    focusedElement.MoveFocus(new TraversalRequest(FocusNavigationDirection.Previous));
                else
                    focusedElement.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            }
            else
            {
                CbFiles.Focus();
            }
        }

        private void SldProgress_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (AllLoaded)
            {
                ArticleManager.Progress = (ArticleManager.SectionSize * (int)(SldProgress.Value - 1));
            }
        }
    }
}
