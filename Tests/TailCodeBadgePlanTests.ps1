$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot

function Assert-Contains($name, $text, $needle) {
    if ($text -notlike "*$needle*") {
        throw "FAIL: $name. Missing: $needle"
    }
    Write-Host "PASS: $name"
}

function Assert-NotContains($name, $text, $needle) {
    if ($text -like "*$needle*") {
        throw "FAIL: $name. Unexpected: $needle"
    }
    Write-Host "PASS: $name"
}

function Assert-Ordered($name, $text, $first, $second) {
    $firstIndex = $text.IndexOf($first, [System.StringComparison]::Ordinal)
    $secondIndex = $text.IndexOf($second, [System.StringComparison]::Ordinal)
    if ($firstIndex -lt 0 -or $secondIndex -lt 0 -or $firstIndex -ge $secondIndex) {
        throw "FAIL: $name. Expected '$first' before '$second'."
    }
    Write-Host "PASS: $name"
}

$configCode = Get-Content -Path (Join-Path $root 'Config\Config.cs') -Raw
$winConfigCode = Get-Content -Path (Join-Path $root 'WinConfig\WinConfig.xaml.cs') -Raw
$mainCode = Get-Content -Path (Join-Path $root 'UI\MainWindow.xaml.cs') -Raw
$paginatorCode = Get-Content -Path (Join-Path $root 'Core\Paginator.cs') -Raw
$copybookModeCode = Get-Content -Path (Join-Path $root 'UI\Modes\CopybookMode.cs') -Raw
$tracingModeCode = Get-Content -Path (Join-Path $root 'UI\Modes\TracingMode.cs') -Raw

Assert-Contains 'config has CiTi tail badge default enabled' $configCode '"词提尾码角标", "是"'
Assert-Contains 'config has ZiTi tail badge default enabled' $configCode '"字提尾码角标", "是"'
Assert-Contains 'WinConfig adds CiTi tail badge item' $winConfigCode '"词提尾码角标"'
Assert-Contains 'WinConfig adds ZiTi tail badge item' $winConfigCode '"字提尾码角标"'
Assert-Ordered 'WinConfig places CiTi tail badge after CiTi lower display item' $winConfigCode '"词提编码下显"' '"词提尾码角标"'
Assert-Ordered 'WinConfig places ZiTi tail badge after ZiTi lower display item' $winConfigCode '"字提编码下显"' '"字提尾码角标"'
Assert-Contains 'MainWindow exposes full inline code display helper' $mainCode 'internal bool IsFullCodeDisplayEnabled()'
Assert-Contains 'MainWindow has a full code display element branch' $mainCode 'CreateFullCodeDisplayElement'
Assert-Contains 'MainWindow has a tail badge element branch' $mainCode 'CreateTailBadgeElement'
Assert-Contains 'MainWindow extracts tail badge text through helper' $mainCode 'CodeDisplayHelper.TryGetTailBadgeText'
Assert-Contains 'MainWindow gates code label progress on full display only' $mainCode 'if (!IsFullCodeDisplayEnabled()) return;'
Assert-Contains 'Paginator line height still follows lower code display switches only' $paginatorCode 'Config.GetBool("词提编码下显") || Config.GetBool("字提编码下显")'
Assert-Contains 'Copybook input offset follows full inline code display only' $copybookModeCode '_main.IsFullCodeDisplayEnabled() ? fs * 0.55 : 0.0'
Assert-Contains 'Tracing input offset follows full inline code display only' $tracingModeCode '_main.IsFullCodeDisplayEnabled() ? fs * 0.55 : 0.0'
Assert-NotContains 'Paginator ignores CiTi tail badge switch' $paginatorCode '"词提尾码角标"'
Assert-NotContains 'Paginator ignores ZiTi tail badge switch' $paginatorCode '"字提尾码角标"'
Assert-NotContains 'Copybook ignores CiTi tail badge switch' $copybookModeCode '"词提尾码角标"'
Assert-NotContains 'Copybook ignores ZiTi tail badge switch' $copybookModeCode '"字提尾码角标"'
Assert-NotContains 'Tracing ignores CiTi tail badge switch' $tracingModeCode '"词提尾码角标"'
Assert-NotContains 'Tracing ignores ZiTi tail badge switch' $tracingModeCode '"字提尾码角标"'
