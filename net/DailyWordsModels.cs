using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TypeSunny.Net
{
    public enum DailyWordsLeaderboardType
    {
        Daily,
        Total
    }

    public enum DailyWordsTypingKind
    {
        Article,
        Single
    }

    public interface IDailyWordsService
    {
        Task<DailyWordsReportResult> ReportAsync(DailyWordsReport report, CancellationToken cancellationToken);

        Task<DailyWordsRankResult> GetCurrentRankAsync(
            DailyWordsLeaderboardType type,
            DateTime? date,
            CancellationToken cancellationToken);

        Task<DailyWordsLeaderboardResult> GetLeaderboardAsync(
            DailyWordsLeaderboardType type,
            DateTime? date,
            int limit,
            CancellationToken cancellationToken);
    }

    public sealed class DailyWordsReport
    {
        public DailyWordsReport(int count, DateTime date)
            : this(count, date, 0, 0, 0, 0)
        {
        }

        public DailyWordsReport(
            int count,
            DateTime date,
            int singleWordCount,
            int articleWordCount,
            double articleAvgSpeed,
            double singleAvgKeystroke)
        {
            Count = Math.Max(0, count);
            Date = date.Date;
            SingleWordCount = Math.Min(Math.Max(0, singleWordCount), Count);
            ArticleWordCount = Math.Min(Math.Max(0, articleWordCount), Count - SingleWordCount);
            ArticleAvgSpeed = ArticleWordCount <= 0 ? 0 : NormalizeMetric(articleAvgSpeed);
            SingleAvgKeystroke = SingleWordCount <= 0 ? 0 : NormalizeMetric(singleAvgKeystroke);
        }

        public int Count { get; private set; }
        public DateTime Date { get; private set; }
        public int SingleWordCount { get; private set; }
        public int ArticleWordCount { get; private set; }
        public double ArticleAvgSpeed { get; private set; }
        public double SingleAvgKeystroke { get; private set; }

        public static DailyWordsReport Combine(DailyWordsReport left, DailyWordsReport right)
        {
            if (left == null)
                return right;
            if (right == null)
                return left;

            int count = left.Count + right.Count;
            int singleWordCount = left.SingleWordCount + right.SingleWordCount;
            int articleWordCount = left.ArticleWordCount + right.ArticleWordCount;
            double articleAvgSpeed = WeightedAverage(
                left.ArticleAvgSpeed,
                left.ArticleWordCount,
                right.ArticleAvgSpeed,
                right.ArticleWordCount);
            double singleAvgKeystroke = WeightedAverage(
                left.SingleAvgKeystroke,
                left.SingleWordCount,
                right.SingleAvgKeystroke,
                right.SingleWordCount);

            return new DailyWordsReport(
                count,
                right.Date,
                singleWordCount,
                articleWordCount,
                articleAvgSpeed,
                singleAvgKeystroke);
        }

        internal static double NormalizeMetric(double value)
        {
            return double.IsNaN(value) || double.IsInfinity(value) || value < 0 ? 0 : value;
        }

        internal static double WeightedAverage(double leftValue, int leftWeight, double rightValue, int rightWeight)
        {
            int safeLeftWeight = Math.Max(0, leftWeight);
            int safeRightWeight = Math.Max(0, rightWeight);
            int totalWeight = safeLeftWeight + safeRightWeight;
            if (totalWeight <= 0)
                return 0;

            return (NormalizeMetric(leftValue) * safeLeftWeight
                + NormalizeMetric(rightValue) * safeRightWeight)
                / totalWeight;
        }
    }

    public sealed class DailyWordsTypedStatisticsAccumulator
    {
        private DailyWordsReport pending = new DailyWordsReport(0, DateTime.MinValue);
        private DateTime singleKeystrokeMetricDate = DateTime.MinValue;
        private double singleKeystrokeWeightedSum;
        private int singleKeystrokeWeight;

        public void Add(
            int words,
            DateTime date,
            DailyWordsTypingKind kind,
            double articleAvgSpeed,
            double singleAvgKeystroke)
        {
            if (words <= 0)
                return;

            var report = kind == DailyWordsTypingKind.Single
                ? new DailyWordsReport(words, date, words, 0, 0, 0)
                : new DailyWordsReport(words, date, 0, words, articleAvgSpeed, 0);

            pending = pending.Count > 0 && pending.Date == report.Date
                ? DailyWordsReport.Combine(pending, report)
                : report;

            if (kind == DailyWordsTypingKind.Single && singleAvgKeystroke > 0)
                AddSingleKeystrokeMetric(words, date, singleAvgKeystroke);
        }

        public void AddSingleKeystrokeMetric(int sourceWords, DateTime date, double singleAvgKeystroke)
        {
            if (sourceWords <= 0)
                return;

            double safeMetric = DailyWordsReport.NormalizeMetric(singleAvgKeystroke);
            if (safeMetric <= 0)
                return;

            DateTime normalizedDate = date.Date;
            if (singleKeystrokeWeight > 0 && singleKeystrokeMetricDate != normalizedDate)
            {
                singleKeystrokeWeightedSum = 0;
                singleKeystrokeWeight = 0;
            }

            singleKeystrokeMetricDate = normalizedDate;
            singleKeystrokeWeightedSum += safeMetric * sourceWords;
            singleKeystrokeWeight += sourceWords;
        }

        public DailyWordsReport Flush()
        {
            DailyWordsReport report = pending ?? new DailyWordsReport(0, DateTime.MinValue);
            if (report.Count <= 0)
                return report;

            bool hasSingleWords = report.SingleWordCount > 0;
            bool hasSingleMetric = singleKeystrokeWeight > 0 && singleKeystrokeMetricDate == report.Date;
            if (hasSingleWords && !hasSingleMetric)
            {
                if (report.ArticleWordCount <= 0)
                    return new DailyWordsReport(0, report.Date);

                pending = new DailyWordsReport(
                    report.SingleWordCount,
                    report.Date,
                    report.SingleWordCount,
                    0,
                    0,
                    0);

                return new DailyWordsReport(
                    report.ArticleWordCount,
                    report.Date,
                    0,
                    report.ArticleWordCount,
                    report.ArticleAvgSpeed,
                    0);
            }

            pending = new DailyWordsReport(0, DateTime.MinValue);
            if (hasSingleMetric)
            {
                int metricSingleWordCount = report.SingleWordCount <= 0
                    ? 0
                    : Math.Min(report.SingleWordCount, singleKeystrokeWeight);
                int metricArticleWordCount = Math.Min(
                    report.ArticleWordCount,
                    Math.Max(0, report.Count - metricSingleWordCount));
                report = new DailyWordsReport(
                    report.Count,
                    report.Date,
                    metricSingleWordCount,
                    metricArticleWordCount,
                    report.ArticleAvgSpeed,
                    singleKeystrokeWeightedSum / singleKeystrokeWeight);

                ResetSingleKeystrokeMetric();
            }

            return report;
        }

        private void ResetSingleKeystrokeMetric()
        {
            singleKeystrokeMetricDate = DateTime.MinValue;
            singleKeystrokeWeightedSum = 0;
            singleKeystrokeWeight = 0;
        }

        public DailyWordsReport Flush(double articleAvgSpeed, double singleAvgKeystroke)
        {
            DailyWordsReport report = Flush();
            if (report.Count <= 0)
                return report;

            return new DailyWordsReport(
                report.Count,
                report.Date,
                report.SingleWordCount,
                report.ArticleWordCount,
                report.ArticleWordCount > 0 ? articleAvgSpeed : report.ArticleAvgSpeed,
                report.SingleAvgKeystroke);
        }
    }

    public sealed class DailyWordsReportResult
    {
        private DailyWordsReportResult(bool isSuccess, string message)
        {
            IsSuccess = isSuccess;
            Message = message ?? "";
        }

        public bool IsSuccess { get; private set; }
        public string Message { get; private set; }

        public static DailyWordsReportResult Success(string message = "")
        {
            return new DailyWordsReportResult(true, message);
        }

        public static DailyWordsReportResult Failure(string message)
        {
            return new DailyWordsReportResult(false, message);
        }
    }

    public sealed class DailyWordsRank
    {
        public DailyWordsRank(
            int rank,
            long wordCount,
            DailyWordsLeaderboardType type,
            DateTime? date,
            long singleWordCount,
            long articleWordCount,
            double articleAvgSpeed,
            double singleAvgKeystroke)
        {
            Rank = rank;
            WordCount = Math.Max(0, wordCount);
            Type = type;
            Date = date;
            SingleWordCount = Math.Max(0, singleWordCount);
            ArticleWordCount = Math.Max(0, articleWordCount);
            ArticleAvgSpeed = DailyWordsReport.NormalizeMetric(articleAvgSpeed);
            SingleAvgKeystroke = DailyWordsReport.NormalizeMetric(singleAvgKeystroke);
        }

        public int Rank { get; private set; }
        public long WordCount { get; private set; }
        public long SingleWordCount { get; private set; }
        public long ArticleWordCount { get; private set; }
        public double ArticleAvgSpeed { get; private set; }
        public double SingleAvgKeystroke { get; private set; }
        public DailyWordsLeaderboardType Type { get; private set; }
        public DateTime? Date { get; private set; }
    }

    public sealed class DailyWordsRankResult
    {
        private DailyWordsRankResult(bool isSuccess, string message, DailyWordsRank rank)
        {
            IsSuccess = isSuccess;
            Message = message ?? "";
            Rank = rank;
        }

        public bool IsSuccess { get; private set; }
        public string Message { get; private set; }
        public DailyWordsRank Rank { get; private set; }

        public static DailyWordsRankResult Success(DailyWordsRank rank)
        {
            return new DailyWordsRankResult(true, "", rank);
        }

        public static DailyWordsRankResult Failure(string message)
        {
            return new DailyWordsRankResult(false, message, null);
        }
    }

    public sealed class DailyWordsLeaderboardEntry
    {
        public DailyWordsLeaderboardEntry(
            int rank,
            long userId,
            string username,
            long wordCount,
            DailyWordsLeaderboardType type,
            DateTime? date,
            long singleWordCount,
            long articleWordCount,
            double articleAvgSpeed,
            double singleAvgKeystroke)
        {
            Rank = rank;
            UserId = userId;
            Username = username ?? "";
            WordCount = Math.Max(0, wordCount);
            Type = type;
            Date = date;
            SingleWordCount = Math.Max(0, singleWordCount);
            ArticleWordCount = Math.Max(0, articleWordCount);
            ArticleAvgSpeed = DailyWordsReport.NormalizeMetric(articleAvgSpeed);
            SingleAvgKeystroke = DailyWordsReport.NormalizeMetric(singleAvgKeystroke);
        }

        public int Rank { get; private set; }
        public long UserId { get; private set; }
        public string Username { get; private set; }
        public long WordCount { get; private set; }
        public long SingleWordCount { get; private set; }
        public long ArticleWordCount { get; private set; }
        public double ArticleAvgSpeed { get; private set; }
        public double SingleAvgKeystroke { get; private set; }
        public DailyWordsLeaderboardType Type { get; private set; }
        public DateTime? Date { get; private set; }
    }

    public sealed class DailyWordsLeaderboardResult
    {
        private DailyWordsLeaderboardResult(
            bool isSuccess,
            string message,
            DailyWordsLeaderboardType type,
            DateTime? date,
            IReadOnlyList<DailyWordsLeaderboardEntry> entries)
        {
            IsSuccess = isSuccess;
            Message = message ?? "";
            Type = type;
            Date = date;
            Entries = entries ?? Array.Empty<DailyWordsLeaderboardEntry>();
        }

        public bool IsSuccess { get; private set; }
        public string Message { get; private set; }
        public DailyWordsLeaderboardType Type { get; private set; }
        public DateTime? Date { get; private set; }
        public IReadOnlyList<DailyWordsLeaderboardEntry> Entries { get; private set; }

        public static DailyWordsLeaderboardResult Success(
            DailyWordsLeaderboardType type,
            DateTime? date,
            IReadOnlyList<DailyWordsLeaderboardEntry> entries)
        {
            return new DailyWordsLeaderboardResult(true, "", type, date, entries);
        }

        public static DailyWordsLeaderboardResult Failure(string message)
        {
            return new DailyWordsLeaderboardResult(
                false,
                message,
                DailyWordsLeaderboardType.Daily,
                null,
                Array.Empty<DailyWordsLeaderboardEntry>());
        }
    }
}
