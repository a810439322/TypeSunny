$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$paginator = Get-Content -Path (Join-Path $root 'Core\Paginator.cs') -Raw
$mainCode = Get-Content -Path (Join-Path $root 'UI\MainWindow.xaml.cs') -Raw
$copybook = Get-Content -Path (Join-Path $root 'UI\Modes\CopybookMode.cs') -Raw
$tracing = Get-Content -Path (Join-Path $root 'UI\Modes\TracingMode.cs') -Raw

function Assert-Contains($name, $content, $needle) {
    if (-not $content.Contains($needle)) {
        throw "${name}: expected to find [$needle]"
    }
}

function Assert-NotContains($name, $content, $needle) {
    if ($content.Contains($needle)) {
        throw "${name}: expected not to find [$needle]"
    }
}

Assert-NotContains 'paginator should not expand line height for code display' $paginator 'lineH *= 1.5'
Assert-NotContains 'main display element should not stack code vertically' $mainCode 'new StackPanel { Orientation = Orientation.Vertical }'
Assert-NotContains 'copybook/tracing should not disable original lower code display' $mainCode '|| IsCopybookOrTracingActive()'
Assert-Contains 'main should keep typed code feedback state by global index' $mainCode 'CodeLabelInputs'
Assert-Contains 'main should update lower code labels from typed text by global index' $mainCode 'UpdateCodeLabelProgress(int globalIndex, string typedText)'
Assert-Contains 'main should clear lower code label feedback when input is undone or display is disabled' $mainCode 'ClearCodeLabelProgress'
Assert-Contains 'main should clear all lower code feedback when code display is turned off' $mainCode 'ClearAllCodeLabelProgress();'
Assert-NotContains 'copybook composition position should not depend on display-only code offset' $copybook 'codeDisplayExtra'
Assert-NotContains 'tracing composition position should not depend on display-only code offset' $tracing 'codeDisplayExtra'
Assert-NotContains 'copybook should not create extra persistent typed-code hints' $copybook 'ShowTypedCodeHint'
Assert-NotContains 'tracing should not create extra persistent typed-code hints' $tracing 'ShowTypedCodeHint'
Assert-NotContains 'copybook should not color the IME composition overlay' $copybook 'GetCompositionGlyphBrush'
Assert-NotContains 'tracing should not color the IME composition overlay' $tracing 'GetCompositionGlyphBrush'
Assert-Contains 'copybook should resync presentation when code display is toggled' $copybook 'public void SyncCompositionPresentation()'
Assert-Contains 'tracing should resync presentation when code display is toggled' $tracing 'public void SyncCompositionPresentation()'
Assert-Contains 'main should expose typing-code helper' $mainCode 'GetTypingCodeText'
