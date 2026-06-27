$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$testDir = Join-Path ([System.IO.Path]::GetTempPath()) ("typesunny-home-bottom-toolbar-layout-test-" + [System.Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $testDir | Out-Null

try {
    $policyPath = Join-Path $root 'UI\HomeBottomToolbarLayoutPolicy.cs'
    $testPath = Join-Path $root 'Tests\HomeBottomToolbarLayoutPolicyTests.cs'

    @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>disable</ImplicitUsings>
    <Nullable>disable</Nullable>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="$($policyPath.Replace('\', '\\'))" Link="HomeBottomToolbarLayoutPolicy.cs" />
    <Compile Include="$($testPath.Replace('\', '\\'))" Link="HomeBottomToolbarLayoutPolicyTests.cs" />
  </ItemGroup>
</Project>
"@ | Set-Content -Path (Join-Path $testDir 'HomeBottomToolbarLayoutPolicyTests.csproj') -Encoding UTF8

    dotnet run --project (Join-Path $testDir 'HomeBottomToolbarLayoutPolicyTests.csproj') --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "HomeBottomToolbarLayoutPolicyTests failed with exit code $LASTEXITCODE."
    }
}
finally {
    try { Remove-Item -LiteralPath $testDir -Recurse -Force } catch { }
}
