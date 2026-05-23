$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot

function Assert-Contains($name, $text, $needle) {
    if (-not $text.Contains($needle)) {
        throw "FAIL: $name. Missing: $needle"
    }
    Write-Host "PASS: $name"
}

function Assert-NotContains($name, $text, $needle) {
    if ($text.Contains($needle)) {
        throw "FAIL: $name. Unexpected: $needle"
    }
    Write-Host "PASS: $name"
}

function Assert-ImeHostTrimPolicy($modeName, $relativePath) {
    $code = Get-Content -Path (Join-Path $root $relativePath) -Raw

    Assert-NotContains "$modeName does not synchronously trim from TextChanged" $code '_inputCapture.TextChanged += OnInputCaptureTextChanged'
    Assert-NotContains "$modeName does not detach TextChanged trim handler" $code '_inputCapture.TextChanged -= OnInputCaptureTextChanged'
    Assert-NotContains "$modeName has no TextChanged trim handler method" $code 'private void OnInputCaptureTextChanged'
    Assert-Contains "$modeName trims IME host text only after dispatcher idle" $code 'DispatcherPriority.ApplicationIdle'
    Assert-NotContains "$modeName does not schedule IME host trim at Normal priority" $code 'DispatcherPriority.Normal'
}

Assert-ImeHostTrimPolicy 'Copybook mode' 'UI\Modes\CopybookMode.cs'
Assert-ImeHostTrimPolicy 'Tracing mode' 'UI\Modes\TracingMode.cs'

Write-Host 'All IME composition host trim tests passed.'
