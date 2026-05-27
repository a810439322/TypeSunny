$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$testDir = Join-Path ([System.IO.Path]::GetTempPath()) ("typesunny-trainer-title-stats-test-" + [System.Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $testDir | Out-Null

try {
    $statsPath = Join-Path $root 'WinTrainer\TrainerTitleWordStats.cs'
    $trainerLogPath = Join-Path $root 'WinTrainer\TrainerLog.cs'
    $articleLogPath = Join-Path $root 'Logs\ArticleLog.cs'
    $testPath = Join-Path $root 'Tests\TrainerTitleWordStatsTests.cs'

    @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>disable</ImplicitUsings>
    <Nullable>disable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
    <Compile Include="$($statsPath.Replace('\', '\\'))" Link="TrainerTitleWordStats.cs" />
    <Compile Include="$($trainerLogPath.Replace('\', '\\'))" Link="TrainerLog.cs" />
    <Compile Include="$($articleLogPath.Replace('\', '\\'))" Link="ArticleLog.cs" />
    <Compile Include="$($testPath.Replace('\', '\\'))" Link="TrainerTitleWordStatsTests.cs" />
  </ItemGroup>
</Project>
"@ | Set-Content -Path (Join-Path $testDir 'TrainerTitleWordStatsTests.csproj') -Encoding UTF8

    dotnet run --project (Join-Path $testDir 'TrainerTitleWordStatsTests.csproj') --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "TrainerTitleWordStatsTests failed with exit code $LASTEXITCODE."
    }
}
finally {
    Remove-Item -Recurse -Force $testDir -ErrorAction SilentlyContinue
}
