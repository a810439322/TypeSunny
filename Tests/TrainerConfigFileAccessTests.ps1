$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$testDir = Join-Path ([System.IO.Path]::GetTempPath()) ("typesunny-trainer-config-file-access-test-" + [System.Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $testDir | Out-Null

try {
    $trainerConfigPath = Join-Path $root 'WinTrainer\TrainerConfig.cs'
    $testPath = Join-Path $root 'Tests\TrainerConfigFileAccessTests.cs'

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
    <Compile Include="$($trainerConfigPath.Replace('\', '\\'))" Link="TrainerConfig.cs" />
    <Compile Include="$($testPath.Replace('\', '\\'))" Link="TrainerConfigFileAccessTests.cs" />
  </ItemGroup>
</Project>
"@ | Set-Content -Path (Join-Path $testDir 'TrainerConfigFileAccessTests.csproj') -Encoding UTF8

    dotnet run --project (Join-Path $testDir 'TrainerConfigFileAccessTests.csproj') --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "TrainerConfigFileAccessTests failed with exit code $LASTEXITCODE."
    }
}
finally {
    Remove-Item -Recurse -Force $testDir -ErrorAction SilentlyContinue
}
