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
Assert-Contains 'release workflow writes package publish time manifest' $workflow 'package_published_at'
Assert-Contains 'release workflow names package manifest by version' $workflow 'TypeSunny-${ver}-package.json'
Assert-Contains 'release workflow computes package publish ticks before build' $workflow 'package_published_utc_ticks'
Assert-Contains 'release workflow passes package publish ticks into release build' $workflow '/p:ReleasePackagePublishedUtcTicks='
Assert-Contains 'github release uploads package manifest' $workflow 'TypeSunny-${VERSION}-package.json'
Assert-Contains 'gitee release uploads package manifest as required identity data' $workflow 'Upload-Asset "TypeSunny-${ver}-package.json" $releaseId $token 10 $true'

if ($workflow.Contains('release-identity.json')) {
    throw 'release workflow should not embed release-identity.json into packages.'
}

Write-Host 'All release workflow retention tests passed.'
