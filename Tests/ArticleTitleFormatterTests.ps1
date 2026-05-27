$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$testDir = Join-Path ([System.IO.Path]::GetTempPath()) ("typesunny-article-title-formatter-test-" + [System.Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $testDir | Out-Null

try {
    $formatterPath = Join-Path $root 'Article\ArticleTitleFormatter.cs'
    $testPath = Join-Path $root 'Tests\ArticleTitleFormatterTests.cs'

    @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>disable</ImplicitUsings>
    <Nullable>disable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="$($formatterPath.Replace('\', '\\'))" Link="ArticleTitleFormatter.cs" Condition="Exists('$($formatterPath.Replace('\', '\\'))')" />
    <Compile Include="$($testPath.Replace('\', '\\'))" Link="ArticleTitleFormatterTests.cs" />
  </ItemGroup>
</Project>
"@ | Set-Content -Path (Join-Path $testDir 'ArticleTitleFormatterTests.csproj') -Encoding UTF8

    dotnet run --project (Join-Path $testDir 'ArticleTitleFormatterTests.csproj') --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "ArticleTitleFormatterTests failed with exit code $LASTEXITCODE."
    }
}
finally {
    Remove-Item -Recurse -Force $testDir -ErrorAction SilentlyContinue
}
