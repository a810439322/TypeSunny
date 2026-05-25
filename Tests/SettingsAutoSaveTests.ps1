$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$xaml = Get-Content -Path (Join-Path $root 'WinConfig\WinConfig.xaml') -Raw -Encoding UTF8
$code = Get-Content -Path (Join-Path $root 'WinConfig\WinConfig.xaml.cs') -Raw -Encoding UTF8
$mainCode = Get-Content -Path (Join-Path $root 'UI\MainWindow.xaml.cs') -Raw -Encoding UTF8
$configCode = Get-Content -Path (Join-Path $root 'Config\Config.cs') -Raw -Encoding UTF8

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

function Assert-Ordered($name, $content, $first, $second) {
    $firstIndex = $content.IndexOf($first, [System.StringComparison]::Ordinal)
    $secondIndex = $content.IndexOf($second, [System.StringComparison]::Ordinal)
    if ($firstIndex -lt 0 -or $secondIndex -lt 0 -or $firstIndex -ge $secondIndex) {
        throw "$name expected [$first] before [$second]"
    }
}

Assert-NotContains 'settings xaml removes bottom apply button' $xaml 'x:Name="Save"'
Assert-NotContains 'settings xaml removes bottom close button' $xaml 'x:Name="Cancel"'
Assert-Contains 'category switch saves current controls first' $code 'SaveCurrentCategoryControls();'
Assert-Contains 'closing flushes autosaved config' $code 'Config.WriteConfig(0);'
$closingMatch = [regex]::Match($code, 'private void Window_Closing[\s\S]*?(?=\n        /// <summary>|\n        private string FindLabelInContentPanel)')
if (-not $closingMatch.Success) {
    throw 'expected to find Window_Closing method'
}
Assert-NotContains 'closing no longer asks whether to save' $closingMatch.Value 'MessageBox.Show'
Assert-Contains 'textboxes attach lost-focus autosave' $code 'AttachTextBoxAutoSave'
Assert-Contains 'autosave supports enter commits' $code 'Key.Enter'
Assert-Contains 'custom sections can register fallback saves' $code 'AddCategoryFallbackSave'
Assert-Contains 'checkboxes attach autosave' $code 'AttachCheckBoxAutoSave'
Assert-Contains 'comboboxes attach autosave' $code 'AttachComboBoxAutoSave'
$endpointResetNeedle = 'SaveConfigValue' + '(itemKey, tb.Text'
Assert-Contains 'endpoint reset buttons save immediately' $code $endpointResetNeedle
$colorSaveNeedle = 'SaveConfigValue' + '(colorKey, colorHex'
Assert-Contains 'color picker writes selected color immediately' $code $colorSaveNeedle
Assert-Contains 'dynamic difficulty saves on selection change' $code 'SaveWenlaiDifficultySelection'
Assert-Contains 'dynamic category saves on selection change' $code 'SaveWenlaiCategorySelection'
Assert-Contains 'unchanged-value filter compares against current config' $code 'value[i] == Config.GetString(key[i])'
Assert-Contains 'unchanged-value filter removes unchanged keys' $code 'key.RemoveAt(i);'
$saveCurrentMatch = [regex]::Match($code, 'private void SaveCurrentCategoryControls\(\)[\s\S]*?(?=\n        private static void FilterUnchangedConfigValues)')
if (-not $saveCurrentMatch.Success) {
    throw 'expected to find SaveCurrentCategoryControls method'
}
Assert-Contains 'category fallback has unchanged-value filter' $saveCurrentMatch.Value 'FilterUnchangedConfigValues(key, value);'
Assert-Ordered 'category fallback filters unchanged values before code-display side effects' $saveCurrentMatch.Value 'FilterUnchangedConfigValues(key, value);' 'ApplyCodeDisplayMutualExclusion(key, value);'

$reloadCfgMatch = [regex]::Match($mainCode, 'private void ReloadCfg\(\)[\s\S]*?(?=\n        private bool ShouldLoadCiTiSegments)')
if (-not $reloadCfgMatch.Success) {
    throw 'expected to find MainWindow.ReloadCfg method'
}
Assert-Contains 'autosave config refresh recalculates top-right ZiTi hint' $reloadCfgMatch.Value 'UpdateZiTi();'
Assert-Contains 'autosave config refresh recalculates prediction title display' $reloadCfgMatch.Value 'RefreshCurrentDifficultyPredictionDisplay();'
Assert-Contains 'prediction display list changes notify main window refresh' $code 'ScheduleConfigSavedRefresh();'
Assert-Contains 'prediction enable checkbox shows cold-start hint' $code 'ShowPredictionEnableTip();'
$confidenceNeedle = (-join @([char]0x9884, [char]0x6D4B, [char]0x7F6E, [char]0x4FE1, [char]0x5EA6, [char]0x4F4E, [char]0x4E8E)) + '30%'
$speedOnlyNeedle = -join @([char]0x901F, [char]0x5EA6, [char]0x5FC5, [char]0x5F00, [char]0xFF0C, [char]0x5176, [char]0x4ED6, [char]0x9879, [char]0x76EE, [char]0x53EF, [char]0x9009)
$speedAndDifficultyNeedle = -join @([char]0x901F, [char]0x5EA6, [char]0x548C, [char]0x96BE, [char]0x5EA6, [char]0x5FC5, [char]0x5F00)
$speedFollowHintNeedle = -join @([char]0x901F, [char]0x5EA6, [char]0x8DDF, [char]0x968F, [char]0x63D0, [char]0x793A)
$trainerHitHintNeedle = -join @([char]0x7EC3, [char]0x5355, [char]0x4E0B, [char]0x663E, [char]0x793A, [char]0x51FB, [char]0x952E)
$trainerHitHintTooltipNeedle = -join @([char]0x5F00, [char]0x542F, [char]0x540E, [char]0xFF0C, [char]0x7EC3, [char]0x5355, [char]0x573A, [char]0x666F, [char]0x4E0B, [char]0x901F, [char]0x5EA6, [char]0x8DDF, [char]0x968F, [char]0x63D0, [char]0x793A, [char]0x53D8, [char]0x4E3A, [char]0x51FB, [char]0x952E, [char]0x8DDF, [char]0x968F, [char]0x63D0, [char]0x793A, [char]0xFF0C, [char]0x5176, [char]0x4ED6, [char]0x573A, [char]0x666F, [char]0x6B64, [char]0x5F00, [char]0x5173, [char]0x4E0D, [char]0x751F, [char]0x6548, [char]0x3002)
$enabledNeedle = -join @([char]0x662F)
Assert-Contains 'prediction enable hint explains confidence threshold' $code $confidenceNeedle
Assert-Contains 'prediction display list copy says only speed is required' $code $speedOnlyNeedle
Assert-NotContains 'prediction display list copy no longer says difficulty required' $code $speedAndDifficultyNeedle
Assert-Contains 'autosave refresh keeps settings window active' $code 'InvokeConfigSavedWithoutSwitchingWindows();'
Assert-Contains 'autosave focus restore only runs when settings was active' $code 'bool settingsWasActive = IsActive;'
Assert-Contains 'autosave focus restore does not steal user-selected main focus' $code 'if (!settingsWasActive || !IsVisible)'
Assert-Contains 'trainer speed-follow hit-rate setting has default enabled' $configCode ('"' + $trainerHitHintNeedle + '", "' + $enabledNeedle + '"')
Assert-Contains 'settings page includes trainer speed-follow hit-rate child switch' $code ('"  ' + $trainerHitHintNeedle + '"')
Assert-Contains 'settings page explains trainer speed-follow hit-rate switch' $code $trainerHitHintTooltipNeedle
Assert-Contains 'main window reads trainer speed-follow hit-rate switch' $mainCode ('Config.GetBool("' + $trainerHitHintNeedle + '")')
Assert-Ordered 'trainer speed-follow hit-rate switch appears below speed-follow hint' $code ('"' + $speedFollowHintNeedle + '"') ('"  ' + $trainerHitHintNeedle + '"')

Write-Host 'All SettingsAutoSave tests passed.'
