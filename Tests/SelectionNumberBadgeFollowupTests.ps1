$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$configCode = Get-Content -Path (Join-Path $root 'Config\Config.cs') -Raw
$winConfigCode = Get-Content -Path (Join-Path $root 'WinConfig\WinConfig.xaml.cs') -Raw
$mainCode = Get-Content -Path (Join-Path $root 'UI\MainWindow.xaml.cs') -Raw

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

Assert-Contains 'config defaults CiTi selection-number badge off when lower display defaults on' $configCode '"词提选重数字角标", "否"'
Assert-Contains 'config uses selection-number badge name for ZiTi' $configCode '"字提选重数字角标", "是"'
Assert-Contains 'config normalizes selection-number badge mutual exclusion' $configCode 'EnforceSelectionNumberBadgeMutualExclusion();'
Assert-Contains 'WinConfig shows CiTi selection-number badge item' $winConfigCode '"词提选重数字角标"'
Assert-Contains 'WinConfig shows ZiTi selection-number badge item' $winConfigCode '"字提选重数字角标"'
Assert-Ordered 'WinConfig places CiTi badge after CiTi code display' $winConfigCode '"词提编码下显"' '"词提选重数字角标"'
Assert-Ordered 'WinConfig places ZiTi badge after ZiTi code display' $winConfigCode '"字提编码下显"' '"字提选重数字角标"'
Assert-Contains 'WinConfig finds CiTi badge toggle for mutual exclusion' $winConfigCode 'FindCheckBoxByLabel("词提选重数字角标")'
Assert-Contains 'WinConfig finds ZiTi badge toggle for mutual exclusion' $winConfigCode 'FindCheckBoxByLabel("字提选重数字角标")'
Assert-Contains 'WinConfig turns off CiTi badge when CiTi lower display is saved on' $winConfigCode 'UpsertConfigValue(key, value, "词提选重数字角标", "否")'
Assert-Contains 'WinConfig turns off ZiTi badge when ZiTi lower display is saved on' $winConfigCode 'UpsertConfigValue(key, value, "字提选重数字角标", "否")'
Assert-Contains 'WinConfig turns off CiTi lower display when CiTi badge is saved on' $winConfigCode 'UpsertConfigValue(key, value, "词提编码下显", "否")'
Assert-Contains 'WinConfig turns off ZiTi lower display when ZiTi badge is saved on' $winConfigCode 'UpsertConfigValue(key, value, "字提编码下显", "否")'
Assert-Contains 'WinConfig adds CiTi color shutdown toggle' $winConfigCode '"词提关闭所有颜色"'
Assert-Ordered 'WinConfig places CiTi color shutdown after CiTi palette' $winConfigCode '"词提选重色"' '"词提关闭所有颜色"'
Assert-Contains 'MainWindow resolves badge even when full code display is enabled' $mainCode 'TryGetSelectionNumberBadgeDisplay'
Assert-Contains 'MainWindow can render badge alongside full code display' $mainCode 'CreateFullCodeDisplayElement(textBlock, globalIndex, badgeText, badgeBrush)'
Assert-Contains 'MainWindow uses CiTi color shutdown toggle for source foreground' $mainCode 'Config.GetBool("词提关闭所有颜色")'
Assert-Contains 'MainWindow uses CiTi color shutdown toggle for code display color' $mainCode 'GetCiTiDisplayColor(globalIndex)'
Assert-Contains 'MainWindow uses CiTi color shutdown toggle for badge color' $mainCode 'GetCiTiBadgeColor(globalIndex)'
Assert-NotContains 'old tail badge naming should be gone from config' $configCode '尾码角标'
Assert-NotContains 'old tail badge naming should be gone from win config' $winConfigCode '尾码角标'
