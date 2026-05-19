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
Assert-NotContains 'copybook composition position should not depend on display-only code offset' $copybook 'codeDisplayExtra'
Assert-NotContains 'tracing composition position should not depend on display-only code offset' $tracing 'codeDisplayExtra'
Assert-Contains 'main should expose typing-code helper' $mainCode 'GetTypingCodeText'
