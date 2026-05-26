$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$testDir = Join-Path ([System.IO.Path]::GetTempPath()) ("typesunny-score-label-display-test-" + [System.Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $testDir | Out-Null

try {
    $formatterPath = Join-Path $root 'UI\ScoreLabelDisplayFormatter.cs'
    $testPath = Join-Path $root 'Tests\ScoreLabelDisplayFormatterTests.cs'

    @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>disable</Nullable>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="$($formatterPath.Replace('\', '\\'))" Link="ScoreLabelDisplayFormatter.cs" />
    <Compile Include="$($testPath.Replace('\', '\\'))" Link="ScoreLabelDisplayFormatterTests.cs" />
  </ItemGroup>
</Project>
"@ | Set-Content -Path (Join-Path $testDir 'ScoreLabelDisplayFormatterTests.csproj') -Encoding UTF8

    dotnet run --project (Join-Path $testDir 'ScoreLabelDisplayFormatterTests.csproj') --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "ScoreLabelDisplayFormatterTests failed with exit code $LASTEXITCODE."
    }
}
finally {
    try { Remove-Item -LiteralPath $testDir -Recurse -Force } catch { }
}
