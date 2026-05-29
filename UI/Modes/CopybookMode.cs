using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using TypeSunny.Core;
using TypeSunny.UI;
using TypeSunny.Logs;
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
        private SmoothCaret _cursor;
        private readonly List<FrameworkElement> _wrongCharHints = new List<FrameworkElement>();
        private readonly ImeBackspacePolicy _imeBackspacePolicy = new ImeBackspacePolicy();
        private readonly FinishOnceGate _finishGate = new FinishOnceGate();
        private readonly CopybookInputBuffer _inputBuffer = new CopybookInputBuffer();
        private int _currentIndex;
        private bool _isActive;
        private int _visualAdvanceVersion;
        private bool _isImeComposing;
        private string _activeCompositionText = "";
        private GridLength _savedTypingRowHeight;
        private double _savedTypingRowMinHeight;
        private GridLength _savedSplitterRowHeight;
        private double _savedArticleRowHeight;
        private double _savedTypingRowHeightValue;
        private bool _isScrollAnimating;
        private readonly List<PendingBackgroundChange> _pendingBackgroundChanges = new List<PendingBackgroundChange>();

        public bool IsActive => _isActive;
        public int CurrentIndex => _currentIndex;
        public int TypedLength => _inputBuffer.Length;

        internal int GetBackgroundAnimationDurationMilliseconds()
        {
            return _cursor != null ? _cursor.GetBackgroundDurationMilliseconds() : SmoothMotionTiming.MediumDurationMilliseconds;
        }

        public CopybookMode(MainWindow main)
        {
            _main = main;
        }

        private struct PendingBackgroundChange
        {
            public int GlobalIndex;
            public Brush Background;
        }

        /// <summary>
        /// 激活字帖模式
        /// </summary>
        public void Enable()
        {
            if (_isActive) return;

            _isActive = true;
            _currentIndex = Score.InputWordCount;
            SyncInputBufferFromCurrentState();

            // 隐藏跟打区（只隐藏输入框，保留按钮区）
            _main.TbxInput.Visibility = Visibility.Collapsed;

            // 保存并清零跟打区 RowDefinition，否则 Row 仍占空间
            var parentGrid = (Grid)_main.typingAreaAndButtonsGrid.Parent;
            // Row 3 = GridSplitter, Row 4 = typingAreaAndButtons
            _savedSplitterRowHeight = parentGrid.RowDefinitions[3].Height;
            _savedTypingRowHeight = parentGrid.RowDefinitions[4].Height;
            _savedTypingRowMinHeight = parentGrid.RowDefinitions[4].MinHeight;
            _savedArticleRowHeight = parentGrid.RowDefinitions[2].ActualHeight;
            _savedTypingRowHeightValue = parentGrid.RowDefinitions[4].ActualHeight;

            // 锁定 Row 6 为像素值，释放的空间全部给 Row 2
            // 如果成绩区已收起（Height=0px），保持为 0
            var row6Height = parentGrid.RowDefinitions[6].Height;
            if (row6Height.IsAbsolute && row6Height.Value == 0)
            {
                parentGrid.RowDefinitions[6].Height = new GridLength(0, GridUnitType.Pixel);
            }
            else
            {
                double resultsH = parentGrid.RowDefinitions[6].ActualHeight;
                parentGrid.RowDefinitions[6].Height = new GridLength(resultsH, GridUnitType.Pixel);
            }

            parentGrid.RowDefinitions[3].Height = new GridLength(0);
            parentGrid.RowDefinitions[4].Height = new GridLength(0, GridUnitType.Auto);
            parentGrid.RowDefinitions[4].MinHeight = 0;
            _main.gridSplitterArticleTyping.Visibility = Visibility.Collapsed;

            // Row 2 设为 Star(1)，作为唯一的 Star 行自动吃掉所有释放的空间
            parentGrid.RowDefinitions[2].Height = new GridLength(1, GridUnitType.Star);

            double fs = MainWindow.DisplayFontSize;

            // 创建覆盖层 Canvas（透明背景使整个发文区可点击）
            _overlay = new Canvas();
            _overlay.IsHitTestVisible = true;
            _overlay.Background = Brushes.Transparent;
            _overlay.Cursor = Cursors.IBeam;
            _overlay.MouseDown += (s, ev) => { if (_inputCapture != null) _inputCapture.Focus(); };
            _overlay.PreviewMouseWheel += OnOverlayPreviewMouseWheel;
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
            _cursor = new SmoothCaret(fs, Colors.DisplayForeground);

            // 创建未上屏编码显示
            _compositionText = new TextBlock();
            _compositionText.FontSize = fs * 0.4;
            _compositionText.Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x99, 0xFF));
            _compositionText.Background = Brushes.Transparent;
            _compositionText.Padding = new Thickness(1, 0, 1, 0);
            _compositionText.Visibility = Visibility.Collapsed;
            _compositionText.IsHitTestVisible = false;
            _compositionText.RenderTransformOrigin = new Point(0.5, 0.5);
            _compositionText.RenderTransform = new ScaleTransform(1.0, 1.0);

            _overlay.Children.Add(_inputCapture);
            _overlay.Children.Add(_compositionText);
            _overlay.Children.Add(_cursor.Element);

            // 添加到 BdDisplay 内的 Grid
            var grid = (Grid)_main.BdDisplay.Child;
            grid.Children.Add(_overlay);

            // 注册事件
            _inputCapture.PreviewTextInput += OnTextInput;
            _inputCapture.PreviewKeyDown += OnPreviewKeyDown;
            _inputCapture.AddHandler(Keyboard.PreviewKeyUpEvent, new KeyEventHandler(OnPreviewKeyUp), true);
            _inputCapture.LostFocus += OnLostFocus;
            _inputCapture.GotFocus += OnGotFocus;
            _main.Activated += OnWindowActivated;
            _main.Deactivated += OnWindowDeactivated;
            _main.ScDisplay.ScrollChanged += OnDisplayScrollChanged;
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
                _inputCapture.RemoveHandler(Keyboard.PreviewKeyUpEvent, new KeyEventHandler(OnPreviewKeyUp));
                _inputCapture.LostFocus -= OnLostFocus;
                _inputCapture.GotFocus -= OnGotFocus;
                _overlay.PreviewMouseWheel -= OnOverlayPreviewMouseWheel;
                TextCompositionManager.RemovePreviewTextInputStartHandler(_inputCapture, OnCompositionStart);
                TextCompositionManager.RemovePreviewTextInputUpdateHandler(_inputCapture, OnCompositionUpdate);
            }
            _main.Activated -= OnWindowActivated;
            _main.Deactivated -= OnWindowDeactivated;
            _main.ScDisplay.ScrollChanged -= OnDisplayScrollChanged;
            StopScrollSync();

            if (_overlay != null)
            {
                var grid = (Grid)_main.BdDisplay.Child;
                grid.Children.Remove(_overlay);
                _overlay = null;
            }

            _inputCapture = null;
            _compositionText = null;
            _cursor = null;
            _pendingBackgroundChanges.Clear();
            _wrongCharHints.Clear();

            // 清除已打字的背景色
            for (int i = 0; i < TextInfo.Blocks.Count; i++)
                _main.SetDisplayBlockStateBackground(i, null);

            // 恢复跟打区
            var parentGrid = (Grid)_main.typingAreaAndButtonsGrid.Parent;
            parentGrid.RowDefinitions[4].MinHeight = _savedTypingRowMinHeight;

            // 锁定 Row 6 为像素值，防止恢复 Row 3 splitter 时挤占成绩区
            double resultsH = parentGrid.RowDefinitions[6].ActualHeight;
            parentGrid.RowDefinitions[6].Height = new GridLength(resultsH, GridUnitType.Pixel);

            // 跟打区的空间只从发文区里取
            double currentArticleH = parentGrid.RowDefinitions[2].ActualHeight;
            // splitter 大约 5px
            double splitterH = 5;
            double typingH = _savedTypingRowHeightValue;
            // 确保跟打区不超过发文区当前高度的一半
            double maxTypingH = (currentArticleH - splitterH) / 2.0;
            if (typingH > maxTypingH) typingH = maxTypingH;
            if (typingH < 30) typingH = 30;

            double newArticleH = currentArticleH - splitterH - typingH;
            if (newArticleH < 50) newArticleH = 50;

            parentGrid.RowDefinitions[2].Height = new GridLength(newArticleH, GridUnitType.Pixel);
            parentGrid.RowDefinitions[3].Height = _savedSplitterRowHeight;
            parentGrid.RowDefinitions[4].Height = new GridLength(typingH, GridUnitType.Pixel);

            // 恢复为 Star 让布局可自适应（不动 Row 6，避免拖动 splitter 时成绩区跟着跑）
            _main.Dispatcher.BeginInvoke(new Action(() =>
            {
                double ah = parentGrid.RowDefinitions[2].ActualHeight;
                double th = parentGrid.RowDefinitions[4].ActualHeight;
                if (ah > 0) parentGrid.RowDefinitions[2].Height = new GridLength(ah, GridUnitType.Star);
                if (th > 0) parentGrid.RowDefinitions[4].Height = new GridLength(th, GridUnitType.Star);
            }), System.Windows.Threading.DispatcherPriority.Loaded);

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
            _inputBuffer.Clear();
            _finishGate.Reset();
            _pendingBackgroundChanges.Clear();
            // 清除所有错字提示
            ClearWrongCharHints();
            _imeBackspacePolicy.Reset();
            ClearImeCompositionState();
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
                UpdatePosition(false);
                ResetInputCaptureHostIfIdle();
                _inputCapture.Focus();
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        public void FocusInputCapture()
        {
            if (!_isActive || _inputCapture == null) return;
            ResetInputCaptureHostIfIdle();
            _inputCapture.Focus();
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

        private void SyncInputBufferFromCurrentState()
        {
            int typedCount = Score.InputWordCount;
            if (typedCount < 0) typedCount = 0;
            if (typedCount > TextInfo.Words.Count) typedCount = TextInfo.Words.Count;

            string typedText = "";
            for (int i = 0; i < typedCount; i++)
                typedText += TextInfo.Words[i];

            _inputBuffer.SetText(typedText, typedCount);
            _currentIndex = _inputBuffer.CaretIndex;
        }

        private void OnLostFocus(object sender, RoutedEventArgs e)
        {
            if (!_isActive || _inputCapture == null) return;
            // Do not immediately steal focus back here: TSF IME composition can
            // briefly move focus during pre-edit. Focus is restored only on
            // explicit entry points such as clicking the article area or
            // re-activating the main window.
            _cursor?.Hide();
        }

        private void OnGotFocus(object sender, RoutedEventArgs e)
        {
            _cursor?.Show();
        }

        private void OnWindowDeactivated(object sender, EventArgs e)
        {
            _cursor?.Hide();
        }

        private void OnWindowActivated(object sender, EventArgs e)
        {
            if (!_isActive || _inputCapture == null) return;
            _cursor?.Show();
            _inputCapture.Focus();
        }

        private void OnDisplayScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (!_isActive || _overlay == null)
                return;

            RefreshWrongCharHints();
            UpdatePosition(false);
        }

        private void OnOverlayPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!_isActive)
                return;

            _main.ScDisplay.ScrollToVerticalOffset(_main.ScDisplay.VerticalOffset - e.Delta);
            e.Handled = true;
        }

        private void OnCompositionStart(object sender, TextCompositionEventArgs e)
        {
            SetImeCompositionState(e.TextComposition.CompositionText ?? "");
            _main.RefreshImeCandidateWindowPosition();
            if (!Score.IsComposing)
            {
                Score.IsComposing = true;
                Score.CompositionStartHit = Score.Hit;
            }
        }

        private void OnCompositionUpdate(object sender, TextCompositionEventArgs e)
        {
            if (!_isActive) return;

            string composition = e.TextComposition.CompositionText ?? "";
            SetImeCompositionState(composition);
        }

        private void OnTextInput(object sender, TextCompositionEventArgs e)
        {
            if (!_isActive) return;
            if (_finishGate.IsPending || StateManager.typingState == TypingState.end)
            {
                e.Handled = true;
                return;
            }

            // 统计（废码、打词率、提交记录、标顶等）
            _main.HandleTextInputStats(e);

            // 空码/ESC取消
            if (string.IsNullOrEmpty(e.Text))
            {
                ClearImeCompositionState(clearFeedback: true);
                bool disableBackInEffect = Config.GetBool("禁用回改")
                    && StateManager.txtSource != TxtSource.raceApi
                    && StateManager.txtSource != TxtSource.jbs
                    && StateManager.txtSource != TxtSource.jisucup;
                if (disableBackInEffect)
                {
                    // 禁用回改模式：空码时强制上屏一个空格，继续往下走逐字比对
                }
                else
                {
                    HideCompositionText();
                    e.Handled = true;
                    return;
                }
            }

            // 回车始终作为暂停，不插入任何字符
            if (e.Text == "\r")
            {
                ClearImeCompositionState(clearFeedback: true);
                e.Handled = true;
                return;
            }

            // 禁用回改模式：空码强制当空格处理
            string inputText = e.Text;
            bool disableBackActive = Config.GetBool("禁用回改")
                && StateManager.txtSource != TxtSource.raceApi
                && StateManager.txtSource != TxtSource.jbs
                && StateManager.txtSource != TxtSource.jisucup;
            if (disableBackActive && string.IsNullOrEmpty(inputText))
                inputText = " ";

            string committedComposition = _activeCompositionText;
            ClearImeCompositionState(clearFeedback: false);
            ProcessInputText(inputText, committedComposition);

            // 不设 e.Handled = true —— 让事件继续流向 TextBox 内部处理器，
            // 否则 TSF 五码顶字时"提交+首码进新composition"的连续链路会被打断。
            // _inputCapture.Text 会因此累积已上屏文字，用延迟清理防止无限增长。
            ScheduleInputCaptureTrim();
        }

        private void ProcessSingleChar(string ch)
        {
            ProcessInputText(ch);
        }

        private void ProcessInputText(string inputText, string committedComposition = null)
        {
            if (TextInfo.Words == null || TextInfo.Words.Count == 0) return;

            if (_currentIndex >= TextInfo.Words.Count)
            {
                if (TextInfo.wordStates[TextInfo.Words.Count - 1] != WordStates.RIGHT)
                    FlushPendingBackgroundChanges();

                HideCompositionText();
                _main.UpdateTitleProgress(_inputBuffer.Length);
                UpdatePosition(true);
                _main.UpdateZiTi();
                return;
            }

            // 全局字数计数（正常模式由 TbxInput_TextChanged 处理）
            var si = new StringInfo(inputText);
            int wordsToRecord = _main.ResolveTypedWordCountDelta(inputText, si.LengthInTextElements, _currentIndex);
            _main.RecordTypedWords(wordsToRecord);
            _cursor?.RecordInput();

            int commitIndex = _currentIndex;
            int inserted = _inputBuffer.Insert(inputText);
            _currentIndex = _inputBuffer.CaretIndex;

            if (inserted > 0)
            {
                ClearCodeLabelProgressFrom(commitIndex);
                RefreshTypedStateFromInputBuffer();

                if (commitIndex < TextInfo.Words.Count && !string.IsNullOrEmpty(committedComposition))
                {
                    string typed = _inputBuffer.GetElement(commitIndex);
                    bool isCorrect = typed == TextInfo.Words[commitIndex] || _main.IsLookingType;
                    _main.CommitCodeLabelProgress(commitIndex, committedComposition, isCorrect);
                }
            }
            HideCompositionText();

            // 更新标题栏进度条和窗口标题
            _main.UpdateTitleProgress(_inputBuffer.Length);

            // 检查是否结束：必须打完且最后一个字正确才结算
            if (_inputBuffer.Length >= TextInfo.Words.Count
                && TextInfo.wordStates[TextInfo.Words.Count - 1] == WordStates.RIGHT)
            {
                ScheduleFinalVisualsAndStop();
            }
            else if (_currentIndex >= TextInfo.Words.Count)
            {
                FlushPendingBackgroundChanges();
                UpdatePosition(true);
                _main.UpdateZiTi();
            }
            else if (_currentIndex < TextInfo.Words.Count)
            {
                ScheduleAdvanceVisuals();
            }
        }

        private void ScheduleInputCaptureTrim()
        {
            _main.Dispatcher.BeginInvoke(new Action(() =>
            {
                TrimInputCaptureTextAfterCommit();
            }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        private void TrimInputCaptureTextAfterCommit()
        {
            ResetInputCaptureHostIfIdle();
        }

        private void ResetInputCaptureHostIfIdle()
        {
            if (!_isActive || _inputCapture == null)
                return;

            if (HasActiveComposition())
                return;

            if (_inputCapture.Text.Length > 0)
                _inputCapture.Text = "";

            _inputCapture.Select(0, 0);
        }

        private void RefreshTypedStateFromInputBuffer()
        {
            Score.TotalWordCount = TextInfo.Words.Count;
            Score.InputWordCount = _inputBuffer.Length;
            Score.Wrong = 0;

            ClearWrongCharHints();

            var states = _inputBuffer.BuildStates(TextInfo.Words, _main.IsLookingType);
            for (int i = 0; i < TextInfo.wordStates.Count; i++)
            {
                WordStates previousState = TextInfo.wordStates[i];
                WordStates state = i < states.Length
                    ? ToWordState(states[i])
                    : WordStates.NO_TYPE;

                TextInfo.wordStates[i] = state;

                if (!_main.IsBlindType)
                {
                    if (previousState != state && IsIndexOnCurrentPage(i))
                    {
                        Brush background = GetQueuedBackgroundForState(state);
                        QueueDisplayBlockStateBackground(i, background);
                    }
                    else if (IsIndexOnCurrentPage(i))
                    {
                        Brush background = GetQueuedBackgroundForState(state);
                        _main.ResyncDisplayBlockStateBackgroundByGlobalIndex(i, background);
                    }

                    if (state == WordStates.WRONG && IsIndexOnCurrentPage(i))
                        ShowWrongCharHint(_inputBuffer.GetElement(i), i - TextInfo.PageStartIndex);
                }

                if (!_main.IsLookingType && state == WordStates.WRONG)
                    Score.Wrong++;
            }
        }

        private static WordStates ToWordState(CopybookInputState state)
        {
            switch (state)
            {
                case CopybookInputState.Right:
                    return WordStates.RIGHT;
                case CopybookInputState.Wrong:
                    return WordStates.WRONG;
                default:
                    return WordStates.NO_TYPE;
            }
        }

        private static bool IsIndexOnCurrentPage(int globalIndex)
        {
            int localIndex = globalIndex - TextInfo.PageStartIndex;
            return localIndex >= 0 && localIndex < TextInfo.Blocks.Count;
        }

        private static Brush GetQueuedBackgroundForState(WordStates state)
        {
            if (state == WordStates.RIGHT)
                return Colors.CorrectBackground;
            if (state == WordStates.WRONG)
                return Colors.IncorrectBackground;
            return null;
        }

        private void ClearCodeLabelProgressFrom(int globalStart)
        {
            if (globalStart < 0)
                globalStart = 0;

            var keysToClear = new List<int>();
            foreach (int key in TextInfo.CodeLabelInputs.Keys)
            {
                if (key >= globalStart)
                    keysToClear.Add(key);
            }

            foreach (int key in keysToClear)
                _main.ClearCodeLabelProgress(key);
        }

        private void ScheduleAdvanceVisuals()
        {
            int requestVersion = ++_visualAdvanceVersion;
            _main.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!_isActive || requestVersion != _visualAdvanceVersion)
                    return;

                // 先滚动再定位光标，否则滚动会改变 TextBlock 相对于 Grid 的坐标
                if (Config.GetBool("贪吃蛇模式") || StateManager.txtSource == TxtSource.raceApi)
                    _main.SnakeModeUpdateFromCopybook(_currentIndex);
                else
                    ScrollToCurrentChar();

                FlushPendingBackgroundChanges();
                UpdatePosition(true);
                _main.UpdateZiTi();
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!_isActive) return;
            Key inputKey = e.Key == Key.ImeProcessed ? e.ImeProcessedKey : e.Key;

            if (_main.TryHandleFinishedActionFromKey(inputKey))
            {
                e.Handled = FinishedInputPolicy.ShouldHandlePreviewKeyDown(true);
                return;
            }

            if (_finishGate.IsPending || StateManager.typingState == TypingState.end)
            {
                e.Handled = FinishedInputPolicy.ShouldHandlePreviewKeyDown(false);
                return;
            }

            bool isBackspace = inputKey == Key.Back;
            bool isDelete = inputKey == Key.Delete;
            bool shouldDeletePreviousWord = false;

            if (isBackspace)
            {
                bool isImeProcessedBackspace = e.Key == Key.ImeProcessed && e.ImeProcessedKey == Key.Back;
                if (isImeProcessedBackspace)
                    _imeBackspacePolicy.NotifyImeBackspaceStarted();
                shouldDeletePreviousWord = _imeBackspacePolicy.ShouldDeletePreviousWord(
                    isImeProcessedBackspace,
                    HasActiveComposition());
            }

            bool isNavKey = inputKey == Key.Left || inputKey == Key.Right
                            || inputKey == Key.Up || inputKey == Key.Down
                            || inputKey == Key.Home || inputKey == Key.End;

            // 禁用回改模式：拦截退格、Esc、Ctrl+Z、方向/Home/End 导航键
            // （赛文模式不受限制，IME编码中放行退格）
            if (Config.GetBool("禁用回改")
                && StateManager.txtSource != TxtSource.raceApi
                && StateManager.txtSource != TxtSource.jbs
                && StateManager.txtSource != TxtSource.jisucup)
            {
                if (isBackspace)
                {
                    if (shouldDeletePreviousWord)
                    {
                        e.Handled = true;
                        return;
                    }
                }
                else if (inputKey == Key.Escape ||
                    (inputKey == Key.Z && Keyboard.Modifiers == ModifierKeys.Control) ||
                    isNavKey ||
                    isDelete)
                {
                    e.Handled = true;
                    return;
                }
            }

            // 导航键：移动当前比对位置，不修改 wordStates。
            // IME 编码中放行，让候选框消费方向键。
            if (isNavKey && !HasActiveComposition())
            {
                HandleNavigationKey(inputKey, Keyboard.Modifiers);
                e.Handled = true;
                return;
            }

            // 按键统计（击键、键法、选重、标顶、退格等）
            _main.HandleKeyDownStats(e);

            // 直接按空格（非 IME 上屏）不会触发 PreviewTextInput，需要在这里手动处理
            // IME 用空格选词时 e.Key == Key.ImeProcessed，提交的文字走 OnTextInput，这里不处理
            if (inputKey == Key.Space && e.Key == Key.Space)
            {
                ProcessInputText(" ");
                ScheduleInputCaptureTrim();
                e.Handled = true;
                return;
            }

            if (isBackspace || isDelete)
            {
                if (isBackspace && !shouldDeletePreviousWord)
                {
                    return;
                }

                bool deleted = isBackspace ? _inputBuffer.Backspace() : _inputBuffer.Delete();
                if (deleted)
                {
                    _currentIndex = _inputBuffer.CaretIndex;
                    ClearCodeLabelProgressFrom(_currentIndex);
                    RefreshTypedStateFromInputBuffer();

                    UpdatePosition(true);
                    if (Config.GetBool("贪吃蛇模式") || StateManager.txtSource == TxtSource.raceApi)
                        _main.SnakeModeUpdateFromCopybook(_currentIndex);
                    else
                        ScrollToCurrentChar();

                    FlushPendingBackgroundChanges();
                    // 更新字提显示
                    _main.UpdateZiTi();
                    _main.UpdateTitleProgress(_inputBuffer.Length);
                }
                e.Handled = true;
            }
        }

        private void OnPreviewKeyUp(object sender, KeyEventArgs e)
        {
            Key inputKey = e.Key == Key.ImeProcessed ? e.ImeProcessedKey : e.Key;
            if (inputKey == Key.Back)
                _imeBackspacePolicy.NotifyPhysicalBackspaceReleased();
        }

        private void HandleNavigationKey(Key key, ModifierKeys mods)
        {
            int total = TextInfo.Words.Count;
            if (total == 0) return;

            bool ctrl = (mods & ModifierKeys.Control) == ModifierKeys.Control;
            int maxIdx = total - 1;
            int target = _currentIndex;

            switch (key)
            {
                case Key.Left:
                    target = ctrl ? FindPrevWordIndex(_currentIndex) : _currentIndex - 1;
                    break;
                case Key.Right:
                    target = ctrl ? FindNextWordIndex(_currentIndex) : _currentIndex + 1;
                    break;
                case Key.Up:
                {
                    int adj = FindAdjacentLineIndex(_currentIndex, upward: true);
                    if (adj >= 0) target = adj;
                    break;
                }
                case Key.Down:
                {
                    int adj = FindAdjacentLineIndex(_currentIndex, upward: false);
                    if (adj >= 0) target = adj;
                    break;
                }
                case Key.Home:
                {
                    if (ctrl) target = 0;
                    else
                    {
                        int lineStart = FindLineEdgeIndex(_currentIndex, toStart: true);
                        if (lineStart >= 0) target = lineStart;
                    }
                    break;
                }
                case Key.End:
                {
                    if (ctrl) target = maxIdx;
                    else
                    {
                        int lineEnd = FindTypedLineEndIndex(_currentIndex);
                        if (lineEnd >= 0) target = lineEnd;
                    }
                    break;
                }
            }

            if (target < 0) target = 0;
            if (target > maxIdx) target = maxIdx;
            if (target == _currentIndex) return;

            _currentIndex = target;
            _inputBuffer.MoveCaret(_currentIndex);
            _currentIndex = _inputBuffer.CaretIndex;
            Score.InputWordCount = _inputBuffer.Length;

            _main.ClearCodeLabelProgress(_currentIndex);
            UpdatePosition(true);
            if (Config.GetBool("贪吃蛇模式") || StateManager.txtSource == TxtSource.raceApi)
                _main.SnakeModeUpdateFromCopybook(_currentIndex);
            else
                ScrollToCurrentChar();
            _main.UpdateZiTi();
            _main.UpdateTitleProgress(_inputBuffer.Length);
        }

        private int FindPrevWordIndex(int curGlobal)
        {
            if (curGlobal <= 0) return 0;

            // 优先按词提分段跳：先回到当前段起点，已在起点则跳到上一段起点
            if (TextInfo.CiTiSegmentIndices != null
                && curGlobal < TextInfo.CiTiSegmentIndices.Count
                && TextInfo.CiTiSegmentIndices.Count > 0)
            {
                int curSeg = TextInfo.CiTiSegmentIndices[curGlobal];
                if (curSeg >= 0)
                {
                    // 当前段起点
                    int segStart = curGlobal;
                    while (segStart > 0 && TextInfo.CiTiSegmentIndices[segStart - 1] == curSeg)
                        segStart--;
                    if (segStart < curGlobal) return segStart;
                    // 已在起点：跳上一段起点
                    int prev = segStart - 1;
                    if (prev < 0) return 0;
                    int prevSeg = TextInfo.CiTiSegmentIndices[prev];
                    while (prev > 0 && TextInfo.CiTiSegmentIndices[prev - 1] == prevSeg)
                        prev--;
                    return prev;
                }
            }

            // 无词提：按标点边界跳
            int idx = curGlobal - 1;
            while (idx > 0 && !IsPunctuation(TextInfo.Words[idx - 1]))
                idx--;
            return idx;
        }

        private int FindNextWordIndex(int curGlobal)
        {
            int maxIdx = TextInfo.Words.Count - 1;
            if (curGlobal >= maxIdx) return maxIdx;

            if (TextInfo.CiTiSegmentIndices != null
                && curGlobal < TextInfo.CiTiSegmentIndices.Count
                && TextInfo.CiTiSegmentIndices.Count > 0)
            {
                int curSeg = TextInfo.CiTiSegmentIndices[curGlobal];
                int idx = curGlobal + 1;
                while (idx < TextInfo.CiTiSegmentIndices.Count
                       && TextInfo.CiTiSegmentIndices[idx] == curSeg)
                    idx++;
                return Math.Min(idx, maxIdx);
            }

            int p = curGlobal + 1;
            while (p < maxIdx && !IsPunctuation(TextInfo.Words[p - 1]))
                p++;
            return p;
        }

        private static bool IsPunctuation(string ch)
        {
            if (string.IsNullOrEmpty(ch)) return false;
            return "，。！？、；：,.!?;: ".IndexOf(ch[0]) >= 0
                || "　".IndexOf(ch[0]) >= 0;
        }

        // 在当前页 Blocks 中找到与当前字 Y 不同的相邻行，
        // 然后在该行里取 X 距离最近的字，返回全局索引。无可移动行返回 -1。
        private int FindAdjacentLineIndex(int curGlobal, bool upward)
        {
            int localCur = curGlobal - TextInfo.PageStartIndex;
            if (localCur < 0 || localCur >= TextInfo.Blocks.Count) return -1;

            try
            {
                var grid = (Grid)_main.BdDisplay.Child;
                var curBlock = TextInfo.Blocks[localCur];
                var curPos = curBlock.TranslatePoint(new Point(0, 0), grid);
                double fs = MainWindow.DisplayFontSize;
                double yTolerance = fs * 0.4;

                double targetY = double.NaN;
                for (int i = 0; i < TextInfo.Blocks.Count; i++)
                {
                    if (i == localCur) continue;
                    var pos = TextInfo.Blocks[i].TranslatePoint(new Point(0, 0), grid);
                    if (upward)
                    {
                        if (pos.Y < curPos.Y - yTolerance
                            && (double.IsNaN(targetY) || pos.Y > targetY))
                            targetY = pos.Y;
                    }
                    else
                    {
                        if (pos.Y > curPos.Y + yTolerance
                            && (double.IsNaN(targetY) || pos.Y < targetY))
                            targetY = pos.Y;
                    }
                }
                if (double.IsNaN(targetY)) return -1;

                int bestLocal = -1;
                double bestDist = double.MaxValue;
                for (int i = 0; i < TextInfo.Blocks.Count; i++)
                {
                    var pos = TextInfo.Blocks[i].TranslatePoint(new Point(0, 0), grid);
                    if (Math.Abs(pos.Y - targetY) > yTolerance) continue;
                    double dist = Math.Abs(pos.X - curPos.X);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestLocal = i;
                    }
                }

                return bestLocal >= 0 ? bestLocal + TextInfo.PageStartIndex : -1;
            }
            catch { return -1; }
        }

        private int FindTypedLineEndIndex(int curGlobal)
        {
            int localCur = curGlobal - TextInfo.PageStartIndex;
            if (localCur < 0 || localCur >= TextInfo.Blocks.Count) return -1;

            try
            {
                var lineIndexes = new List<int>();
                var grid = (Grid)_main.BdDisplay.Child;
                var curBlock = TextInfo.Blocks[localCur];
                var curPos = curBlock.TranslatePoint(new Point(0, 0), grid);
                double fs = MainWindow.DisplayFontSize;
                double yTolerance = fs * 0.4;

                for (int i = 0; i < TextInfo.Blocks.Count; i++)
                {
                    var pos = TextInfo.Blocks[i].TranslatePoint(new Point(0, 0), grid);
                    if (Math.Abs(pos.Y - curPos.Y) <= yTolerance)
                        lineIndexes.Add(i + TextInfo.PageStartIndex);
                }

                if (lineIndexes.Count == 0) return -1;

                return CopybookNavigation.FindEndTargetWithinTypedLine(
                    curGlobal,
                    TextInfo.Words.Count,
                    lineIndexes,
                    IsTypedIndex);
            }
            catch { return -1; }
        }

        private static bool IsTypedIndex(int globalIndex)
        {
            return globalIndex >= 0
                && globalIndex < TextInfo.wordStates.Count
                && TextInfo.wordStates[globalIndex] != WordStates.NO_TYPE;
        }

        // 在当前视觉行内找到最左或最右的字，返回全局索引
        private int FindLineEdgeIndex(int curGlobal, bool toStart)
        {
            int localCur = curGlobal - TextInfo.PageStartIndex;
            if (localCur < 0 || localCur >= TextInfo.Blocks.Count) return -1;

            try
            {
                var grid = (Grid)_main.BdDisplay.Child;
                var curBlock = TextInfo.Blocks[localCur];
                var curPos = curBlock.TranslatePoint(new Point(0, 0), grid);
                double fs = MainWindow.DisplayFontSize;
                double yTolerance = fs * 0.4;

                int bestLocal = localCur;
                double bestX = curPos.X;
                for (int i = 0; i < TextInfo.Blocks.Count; i++)
                {
                    var pos = TextInfo.Blocks[i].TranslatePoint(new Point(0, 0), grid);
                    if (Math.Abs(pos.Y - curPos.Y) > yTolerance) continue;
                    if (toStart ? pos.X < bestX : pos.X > bestX)
                    {
                        bestX = pos.X;
                        bestLocal = i;
                    }
                }
                return bestLocal + TextInfo.PageStartIndex;
            }
            catch { return -1; }
        }

        private bool HasActiveComposition()
        {
            return _isImeComposing || !string.IsNullOrEmpty(_activeCompositionText);
        }

        private void SetImeCompositionState(string composition)
        {
            _activeCompositionText = composition ?? "";
            _imeBackspacePolicy.NotifyCompositionText(_activeCompositionText);
            _isImeComposing = !string.IsNullOrEmpty(_activeCompositionText);
            if (_main.IsCodeDisplayEnabled())
            {
                if (_isImeComposing)
                    _main.UpdateCodeLabelProgress(_currentIndex, _activeCompositionText);
                else
                    _main.ClearCodeLabelProgress(_currentIndex);
                HideCompositionText();
            }
            else
            {
                UpdateCompositionText(_activeCompositionText);
            }
        }

        private void ClearImeCompositionState(bool clearFeedback = true)
        {
            _activeCompositionText = "";
            _isImeComposing = false;
            _imeBackspacePolicy.NotifyCompositionEnded();
            if (_main.IsCodeDisplayEnabled() && clearFeedback)
                _main.ClearCodeLabelProgress(_currentIndex);
            HideCompositionText();
        }

        private void UpdateCompositionText(string composition)
        {
            if (_compositionText == null)
                return;

            _compositionText.Inlines.Clear();
            _compositionText.Text = composition ?? "";

            _compositionText.Opacity = 1.0;
            if (_compositionText.RenderTransform is ScaleTransform scale)
            {
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                scale.ScaleX = 1.0;
                scale.ScaleY = 1.0;
            }

            _compositionText.Visibility = string.IsNullOrEmpty(composition)
                ? Visibility.Collapsed
                : Visibility.Visible;
            UpdateCompositionPosition();
        }

        private void HideCompositionText()
        {
            if (_compositionText == null)
                return;

            _compositionText.Inlines.Clear();
            _compositionText.Text = "";
            _compositionText.Visibility = Visibility.Collapsed;
        }

        private void ShowWrongCharHint(string wrongChar, int index)
        {
            if (_overlay == null || index < 0 || index >= TextInfo.Blocks.Count) return;

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

        private void ClearWrongCharHints()
        {
            if (_overlay != null)
            {
                foreach (var hint in _wrongCharHints)
                    _overlay.Children.Remove(hint);
            }

            _wrongCharHints.Clear();
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
            double viewportTop = _main.ScDisplay.VerticalOffset;
            double viewportBottom = viewportTop + _main.ScDisplay.ViewportHeight;
            foreach (var fe in _wrongCharHints)
            {
                var border = fe as Border;
                if (border == null) continue;
                if (!(border.Tag is int idx) || idx < 0 || idx >= TextInfo.Blocks.Count) continue;
                try
                {
                    var hint = (TextBlock)border.Child;
                    hint.FontSize = fs * 0.5;
                    double hintHeight = hint.FontSize * 1.2;
                    var block = TextInfo.Blocks[idx];
                    var pos = block.TranslatePoint(new Point(0, 0), grid);
                    double blockTop = pos.Y + _main.ScDisplay.VerticalOffset;
                    double blockBottom = blockTop + block.ActualHeight;

                    if (blockTop < viewportTop || blockBottom > viewportBottom || block.Opacity <= 0.001)
                    {
                        border.Visibility = Visibility.Collapsed;
                        continue;
                    }

                    border.Visibility = Visibility.Visible;
                    double blockCenter = pos.X + block.ActualWidth / 2;
                    border.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    double hintWidth = border.DesiredSize.Width;
                    Canvas.SetLeft(border, blockCenter - hintWidth / 2);
                    Canvas.SetTop(border, pos.Y - hintHeight - wrongOffset + 0.1 * fs);
                }
                catch { }
            }
        }

        /// <summary>
        /// 主题切换后刷新光标颜色、错字提示颜色，并重新定位光标
        /// </summary>
        public void RefreshTheme()
        {
            if (!_isActive) return;

            // 更新光标颜色
            _cursor?.ApplyForeground(Colors.DisplayForeground);
            _cursor?.UpdateBlinkingAnimation();

            // 更新已有错字提示的颜色
            foreach (var fe in _wrongCharHints)
            {
                var border = fe as Border;
                if (border == null) continue;
                border.Background = _main.BdDisplay.Background;
                border.BorderBrush = Colors.DisplayForeground;
                if (border.Child is TextBlock hint)
                    hint.Foreground = Colors.IncorrectBackground;
            }

            // 重新定位光标（TextBlocks 已被重建）
            _main.RefreshCodeLabelProgress();
            SyncCompositionPresentation();
            ScheduleUpdatePosition();
        }

        public void SyncCompositionPresentation()
        {
            if (!_isActive)
                return;

            if (_main.IsCodeDisplayEnabled())
            {
                if (!string.IsNullOrEmpty(_activeCompositionText))
                    _main.UpdateCodeLabelProgress(_currentIndex, _activeCompositionText);
                HideCompositionText();
            }
            else
            {
                if (!string.IsNullOrEmpty(_activeCompositionText))
                    UpdateCompositionText(_activeCompositionText);
                else
                    HideCompositionText();
            }
        }

        private void ScheduleFinalVisualsAndStop()
        {
            if (!_finishGate.TryBegin())
                return;

            _main.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!_isActive) return;

                FlushPendingBackgroundChanges();
                int lastIdx = TextInfo.Blocks.Count - 1;

                // 1. 更新光标到最后一个字右侧
                if (lastIdx >= 0 && _cursor != null)
                {
                    try
                    {
                        var grid = (Grid)_main.BdDisplay.Child;
                        var block = TextInfo.Blocks[lastIdx];
                        block.UpdateLayout();
                        var pos = block.TranslatePoint(new Point(0, 0), grid);
                        double fs = MainWindow.DisplayFontSize;

                        var fm = _main.GetCurrentFontFamily();
                        double height = fs * (1.0 + Config.GetDouble("行距"));
                        double availablePad = Math.Max(0, height - fs * fm.LineSpacing);
                        double padTop = (availablePad / 2 + Math.Min((height - fs) / 2, availablePad)) / 2;

                        double lineHeight = fs * fm.LineSpacing;
                        _cursor.SetPosition(pos.X + block.ActualWidth - 2, pos.Y + padTop, lineHeight);
                    }
                    catch { }
                }

                // 2. 结束（StopTyping 内部会刷新速度和速度跟随）
                _main.StopTyping();
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        private void UpdatePosition(bool animated = false)
        {
            if (_inputCapture == null || _currentIndex < 0 || TextInfo.Blocks.Count == 0)
                return;

            try
            {
                var grid = (Grid)_main.BdDisplay.Child;
                int visualIndex = Math.Min(_currentIndex, TextInfo.Blocks.Count - 1);
                var block = TextInfo.Blocks[visualIndex];
                block.UpdateLayout();
                var pos = block.TranslatePoint(new Point(0, 0), grid);
                double x = pos.X;
                double y = pos.Y;
                if (_currentIndex >= TextInfo.Blocks.Count)
                    x += block.ActualWidth;

                double fs = MainWindow.DisplayFontSize;
                double candidateOffset = Config.GetDouble("字帖候选框高度") * fs;
                double codeDisplayExtra = _main.GetCodeDisplayImeOffset(fs);

                // 计算文字在 TextBlock 内的实际 padTop（与 PageReArrange 一致）
                var fm = _main.GetCurrentFontFamily();
                double height = fs * (1.0 + Config.GetDouble("行距"));
                double availablePad = Math.Max(0, height - fs * fm.LineSpacing);
                double padTop = (availablePad / 2 + Math.Min((height - fs) / 2, availablePad)) / 2;

                // InputCapture 控制 IME 候选框位置
                Canvas.SetLeft(_inputCapture, x);
                double inputTop = y + 1.0 * fs + candidateOffset + codeDisplayExtra;
                Canvas.SetTop(_inputCapture, inputTop);
                _main.UpdateImeCandidateWindowPosition(grid, new Point(x, inputTop));

                // 自定义光标定位到当前字左侧，与文字垂直居中对齐
                if (_cursor != null)
                {
                    double lineHeight = fs * fm.LineSpacing;
                    if (animated)
                        _cursor.AnimatePosition(x - 2, y + padTop, lineHeight);
                    else if (_isScrollAnimating)
                        _cursor.TrackPosition(x - 2, y + padTop, lineHeight);
                    else
                        _cursor.SetPosition(x - 2, y + padTop, lineHeight);
                }

                PositionCodeTextElement(_compositionText, visualIndex);
            }
            catch { }
        }

        private void UpdateCompositionPosition()
        {
            PositionCodeTextElement(_compositionText, _currentIndex);
        }

        private void PositionCodeTextElement(TextBlock element, int index)
        {
            if (element == null || index < 0 || index >= TextInfo.Blocks.Count || TextInfo.Blocks.Count == 0)
                return;

            try
            {
                var grid = (Grid)_main.BdDisplay.Child;
                var block = TextInfo.Blocks[index];
                var pos = block.TranslatePoint(new Point(0, 0), grid);
                double fs = MainWindow.DisplayFontSize;
                double compositionOffset = Config.GetDouble("字帖编码高度") * fs;
                double x = pos.X;
                double y = pos.Y + block.ActualHeight - 0.25 * fs + compositionOffset;

                Canvas.SetLeft(element, x);
                Canvas.SetTop(element, y);
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
                _main.SmoothScrollTo(targetOffset, started: StartScrollSync, completed: StopScrollSync);
            }
            catch { }
        }

        private void StartScrollSync()
        {
            if (!_isActive || _isScrollAnimating)
                return;

            _isScrollAnimating = true;
            CompositionTarget.Rendering += OnRenderingDuringScroll;
        }

        private void StopScrollSync()
        {
            if (_isScrollAnimating)
                CompositionTarget.Rendering -= OnRenderingDuringScroll;

            if (_isActive)
                UpdatePosition(false);

            _isScrollAnimating = false;
        }

        private void QueueDisplayBlockStateBackground(int globalIndex, Brush background)
        {
            RemovePendingBackgroundChange(globalIndex);
            _pendingBackgroundChanges.Add(new PendingBackgroundChange
            {
                GlobalIndex = globalIndex,
                Background = background
            });
        }

        private void RemovePendingBackgroundChange(int globalIndex)
        {
            for (int i = _pendingBackgroundChanges.Count - 1; i >= 0; i--)
            {
                if (_pendingBackgroundChanges[i].GlobalIndex == globalIndex)
                    _pendingBackgroundChanges.RemoveAt(i);
            }
        }

        private void FlushPendingBackgroundChanges()
        {
            if (_pendingBackgroundChanges.Count == 0)
                return;

            foreach (var change in _pendingBackgroundChanges)
                _main.SetDisplayBlockStateBackgroundByGlobalIndex(change.GlobalIndex, change.Background);

            _pendingBackgroundChanges.Clear();
        }

        private void OnRenderingDuringScroll(object sender, EventArgs e)
        {
            if (!_isActive || !_isScrollAnimating)
                return;

            UpdatePosition(false);
        }
    }
}
