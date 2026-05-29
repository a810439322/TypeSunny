$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$testDir = Join-Path ([System.IO.Path]::GetTempPath()) ("typesunny-release-identity-test-" + [System.Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $testDir | Out-Null

try {
    $identityPath = Join-Path $root 'Version\ReleaseIdentity.cs'
    $testPath = Join-Path $root 'Tests\VersionReleaseIdentityTests.cs'

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
    <Compile Include="$($identityPath.Replace('\', '\\'))" Link="ReleaseIdentity.cs" Condition="Exists('$($identityPath.Replace('\', '\\'))')" />
    <Compile Include="$($testPath.Replace('\', '\\'))" Link="VersionReleaseIdentityTests.cs" />
  </ItemGroup>
</Project>
"@ | Set-Content -Path (Join-Path $testDir 'VersionReleaseIdentityTests.csproj') -Encoding UTF8

    dotnet run --project (Join-Path $testDir 'VersionReleaseIdentityTests.csproj') --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "VersionReleaseIdentityTests failed with exit code $LASTEXITCODE."
    }
}
finally {
    Remove-Item -Recurse -Force $testDir -ErrorAction SilentlyContinue
}
