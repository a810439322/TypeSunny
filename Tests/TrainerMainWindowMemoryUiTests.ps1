$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$mainCode = Get-Content -Raw -Encoding UTF8 (Join-Path $root 'UI\MainWindow.xaml.cs')
$trainerXaml = Get-Content -Raw -Encoding UTF8 (Join-Path $root 'WinTrainer\WinTrainer.xaml')
$trainerCode = Get-Content -Raw -Encoding UTF8 (Join-Path $root 'WinTrainer\WinTrainer.xaml.cs')
$configCode = Get-Content -Raw -Encoding UTF8 (Join-Path $root 'WinConfig\WinConfig.xaml.cs')

function Assert-Contains($name, $text, $needle) {
    if (-not $text.Contains($needle)) {
        throw "$name missing expected text: $needle"
    }
}

function Get-Block($text, $startNeedle, $endNeedle) {
    $start = $text.IndexOf($startNeedle)
    if ($start -lt 0) {
        throw "Unable to find block start: $startNeedle"
    }

    $end = $text.IndexOf($endNeedle, $start + $startNeedle.Length)
    if ($end -lt 0) {
        throw "Unable to find block end: $endNeedle"
    }

    return $text.Substring($start, $end - $start)
}

$loadTextBlock = Get-Block $mainCode 'public void LoadText' 'if (retypeType == RetypeType.wrongRetype)'
Assert-Contains 'LoadText syncs trainer main-window scope after source assignment' $loadTextBlock 'SyncTrainerMainWindowConfigScope(source);'

$resizeBlock = Get-Block $mainCode 'private void RunWindowResizeCompletedWork()' 'private void NextArticle()'
Assert-Contains 'resize saves scoped width' $resizeBlock 'TrainerMainWindowConfigScope.Set('
Assert-Contains 'resize saves scoped compact height' $resizeBlock 'SuperCompactModeConfigKey'

$ratioBlock = Get-Block $mainCode 'private void SaveDisplayInputRatio(' 'private void GridSplitter_DragCompleted'
Assert-Contains 'ratio saves scoped config values' $ratioBlock 'TrainerMainWindowConfigScope.SetRaw('

$homeBlock = Get-Block $mainCode 'public void ApplyHomeToolbarSettings(bool applySuperCompactMode = true)' 'private void BeginSuppressWindowSizeChangeUpdates()'
Assert-Contains 'home toolbar reads scoped feature order' $homeBlock 'TrainerMainWindowConfigScope.GetString(HomeToolbarSettings.FeatureOrderConfigKey)'
Assert-Contains 'home toolbar reads scoped visibility' $homeBlock 'TrainerMainWindowConfigScope.GetBool(entry.VisibilityConfigKey)'
Assert-Contains 'home toolbar does not recreate reset trainer feature order just by applying settings' $homeBlock 'TrainerMainWindowConfigScope.HasCurrentScopeValue(HomeToolbarSettings.FeatureOrderConfigKey)'
Assert-Contains 'home toolbar only writes normalized feature order when value changes' $homeBlock 'if (normalizedFeatureOrder != currentFeatureOrder)'

Assert-Contains 'super compact reads scoped bool' $mainCode 'TrainerMainWindowConfigScope.GetBool(SuperCompactModeConfigKey)'
Assert-Contains 'main exposes reset trainer memory' $mainCode 'public void ResetTrainerMainWindowMemory()'

$applyScopedBlock = Get-Block $mainCode 'private void ApplyScopedMainWindowState()' 'private void ResetSuperCompactLayoutForScopedStateChange()'
Assert-Contains 'scoped apply clears previous compact layout without persisting old snapshot' $applyScopedBlock 'ResetSuperCompactLayoutForScopedStateChange();'
Assert-Contains 'scoped apply defers compact layout until scoped results state is restored' $applyScopedBlock 'ApplyHomeToolbarSettings(false);'
Assert-Contains 'scoped apply uses target normal height when capturing compact snapshot' $applyScopedBlock 'ApplySuperCompactModeLayout(true, true, normalHeight);'
if ($applyScopedBlock.IndexOf('ResetSuperCompactLayoutForScopedStateChange();') -gt $applyScopedBlock.IndexOf('ApplyHomeToolbarSettings(false);')) {
    throw 'scoped apply must clear old compact layout before applying scoped toolbar settings.'
}
if ($applyScopedBlock.IndexOf('ApplyHomeToolbarSettings(false);') -gt $applyScopedBlock.IndexOf('ApplySuperCompactModeLayout(true, true, normalHeight);')) {
    throw 'scoped apply must restore results state before applying target compact layout.'
}

Assert-Contains 'home toolbar can skip compact layout while switching scopes' $homeBlock 'public void ApplyHomeToolbarSettings(bool applySuperCompactMode = true)'
Assert-Contains 'home toolbar guards compact reapply during scoped state switching' $homeBlock 'if (isSuperCompact && applySuperCompactMode)'

Assert-Contains 'trainer has memory checkbox' $trainerXaml 'CbTrainerMainWindowMemory'
Assert-Contains 'trainer has reset memory button' $trainerXaml 'BtnResetTrainerMainWindowMemory'
Assert-Contains 'trainer stacks send-close and main-window-memory checkboxes vertically' $trainerXaml 'x:Name="TrainerSendOptionsPanel"'
Assert-Contains 'trainer send option stack is vertical' $trainerXaml 'x:Name="TrainerSendOptionsPanel" Orientation="Vertical"'
Assert-Contains 'trainer reads memory switch' $trainerCode 'TrainerMainWindowConfigScope.EnabledConfigKey'
Assert-Contains 'trainer toggle refreshes main scope' $trainerCode 'RefreshTrainerMainWindowMemoryMode()'
Assert-Contains 'trainer reset calls main reset' $trainerCode 'ResetTrainerMainWindowMemory()'

$homeSettingsBlock = Get-Block $configCode 'private void AppendHomeToolbarSettings' 'private void AppendFixedHomeModuleSettings'
Assert-Contains 'settings home toolbar reads scoped visibility' $homeSettingsBlock 'TrainerMainWindowConfigScope.GetBool(entry.VisibilityConfigKey)'
Assert-Contains 'settings home toolbar reads scoped order' $homeSettingsBlock 'TrainerMainWindowConfigScope.GetString(HomeToolbarSettings.FeatureOrderConfigKey)'
Assert-Contains 'settings home toolbar writes scoped visibility' $homeSettingsBlock 'TrainerMainWindowConfigScope.Set(entry.VisibilityConfigKey'
Assert-Contains 'settings home toolbar writes scoped order' $homeSettingsBlock 'TrainerMainWindowConfigScope.Set(HomeToolbarSettings.FeatureOrderConfigKey'

$fixedSettingsBlock = Get-Block $configCode 'private void AppendFixedHomeModuleSettings' 'private void ColorButton_Click'
Assert-Contains 'settings fixed modules read scoped visibility' $fixedSettingsBlock 'TrainerMainWindowConfigScope.GetBool(entry.VisibilityConfigKey)'
Assert-Contains 'settings fixed modules write scoped visibility' $fixedSettingsBlock 'TrainerMainWindowConfigScope.Set(entry.VisibilityConfigKey'

Write-Host 'All trainer main-window memory UI tests passed.'
