$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$jbsHelperCode = Get-Content -Path (Join-Path $root 'net\JbsHelper.cs') -Raw
$jiSuCupHelperCode = Get-Content -Path (Join-Path $root 'net\JiSuCupHelper.cs') -Raw
$mainWindowCode = Get-Content -Path (Join-Path $root 'UI\MainWindow.xaml.cs') -Raw

function Assert-Contains($name, $content, $needle) {
    if (-not $content.Contains($needle)) {
        throw "$name expected to contain [$needle]"
    }
}

function Assert-NotContains($name, $content, $needle) {
    if ($content.Contains($needle)) {
        throw "$name expected not to contain [$needle]"
    }
}

Assert-Contains 'JBS login dialog submits with Enter' $jbsHelperCode 'IsDefault = true'
Assert-Contains 'JiSuCup login dialog submits with Enter' $jiSuCupHelperCode 'IsDefault = true'
Assert-Contains 'JBS login success refreshes its load article item' $jbsHelperCode 'UpdateArticleButtonStatus();'
Assert-Contains 'JBS helper reads shared display name' $jbsHelperCode 'JsRaceLoginState.DisplayName'
Assert-Contains 'JiSuCup helper reads shared display name' $jiSuCupHelperCode 'JsRaceLoginState.DisplayName'
Assert-Contains 'JBS helper saves shared login state' $jbsHelperCode 'JsRaceLoginState.SaveLogin'
Assert-Contains 'JiSuCup helper saves shared login state' $jiSuCupHelperCode 'JsRaceLoginState.SaveLogin'
Assert-Contains 'JBS load article gate uses shared login state' $mainWindowCode 'JsRaceLoginState.IsLoggedIn'
Assert-Contains 'JiSuCup load article gate uses shared login state' $mainWindowCode 'JsRaceLoginState.IsLoggedIn'
Assert-NotContains 'MainWindow should not read split JBS display directly' $mainWindowCode 'Config.GetString("极速显示名称")'
Assert-NotContains 'MainWindow should not read split JiSuCup display directly' $mainWindowCode 'Config.GetString("极速杯显示名称")'

Write-Host 'All JS race UI behavior tests passed.'
