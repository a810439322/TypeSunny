$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$workflow = Get-Content -Path (Join-Path $root '.github\workflows\release.yml') -Raw

function Assert-Contains($name, $content, $needle) {
    if (-not $content.Contains($needle)) {
        throw "$name expected to contain [$needle]"
    }
}

function Assert-Match($name, $content, $pattern) {
    if ($content -notmatch $pattern) {
        throw "$name expected to match [$pattern]"
    }
}

Assert-Contains 'gitee cleanup keeps three releases' $workflow '$keepReleaseCount = 3'
Assert-Contains 'gitee release listing uses pagination helper' $workflow 'function Get-GiteeReleases($token)'
Assert-Contains 'gitee release listing requests max page size' $workflow '$perPage = 100'
Assert-Contains 'gitee release listing advances pages' $workflow '$page++'
Assert-Contains 'gitee cleanup uses shared helper' $workflow 'function Remove-OldGiteeReleases($token, $keepReleaseCount)'
Assert-Contains 'gitee cleanup sorts newest releases first' $workflow 'Sort-Object @{ Expression = { [DateTime]$_.created_at }; Descending = $true }'
Assert-Contains 'gitee cleanup skips the newest releases' $workflow 'Select-Object -Skip $keepReleaseCount'
Assert-Match 'gitee cleanup deletes releases beyond the retention count' $workflow 'Invoke-RestMethod\s+-Uri\s+"https://gitee\.com/api/v5/repos/fuchuxuan/TypeSunny/releases/\$\(\$rel\.id\)\?access_token=\$\{token\}"\s+`\r?\n\s+-Method Delete'
Assert-Contains 'gitee cleanup runs before uploading assets' $workflow '--- Step 2: Cleaning up old Gitee releases before upload ---'
Assert-Contains 'gitee cleanup runs after uploading assets' $workflow '--- Step 3: Cleaning up old Gitee releases after upload ---'

Write-Host 'All release workflow retention tests passed.'
