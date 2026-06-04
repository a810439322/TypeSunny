$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$articleFetcherCode = Get-Content -Path (Join-Path $root 'ArticleSender\ArticleFetcher.cs') -Raw -Encoding UTF8
$apiClientCode = Get-Content -Path (Join-Path $root 'Net\Http\ApiClient.cs') -Raw -Encoding UTF8

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

Assert-Contains 'difficulty stats parser unwraps nested envelopes' $articleFetcherCode 'UnwrapStatsData(response.RawData)'
Assert-Contains 'difficulty stats parser supports levelStats object' $articleFetcherCode 'statsContainer?["levelStats"] as JObject'
Assert-Contains 'difficulty stats parser supports difficultyStats object' $articleFetcherCode 'statsContainer?["difficultyStats"] as JObject'
Assert-Contains 'difficulty stats parser supports stats object' $articleFetcherCode 'statsContainer?["stats"] as JObject'
Assert-Contains 'difficulty stats parser supports levels object' $articleFetcherCode 'statsContainer?["levels"] as JObject'
Assert-Contains 'difficulty stats parser supports array payloads' $articleFetcherCode 'statsArray'
Assert-Contains 'difficulty stats parser reads level from object key before token fields' $articleFetcherCode 'if (labelToLevel.ContainsKey(key))'
Assert-Contains 'difficulty stats parser reads numeric object values as counts' $articleFetcherCode 'if (token.Type != JTokenType.Object)'
Assert-Contains 'difficulty stats parser reads labels from array items' $articleFetcherCode '"difficultyLabel", "label", "name", "levelName", "difficulty", "level"'
Assert-Contains 'difficulty stats parser reads segmentCount' $articleFetcherCode '"segmentCount"'
Assert-Contains 'difficulty stats parser reads totalSegments' $articleFetcherCode '"totalSegments"'
Assert-Contains 'difficulty stats parser reads totalCount' $articleFetcherCode '"totalCount"'
Assert-Contains 'difficulty stats cache is keyed by category' $articleFetcherCode 'difficultyCacheByCategory[category]'
Assert-Contains 'difficulty stats cache lookup uses selected category' $articleFetcherCode 'TryGetCachedDifficulties(configCategory'
Assert-Contains 'difficulty stats keeps default difficulty options visible' $articleFetcherCode 'NormalizeDifficulties(difficulties)'
Assert-Contains 'difficulty defaults include first level' $articleFetcherCode 'new DifficultyInfo { Id = 1, Name = '

Assert-NotContains 'article fetcher should not write temporary wenlai log file' $articleFetcherCode 'wenlai-request.log'
Assert-NotContains 'api client should not write temporary http log file' $apiClientCode 'http-request.log'
Assert-NotContains 'api client should not retain temporary request logger' $apiClientCode 'WriteHttpRequestLog'
Assert-NotContains 'difficulty stats should not use global latest-request gate' $articleFetcherCode 'difficultyRequestVersion'
Assert-NotContains 'difficulty stats should not use interlocked gate' $articleFetcherCode 'Interlocked.Read(ref difficultyRequestVersion)'

Write-Host 'All Wenlai difficulty stats parsing tests passed.'
