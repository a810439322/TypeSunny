$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$testDir = Join-Path ([System.IO.Path]::GetTempPath()) ("typesunny-score-panel-layout-test-" + [System.Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $testDir | Out-Null

try {
    $policyPath = Join-Path $root 'UI\ScorePanelLayoutPolicy.cs'
    $testPath = Join-Path $root 'Tests\ScorePanelLayoutPolicyTests.cs'

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
    <Compile Include="$($policyPath.Replace('\', '\\'))" Link="ScorePanelLayoutPolicy.cs" />
    <Compile Include="$($testPath.Replace('\', '\\'))" Link="ScorePanelLayoutPolicyTests.cs" />
  </ItemGroup>
</Project>
"@ | Set-Content -Path (Join-Path $testDir 'ScorePanelLayoutPolicyTests.csproj') -Encoding UTF8

    dotnet run --project (Join-Path $testDir 'ScorePanelLayoutPolicyTests.csproj') --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "ScorePanelLayoutPolicyTests failed with exit code $LASTEXITCODE."
    }
}
finally {
    try { Remove-Item -LiteralPath $testDir -Recurse -Force } catch { }
}
