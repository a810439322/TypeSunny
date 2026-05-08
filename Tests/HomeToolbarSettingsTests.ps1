$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
Add-Type -Path (Join-Path $root 'UI\HomeToolbarSettings.cs')
$configCode = Get-Content -Path (Join-Path $root 'Config\Config.cs') -Raw

function Assert-Sequence($name, $expected, $actual) {
    $expectedText = [string]::Join(',', $expected)
    $actualText = [string]::Join(',', $actual)
    if ($expectedText -ne $actualText) {
        throw "$name expected [$expectedText], got [$actualText]"
    }
}

function Assert-Equal($name, $expected, $actual) {
    if ($expected -ne $actual) {
        throw "$name expected [$expected], got [$actual]"
    }
}

$visibility = [System.Collections.Generic.Dictionary[string,bool]]::new()
foreach ($entry in [TypeSunny.UI.HomeToolbarSettings]::FeatureEntries) {
    $visibility[$entry.VisibilityConfigKey] = $true
}

$defaultOrder = [TypeSunny.UI.HomeToolbarSettings]::GetVisibleFeatureEntries('', $visibility)
Assert-Sequence 'default order' @('wenlai', 'trainer', 'shuang', 'race') ($defaultOrder | ForEach-Object { $_.Key })
Assert-Equal 'default config order' $true ($configCode.Contains('"首页功能按钮顺序", "文来,晴练单,晴双拼,赛文"'))

$normalized = [TypeSunny.UI.HomeToolbarSettings]::NormalizeFeatureOrder('赛文,文来,未知,文来')
Assert-Equal 'normalized order' '赛文,文来,晴练单,晴双拼' $normalized

$visibility['显示首页文来'] = $false
$filtered = [TypeSunny.UI.HomeToolbarSettings]::GetVisibleFeatureEntries('赛文,文来,晴练单,晴双拼', $visibility)
Assert-Sequence 'hidden entries are filtered' @('race', 'trainer', 'shuang') ($filtered | ForEach-Object { $_.Key })

$shuang = [TypeSunny.UI.HomeToolbarSettings]::FindFeatureEntry('shuang')
Assert-Equal 'shuang label' '晴双拼' $shuang.DisplayName

$fixedModules = [TypeSunny.UI.HomeToolbarSettings]::FixedModuleEntries
Assert-Sequence 'fixed module labels' @('设置', '本地文章模块', '重打', '剪贴板载文', '群载文', '选群') ($fixedModules | ForEach-Object { $_.DisplayName })

Write-Host 'All HomeToolbarSettings tests passed.'
