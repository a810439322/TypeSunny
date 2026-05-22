$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$testDir = Join-Path ([System.IO.Path]::GetTempPath()) ("typesunny-score-format-test-" + [System.Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $testDir | Out-Null

try {
    $scorePath = Join-Path $root 'Core\Score.cs'
    $testPath = Join-Path $root 'Tests\ScoreFormatRowsTests.cs'
    $stubPath = Join-Path $testDir 'ScoreFormatRowsStubs.cs'

    @'
namespace TypeSunny
{
    internal static class Config
    {
        public static bool GetBool(string name) { return false; }
        public static string GetString(string name) { return ""; }
    }

    internal static class WinTrainer
    {
        public static double TargetHit = 0;
    }
}

namespace TypeSunny.Utils
{
    internal static class IntStringDict
    {
        public static System.Collections.Generic.Dictionary<int, string> Selection =
            new System.Collections.Generic.Dictionary<int, string>();
        public static System.Collections.Generic.Dictionary<int, string> BiaoDing =
            new System.Collections.Generic.Dictionary<int, string>();
    }
}

namespace TypeSunny.Core
{
    public enum RetypeType
    {
        first,
        retype,
        shuffle,
        wrongRetype,
        slowRetype,
    }

    public enum TxtSource
    {
        unchange,
        qq,
        clipboard,
        changeSheng,
        jbs,
        jisucup,
        book,
        trainer,
        articlesender,
        raceApi
    }

    internal static class StateManager
    {
        public static string Version = "TypeSunny";
        public static TxtSource txtSource = TxtSource.unchange;
        public static RetypeType retypeType = RetypeType.first;
    }

    internal static class RetypeCounter
    {
        public static int Get(string key) { return 0; }
    }

    internal static class TextInfo
    {
        public static string TextMD5 = "";
    }
}
'@ | Set-Content -Path $stubPath -Encoding UTF8

    @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>disable</Nullable>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="$($scorePath.Replace('\', '\\'))" Link="Score.cs" />
    <Compile Include="$($testPath.Replace('\', '\\'))" Link="ScoreFormatRowsTests.cs" />
    <Compile Include="$($stubPath.Replace('\', '\\'))" Link="ScoreFormatRowsStubs.cs" />
  </ItemGroup>
</Project>
"@ | Set-Content -Path (Join-Path $testDir 'ScoreFormatRowsTests.csproj') -Encoding UTF8

    dotnet run --project (Join-Path $testDir 'ScoreFormatRowsTests.csproj') --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "ScoreFormatRowsTests failed with exit code $LASTEXITCODE."
    }
}
finally {
    try { Remove-Item -LiteralPath $testDir -Recurse -Force } catch { }
}
