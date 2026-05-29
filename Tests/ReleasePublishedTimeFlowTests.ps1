$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$testDir = Join-Path ([System.IO.Path]::GetTempPath()) ("typesunny-release-time-flow-test-" + [System.Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $testDir | Out-Null

try {
    $testPath = Join-Path $root 'Tests\ReleasePublishedTimeFlowTests.cs'

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
    <Compile Include="$($testPath.Replace('\', '\\'))" Link="ReleasePublishedTimeFlowTests.cs" />
  </ItemGroup>
</Project>
"@ | Set-Content -Path (Join-Path $testDir 'ReleasePublishedTimeFlowTests.csproj') -Encoding UTF8

    dotnet run --project (Join-Path $testDir 'ReleasePublishedTimeFlowTests.csproj') -- "$root"
    if ($LASTEXITCODE -ne 0) {
        throw "ReleasePublishedTimeFlowTests failed with exit code $LASTEXITCODE."
    }
}
finally {
    Remove-Item -Recurse -Force $testDir -ErrorAction SilentlyContinue
}
