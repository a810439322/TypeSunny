$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$helperCode = Get-Content -Path (Join-Path $root 'Utils\ThemeColorHelper.Controls.cs') -Raw

if ($helperCode -notmatch 'MenuItem\.BorderThicknessProperty,\s*new Thickness\(0\)') {
    throw 'Menu item theme should not draw a per-item border; expected BorderThickness 0.'
}

if ($helperCode -match 'MenuItem\.BorderThicknessProperty,\s*new Thickness\(1\)') {
    throw 'Menu item theme still draws a 1px per-item border.'
}

if ($helperCode -notmatch 'menu\.Template\s*=\s*GetContextMenuTemplate\(\)') {
    throw 'Context menu theme should override the default ContextMenu template to remove the system icon gutter.'
}

if ($helperCode -match 'mi\.Background\s*=\s*menuBg') {
    throw 'ApplyMenuItemThemeRecursive should not set local Background values; local values block highlighted style/template behavior.'
}

if ($helperCode -match 'mi\.Foreground\s*=\s*menuFg') {
    throw 'ApplyMenuItemThemeRecursive should not set local Foreground values; local values block style/template behavior.'
}

if ($helperCode -match 'style\.Triggers\.Add\(highlightTrigger\)') {
    throw 'Menu highlight visual should be handled by the control template, not a Style.Trigger blocked by local values.'
}

if ($helperCode -notmatch "Property='IsHighlighted'[\s\S]*TargetName='Root'[\s\S]*Property='Background'") {
    throw 'Menu item template should paint the Root background when IsHighlighted is true.'
}

Write-Host 'All menu theme regression tests passed.'
