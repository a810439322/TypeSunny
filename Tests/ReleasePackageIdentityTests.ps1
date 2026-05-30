$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$testDir = Join-Path ([System.IO.Path]::GetTempPath()) ("typesunny-release-package-identity-test-" + [System.Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $testDir | Out-Null

try {
    $identityPath = Join-Path $root 'Version\ReleaseIdentity.cs'
    $packageIdentityPath = Join-Path $root 'Version\ReleasePackageIdentity.cs'
    $testPath = Join-Path $root 'Tests\ReleasePackageIdentityTests.cs'
    $newtonsoftPath = Join-Path $root 'packages\Newtonsoft.Json.13.0.3\lib\net45\Newtonsoft.Json.dll'

@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>disable</ImplicitUsings>
    <Nullable>disable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="Newtonsoft.Json">
      <HintPath>$($newtonsoftPath.Replace('\', '\\'))</HintPath>
    </Reference>
    <Compile Include="$($identityPath.Replace('\', '\\'))" Link="ReleaseIdentity.cs" />
    <Compile Include="$($packageIdentityPath.Replace('\', '\\'))" Link="ReleasePackageIdentity.cs" />
    <Compile Include="$($testPath.Replace('\', '\\'))" Link="ReleasePackageIdentityTests.cs" />
  </ItemGroup>
</Project>
"@ | Set-Content -Path (Join-Path $testDir 'ReleasePackageIdentityTests.csproj') -Encoding UTF8

    dotnet run --project (Join-Path $testDir 'ReleasePackageIdentityTests.csproj') --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "ReleasePackageIdentityTests failed with exit code $LASTEXITCODE."
    }
}
finally {
    Remove-Item -Path $testDir -Recurse -Force -ErrorAction SilentlyContinue
}
