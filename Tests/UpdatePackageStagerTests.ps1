$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$testDir = Join-Path ([System.IO.Path]::GetTempPath()) ("typesunny-update-stager-test-" + [System.Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $testDir | Out-Null

try {
    $stagerPath = Join-Path $root 'Utils\UpdatePackageStager.cs'
    $testPath = Join-Path $root 'Tests\UpdatePackageStagerTests.cs'

@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>disable</ImplicitUsings>
    <Nullable>disable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="$($stagerPath.Replace('\', '\\'))" Link="UpdatePackageStager.cs" />
    <Compile Include="$($testPath.Replace('\', '\\'))" Link="UpdatePackageStagerTests.cs" />
  </ItemGroup>
</Project>
"@ | Set-Content -Path (Join-Path $testDir 'UpdatePackageStagerTests.csproj') -Encoding UTF8

    dotnet run --project (Join-Path $testDir 'UpdatePackageStagerTests.csproj') --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "UpdatePackageStagerTests failed with exit code $LASTEXITCODE."
    }
}
finally {
    Remove-Item -Path $testDir -Recurse -Force -ErrorAction SilentlyContinue
}
