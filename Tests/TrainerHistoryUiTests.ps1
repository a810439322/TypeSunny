$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot

function Assert-Contains {
    param(
        [string]$Name,
        [string]$Text,
        [string]$Expected
    )

    if (-not $Text.Contains($Expected)) {
        throw "$Name missing expected text: $Expected"
    }
}

function Assert-NotContains {
    param(
        [string]$Name,
        [string]$Text,
        [string]$Unexpected
    )

    if ($Text.Contains($Unexpected)) {
        throw "$Name should not contain unexpected text: $Unexpected"
    }
}

$trainerXaml = Get-Content -Raw (Join-Path $root 'WinTrainer\WinTrainer.xaml')
$trainerCode = Get-Content -Raw (Join-Path $root 'WinTrainer\WinTrainer.xaml.cs')
$projectFile = Get-Content -Raw (Join-Path $root 'TypeSunny.csproj')
$historyWindowPath = Join-Path $root 'WinTrainer\WinTrainerHistoryWindow.cs'
$historyLabel = -join ([char]0x5386, [char]0x53F2)

Assert-Contains 'trainer history button label' $trainerXaml ('Content="' + $historyLabel + '"')
Assert-Contains 'trainer history button click' $trainerXaml 'Click="BtnHistory_Click"'
Assert-Contains 'trainer progress area reserves history column' $trainerXaml '<ColumnDefinition Width="auto"/>'
Assert-Contains 'trainer reset stack stays with slider column' $trainerXaml '<StackPanel Grid.Column="2" HorizontalAlignment="Center" VerticalAlignment="Center" Orientation="Horizontal">'
Assert-Contains 'trainer history button has independent column' $trainerXaml ('<Button x:Name="BtnHistory" Grid.Column="3" Content="' + $historyLabel + '"')
Assert-Contains 'trainer history click handler' $trainerCode 'BtnHistory_Click'
Assert-Contains 'trainer history window launch' $trainerCode 'WinTrainerHistoryWindow'
Assert-NotContains 'trainer history must not be controlled by UpdateUIState' $trainerCode 'BtnHistory.Visibility'
Assert-Contains 'project includes history window' $projectFile 'WinTrainer\WinTrainerHistoryWindow.cs'

$toolbarStart = $trainerXaml.IndexOf('<StackPanel Grid.Column="0" Orientation="Horizontal">')
$toolbarEnd = $trainerXaml.IndexOf('</StackPanel>', $toolbarStart)
if ($toolbarStart -lt 0 -or $toolbarEnd -lt 0) {
    throw 'trainer toolbar stack panel was not found.'
}
$toolbarButtons = $trainerXaml.Substring($toolbarStart, $toolbarEnd - $toolbarStart)
Assert-NotContains 'trainer toolbar near send button' $toolbarButtons 'BtnHistory_Click'

$progressStart = $trainerXaml.IndexOf('<Grid Grid.Row="3"')
$settingsStart = $trainerXaml.IndexOf('<Grid Grid.Row="4"')
if ($progressStart -lt 0 -or $settingsStart -lt 0 -or $settingsStart -le $progressStart) {
    throw 'trainer progress/reset area was not found.'
}
$progressArea = $trainerXaml.Substring($progressStart, $settingsStart - $progressStart)
Assert-Contains 'trainer progress area has reset statistics' $progressArea 'BtnReset_Click'
Assert-Contains 'trainer progress area has history button' $progressArea 'BtnHistory_Click'
Assert-NotContains 'trainer history button must not share slider column' $progressArea ('x:Name="BtnHistory" Content="' + $historyLabel + '"')
Assert-NotContains 'trainer history button must stay visible' $progressArea ('x:Name="BtnHistory" Grid.Column="3" Content="' + $historyLabel + '" Margin="5,0,0,0" Padding="15,4" Click="BtnHistory_Click" Style="{StaticResource ModernButtonStyle}" Visibility="')

if (-not (Test-Path $historyWindowPath)) {
    throw 'WinTrainerHistoryWindow.cs does not exist.'
}

$historyWindowCode = Get-Content -Raw $historyWindowPath
Assert-Contains 'history window reads trainer records' $historyWindowCode 'TrainerLog.ReadRecentRecords()'
Assert-Contains 'history window filters by title' $historyWindowCode 'ArticleName'
Assert-Contains 'history window uses table' $historyWindowCode 'DataGrid'
Assert-Contains 'history window applies theme colors' $historyWindowCode 'private void ApplyThemeColors()'
Assert-Contains 'history window reads window background theme' $historyWindowCode 'var windowBgColor = (Color)ColorConverter.ConvertFromString("#" + Config.GetString('
Assert-Contains 'history window reads window foreground theme' $historyWindowCode 'var windowFgColor = (Color)ColorConverter.ConvertFromString("#" + Config.GetString('
Assert-Contains 'history window reads button background theme' $historyWindowCode 'var buttonBgColor = (Color)ColorConverter.ConvertFromString("#" + Config.GetString('
Assert-Contains 'history window reads button foreground theme' $historyWindowCode 'var buttonFgColor = (Color)ColorConverter.ConvertFromString("#" + Config.GetString('
Assert-Contains 'history window reads menu background theme' $historyWindowCode 'var menuBgColor = (Color)ColorConverter.ConvertFromString("#" + Config.GetString('
Assert-Contains 'history window uses subtle themed borders' $historyWindowCode 'ThemeColorHelper.GetSubtleBorderColor'
Assert-Contains 'history window themes datagrid' $historyWindowCode 'ApplyDataGridTheme'
Assert-Contains 'history window themes datagrid headers' $historyWindowCode 'DataGridColumnHeader'
Assert-Contains 'history window applies theme on construction' $historyWindowCode 'ApplyThemeColors();'

Write-Host 'Trainer history UI structure test passed.'
