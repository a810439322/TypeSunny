$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$articleFetcherCode = Get-Content -Path (Join-Path $root 'ArticleSender\ArticleFetcher.cs') -Raw -Encoding UTF8
$mainWindowCode = Get-Content -Path (Join-Path $root 'UI\MainWindow.xaml.cs') -Raw -Encoding UTF8
$configWindowCode = Get-Content -Path (Join-Path $root 'WinConfig\WinConfig.xaml.cs') -Raw -Encoding UTF8
$wenlaiCategoryKey = [string]::Concat([char[]]@(0x6587, 0x6765, 0x5206, 0x7C7B))
$getCategoryConfigNeedle = 'Config.GetString("' + $wenlaiCategoryKey + '")'
$setAllCategoryNeedle = 'Config.Set("' + $wenlaiCategoryKey + '", "");'
$setSelectedCategoryNeedle = 'Config.Set("' + $wenlaiCategoryKey + '", cat.Code);'
$saveCategoryNeedle = 'SaveConfigValue("' + $wenlaiCategoryKey + '",'

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

function Get-Block($name, $content, $start, $end) {
    $pattern = [regex]::Escape($start) + '([\s\S]*?)' + [regex]::Escape($end)
    $match = [regex]::Match($content, $pattern)
    if (-not $match.Success) {
        throw "$name expected to find block between [$start] and [$end]"
    }
    return $start + $match.Groups[1].Value
}

$difficultyFetchBlock = Get-Block 'difficulty fetch' $articleFetcherCode 'public static async Task<List<DifficultyInfo>> GetDifficultiesAsync(string categoryOverride = null, bool forceRefresh = false)' 'if (!response.IsSuccess || response.RawData == null)'
Assert-Contains 'difficulty stats reads selected category' $difficultyFetchBlock $getCategoryConfigNeedle
Assert-Contains 'difficulty stats accepts explicit category override' $difficultyFetchBlock 'categoryOverride ??'
Assert-Contains 'difficulty stats supports forced category refresh' $difficultyFetchBlock '!forceRefresh && TryGetCachedDifficulties'
Assert-Contains 'difficulty stats coalesces duplicate requests' $articleFetcherCode 'difficultyRequestsByCategory'
Assert-Contains 'difficulty stats builds query params' $difficultyFetchBlock 'new Dictionary<string, string>'
Assert-Contains 'difficulty stats sends selected category' $difficultyFetchBlock 'queryParams["category"] = configCategory'
Assert-Contains 'difficulty stats calls categorized endpoint' $difficultyFetchBlock 'client.GetAsync("/api/segments/stats", queryParams)'
Assert-Contains 'difficulty sync getter falls back to static difficulties' $articleFetcherCode 'CreateDefaultDifficulties()'

$startupLoadedBlock = Get-Block 'main window loaded startup' $mainWindowCode 'private void Window_Loaded(object sender, RoutedEventArgs e)' 'StartVersionCheck();'
Assert-NotContains 'startup should not request wenlai difficulty stats' $startupLoadedBlock 'GetDifficultiesAsync'

$wenlaiMenuBlock = Get-Block 'wenlai menu init' $mainWindowCode 'private void InitializeWenlaiMenu(string categoryOverride = null)' 'private async void MenuWenlai_Opened'
Assert-Contains 'wenlai menu resolves explicit selected category' $wenlaiMenuBlock 'categoryOverride ?? Config.GetString'
Assert-Contains 'wenlai menu reads difficulty cache for explicit category' $wenlaiMenuBlock 'ArticleFetcher.GetDifficulties(selectedCategory)'
Assert-Contains 'wenlai menu checks explicit category cache' $wenlaiMenuBlock 'ArticleFetcher.HasCachedDifficulties(selectedCategory)'
Assert-NotContains 'wenlai menu should not auto request difficulty stats while building' $wenlaiMenuBlock 'Task.Run(async'
Assert-NotContains 'wenlai menu should not show loading difficulty placeholder' $wenlaiMenuBlock '加载难度数据'

$categoryFetchBlock = Get-Block 'category fetch' $articleFetcherCode 'public static async Task<List<CategoryInfo>> GetCategoriesAsync()' 'cachedCategories = categories;'
Assert-Contains 'category parser uses shared category code reader' $categoryFetchBlock 'ReadCategoryCode(item)'
Assert-Contains 'category code reader supports category field' $articleFetcherCode '"category"'
Assert-Contains 'category code reader supports value field' $articleFetcherCode '"value"'
Assert-Contains 'category code reader supports slug field' $articleFetcherCode '"slug"'
Assert-Contains 'category code reader supports key field' $articleFetcherCode '"key"'
Assert-Contains 'category parser skips entries with no category code' $categoryFetchBlock 'string.IsNullOrWhiteSpace(code)'

$categoryAllClickBlock = Get-Block 'wenlai all category click' $mainWindowCode $setAllCategoryNeedle 'categoryItem.Items.Add(allItem);'
Assert-Contains 'all category forces uncategorized stats refresh' $categoryAllClickBlock 'ArticleFetcher.GetDifficultiesAsync("", true)'
Assert-Ordered 'all category saves before stats refresh' $categoryAllClickBlock $setAllCategoryNeedle 'ArticleFetcher.GetDifficultiesAsync("", true);'
Assert-Ordered 'all category waits for stats before menu rebuild' $categoryAllClickBlock 'await ArticleFetcher.GetDifficultiesAsync("", true);' 'InitializeWenlaiMenu("");'
Assert-Contains 'all category refreshes settings with all-category stats' $categoryAllClickBlock 'NotifyConfigWindowsRefreshWenlai("", false)'

$categoryItemClickBlock = Get-Block 'wenlai category click' $mainWindowCode $setSelectedCategoryNeedle 'categoryItem.Items.Add(catMenuItem);'
Assert-Contains 'selected category forces selected stats refresh' $categoryItemClickBlock 'ArticleFetcher.GetDifficultiesAsync(cat.Code, true)'
Assert-Ordered 'selected category saves before stats refresh' $categoryItemClickBlock $setSelectedCategoryNeedle 'ArticleFetcher.GetDifficultiesAsync(cat.Code, true);'
Assert-Ordered 'selected category waits for stats before menu rebuild' $categoryItemClickBlock 'await ArticleFetcher.GetDifficultiesAsync(cat.Code, true);' 'InitializeWenlaiMenu(cat.Code);'
Assert-Contains 'selected category refreshes settings with selected stats' $categoryItemClickBlock 'NotifyConfigWindowsRefreshWenlai(cat.Code, false)'

$configCategorySaveBlock = Get-Block 'settings category save' $configWindowCode 'private async void SaveWenlaiCategorySelection(ComboBox comboBox)' 'private static readonly string[] ColorConfigItems'
Assert-Contains 'settings category refreshes difficulty UI with selected category' $configCategorySaveBlock 'ReloadWenlaiDifficultyConfig(selectedCategory, true)'
Assert-Ordered 'settings category saves before stats refresh' $configCategorySaveBlock $saveCategoryNeedle 'ReloadWenlaiDifficultyConfig(selectedCategory, true)'
Assert-Contains 'settings category refreshes main menu with selected category' $configCategorySaveBlock 'new object[] { selectedCategory }'

$notifySettingsBlock = Get-Block 'main window settings refresh notification' $mainWindowCode 'private void NotifyConfigWindowsRefreshWenlai' '/// <summary>'
Assert-Contains 'main window passes selected category to settings refresh' $notifySettingsBlock 'ReloadWenlaiDifficultyConfig(categoryOverride, forceRefresh)'

$findConfigControlBlock = Get-Block 'settings config item lookup' $configWindowCode 'private Panel FindConfigItemControl' 'private async Task LoadDifficultyDataAsync'
Assert-Contains 'settings difficulty refresh searches actual ContentPanel' $findConfigControlBlock 'ContentPanel.Children'
Assert-NotContains 'settings difficulty refresh must not search missing panelSettings' $findConfigControlBlock 'panelSettings'

$settingsDifficultyLoadBlock = Get-Block 'settings difficulty list render' $configWindowCode 'private async Task LoadDifficultyDataAsync' 'private async Task LoadCategoryDataAsync'
Assert-Contains 'settings difficulty list renders every difficulty with count' $settingsDifficultyLoadBlock 'cb.Items.Add($"{difficultyName} ({count}'
Assert-NotContains 'settings difficulty list must not hide zero-count difficulty levels' $settingsDifficultyLoadBlock 'if (count == 0)'

Write-Host 'All Wenlai category stats refresh tests passed.'
