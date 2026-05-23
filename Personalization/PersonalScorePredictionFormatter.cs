using System;
using System.Collections.Generic;
using System.Linq;

namespace TypeSunny.Personalization
{
    internal static class PersonalScorePredictionFormatter
    {
        public const double ScoreAttachmentConfidenceThreshold = 0.80;

        /// <summary>
        /// 显示阈值：置信度低于该值时整段预测都不显示，让用户看到的还是纯基础难度。
        /// 冷启动期 confidence ≈ 0，预测往往严重失真，显示出来会让用户失去信任。
        /// 阈值定在 0.30 是经验值——大致对应"至少 1/3 字符在 DP 路径上是成熟样本"。
        /// </summary>
        public const double MinDisplayConfidence = 0.30;

        private static readonly string[] DefaultOrderItems =
        {
            "速度",
            "难度",
            "用时",
            "击键",
            "码长",
            "总键数",
            "置信"
        };

        private static readonly HashSet<string> ValidItems = new HashSet<string>(DefaultOrderItems);
        private static readonly HashSet<string> ForceShowItems = new HashSet<string> { "速度" };
        private static readonly HashSet<string> DefaultVisibleItems = new HashSet<string> { "速度", "难度", "置信" };

        public static IReadOnlyList<string> DefaultOrder
        {
            get { return DefaultOrderItems; }
        }

        public static bool IsValidItem(string item)
        {
            return !string.IsNullOrWhiteSpace(item) && ValidItems.Contains(item);
        }

        public static bool IsForceShowItem(string item)
        {
            return !string.IsNullOrWhiteSpace(item) && ForceShowItems.Contains(item);
        }

        public static bool IsDefaultVisible(string item)
        {
            return !string.IsNullOrWhiteSpace(item) && DefaultVisibleItems.Contains(item);
        }

        public static List<string> NormalizeOrder(string raw)
        {
            var result = new List<string>();
            var seen = new HashSet<string>();

            foreach (string item in (raw ?? "").Split(','))
            {
                string trimmed = item.Trim();
                if (IsValidItem(trimmed) && seen.Add(trimmed))
                    result.Add(trimmed);
            }

            foreach (string item in DefaultOrderItems)
            {
                if (seen.Add(item))
                    result.Add(item);
            }

            return result;
        }

        public static bool CanAttachToScore(PersonalScorePrediction prediction)
        {
            return prediction != null
                && prediction.PredictedSpeed > 0
                && prediction.Confidence > ScoreAttachmentConfidenceThreshold;
        }

        public static string Format(
            PersonalScorePrediction prediction,
            IEnumerable<string> order,
            Func<string, bool> isVisible)
        {
            if (prediction == null || prediction.PredictedSpeed <= 0)
                return "";

            // 低置信完全不显示，调用方会回退到只显示基础难度
            if (prediction.Confidence < MinDisplayConfidence)
                return "";

            var parts = new List<string>();
            foreach (string item in NormalizeOrder(string.Join(",", order ?? DefaultOrderItems)))
            {
                bool visible = IsForceShowItem(item) || (isVisible != null ? isVisible(item) : true);
                if (!visible)
                    continue;

                string text = FormatItem(prediction, item);
                if (!string.IsNullOrEmpty(text))
                    parts.Add(text);
            }

            return string.Join(" ", parts);
        }

        private static string FormatItem(PersonalScorePrediction prediction, string item)
        {
            switch (item)
            {
                case "速度":
                    return "预测速度" + prediction.PredictedSpeed.ToString("0.00");
                case "难度":
                    return "个难" + QingDifficultyScale.Format(prediction.PersonalDifficultyScore);
                case "用时":
                    return "用时" + FormatSeconds(prediction.PredictedSeconds);
                case "击键":
                    return "击键" + prediction.PredictedHitRate.ToString("0.00");
                case "码长":
                    return "码长" + prediction.PredictedKpw.ToString("0.00");
                case "总键数":
                    return "总键数" + prediction.PredictedTotalHits.ToString("0");
                case "置信":
                    return "置信" + (prediction.Confidence * 100).ToString("0") + "%";
                default:
                    return "";
            }
        }

        private static string FormatSeconds(double seconds)
        {
            TimeSpan time = TimeSpan.FromSeconds(Math.Max(0, seconds));
            if (time.TotalHours >= 1)
                return time.ToString(@"h\:mm\:ss");
            return time.ToString(@"m\:ss");
        }
    }
}
