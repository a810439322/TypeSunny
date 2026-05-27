$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$testDir = Join-Path ([System.IO.Path]::GetTempPath()) ("typesunny-trainer-actual-counter-test-" + [System.Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $testDir | Out-Null

try {
    $helperPath = Join-Path $root 'WinTrainer\TrainerActualWordCounter.cs'
    $typedCounterPath = Join-Path $root 'WinTrainer\TrainerTypedWordCounter.cs'
    $testPath = Join-Path $root 'Tests\TrainerActualWordCounterTests.cs'

    @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>disable</ImplicitUsings>
    <Nullable>disable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="$($helperPath.Replace('\', '\\'))" Link="TrainerActualWordCounter.cs" />
    <Compile Include="$($typedCounterPath.Replace('\', '\\'))" Link="TrainerTypedWordCounter.cs" />
    <Compile Include="$($testPath.Replace('\', '\\'))" Link="TrainerActualWordCounterTests.cs" />
  </ItemGroup>
</Project>
"@ | Set-Content -Path (Join-Path $testDir 'TrainerActualWordCounterTests.csproj') -Encoding UTF8

    dotnet run --project (Join-Path $testDir 'TrainerActualWordCounterTests.csproj') --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "TrainerActualWordCounterTests failed with exit code $LASTEXITCODE."
    }
}
finally {
    Remove-Item -Recurse -Force $testDir -ErrorAction SilentlyContinue
}
