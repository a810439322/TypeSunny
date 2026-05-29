$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$dialogThemingCode = Get-Content -Raw -Encoding UTF8 (Join-Path $root 'Utils\DialogTheming.cs')
$trainerCode = Get-Content -Raw -Encoding UTF8 (Join-Path $root 'WinTrainer\WinTrainer.xaml.cs')

function Assert-Contains($name, $text, $needle) {
    if (-not $text.Contains($needle)) {
        throw "$name missing expected text: $needle"
    }
}

function Assert-NotContains($name, $text, $needle) {
    if ($text.Contains($needle)) {
        throw "$name should not contain text: $needle"
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

Assert-Contains 'dialog theming has shared dialog button template helper' $dialogThemingCode 'private static ControlTemplate GetDialogButtonTemplate(Palette p, bool isAccent)'
Assert-Contains 'dialog theming has shared button application helper' $dialogThemingCode 'private static void ApplyButtonTheme(Button button, Palette p, bool isAccent)'
Assert-Contains 'dialog theming applies shared dialog button template' $dialogThemingCode 'button.Template = GetDialogButtonTemplate(p, isAccent);'
Assert-Contains 'legacy dialog theming applies normal button template' $dialogThemingCode 'ApplyButtonTheme(button, p, false);'
Assert-Contains 'legacy dialog theming applies accent button template' $dialogThemingCode 'ApplyButtonTheme(accentButton, p, true);'
Assert-Contains 'dialog button template controls hover background' $dialogThemingCode 'Property=''IsMouseOver'''
Assert-Contains 'dialog button template controls pressed background' $dialogThemingCode 'Property=''IsPressed'''
Assert-Contains 'dialog button template uses app hover color' $dialogThemingCode 'ButtonHoverBg'
Assert-Contains 'dialog button template uses app pressed color' $dialogThemingCode 'ButtonPressedBg'
Assert-Contains 'dialog accent button template uses accent hover color' $dialogThemingCode 'AccentHoverBg'
Assert-Contains 'dialog button template binds text foreground' $dialogThemingCode 'TextBlock.Foreground=''{TemplateBinding Foreground}'''
Assert-Contains 'dialog button template removes native focus visual' $dialogThemingCode 'button.FocusVisualStyle = null;'
Assert-NotContains 'chromeless generic buttons no longer auto-accent default buttons' $dialogThemingCode 'btn.Background = btn.IsDefault ? p.Accent : p.ButtonBg;'

$confirmBlock = Get-Block $trainerCode 'private bool ConfirmResetTrainerMainWindowMemoryOnDisable()' 'private void CloseTrainerWindowAfterSendIfNeeded()'
Assert-Contains 'trainer confirmation still uses shared chromeless template' $confirmBlock 'DialogTheming.ApplyChromelessTheme(dialog)'
Assert-NotContains 'trainer confirmation does not force default accent button' $confirmBlock 'IsDefault = true'

Write-Host 'All dialog theming button template tests passed.'
