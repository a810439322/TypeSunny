$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$mainCode = Get-Content -Path (Join-Path $root 'UI\MainWindow.xaml.cs') -Raw

function Assert-Contains($name, $content, $needle) {
    if (-not $content.Contains($needle)) {
        throw "$name expected to contain [$needle]"
    }
}

Assert-Contains 'typing cursor visibility helper' $mainCode 'private void UpdateMouseCursorForTypingState()'
Assert-Contains 'cursor visibility helper dispatches to UI thread' $mainCode 'if (!Dispatcher.CheckAccess())'
Assert-Contains 'typing state hides mouse cursor' $mainCode 'Cursor = StateManager.typingState == TypingState.typing ? Cursors.None : Cursors.Arrow;'
Assert-Contains 'text input typing state updates cursor' $mainCode 'StartTypingSessionFromInput();'
Assert-Contains 'enter pause restores cursor' $mainCode 'PauseTypingSession();'
Assert-Contains 'window deactivate pause restores cursor' $mainCode 'PauseTypingSession();'
Assert-Contains 'new article ready restores cursor' $mainCode 'UpdateMouseCursorForTypingState();'
Assert-Contains 'finish end restores cursor' $mainCode 'UpdateMouseCursorForTypingState();'

Write-Host 'All typing cursor visibility tests passed.'
