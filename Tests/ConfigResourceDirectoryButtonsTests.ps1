$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$configCode = Get-Content -Path (Join-Path $root 'WinConfig\WinConfig.xaml.cs') -Raw

function Assert-Contains($name, $content, $needle) {
    if (-not $content.Contains($needle)) {
        throw "$name expected to contain [$needle]"
    }
}

function Assert-Ordered($name, $content, $first, $second) {
    $firstIndex = $content.IndexOf($first)
    $secondIndex = if ($firstIndex -ge 0) { $content.IndexOf($second, $firstIndex) } else { -1 }
    if ($firstIndex -lt 0 -or $secondIndex -lt 0 -or $firstIndex -ge $secondIndex) {
        throw "$name expected [$first] before [$second]"
    }
}

Assert-Contains 'config resource directory opener helper exists' $configCode 'private void OpenResourceDirectory(string folderName)'
Assert-Contains 'directory opener creates missing resource folder' $configCode 'System.IO.Directory.CreateDirectory(folderPath)'
Assert-Contains 'directory opener uses explorer shell execution' $configCode 'UseShellExecute = true'
Assert-Contains 'directory button helper exists' $configCode 'private Button CreateOpenDirectoryButton(string folderName)'
Assert-Contains 'directory button label' $configCode 'Content = "打开目录"'

Assert-Ordered 'ZiTi scheme opens a resource directory from same row' $configCode 'Tag = "ZiTiScheme"' 'CreateSelectorWithDirectoryButton(cb,'
Assert-Ordered 'CiTi scheme opens a resource directory from same row' $configCode 'Tag = "CiTiScheme"' 'CreateSelectorWithDirectoryButton(cb,'
Assert-Ordered 'Logo selector opens icon directory from same row' $configCode 'Tag = "CurrentLogo"' 'CreateSelectorWithDirectoryButton(cb, "ico")'

Assert-Contains 'rename dialog allows content-sized height' $configCode 'SizeToContent = SizeToContent.Height'
Assert-Contains 'rename dialog leaves enough themed chrome height' $configCode 'MinHeight = 220'
Assert-Contains 'rename dialog has bottom padding for buttons' $configCode 'Margin = new Thickness(20, 20, 20, 24)'
Assert-Contains 'rename dialog ok button marked as default' $configCode 'IsDefault = true'
Assert-Contains 'rename dialog cancel button marked as cancel' $configCode 'IsCancel = true'

Write-Host 'Config resource directory button tests passed.'
