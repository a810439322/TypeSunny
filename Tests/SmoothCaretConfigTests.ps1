$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$configCode = Get-Content -Raw -Encoding UTF8 (Join-Path $root 'Config\Config.cs')
$configWindowCode = Get-Content -Raw -Encoding UTF8 (Join-Path $root 'WinConfig\WinConfig.xaml.cs')

function Join-Chars {
    param([int[]]$Codes)
    return -join ($Codes | ForEach-Object { [char]$_ })
}

$followTitle = Join-Chars @(0x8ddf, 0x6253)
$copybookMode = Join-Chars @(0x5b57, 0x5e16, 0x6a21, 0x5f0f)
$tracingMode = Join-Chars @(0x4e34, 0x6479, 0x6a21, 0x5f0f)
$blindMode = Join-Chars @(0x76f2, 0x6253, 0x6a21, 0x5f0f)
$smoothCaret = Join-Chars @(0x5e73, 0x6ed1, 0x5149, 0x6807)
$smoothCaretMode = Join-Chars @(0x5e73, 0x6ed1, 0x5149, 0x6807, 0x6a21, 0x5f0f)
$smoothCaretFixedDuration = Join-Chars @(0x5e73, 0x6ed1, 0x5149, 0x6807, 0x56fa, 0x5b9a, 0x65f6, 0x957f)
$smoothLineWrap = Join-Chars @(0x5e73, 0x6ed1, 0x6362, 0x884c)
$smoothScroll = Join-Chars @(0x5e73, 0x6ed1, 0x6eda, 0x52a8)
$smoothFast = Join-Chars @(0x5e73, 0x6ed1, 0x5149, 0x6807, 0x5feb)
$smoothMedium = Join-Chars @(0x5e73, 0x6ed1, 0x5149, 0x6807, 0x4e2d)
$smoothSlow = Join-Chars @(0x5e73, 0x6ed1, 0x5149, 0x6807, 0x6162)
$dynamic = Join-Chars @(0x52a8, 0x6001)
$fixed = Join-Chars @(0x56fa, 0x5b9a)
$yes = Join-Chars @(0x662f)
$no = Join-Chars @(0x5426)
$off = Join-Chars @(0x5173, 0x95ed)
$slow = Join-Chars @(0x6162)
$medium = Join-Chars @(0x4e2d)
$fast = Join-Chars @(0x5feb)
$smoothCaretLabel = Join-Chars @(0x5e73, 0x6ed1, 0x5149, 0x6807)
$smoothCaretModeLabel = Join-Chars @(0x5149, 0x6807, 0x52a8, 0x753b, 0x6a21, 0x5f0f)
$fixedDurationLabel = Join-Chars @(0x56fa, 0x5b9a, 0x52a8, 0x753b, 0x65f6, 0x957f)
$rapidTypingDurationLabel = Join-Chars @(0x8fde, 0x6253, 0x65f6, 0x52a8, 0x753b, 0x65f6, 0x957f)
$normalTypingDurationLabel = Join-Chars @(0x6b63, 0x5e38, 0x8f93, 0x5165, 0x52a8, 0x753b, 0x65f6, 0x957f)
$pauseDurationLabel = Join-Chars @(0x505c, 0x987f, 0x540e, 0x52a8, 0x753b, 0x65f6, 0x957f)

function Assert-Match {
    param(
        [string]$Name,
        [string]$Text,
        [string]$Pattern
    )

    if ($Text -notmatch $Pattern) {
        throw $Name
    }
}

function Assert-NotMatch {
    param(
        [string]$Name,
        [string]$Text,
        [string]$Pattern
    )

    if ($Text -match $Pattern) {
        throw $Name
    }
}

function Quote-Regex {
    param([string]$Text)
    return [regex]::Escape($Text)
}

$followCategoryMatch = [regex]::Match(
    $configWindowCode,
    "Title\s*=\s*`"$(Quote-Regex $followTitle)`"[\s\S]*?Items\s*=\s*new\[\][\s\S]*?\{(?<items>[\s\S]*?)\}\s*\}\s*,\s*new ConfigCategory")

if (-not $followCategoryMatch.Success) {
    throw 'Unable to find the follow typing settings category.'
}

$followItems = $followCategoryMatch.Groups['items'].Value

Assert-Match 'Smooth caret should default to enabled.' $configCode "`"$(Quote-Regex $smoothCaret)`"\s*,\s*`"$(Quote-Regex $yes)`""
Assert-Match 'Smooth caret mode should default to dynamic.' $configCode "`"$(Quote-Regex $smoothCaretMode)`"\s*,\s*`"$(Quote-Regex $dynamic)`""
Assert-Match 'Smooth caret fixed duration should default to 200ms.' $configCode "`"$(Quote-Regex $smoothCaretFixedDuration)`"\s*,\s*`"200`""
Assert-Match 'Smooth line wrap should default to yes.' $configCode "`"$(Quote-Regex $smoothLineWrap)`"\s*,\s*`"$(Quote-Regex $yes)`""
Assert-Match 'Smooth caret fast duration should default to 140ms.' $configCode "`"$(Quote-Regex $smoothFast)`"\s*,\s*`"140`""
Assert-Match 'Smooth caret medium duration should default to 200ms.' $configCode "`"$(Quote-Regex $smoothMedium)`"\s*,\s*`"200`""
Assert-Match 'Smooth caret slow duration should default to 280ms.' $configCode "`"$(Quote-Regex $smoothSlow)`"\s*,\s*`"280`""

Assert-Match 'Follow typing category should keep copybook and tracing mode separated from smooth settings.' $followItems (
    "`"$(Quote-Regex $copybookMode)`"[\s\S]*" +
    "`"$(Quote-Regex $tracingMode)`"[\s\S]*" +
    "`"$(Quote-Regex $blindMode)`"[\s\S]*" +
    "`"$(Quote-Regex $smoothCaret)`"[\s\S]*" +
    "`"  $(Quote-Regex $smoothCaretMode)`"[\s\S]*" +
    "`"  $(Quote-Regex $smoothCaretFixedDuration)`"[\s\S]*" +
    "`"  $(Quote-Regex $smoothFast)`"[\s\S]*" +
    "`"  $(Quote-Regex $smoothMedium)`"[\s\S]*" +
    "`"  $(Quote-Regex $smoothSlow)`"[\s\S]*" +
    "`"$(Quote-Regex $smoothLineWrap)`"")

Assert-Match 'WinConfig should create a toggle for smooth caret.' $configWindowCode "itemKey\s*==\s*`"$(Quote-Regex $smoothCaret)`"[\s\S]*CheckBox"
Assert-Match 'WinConfig should create a mode dropdown for dynamic or fixed smooth caret.' $configWindowCode "itemKey\s*==\s*`"$(Quote-Regex $smoothCaretMode)`"[\s\S]*`"$(Quote-Regex $dynamic)`"[\s\S]*`"$(Quote-Regex $fixed)`""
Assert-Match 'WinConfig should save the selected smooth caret mode as text.' $configWindowCode "labelText\s*==\s*`"$(Quote-Regex $smoothCaretMode)`"[\s\S]*comboBox\.Items\[comboBox\.SelectedIndex\]\.ToString\(\)"
Assert-Match 'WinConfig should create numeric text boxes for smooth caret durations.' $configWindowCode "IsSmoothCaretDurationItem\(itemKey\)[\s\S]*TextBox"
Assert-Match 'WinConfig should show clear labels for smooth caret durations.' $configWindowCode "$(Quote-Regex $smoothCaretLabel)[\s\S]*$(Quote-Regex $smoothCaretModeLabel)[\s\S]*$(Quote-Regex $fixedDurationLabel)[\s\S]*$(Quote-Regex $rapidTypingDurationLabel)[\s\S]*$(Quote-Regex $normalTypingDurationLabel)[\s\S]*$(Quote-Regex $pauseDurationLabel)"
Assert-Match 'WinConfig should hide fixed duration unless fixed mode is selected.' $configWindowCode "UpdateSmoothCaretOptionVisibility[\s\S]*$(Quote-Regex $smoothCaretFixedDuration)[\s\S]*mode\s*==\s*`"$(Quote-Regex $fixed)`""
Assert-Match 'WinConfig should hide dynamic duration anchors unless dynamic mode is selected.' $configWindowCode "UpdateSmoothCaretOptionVisibility[\s\S]*$(Quote-Regex $smoothFast)[\s\S]*mode\s*==\s*`"$(Quote-Regex $dynamic)`""
Assert-Match 'Config should migrate old smooth caret off value to the new switch.' $configCode "MigrateSmoothCaretLegacyValue[\s\S]*$(Quote-Regex $off)[\s\S]*$(Quote-Regex $no)"
Assert-Match 'Config should migrate old fixed speed values to fixed mode.' $configCode "MigrateSmoothCaretLegacyValue[\s\S]*$(Quote-Regex $fast)[\s\S]*$(Quote-Regex $fixed)[\s\S]*$(Quote-Regex $medium)[\s\S]*$(Quote-Regex $fixed)[\s\S]*$(Quote-Regex $slow)[\s\S]*$(Quote-Regex $fixed)"
Assert-Match 'Config should migrate old dynamic value to dynamic mode.' $configCode "MigrateSmoothCaretLegacyValue[\s\S]*$(Quote-Regex $dynamic)"
Assert-Match 'Config should migrate old smooth scroll key to smooth line wrap.' $configCode "key\s*==\s*`"$(Quote-Regex $smoothScroll)`"[\s\S]*return\s+`"$(Quote-Regex $smoothLineWrap)`""

Write-Host 'Smooth caret config tests passed.'
