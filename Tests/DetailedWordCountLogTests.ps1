$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$testDir = Join-Path ([System.IO.Path]::GetTempPath()) ("typesunny-detailed-word-count-test-" + [System.Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $testDir | Out-Null

try {
    $stateManagerPath = Join-Path $root 'Core\StateManager.cs'
    $detailedLogPath = Join-Path $root 'Logs\DetailedWordCountLog.cs'
    $testPath = Join-Path $root 'Tests\DetailedWordCountLogTests.cs'
    $newtonsoftPath = Join-Path $root 'packages\Newtonsoft.Json.13.0.3\lib\netstandard2.0\Newtonsoft.Json.dll'

    @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>disable</ImplicitUsings>
    <Nullable>disable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="$($stateManagerPath.Replace('\', '\\'))" Link="StateManager.cs" />
    <Compile Include="$($detailedLogPath.Replace('\', '\\'))" Link="DetailedWordCountLog.cs" />
    <Compile Include="$($testPath.Replace('\', '\\'))" Link="DetailedWordCountLogTests.cs" />
    <Reference Include="Newtonsoft.Json">
      <HintPath>$($newtonsoftPath.Replace('\', '\\'))</HintPath>
    </Reference>
  </ItemGroup>
</Project>
"@ | Set-Content -Path (Join-Path $testDir 'DetailedWordCountLogTests.csproj') -Encoding UTF8

    dotnet run --project (Join-Path $testDir 'DetailedWordCountLogTests.csproj') --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "DetailedWordCountLogTests failed with exit code $LASTEXITCODE."
    }
}
finally {
    try { Remove-Item -LiteralPath $testDir -Recurse -Force } catch { }
}
