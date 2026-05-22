$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$mainCode = Get-Content -Path (Join-Path $root 'UI\MainWindow.xaml.cs') -Raw
$mainXaml = Get-Content -Path (Join-Path $root 'UI\MainWindow.xaml') -Raw

function Assert-Contains($name, $content, $needle) {
    if (-not $content.Contains($needle)) {
        throw "$name expected to contain [$needle]"
    }
}

Assert-Contains 'typing cursor visibility helper' $mainCode 'private void UpdateMouseCursorForTypingState()'
Assert-Contains 'cursor visibility helper dispatches to UI thread' $mainCode 'if (!Dispatcher.CheckAccess())'
Assert-Contains 'typing state hides mouse cursor' $mainCode 'Cursor = StateManager.typingState == TypingState.typing ? Cursors.None : Cursors.Arrow;'
Assert-Contains 'typing cursor reveal duration is 3 seconds' $mainCode 'private const int MouseCursorTemporaryRevealMilliseconds = 3000;'
Assert-Contains 'typing cursor reveal movement threshold exists' $mainCode 'private const double MouseCursorRevealMovementThreshold = 2.0;'
Assert-Contains 'typing cursor reveal timer exists' $mainCode 'private DispatcherTimer _mouseCursorRevealTimer;'
Assert-Contains 'typing cursor reveal last position is tracked' $mainCode 'private Point? _lastMouseCursorRevealPosition;'
Assert-Contains 'window mouse move reveals hidden typing cursor' $mainXaml 'PreviewMouseMove="MainWin_PreviewMouseMove"'
Assert-Contains 'window mouse click reveals hidden typing cursor' $mainXaml 'PreviewMouseDown="MainWin_PreviewMouseInteraction"'
Assert-Contains 'typing cursor reveal mouse move handler exists' $mainCode 'private void MainWin_PreviewMouseMove(object sender, MouseEventArgs e)'
Assert-Contains 'typing cursor reveal mouse click handler exists' $mainCode 'private void MainWin_PreviewMouseInteraction(object sender, System.Windows.Input.MouseButtonEventArgs e)'
Assert-Contains 'typing cursor reveal helper exists' $mainCode 'private void TemporarilyRevealMouseCursorDuringTyping()'
Assert-Contains 'typing cursor reveal threshold helper exists' $mainCode 'private bool ShouldRevealMouseCursorForPointerPosition(Point currentPosition)'
Assert-Contains 'typing cursor reveal mouse move uses pointer position' $mainCode 'if (!ShouldRevealMouseCursorForPointerPosition(e.GetPosition(this)))'
Assert-Contains 'typing cursor reveal helper shows arrow' $mainCode 'SetMouseCursor(Cursors.Arrow, null);'
Assert-Contains 'typing cursor reveal timer hides again' $mainCode 'HideMouseCursorAfterRevealTimer'
Assert-Contains 'text input typing state updates cursor' $mainCode 'StartTypingSessionFromInput();'
Assert-Contains 'enter pause restores cursor' $mainCode 'PauseTypingSession();'
Assert-Contains 'window deactivate pause restores cursor' $mainCode 'PauseTypingSession();'
Assert-Contains 'new article ready restores cursor' $mainCode 'UpdateMouseCursorForTypingState();'
Assert-Contains 'finish end restores cursor' $mainCode 'UpdateMouseCursorForTypingState();'

Write-Host 'All typing cursor visibility tests passed.'
