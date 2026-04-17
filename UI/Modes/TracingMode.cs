using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using TypeSunny.Core;
using TypeSunny.Logs;
using TextInfo = TypeSunny.Core.TextInfo;
using Colors = TypeSunny.Utils.Colors;

namespace TypeSunny.UI.Modes
{
    /// <summary>
    /// 临摹模式 - 每行原文下方显示用户输入的镜像行
    /// </summary>
    public class TracingMode
    {
        private readonly MainWindow _main;
        private Canvas _overlay;
        private TextBox _inputCapture;
        private TextBlock _compositionText;
        private Border _cursor;
        private int _currentIndex;
        private bool _isActive;
        private bool _pendingMirrorRebuild;
        private GridLength _savedTypingRowHeight;
        private double _savedTypingRowMinHeight;
        private GridLength _savedSplitterRowHeight;

        // 镜像行：与 TextInfo.Blocks 一一对应
        private readonly List<TextBlock> _mirrorBlocks = new List<TextBlock>();
        // 行分组信息：每个元素是该行包含的 TextInfo.Blocks 索引列表
        private List<List<int>> _lineGroups = new List<List<int>>();

        public bool IsActive => _isActive;
        public int CurrentIndex => _currentIndex;

        /// <summary>
        /// 获取当前镜像块相对于第一个原文块的位置（供速度跟随使用）
        /// </summary>
        public bool TryGetMirrorBlockPosition(int index, out double left, out double top, out double height)
        {
            left = top = height = 0;
            if (index < 0 || index >= _mirrorBlocks.Count || TextInfo.Blocks.Count == 0) return false;
            try
            {
                var mirrorBlock = _mirrorBlocks[index];
                var firstBlock = TextInfo.Blocks[0];
                var pos = mirrorBlock.TranslatePoint(new Point(0, 0), firstBlock);
                left = pos.X;
                top = pos.Y;
                height = mirrorBlock.ActualHeight;
                return true;
            }
            catch { return false; }
        }

        public TracingMode(MainWindow main)
        {
            _main = main;
        }

        /// <summary>
        /// 激活临摹模式
        /// </summary>
        public void Enable()
        {
            if (_isActive) return;

            _isActive = true;
            _currentIndex = Score.InputWordCount;

            // 隐藏跟打区
            _main.TbxInput.Visibility = Visibility.Collapsed;
            var parentGrid = (Grid)_main.typingAreaAndButtonsGrid.Parent;
            _savedSplitterRowHeight = parentGrid.RowDefinitions[3].Height;
            _savedTypingRowHeight = parentGrid.RowDefinitions[4].Height;
            _savedTypingRowMinHeight = parentGrid.RowDefinitions[4].MinHeight;
            parentGrid.RowDefinitions[3].Height = new GridLength(0);
            parentGrid.RowDefinitions[4].Height = new GridLength(0, GridUnitType.Auto);
            parentGrid.RowDefinitions[4].MinHeight = 0;
            _main.gridSplitterArticleTyping.Visibility = Visibility.Collapsed;

            double fs = MainWindow.DisplayFontSize;

            // 创建覆盖层 Canvas
            _overlay = new Canvas();
            _overlay.IsHitTestVisible = true;
            _overlay.Background = Brushes.Transparent;
            _overlay.Cursor = Cursors.IBeam;
            _overlay.MouseDown += (s, ev) => { if (_inputCapture != null) _inputCapture.Focus(); };
            Panel.SetZIndex(_overlay, 5);
            _overlay.HorizontalAlignment = HorizontalAlignment.Stretch;
            _overlay.VerticalAlignment = VerticalAlignment.Stretch;
            _overlay.SetBinding(Canvas.WidthProperty,
                new System.Windows.Data.Binding("ActualWidth") { Source = _main.BdDisplay.Child });
            _overlay.SetBinding(Canvas.HeightProperty,
                new System.Windows.Data.Binding("ActualHeight") { Source = _main.BdDisplay.Child });

            // 输入捕获 TextBox（透明，仅锚定 IME）
            _inputCapture = new TextBox();
            _inputCapture.Width = fs;
            _inputCapture.Height = fs * 0.6;
            _inputCapture.Background = Brushes.Transparent;
            _inputCapture.Foreground = Brushes.Transparent;
            _inputCapture.BorderThickness = new Thickness(0);
            _inputCapture.CaretBrush = Brushes.Transparent;
            _inputCapture.FontSize = fs * 0.5;
            _inputCapture.AcceptsReturn = false;
            _inputCapture.MaxLines = 1;
            _inputCapture.Padding = new Thickness(0);

            // 自定义光标
            _cursor = new Border();
            _cursor.Width = 2;
            _cursor.Height = fs;
            _cursor.Background = Colors.DisplayForeground;
            var blink = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromMilliseconds(500)));
            blink.AutoReverse = true;
            blink.RepeatBehavior = RepeatBehavior.Forever;
            _cursor.BeginAnimation(UIElement.OpacityProperty, blink);

            // 未上屏编码显示
            _compositionText = new TextBlock();
            _compositionText.FontSize = fs * 0.4;
            _compositionText.Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x99, 0xFF));
            _compositionText.Background = Brushes.Transparent;
            _compositionText.Padding = new Thickness(1, 0, 1, 0);
            _compositionText.Visibility = Visibility.Collapsed;

            _overlay.Children.Add(_inputCapture);
            _overlay.Children.Add(_compositionText);
            _overlay.Children.Add(_cursor);

            var grid = (Grid)_main.BdDisplay.Child;
            grid.Children.Add(_overlay);

            // 注册事件
            _inputCapture.PreviewTextInput += OnTextInput;
            _inputCapture.PreviewKeyDown += OnPreviewKeyDown;
            _inputCapture.LostFocus += OnLostFocus;
            _inputCapture.GotFocus += OnGotFocus;
            _main.Activated += OnWindowActivated;
            _main.Deactivated += OnWindowDeactivated;
            TextCompositionManager.AddPreviewTextInputStartHandler(_inputCapture, OnCompositionStart);
            TextCompositionManager.AddPreviewTextInputUpdateHandler(_inputCapture, OnCompositionUpdate);

            // 不在这里调 ScheduleInsertMirrorBlocks，
            // 由外部 PageProgressUpdate 结束后统一调度，避免用旧 Blocks 构建镜像行
            _inputCapture.Focus();
        }

        /// <summary>
        /// 关闭临摹模式
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
            _main.Activated -= OnWindowActivated;
            _main.Deactivated -= OnWindowDeactivated;

            if (_overlay != null)
            {
                var grid = (Grid)_main.BdDisplay.Child;
                grid.Children.Remove(_overlay);
                _overlay = null;
            }

            _inputCapture = null;
            _compositionText = null;
            _cursor = null;

            // 清除背景色
            for (int i = 0; i < TextInfo.Blocks.Count; i++)
                TextInfo.Blocks[i].Background = null;

            // 移除镜像行
            RemoveMirrorBlocks();

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
            _compositionText.Visibility = Visibility.Collapsed;
            if (_inputCapture != null)
            {
                _inputCapture.Text = "";
                _inputCapture.Focus();
            }
            RemoveMirrorBlocks();
        }

        /// <summary>
        /// 主题切换后刷新光标颜色并重建镜像行
        /// </summary>
        public void RefreshTheme()
        {
            if (!_isActive) return;

            // 更新光标颜色
            if (_cursor != null)
                _cursor.Background = Colors.DisplayForeground;

            // 重建镜像行（会使用新的 Colors.DisplayForeground 计算镜像前景色）
            ScheduleInsertMirrorBlocks();
        }

        /// <summary>
        /// 延迟到布局完成后插入镜像行并定位光标
        /// </summary>
        public void ScheduleInsertMirrorBlocks()
        {
            if (!_isActive || _inputCapture == null) return;
            if (_pendingMirrorRebuild) return;
            _pendingMirrorRebuild = true;
            _main.Dispatcher.BeginInvoke(new Action(() =>
            {
                _pendingMirrorRebuild = false;
                if (!_isActive || TextInfo.Blocks.Count == 0) return;
                _main.TbDispay.UpdateLayout();
                InsertMirrorBlocks();
                _main.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (!_isActive || _currentIndex >= TextInfo.Blocks.Count) return;
                    UpdatePosition();
                    _inputCapture?.Focus();
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        /// <summary>
        /// 根据原文行的布局位置，插入镜像行
        /// </summary>
        private void InsertMirrorBlocks()
        {
            RemoveMirrorBlocks();
            if (TextInfo.Blocks.Count == 0) return;

            double fs = MainWindow.DisplayFontSize;
            double height = fs * (1.0 + Config.GetDouble("行距"));
            var fontFamily = _main.ScDisplay.FontFamily;
            double availablePad = Math.Max(0, height - fs * fontFamily.LineSpacing);
            double padTop = (availablePad / 2 + Math.Min((height - fs) / 2, availablePad)) / 2;
            double padBottom = availablePad - padTop;

            // 按 Y 坐标分组，确定每个视觉行包含哪些 Block 索引
            _lineGroups = new List<List<int>>();
            var wrapPanel = _main.TbDispay;
            double lastY = double.NaN;
            List<int> currentLine = null;

            for (int i = 0; i < TextInfo.Blocks.Count; i++)
            {
                var block = TextInfo.Blocks[i];
                // 获取 block 相对于 WrapPanel 的位置
                Point pos;
                try
                {
                    var parent = VisualTreeHelper.GetParent(block) as UIElement;
                    if (parent == wrapPanel)
                        pos = block.TranslatePoint(new Point(0, 0), wrapPanel);
                    else
                        pos = parent.TranslatePoint(new Point(0, 0), wrapPanel);
                }
                catch { continue; }

                if (double.IsNaN(lastY) || Math.Abs(pos.Y - lastY) > fs * 0.3)
                {
                    currentLine = new List<int>();
                    _lineGroups.Add(currentLine);
                    lastY = pos.Y;
                }
                currentLine.Add(i);
            }

            // 为每个 Block 创建对应的镜像 TextBlock
            for (int i = 0; i < TextInfo.Blocks.Count; i++)
            {
                var tb = new TextBlock();
                tb.Text = "\u3000"; // 全角空格占位
                tb.Height = height;
                tb.Padding = new Thickness(0, padTop, 0, padBottom);
                tb.FontSize = fs;
                tb.FontFamily = fontFamily;
                var c = ((SolidColorBrush)Colors.DisplayForeground).Color;
                tb.Foreground = new SolidColorBrush(Color.FromArgb(100, c.R, c.G, c.B));
                // 与原文 Block 保持相同的宽度属性
                var orig = TextInfo.Blocks[i];
                if (orig.MinWidth > 0) tb.MinWidth = orig.MinWidth;
                tb.TextAlignment = orig.TextAlignment;
                _mirrorBlocks.Add(tb);
            }

            // 恢复已打字的镜像内容
            for (int i = 0; i < _currentIndex && i < _mirrorBlocks.Count; i++)
            {
                // 已打过的字需要从 wordStates 推断，但我们没存实际输入的字
                // 打对的显示原文，打错的显示"×"
                if (TextInfo.wordStates[TextInfo.PageStartIndex + i] == WordStates.RIGHT)
                    _mirrorBlocks[i].Text = TextInfo.Words[TextInfo.PageStartIndex + i];
                else if (TextInfo.wordStates[TextInfo.PageStartIndex + i] == WordStates.WRONG)
                    _mirrorBlocks[i].Text = "×";
            }
            UpdateMirrorLineColors();

            // 重建 TbDispay：每行原文后插入换行符 + 镜像行 + 换行符
            RebuildDisplayWithMirrors();
        }

        /// <summary>
        /// 重建 TbDispay，在每行原文后插入镜像行（带左侧竖线标记）
        /// </summary>
        private void RebuildDisplayWithMirrors()
        {
            var wrapPanel = _main.TbDispay;
            double panelWidth = wrapPanel.ActualWidth;
            if (panelWidth <= 0) panelWidth = wrapPanel.Width;
            if (double.IsNaN(panelWidth) || panelWidth <= 0) panelWidth = 800;
            double fs = MainWindow.DisplayFontSize;
            double height = fs * (1.0 + Config.GetDouble("行距"));

            // 先把所有原文 Block 从父容器中移除
            var originalChildren = new List<UIElement>();
            foreach (UIElement child in wrapPanel.Children)
                originalChildren.Add(child);

            foreach (var child in originalChildren)
            {
                if (child is StackPanel sp)
                    sp.Children.Clear();
                else if (child is Border bd && bd.Child is WrapPanel wp)
                    wp.Children.Clear();
            }
            wrapPanel.Children.Clear();

            // 按行重新添加
            foreach (var line in _lineGroups)
            {
                // 用 Border 包裹原文行
                var border = new Border();
                border.Background = new SolidColorBrush(Color.FromArgb(20, 128, 128, 128));
                border.CornerRadius = new CornerRadius(4);
                var innerPanel = new WrapPanel();
                foreach (int idx in line)
                {
                    if (idx < TextInfo.Blocks.Count)
                        innerPanel.Children.Add(TextInfo.Blocks[idx]);
                }
                border.Child = innerPanel;
                wrapPanel.Children.Add(border);

                // 插入换行元素
                var lineBreak1 = new FrameworkElement();
                lineBreak1.Width = panelWidth;
                lineBreak1.Height = 0;
                wrapPanel.Children.Add(lineBreak1);

                // 添加该行的镜像 Blocks
                foreach (int idx in line)
                {
                    if (idx < _mirrorBlocks.Count)
                        wrapPanel.Children.Add(_mirrorBlocks[idx]);
                }

                // 插入换行元素（镜像行后）
                var lineBreak2 = new FrameworkElement();
                lineBreak2.Width = panelWidth;
                lineBreak2.Height = 0;
                wrapPanel.Children.Add(lineBreak2);
            }
        }

        private void RemoveMirrorBlocks()
        {
            if (_mirrorBlocks.Count > 0)
            {
                var wrapPanel = _main.TbDispay;
                // 先把原文 Blocks 从所有容器中断开（Border>WrapPanel、StackPanel 等）
                foreach (UIElement child in wrapPanel.Children)
                {
                    if (child is Border bd && bd.Child is WrapPanel wp)
                        wp.Children.Clear();
                    else if (child is StackPanel sp)
                        sp.Children.Clear();
                }
                wrapPanel.Children.Clear();
                foreach (var block in TextInfo.Blocks)
                    wrapPanel.Children.Add(block);
            }
            _mirrorBlocks.Clear();
            _lineGroups.Clear();
        }

        /// <summary>
        /// 更新镜像行样式：当前行内已打字符按距离实时渐变，已完成行整体很淡
        /// </summary>
        private void UpdateMirrorLineColors()
        {
            if (_lineGroups.Count == 0 || _mirrorBlocks.Count == 0) return;

            var c = ((SolidColorBrush)Colors.DisplayForeground).Color;
            var mirrorForeground = new SolidColorBrush(Color.FromArgb(100, c.R, c.G, c.B));

            // 找到当前行
            int currentLineIdx = _lineGroups.Count;
            for (int li = 0; li < _lineGroups.Count; li++)
            {
                var line = _lineGroups[li];
                if (line.Count > 0 && _currentIndex >= line[0] && _currentIndex <= line[line.Count - 1])
                {
                    currentLineIdx = li;
                    break;
                }
            }

            for (int li = 0; li < _lineGroups.Count; li++)
            {
                var line = _lineGroups[li];
                if (li < currentLineIdx)
                {
                    // 已完成行：整体很淡
                    foreach (int idx in line)
                    {
                        if (idx < _mirrorBlocks.Count)
                        {
                            _mirrorBlocks[idx].Opacity = 0.15;
                            _mirrorBlocks[idx].Foreground = mirrorForeground;
                        }
                    }
                }
                else if (li == currentLineIdx)
                {
                    // 当前行：已打字符按距离渐变，越早打的越淡
                    int lineCharCount = line.Count;
                    foreach (int idx in line)
                    {
                        if (idx >= _mirrorBlocks.Count) continue;
                        if (idx < _currentIndex)
                        {
                            // 已打：距离当前位置越远越淡
                            int dist = _currentIndex - idx;
                            // 用行内字数做比例，打到行尾时行首刚好到最淡
                            double ratio = (double)dist / lineCharCount;
                            double opacity = 1.0 - ratio * 0.85; // 从1.0渐变到0.15
                            _mirrorBlocks[idx].Opacity = opacity;
                        }
                        else
                        {
                            // 未打：正常
                            _mirrorBlocks[idx].Opacity = 1.0;
                        }
                        _mirrorBlocks[idx].Foreground = Colors.DisplayForeground;
                    }
                }
                else
                {
                    // 未来行：正常
                    foreach (int idx in line)
                    {
                        if (idx < _mirrorBlocks.Count)
                        {
                            _mirrorBlocks[idx].Opacity = 1.0;
                            _mirrorBlocks[idx].Foreground = mirrorForeground;
                        }
                    }
                }
            }
        }

        private void OnLostFocus(object sender, RoutedEventArgs e)
        {
            if (!_isActive || _inputCapture == null) return;
            if (_cursor != null) _cursor.Visibility = Visibility.Collapsed;
            _main.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!_isActive || _inputCapture == null) return;
                if (Keyboard.FocusedElement is System.Windows.Controls.Primitives.ButtonBase)
                    return;
                if (Keyboard.FocusedElement == _main.TbxResults)
                    return;
                if (!_main.IsActive) return;
                _inputCapture.Focus();
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        private void OnGotFocus(object sender, RoutedEventArgs e)
        {
            if (_cursor != null) _cursor.Visibility = Visibility.Visible;
        }

        private void OnWindowDeactivated(object sender, EventArgs e)
        {
            if (_cursor != null) _cursor.Visibility = Visibility.Collapsed;
        }

        private void OnWindowActivated(object sender, EventArgs e)
        {
            if (!_isActive || _inputCapture == null) return;
            if (_cursor != null) _cursor.Visibility = Visibility.Visible;
            _inputCapture.Focus();
        }

        private void OnCompositionStart(object sender, TextCompositionEventArgs e)
        {
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

            _main.HandleTextInputStats(e);

            if (string.IsNullOrEmpty(e.Text))
            {
                if (Config.GetBool("禁用回改")) { /* 继续 */ }
                else
                {
                    _compositionText.Visibility = Visibility.Collapsed;
                    e.Handled = true;
                    return;
                }
            }

            if (e.Text == "\r")
            {
                if (!Config.GetBool("禁用回改"))
                {
                    e.Handled = true;
                    return;
                }
            }

            string inputText = e.Text;
            if (Config.GetBool("禁用回改") && (string.IsNullOrEmpty(inputText) || inputText == "\r"))
                inputText = " ";

            ProcessInputText(inputText);

            e.Handled = true;
        }

        private void ProcessSingleChar(string ch)
        {
            ProcessInputText(ch);
        }

        private void ProcessInputText(string inputText)
        {
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
                        TextInfo.Blocks[_currentIndex].Background = Colors.IncorrectBackground;
                }

                // 更新镜像行：显示用户实际输入的字
                if (_currentIndex < _mirrorBlocks.Count)
                    _mirrorBlocks[_currentIndex].Text = ch;

                _currentIndex++;
            }

            // 更新镜像行颜色
            UpdateMirrorLineColors();

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

            _compositionText.Visibility = Visibility.Collapsed;
            _inputCapture.Text = "";

            if (_currentIndex >= TextInfo.Words.Count
                && TextInfo.wordStates[TextInfo.Words.Count - 1] == WordStates.RIGHT)
            {
                _main.StopTyping();
            }
            else if (_currentIndex < TextInfo.Words.Count)
            {
                if (Config.GetBool("贪吃蛇模式") || StateManager.txtSource == TxtSource.raceApi)
                    _main.SnakeModeUpdateFromCopybook(_currentIndex);
                else
                    ScrollToCurrentChar();

                UpdatePosition();
                _main.UpdateZiTi();
            }
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!_isActive) return;

            if (Config.GetBool("禁用回改"))
            {
                if (e.Key == Key.Back || e.Key == Key.Escape ||
                    (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control))
                {
                    e.Handled = true;
                    return;
                }
            }

            _main.HandleKeyDownStats(e);

            // 空格键不会触发 PreviewTextInput，需要在这里手动处理
            if (e.Key == Key.Space && string.IsNullOrEmpty(_inputCapture.Text))
            {
                ProcessSingleChar(" ");
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Back && string.IsNullOrEmpty(_inputCapture.Text))
            {
                if (_currentIndex > 0)
                {
                    _currentIndex--;
                    TextInfo.wordStates[_currentIndex] = WordStates.NO_TYPE;
                    if (_currentIndex < TextInfo.Blocks.Count)
                        TextInfo.Blocks[_currentIndex].Background = null;

                    // 清除镜像行内容
                    if (_currentIndex < _mirrorBlocks.Count)
                        _mirrorBlocks[_currentIndex].Text = "\u3000";

                    UpdateMirrorLineColors();
                    Score.InputWordCount = _currentIndex;

                    UpdatePosition();
                    if (Config.GetBool("贪吃蛇模式") || StateManager.txtSource == TxtSource.raceApi)
                        _main.SnakeModeUpdateFromCopybook(_currentIndex);
                    else
                        ScrollToCurrentChar();
                    _main.UpdateZiTi();
                }
                e.Handled = true;
            }
        }

        private void UpdatePosition()
        {
            if (_inputCapture == null || _currentIndex >= _mirrorBlocks.Count || _mirrorBlocks.Count == 0)
                return;

            try
            {
                var grid = (Grid)_main.BdDisplay.Child;
                // 光标定位到镜像行的当前字位置
                var mirrorBlock = _mirrorBlocks[_currentIndex];
                var pos = mirrorBlock.TranslatePoint(new Point(0, 0), grid);
                double x = pos.X;
                double y = pos.Y;
                double fs = MainWindow.DisplayFontSize;
                double candidateOffset = Config.GetDouble("字帖候选框高度") * fs;
                double compositionOffset = (Config.GetDouble("字帖编码高度") + 0.2) * fs;

                // 计算文字在 TextBlock 内的实际 padTop（与 PageReArrange 一致）
                var fm = _main.GetCurrentFontFamily();
                double height = fs * (1.0 + Config.GetDouble("行距"));
                double availablePad = Math.Max(0, height - fs * fm.LineSpacing);
                double padTop = (availablePad / 2 + Math.Min((height - fs) / 2, availablePad)) / 2;

                Canvas.SetLeft(_inputCapture, x);
                Canvas.SetTop(_inputCapture, y + 1.0 * fs + candidateOffset);

                if (_cursor != null)
                {
                    double lineHeight = fs * fm.LineSpacing;
                    _cursor.Height = lineHeight;
                    Canvas.SetLeft(_cursor, x - 2);
                    Canvas.SetTop(_cursor, y + padTop);
                }

                Canvas.SetLeft(_compositionText, x);
                Canvas.SetTop(_compositionText, y + mirrorBlock.ActualHeight - 0.25 * fs + compositionOffset);
            }
            catch { }
        }

        private void UpdateCompositionPosition()
        {
            if (_compositionText == null || _currentIndex >= _mirrorBlocks.Count || _mirrorBlocks.Count == 0)
                return;

            try
            {
                var grid = (Grid)_main.BdDisplay.Child;
                var mirrorBlock = _mirrorBlocks[_currentIndex];
                var pos = mirrorBlock.TranslatePoint(new Point(0, 0), grid);
                double fs = MainWindow.DisplayFontSize;
                double compositionOffset = Config.GetDouble("字帖编码高度") * fs;
                Canvas.SetLeft(_compositionText, pos.X);
                Canvas.SetTop(_compositionText, pos.Y + mirrorBlock.ActualHeight - 0.25 * fs + compositionOffset);
            }
            catch { }
        }

        private void ScrollToCurrentChar()
        {
            if (_currentIndex >= _mirrorBlocks.Count || _mirrorBlocks.Count == 0) return;

            try
            {
                var firstBlock = TextInfo.Blocks.Count > 0 ? TextInfo.Blocks[0] : _mirrorBlocks[0];
                double currentPosY =
                    _mirrorBlocks[_currentIndex].TranslatePoint(new Point(0, 0), firstBlock).Y
                    + _mirrorBlocks[_currentIndex].ActualHeight / 2;

                double targetOffset = _main.CalculateScrollOffset(currentPosY);
                _main.SmoothScrollTo(targetOffset);

                // 滚动改变了 ScrollViewer 偏移，Canvas overlay 上的光标坐标需要重新计算
                _main.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (_isActive && _currentIndex < _mirrorBlocks.Count)
                        UpdatePosition();
                }), System.Windows.Threading.DispatcherPriority.Render);
            }
            catch { }
        }
    }
}
