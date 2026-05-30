$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$testDir = Join-Path ([System.IO.Path]::GetTempPath()) ("typesunny-compiled-release-identity-test-" + [System.Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $testDir | Out-Null

try {
    $releaseIdentityPath = Join-Path $root 'Version\ReleaseIdentity.cs'
    $generatedVersionPath = Join-Path $root 'Version\GeneratedVersion.cs'
    $compiledIdentityPath = Join-Path $root 'Version\CompiledReleaseIdentity.cs'
    $testPath = Join-Path $root 'Tests\CompiledReleaseIdentityTests.cs'

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
    <Compile Include="$($releaseIdentityPath.Replace('\', '\\'))" Link="ReleaseIdentity.cs" />
    <Compile Include="$($generatedVersionPath.Replace('\', '\\'))" Link="GeneratedVersion.cs" />
    <Compile Include="$($compiledIdentityPath.Replace('\', '\\'))" Link="CompiledReleaseIdentity.cs" />
    <Compile Include="$($testPath.Replace('\', '\\'))" Link="CompiledReleaseIdentityTests.cs" />
  </ItemGroup>
</Project>
"@ | Set-Content -Path (Join-Path $testDir 'CompiledReleaseIdentityTests.csproj') -Encoding UTF8

    dotnet run --project (Join-Path $testDir 'CompiledReleaseIdentityTests.csproj') --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "CompiledReleaseIdentityTests failed with exit code $LASTEXITCODE."
    }
}
finally {
    Remove-Item -Path $testDir -Recurse -Force -ErrorAction SilentlyContinue
}
