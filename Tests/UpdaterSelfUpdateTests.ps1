$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$testDir = Join-Path ([System.IO.Path]::GetTempPath()) ("typesunny-updater-self-update-test-" + [System.Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $testDir | Out-Null

try {
    $programPath = Join-Path $root 'Updater\Program.cs'
    $writerPath = Join-Path $root 'Updater\UpdaterConfigWriter.cs'
    $testPath = Join-Path $root 'Tests\UpdaterSelfUpdateTests.cs'

@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <StartupObject>TypeSunny.Tests.UpdaterSelfUpdateTests</StartupObject>
    <ImplicitUsings>disable</ImplicitUsings>
    <Nullable>disable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="$($programPath.Replace('\', '\\'))" Link="Program.cs" />
    <Compile Include="$($writerPath.Replace('\', '\\'))" Link="UpdaterConfigWriter.cs" />
    <Compile Include="$($testPath.Replace('\', '\\'))" Link="UpdaterSelfUpdateTests.cs" />
  </ItemGroup>
</Project>
"@ | Set-Content -Path (Join-Path $testDir 'UpdaterSelfUpdateTests.csproj') -Encoding UTF8

    dotnet run --project (Join-Path $testDir 'UpdaterSelfUpdateTests.csproj') --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "UpdaterSelfUpdateTests failed with exit code $LASTEXITCODE."
    }
}
finally {
    Remove-Item -Path $testDir -Recurse -Force -ErrorAction SilentlyContinue
}
