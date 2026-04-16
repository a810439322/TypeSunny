using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TypeSunny.Core;
using TypeSunny.Logs;
using System.Windows.Media.Animation;
using TextInfo = TypeSunny.Core.TextInfo;
using Colors = TypeSunny.Utils.Colors;

namespace TypeSunny.UI.Modes
{
    /// <summary>
    /// 字帖模式 - 去掉跟打框，用户直接在发文区上打字
    /// </summary>
    public class CopybookMode
    {
        private readonly MainWindow _main;
        private Canvas _overlay;
        private TextBox _inputCapture;
        private TextBlock _compositionText;
        private Border _cursor;
        private readonly List<FrameworkElement> _wrongCharHints = new List<FrameworkElement>();
        private int _currentIndex;
        private bool _isActive;
        private GridLength _savedTypingRowHeight;
        private double _savedTypingRowMinHeight;
        private GridLength _savedSplitterRowHeight;

        public bool IsActive => _isActive;
        public int CurrentIndex => _currentIndex;

        public CopybookMode(MainWindow main)
        {
            _main = main;
        }

        /// <summary>
        /// 激活字帖模式
        /// </summary>
        public void Enable()
        {
            if (_isActive) return;

            _isActive = true;
            _currentIndex = Score.InputWordCount;

            // 隐藏跟打区（只隐藏输入框，保留按钮区）
            _main.TbxInput.Visibility = Visibility.Collapsed;

            // 保存并清零跟打区 RowDefinition，否则 Row 仍占空间
            var parentGrid = (Grid)_main.typingAreaAndButtonsGrid.Parent;
            // Row 3 = GridSplitter, Row 4 = typingAreaAndButtons
            _savedSplitterRowHeight = parentGrid.RowDefinitions[3].Height;
            _savedTypingRowHeight = parentGrid.RowDefinitions[4].Height;
            _savedTypingRowMinHeight = parentGrid.RowDefinitions[4].MinHeight;
            parentGrid.RowDefinitions[3].Height = new GridLength(0);
            parentGrid.RowDefinitions[4].Height = new GridLength(0, GridUnitType.Auto);
            parentGrid.RowDefinitions[4].MinHeight = 0;
            _main.gridSplitterArticleTyping.Visibility = Visibility.Collapsed;

            double fs = MainWindow.DisplayFontSize;

            // 创建覆盖层 Canvas（透明背景使整个发文区可点击）
            _overlay = new Canvas();
            _overlay.IsHitTestVisible = true;
            _overlay.Background = Brushes.Transparent;
            _overlay.Cursor = Cursors.IBeam;
            _overlay.MouseDown += (s, ev) => { if (_inputCapture != null) _inputCapture.Focus(); };
            Panel.SetZIndex(_overlay, 5);

            // 让 Canvas 撑满父容器
            _overlay.HorizontalAlignment = HorizontalAlignment.Stretch;
            _overlay.VerticalAlignment = VerticalAlignment.Stretch;
            _overlay.SetBinding(Canvas.WidthProperty,
                new System.Windows.Data.Binding("ActualWidth") { Source = _main.BdDisplay.Child });
            _overlay.SetBinding(Canvas.HeightProperty,
                new System.Windows.Data.Binding("ActualHeight") { Source = _main.BdDisplay.Child });

            // 创建输入捕获 TextBox（完全透明，仅用于锚定 IME 候选框位置）
            _inputCapture = new TextBox();
            _inputCapture.Width = fs;
            _inputCapture.Height = fs * 0.6;
            _inputCapture.Background = Brushes.Transparent;
            _inputCapture.Foreground = Brushes.Transparent;
            _inputCapture.BorderThickness = new Thickness(0);
            _inputCapture.CaretBrush = Brushes.Transparent; // 隐藏原生光标
            _inputCapture.FontSize = fs * 0.5;
            _inputCapture.AcceptsReturn = false;
            _inputCapture.MaxLines = 1;
            _inputCapture.Padding = new Thickness(0);

            // 创建自定义光标（竖线，跟字一样高，带闪烁）
            _cursor = new Border();
            _cursor.Width = 2;
            _cursor.Height = fs;
            _cursor.Background = Colors.DisplayForeground;
            var blink = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromMilliseconds(500)));
            blink.AutoReverse = true;
            blink.RepeatBehavior = RepeatBehavior.Forever;
            _cursor.BeginAnimation(UIElement.OpacityProperty, blink);

            // 创建未上屏编码显示
            _compositionText = new TextBlock();
            _compositionText.FontSize = fs * 0.4;
            _compositionText.Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x99, 0xFF));
            _compositionText.Background = Brushes.Transparent;
            _compositionText.Padding = new Thickness(1, 0, 1, 0);
            _compositionText.Visibility = Visibility.Collapsed;

            _overlay.Children.Add(_inputCapture);
            _overlay.Children.Add(_compositionText);
            _overlay.Children.Add(_cursor);

            // 添加到 BdDisplay 内的 Grid
            var grid = (Grid)_main.BdDisplay.Child;
            grid.Children.Add(_overlay);

            // 注册事件
            _inputCapture.PreviewTextInput += OnTextInput;
            _inputCapture.PreviewKeyDown += OnPreviewKeyDown;
            _inputCapture.LostFocus += OnLostFocus;
            _inputCapture.GotFocus += OnGotFocus;
            TextCompositionManager.AddPreviewTextInputStartHandler(_inputCapture, OnCompositionStart);
            TextCompositionManager.AddPreviewTextInputUpdateHandler(_inputCapture, OnCompositionUpdate);

            // 定位到第一个字（需等布局完成后坐标才准确）
            ScheduleUpdatePosition();
            _inputCapture.Focus();
        }

        /// <summary>
        /// 关闭字帖模式
        /// </summary>
        public void Disable()
        {
            if (!_isActive) return;
            _isActive = false;

            if (_inputCapture != null)
            {
                _inputCapture.PreviewTextInput -= OnTextInput;
                _inputCapture.PreviewKeyDown -= OnPreviewKeyDown;
                _inputCapture.LostFocus -= OnLostFocus;
                _inputCapture.GotFocus -= OnGotFocus;
                TextCompositionManager.RemovePreviewTextInputStartHandler(_inputCapture, OnCompositionStart);
                TextCompositionManager.RemovePreviewTextInputUpdateHandler(_inputCapture, OnCompositionUpdate);
            }

            if (_overlay != null)
            {
                var grid = (Grid)_main.BdDisplay.Child;
                grid.Children.Remove(_overlay);
                _overlay = null;
            }

            _inputCapture = null;
            _compositionText = null;
            _cursor = null;
            _wrongCharHints.Clear();

            // 清除已打字的背景色
            for (int i = 0; i < TextInfo.Blocks.Count; i++)
                TextInfo.Blocks[i].Background = null;

            // 恢复跟打区
            var parentGrid = (Grid)_main.typingAreaAndButtonsGrid.Parent;
            parentGrid.RowDefinitions[3].Height = _savedSplitterRowHeight;
            parentGrid.RowDefinitions[4].Height = _savedTypingRowHeight;
            parentGrid.RowDefinitions[4].MinHeight = _savedTypingRowMinHeight;
            _main.gridSplitterArticleTyping.Visibility = Visibility.Visible;
            _main.TbxInput.Visibility = Visibility.Visible;
            _main.TbxInput.Focus();
        }

        /// <summary>
        /// 重置（载文时调用）
        /// </summary>
        public void Reset()
        {
            if (!_isActive) return;
            _currentIndex = 0;
            // 清除所有错字提示
            foreach (var hint in _wrongCharHints)
                _overlay.Children.Remove(hint);
            _wrongCharHints.Clear();
            _compositionText.Visibility = Visibility.Collapsed;
            if (_inputCapture != null)
            {
                _inputCapture.Text = "";
                _inputCapture.Focus();
            }
            // UpdatePosition 不在这里调，因为新 Blocks 还没创建
            // 由 ScheduleUpdatePosition 在布局完成后调用
        }

        /// <summary>
        /// 延迟到布局完成后更新光标位置（载文/Enable 后调用）
        /// </summary>
        public void ScheduleUpdatePosition()
        {
            if (!_isActive || _inputCapture == null) return;
            _main.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!_isActive || _currentIndex >= TextInfo.Blocks.Count) return;
                UpdatePosition();
                _inputCapture.Focus();
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        /// <summary>
        /// 同步错字提示的透明度（贪吃蛇模式调用）
        /// </summary>
        public void SyncWrongCharHintOpacity()
        {
            foreach (var hint in _wrongCharHints)
            {
                if (hint.Tag is int idx && idx < TextInfo.Blocks.Count)
                    hint.Opacity = TextInfo.Blocks[idx].Opacity;
            }
        }

        private void OnLostFocus(object sender, RoutedEventArgs e)
        {
            if (!_isActive || _inputCapture == null) return;
            // 立即隐藏光标，如果抢回焦点成功 OnGotFocus 会再显示
            if (_cursor != null) _cursor.Visibility = Visibility.Collapsed;
            _main.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!_isActive || _inputCapture == null) return;
                if (Keyboard.FocusedElement is System.Windows.Controls.Primitives.ButtonBase)
                    return;
                if (!_main.IsActive)
                    return;
                _inputCapture.Focus();
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        private void OnGotFocus(object sender, RoutedEventArgs e)
        {
            if (_cursor != null) _cursor.Visibility = Visibility.Visible;
        }

        private void OnCompositionStart(object sender, TextCompositionEventArgs e)
        {
            // 编码开始，记录 composing 状态
            if (!Score.IsComposing)
            {
                Score.IsComposing = true;
                Score.CompositionStartHit = Score.Hit;
            }
        }

        private void OnCompositionUpdate(object sender, TextCompositionEventArgs e)
        {
            if (!_isActive || _compositionText == null) return;

            string composition = e.TextComposition.CompositionText ?? "";
            if (string.IsNullOrEmpty(composition))
            {
                _compositionText.Visibility = Visibility.Collapsed;
            }
            else
            {
                _compositionText.Text = composition;
                _compositionText.Visibility = Visibility.Visible;
                UpdateCompositionPosition();
            }
        }

        private void OnTextInput(object sender, TextCompositionEventArgs e)
        {
            if (!_isActive) return;

            // 统计（废码、打词率、提交记录、标顶等）
            _main.HandleTextInputStats(e);

            // 空码/ESC取消
            if (string.IsNullOrEmpty(e.Text))
            {
                if (Config.GetBool("永不退避"))
                {
                    // 永不退避模式：空码时强制上屏一个空格，继续往下走逐字比对
                }
                else
                {
                    _compositionText.Visibility = Visibility.Collapsed;
                    e.Handled = true;
                    return;
                }
            }

            // 永不退避模式：回车强制当空格上屏
            if (e.Text == "\r")
            {
                if (Config.GetBool("永不退避"))
                {
                    // 不return，下面的逐字比对会处理
                }
                else
                {
                    e.Handled = true;
                    return;
                }
            }

            // 永不退避模式：空码/回车强制当空格处理
            string inputText = e.Text;
            if (Config.GetBool("永不退避") && (string.IsNullOrEmpty(inputText) || inputText == "\r"))
                inputText = " ";

            // 全局字数计数（正常模式由 TbxInput_TextChanged 处理）
            var si = new StringInfo(inputText);
            CounterLog.Buffer[0] += si.LengthInTextElements;

            // 最后一个字打错后再次输入，退回到最后一个字重新比对
            if (_currentIndex >= TextInfo.Words.Count
                && TextInfo.wordStates[TextInfo.Words.Count - 1] != WordStates.RIGHT)
            {
                _currentIndex = TextInfo.Words.Count - 1;
            }

            // 逐字比对
            for (int i = 0; i < si.LengthInTextElements && _currentIndex < TextInfo.Words.Count; i++)
            {
                string ch = si.SubstringByTextElements(i, 1);
                string expected = TextInfo.Words[_currentIndex];

                bool isCorrect = (ch == expected) || _main.IsLookingType;

                if (isCorrect)
                {
                    TextInfo.wordStates[_currentIndex] = WordStates.RIGHT;
                    if (!_main.IsBlindType && _currentIndex < TextInfo.Blocks.Count)
                        TextInfo.Blocks[_currentIndex].Background = Colors.CorrectBackground;
                }
                else
                {
                    TextInfo.wordStates[_currentIndex] = WordStates.WRONG;
                    if (!_main.IsBlindType && _currentIndex < TextInfo.Blocks.Count)
                    {
                        TextInfo.Blocks[_currentIndex].Background = Colors.IncorrectBackground;
                        ShowWrongCharHint(ch, _currentIndex);
                    }
                }

                _currentIndex++;
            }

            // 更新成绩
            Score.TotalWordCount = TextInfo.Words.Count;
            Score.InputWordCount = _currentIndex;
            Score.Wrong = 0;
            if (!_main.IsLookingType)
            {
                for (int i = 0; i < TextInfo.wordStates.Count; i++)
                {
                    if (TextInfo.wordStates[i] == WordStates.WRONG)
                        Score.Wrong++;
                }
            }

            // 隐藏编码显示
            _compositionText.Visibility = Visibility.Collapsed;

            // 清空输入框
            _inputCapture.Text = "";

            // 检查是否结束：必须打完且最后一个字正确才结算
            if (_currentIndex >= TextInfo.Words.Count
                && TextInfo.wordStates[TextInfo.Words.Count - 1] == WordStates.RIGHT)
            {
                _main.StopTyping();
            }
            else if (_currentIndex < TextInfo.Words.Count)
            {
                // 定位到下一个字
                UpdatePosition();

                // 贪吃蛇模式下更新字符显隐和滚动
                if (Config.GetBool("贪吃蛇模式"))
                    _main.SnakeModeUpdateFromCopybook(_currentIndex);
                else
                    ScrollToCurrentChar();

                // 更新字提显示
                _main.UpdateZiTi();
            }

            e.Handled = true;
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!_isActive) return;

            // 永不退避模式：拦截退格、Esc、Ctrl+Z
            if (Config.GetBool("永不退避"))
            {
                if (e.Key == Key.Back || e.Key == Key.Escape ||
                    (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control))
                {
                    e.Handled = true;
                    return;
                }
            }

            // 按键统计（击键、键法、选重、标顶、退格等）
            _main.HandleKeyDownStats(e);

            if (e.Key == Key.Back && string.IsNullOrEmpty(_inputCapture.Text))
            {
                // 没有未上屏编码时，退格回退到上一个字
                if (_currentIndex > 0)
                {
                    _currentIndex--;

                    // 清除上一个字的状态
                    TextInfo.wordStates[_currentIndex] = WordStates.NO_TYPE;
                    if (_currentIndex < TextInfo.Blocks.Count)
                        TextInfo.Blocks[_currentIndex].Background = null;

                    // 移除该位置的错字提示
                    RemoveWrongCharHint(_currentIndex);
                    Score.InputWordCount = _currentIndex;

                    UpdatePosition();
                    if (Config.GetBool("贪吃蛇模式"))
                        _main.SnakeModeUpdateFromCopybook(_currentIndex);
                    else
                        ScrollToCurrentChar();

                    // 更新字提显示
                    _main.UpdateZiTi();
                }
                e.Handled = true;
            }
        }

        private void ShowWrongCharHint(string wrongChar, int index)
        {
            if (_overlay == null || index >= TextInfo.Blocks.Count) return;

            double fs = MainWindow.DisplayFontSize;
            var border = new Border();
            border.Background = _main.BdDisplay.Background;
            border.BorderBrush = Colors.DisplayForeground;
            border.BorderThickness = new Thickness(1);
            border.CornerRadius = new CornerRadius(2);
            border.Padding = new Thickness(1, 0, 1, 0);
            border.Tag = index;

            var hint = new TextBlock();
            hint.Text = wrongChar;
            hint.FontSize = fs * 0.5;
            hint.Foreground = Colors.IncorrectBackground;
            hint.TextAlignment = TextAlignment.Center;

            border.Child = hint;
            _overlay.Children.Add(border);
            _wrongCharHints.Add(border);

            // 定位到当前字上方（相对于 Canvas 父容器 Grid）
            try
            {
                var grid = (Grid)_main.BdDisplay.Child;
                var block = TextInfo.Blocks[index];
                var pos = block.TranslatePoint(new Point(0, 0), grid);
                double hintHeight = hint.FontSize * 1.2;
                double wrongOffset = Config.GetDouble("字帖错字高度") * fs;
                double blockCenter = pos.X + block.ActualWidth / 2;

                border.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                double hintWidth = border.DesiredSize.Width;
                Canvas.SetLeft(border, blockCenter - hintWidth / 2);
                Canvas.SetTop(border, pos.Y - hintHeight - wrongOffset + 0.1 * fs);
            }
            catch { }
        }

        private void RemoveWrongCharHint(int index)
        {
            for (int i = _wrongCharHints.Count - 1; i >= 0; i--)
            {
                if (_wrongCharHints[i].Tag is int idx && idx == index)
                {
                    _overlay.Children.Remove(_wrongCharHints[i]);
                    _wrongCharHints.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 刷新所有错字提示的大小和位置（字体缩放时调用）
        /// </summary>
        public void RefreshWrongCharHints()
        {
            if (!_isActive || _overlay == null) return;
            double fs = MainWindow.DisplayFontSize;
            double wrongOffset = Config.GetDouble("字帖错字高度") * fs;
            var grid = (Grid)_main.BdDisplay.Child;
            foreach (var fe in _wrongCharHints)
            {
                var border = fe as Border;
                if (border == null) continue;
                if (!(border.Tag is int idx) || idx >= TextInfo.Blocks.Count) continue;
                try
                {
                    var hint = (TextBlock)border.Child;
                    hint.FontSize = fs * 0.5;
                    double hintHeight = hint.FontSize * 1.2;
                    var block = TextInfo.Blocks[idx];
                    var pos = block.TranslatePoint(new Point(0, 0), grid);
                    double blockCenter = pos.X + block.ActualWidth / 2;
                    border.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    double hintWidth = border.DesiredSize.Width;
                    Canvas.SetLeft(border, blockCenter - hintWidth / 2);
                    Canvas.SetTop(border, pos.Y - hintHeight - wrongOffset + 0.1 * fs);
                }
                catch { }
            }
        }

        private void UpdatePosition()
        {
            if (_inputCapture == null || _currentIndex >= TextInfo.Blocks.Count || TextInfo.Blocks.Count == 0)
                return;

            try
            {
                var grid = (Grid)_main.BdDisplay.Child;
                var block = TextInfo.Blocks[_currentIndex];
                var pos = block.TranslatePoint(new Point(0, 0), grid);
                double x = pos.X;
                double y = pos.Y;
                double fs = MainWindow.DisplayFontSize;
                double compositionOffset = (Config.GetDouble("字帖编码高度") + 0.2) * fs;
                double candidateOffset = Config.GetDouble("字帖候选框高度") * fs;

                // InputCapture 控制 IME 候选框位置
                Canvas.SetLeft(_inputCapture, x);
                Canvas.SetTop(_inputCapture, y + 1.0 * fs + candidateOffset);

                // 自定义光标定位到当前字左侧
                if (_cursor != null)
                {
                    _cursor.Height = fs;
                    Canvas.SetLeft(_cursor, x - 2);
                    Canvas.SetTop(_cursor, y + 0.3 * fs);
                }

                // CompositionText 贴当前字下沿
                Canvas.SetLeft(_compositionText, x);
                Canvas.SetTop(_compositionText, y + block.ActualHeight - 0.25 * fs + compositionOffset);
            }
            catch { }
        }

        private void UpdateCompositionPosition()
        {
            if (_compositionText == null || _currentIndex >= TextInfo.Blocks.Count || TextInfo.Blocks.Count == 0)
                return;

            try
            {
                var grid = (Grid)_main.BdDisplay.Child;
                var block = TextInfo.Blocks[_currentIndex];
                var pos = block.TranslatePoint(new Point(0, 0), grid);
                double fs = MainWindow.DisplayFontSize;
                double compositionOffset = Config.GetDouble("字帖编码高度") * fs;
                double x = pos.X;
                double y = pos.Y + block.ActualHeight - 0.25 * fs + compositionOffset;

                Canvas.SetLeft(_compositionText, x);
                Canvas.SetTop(_compositionText, y);
            }
            catch { }
        }

        private void ScrollToCurrentChar()
        {
            if (_currentIndex >= TextInfo.Blocks.Count || TextInfo.Blocks.Count == 0) return;

            try
            {
                double currentPosY =
                    TextInfo.Blocks[_currentIndex].TranslatePoint(new Point(0, 0), TextInfo.Blocks[0]).Y
                    + TextInfo.Blocks[_currentIndex].ActualHeight / 2;

                double targetOffset = _main.CalculateScrollOffset(currentPosY);
                _main.SmoothScrollTo(targetOffset);
            }
            catch { }
        }
    }
}