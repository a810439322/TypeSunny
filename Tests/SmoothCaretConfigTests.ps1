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

Assert-Match 'Follow typing category should keep copybook mode while removing user-facing smooth controls.' $followItems "`"$(Quote-Regex $copybookMode)`""
Assert-NotMatch 'Follow typing category should not expose the smooth caret switch.' $followItems "`"\s*$(Quote-Regex $smoothCaret)\s*`""
Assert-NotMatch 'Follow typing category should not expose smooth caret mode.' $followItems "`"\s*$(Quote-Regex $smoothCaretMode)\s*`""
Assert-NotMatch 'Follow typing category should not expose smooth caret fixed duration.' $followItems "`"\s*$(Quote-Regex $smoothCaretFixedDuration)\s*`""
Assert-NotMatch 'Follow typing category should not expose smooth caret fast duration.' $followItems "`"\s*$(Quote-Regex $smoothFast)\s*`""
Assert-NotMatch 'Follow typing category should not expose smooth caret medium duration.' $followItems "`"\s*$(Quote-Regex $smoothMedium)\s*`""
Assert-NotMatch 'Follow typing category should not expose smooth caret slow duration.' $followItems "`"\s*$(Quote-Regex $smoothSlow)\s*`""
Assert-NotMatch 'Follow typing category should not expose smooth line wrap.' $followItems "`"\s*$(Quote-Regex $smoothLineWrap)\s*`""

Assert-NotMatch 'WinConfig should not create a dedicated smooth caret toggle.' $configWindowCode "itemKey\s*==\s*`"$(Quote-Regex $smoothCaret)`"[\s\S]*CheckBox"
Assert-NotMatch 'WinConfig should not create a smooth caret mode dropdown.' $configWindowCode "itemKey\s*==\s*`"$(Quote-Regex $smoothCaretMode)`"[\s\S]*`"$(Quote-Regex $dynamic)`"[\s\S]*`"$(Quote-Regex $fixed)`""
Assert-NotMatch 'WinConfig should not save a selected smooth caret mode from the settings UI.' $configWindowCode "labelText\s*==\s*`"$(Quote-Regex $smoothCaretMode)`"[\s\S]*comboBox\.Items\[comboBox\.SelectedIndex\]\.ToString\(\)"
Assert-NotMatch 'WinConfig should not create smooth caret duration text boxes.' $configWindowCode "IsSmoothCaretDurationItem\(itemKey\)[\s\S]*TextBox"
Assert-NotMatch 'WinConfig should not carry smooth caret option visibility code.' $configWindowCode "UpdateSmoothCaretOptionVisibility"
Assert-Match 'Config should migrate old smooth caret off value to the new switch.' $configCode "MigrateSmoothCaretLegacyValue[\s\S]*$(Quote-Regex $off)[\s\S]*$(Quote-Regex $no)"
Assert-Match 'Config should migrate old fixed speed values to fixed mode.' $configCode "MigrateSmoothCaretLegacyValue[\s\S]*$(Quote-Regex $fast)[\s\S]*$(Quote-Regex $fixed)[\s\S]*$(Quote-Regex $medium)[\s\S]*$(Quote-Regex $fixed)[\s\S]*$(Quote-Regex $slow)[\s\S]*$(Quote-Regex $fixed)"
Assert-Match 'Config should migrate old dynamic value to dynamic mode.' $configCode "MigrateSmoothCaretLegacyValue[\s\S]*$(Quote-Regex $dynamic)"
Assert-Match 'Config should migrate old smooth scroll key to smooth line wrap.' $configCode "key\s*==\s*`"$(Quote-Regex $smoothScroll)`"[\s\S]*return\s+`"$(Quote-Regex $smoothLineWrap)`""

Write-Host 'Smooth caret config tests passed.'
