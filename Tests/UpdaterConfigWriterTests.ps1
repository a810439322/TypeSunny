$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$testDir = Join-Path ([System.IO.Path]::GetTempPath()) ("typesunny-updater-config-writer-test-" + [System.Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $testDir | Out-Null

try {
    $writerPath = Join-Path $root 'Updater\UpdaterConfigWriter.cs'
    $testPath = Join-Path $root 'Tests\UpdaterConfigWriterTests.cs'

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
    <Compile Include="$($writerPath.Replace('\', '\\'))" Link="UpdaterConfigWriter.cs" Condition="Exists('$($writerPath.Replace('\', '\\'))')" />
    <Compile Include="$($testPath.Replace('\', '\\'))" Link="UpdaterConfigWriterTests.cs" />
  </ItemGroup>
</Project>
"@ | Set-Content -Path (Join-Path $testDir 'UpdaterConfigWriterTests.csproj') -Encoding UTF8

    dotnet run --project (Join-Path $testDir 'UpdaterConfigWriterTests.csproj') --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "UpdaterConfigWriterTests failed with exit code $LASTEXITCODE."
    }
}
finally {
    Remove-Item -Recurse -Force $testDir -ErrorAction SilentlyContinue
}
