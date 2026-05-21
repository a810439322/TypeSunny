$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$testDir = Join-Path ([System.IO.Path]::GetTempPath()) ("typesunny-trainerlog-test-" + [System.Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $testDir | Out-Null

try {
    $articleLogPath = (Join-Path $root 'Logs\ArticleLog.cs')
    $trainerLogPath = (Join-Path $root 'WinTrainer\TrainerLog.cs')
    $newtonsoftPath = (Join-Path $root 'packages\Newtonsoft.Json.13.0.3\lib\netstandard2.0\Newtonsoft.Json.dll')

    @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>disable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="$($articleLogPath.Replace('\', '\\'))" Link="ArticleLog.cs" />
    <Compile Include="$($trainerLogPath.Replace('\', '\\'))" Link="TrainerLog.cs" />
    <Reference Include="Newtonsoft.Json">
      <HintPath>$($newtonsoftPath.Replace('\', '\\'))</HintPath>
    </Reference>
  </ItemGroup>
</Project>
"@ | Set-Content -Path (Join-Path $testDir 'TrainerLogActualWordsTest.csproj') -Encoding UTF8

    @'
using System;
using System.IO;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using TypeSunny;
using TypeSunny.Logs;

internal static class Program
{
    private static int Main()
    {
        string dataDir = Path.Combine(Path.GetTempPath(), "typesunny-trainerlog-data-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dataDir);
        Directory.SetCurrentDirectory(dataDir);

        try
        {
            PreservesLegacySummaryWordsWhenAddingNewActualWords();
            RecordsActualWordsFromNewTrainerRecord();

            Console.WriteLine("TrainerLog actual words test passed.");
            return 0;
        }
        finally
        {
            Directory.SetCurrentDirectory(AppContext.BaseDirectory);
            try { Directory.Delete(dataDir, recursive: true); } catch { }
        }
    }

    private static void RecordsActualWordsFromNewTrainerRecord()
    {
        TrainerLog.WriteRecord(new ArticleLog.ArticleRecord
        {
            Time = DateTime.Now,
            ArticleName = "actual-words-sample",
            TotalWords = 10,
            InputWords = 17,
            Speed = 60,
            HitRate = 5,
            Accuracy = 0.95,
            KPW = 5,
            TotalSeconds = 10
        });

        var item = WaitForStatsItem("actual-words-sample");

        if (item.TotalWords != 10)
            throw new Exception("expected TotalWords 10, got " + item.TotalWords + ".");

        if (item.TotalInputWords != 17)
            throw new Exception("expected TotalInputWords 17, got " + item.TotalInputWords + ".");
    }

    private static void PreservesLegacySummaryWordsWhenAddingNewActualWords()
    {
        string summaryPath = GetTrainerSummaryFilePath();
        Directory.CreateDirectory(Path.GetDirectoryName(summaryPath));
        var legacySummary = new TrainerLog.DailyStatisticsData();
        legacySummary.DailySummaries.Add(new ArticleLog.StatisticsData
        {
            Date = DateTime.Now.ToString("yyyy-MM-dd"),
            Summaries =
            {
                new ArticleLog.StatisticsSummary
                {
                    GroupKey = "legacy-actual-words-sample",
                    Count = 1,
                    TotalWords = 10,
                    SumSpeedWeighted = 600,
                    SumHitRateWeighted = 50,
                    SumAccuracyWeighted = 9.5,
                    SumKPWWeighted = 50,
                    MaxSpeed = 60,
                    MinSpeed = 60
                }
            }
        });
        File.WriteAllText(summaryPath, JsonConvert.SerializeObject(legacySummary));

        var seededItem = WaitForStatsItem("legacy-actual-words-sample", expectedTotalWords: 10);
        if (seededItem.TotalInputWords != 10)
            throw new Exception("expected legacy seeded TotalInputWords fallback 10, got " + seededItem.TotalInputWords + ".");

        TrainerLog.WriteRecord(new ArticleLog.ArticleRecord
        {
            Time = DateTime.Now,
            ArticleName = "legacy-actual-words-sample",
            TotalWords = 5,
            InputWords = 8,
            Speed = 50,
            HitRate = 4,
            Accuracy = 0.9,
            KPW = 4.8,
            TotalSeconds = 6
        });

        var item = WaitForStatsItem("legacy-actual-words-sample", expectedTotalWords: 15);

        if (item.TotalInputWords != 18)
            throw new Exception("expected legacy TotalInputWords 18, got " + item.TotalInputWords + ".");
    }

    private static ArticleLog.LocalArticleStatisticsItem WaitForStatsItem(string bookName, int? expectedTotalWords = null)
    {
        string lastSnapshot = "";
        DateTime deadline = DateTime.Now.AddSeconds(5);
        while (DateTime.Now < deadline)
        {
            var stats = TrainerLog.ReadStatisticsInRange(DateTime.Today, DateTime.Today);
            lastSnapshot = string.Join("; ", stats.Select(s => s.BookName + ": total=" + s.TotalWords + ", input=" + s.TotalInputWords));
            var item = stats.FirstOrDefault(s => s.BookName == bookName);

            if (item != null && (!expectedTotalWords.HasValue || item.TotalWords == expectedTotalWords.Value))
                return item;

            Thread.Sleep(50);
        }

        throw new Exception("expected trainer statistics item '" + bookName + "' to be written. CWD: "
            + Environment.CurrentDirectory
            + ", summaryExists=" + File.Exists(GetTrainerSummaryFilePath())
            + ". Last stats: " + lastSnapshot);
    }

    private static string GetTrainerSummaryFilePath()
    {
        var method = typeof(TrainerLog).GetMethod("GetSummaryFilePath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        return (string)method.Invoke(null, null);
    }

}
'@ | Set-Content -Path (Join-Path $testDir 'Program.cs') -Encoding UTF8

    dotnet run --project (Join-Path $testDir 'TrainerLogActualWordsTest.csproj') --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "TrainerLogActualWordsTests failed with exit code $LASTEXITCODE."
    }
}
finally {
    try { Remove-Item -LiteralPath $testDir -Recurse -Force } catch { }
}
