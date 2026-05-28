$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$testDir = Join-Path ([System.IO.Path]::GetTempPath()) ("typesunny-trainer-main-window-config-scope-test-" + [System.Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $testDir | Out-Null

try {
    $configPath = Join-Path $root 'Config\Config.cs'
    $passwordCryptoPath = Join-Path $root 'Utils\PasswordCrypto.cs'
    $scopePath = Join-Path $root 'UI\TrainerMainWindowConfigScope.cs'
    $testPath = Join-Path $root 'Tests\TrainerMainWindowConfigScopeTests.cs'

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
    <Compile Include="$($configPath.Replace('\', '\\'))" Link="Config.cs" />
    <Compile Include="$($passwordCryptoPath.Replace('\', '\\'))" Link="PasswordCrypto.cs" />
    <Compile Include="$($scopePath.Replace('\', '\\'))" Link="TrainerMainWindowConfigScope.cs" />
    <Compile Include="$($testPath.Replace('\', '\\'))" Link="TrainerMainWindowConfigScopeTests.cs" />
  </ItemGroup>
</Project>
"@ | Set-Content -Path (Join-Path $testDir 'TrainerMainWindowConfigScopeTests.csproj') -Encoding UTF8

    dotnet run --project (Join-Path $testDir 'TrainerMainWindowConfigScopeTests.csproj') --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "TrainerMainWindowConfigScopeTests failed with exit code $LASTEXITCODE."
    }
}
finally {
    Remove-Item -Recurse -Force $testDir -ErrorAction SilentlyContinue
}
