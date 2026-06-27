$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$configCode = Get-Content -Path (Join-Path $root 'Config\Config.cs') -Raw -Encoding UTF8
$winConfigCode = Get-Content -Path (Join-Path $root 'WinConfig\WinConfig.xaml.cs') -Raw -Encoding UTF8
$mainCode = Get-Content -Path (Join-Path $root 'UI\MainWindow.xaml.cs') -Raw -Encoding UTF8

function Assert-Contains($name, $content, $needle) {
    if (-not $content.Contains($needle)) {
        throw "$name expected to contain [$needle]"
    }
}

function Assert-Matches($name, $content, $pattern) {
    if (-not [regex]::IsMatch($content, $pattern)) {
        throw "$name expected to match [$pattern]"
    }
}

Assert-Matches 'group article filter default is enabled' $configCode '"\u8FC7\u6EE4_\u751F\u6548_\u7FA4\u8F7D\u6587",\s*"\u662F"'
Assert-Contains 'filter scope grid has five columns' $winConfigCode 'scopeGrid.Columns = 5'
Assert-Matches 'filter scope names include group article' $winConfigCode 'string\[\]\s+scopeNames\s+=\s+\{\s*"\u6587\u6765",\s*"\u672C\u5730\u53D1\u6587",\s*"\u7EC3\u5355\u5668",\s*"\u526A\u8D34\u677F",\s*"\u7FA4\u8F7D\u6587"\s*\}'
Assert-Matches 'group article source uses regex filter scope' $mainCode 'source\s*==\s*TxtSource\.qq\s*&&\s*RegexFilter\.IsEnabled\("\u7FA4\u8F7D\u6587"\)'
Assert-Matches 'group article filter has dedicated prompt' $mainCode '\u8BE5\u7FA4\u8F7D\u6587\u88AB\u8FC7\u6EE4\u89C4\u5219\u5C4F\u853D'

Write-Host 'All regex filter scope tests passed.'
