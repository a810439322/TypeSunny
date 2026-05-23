using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace TypeSunny.Personalization
{
    internal sealed class PersonalScorePredictionSnapshot
    {
        public PersonalScorePredictionSnapshot()
        {
            Units = new List<string>();
            CreatedAt = DateTime.Now;
            TargetTextHash = "";
        }

        public DateTime CreatedAt { get; set; }
        public int TargetCharacters { get; set; }
        public double BaseDifficultyScore { get; set; }
        public double PredictedSeconds { get; set; }
        public double PredictedTotalHits { get; set; }
        public double PredictedSpeed { get; set; }
        public double PredictedHitRate { get; set; }
        public double PredictedKpw { get; set; }
        public double PredictedPersonalDifficultyScore { get; set; }
        public double Confidence { get; set; }
        public List<string> Units { get; set; }

        /// <summary>
        /// 拍照时目标文本的 SHA1 前 8 字节 hex（16 个字符）。
        /// 用于在 Calibrate 时校验 snapshot 与 actual 对应文本一致——避免文本/难度面板刷新后
        /// snapshot 被覆盖，造成 A 文 snapshot 与 B 文 actual 错配污染校准。
        /// </summary>
        public string TargetTextHash { get; set; }

        public bool HasPrediction
        {
            get
            {
                return TargetCharacters > 0
                    && PredictedSeconds > 0
                    && PredictedTotalHits > 0;
            }
        }

        public static PersonalScorePredictionSnapshot FromPrediction(
            string text,
            double baseDifficultyScore,
            PersonalScorePrediction prediction)
        {
            var snapshot = new PersonalScorePredictionSnapshot();
            snapshot.TargetCharacters = CountTextElements(text);
            snapshot.BaseDifficultyScore = Math.Max(0, baseDifficultyScore);
            snapshot.TargetTextHash = ComputeTextHash(text);

            if (prediction == null)
                return snapshot;

            snapshot.PredictedSeconds = Math.Max(0, prediction.PredictedSeconds);
            snapshot.PredictedTotalHits = Math.Max(0, prediction.PredictedTotalHits);
            snapshot.PredictedSpeed = Math.Max(0, prediction.PredictedSpeed);
            snapshot.PredictedHitRate = Math.Max(0, prediction.PredictedHitRate);
            snapshot.PredictedKpw = Math.Max(0, prediction.PredictedKpw);
            snapshot.PredictedPersonalDifficultyScore = Math.Max(0, prediction.PersonalDifficultyScore);
            snapshot.Confidence = Math.Max(0, Math.Min(1, prediction.Confidence));
            snapshot.Units = prediction.Units == null
                ? new List<string>()
                : new List<string>(prediction.Units);
            return snapshot;
        }

        public PersonalScorePrediction ToPrediction()
        {
            return new PersonalScorePrediction
            {
                Units = Units == null ? new List<string>() : new List<string>(Units),
                PredictedSeconds = PredictedSeconds,
                PredictedTotalHits = PredictedTotalHits,
                PredictedSpeed = PredictedSpeed,
                PredictedHitRate = PredictedHitRate,
                PredictedKpw = PredictedKpw,
                PersonalDifficultyScore = PredictedPersonalDifficultyScore,
                Confidence = Confidence
            };
        }

        /// <summary>
        /// 浅克隆所有数值字段 + 深拷贝 Units 列表。
        /// 用于把"显示用 snapshot"快照成"校准用 snapshot"，让后续的显示刷新不影响校准副本。
        /// </summary>
        public PersonalScorePredictionSnapshot Clone()
        {
            return new PersonalScorePredictionSnapshot
            {
                CreatedAt = CreatedAt,
                TargetCharacters = TargetCharacters,
                BaseDifficultyScore = BaseDifficultyScore,
                PredictedSeconds = PredictedSeconds,
                PredictedTotalHits = PredictedTotalHits,
                PredictedSpeed = PredictedSpeed,
                PredictedHitRate = PredictedHitRate,
                PredictedKpw = PredictedKpw,
                PredictedPersonalDifficultyScore = PredictedPersonalDifficultyScore,
                Confidence = Confidence,
                Units = Units == null ? new List<string>() : new List<string>(Units),
                TargetTextHash = TargetTextHash
            };
        }

        /// <summary>
        /// 计算文本的标识 hash（SHA1 前 8 字节 hex）。
        /// 用于 snapshot 与 actual 配对校验；冲撞概率 2^-64 远低于错配风险。
        /// </summary>
        public static string ComputeTextHash(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";

            using (var sha = SHA1.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
                var sb = new StringBuilder(16);
                for (int i = 0; i < 8; i++)
                    sb.Append(bytes[i].ToString("x2"));
                return sb.ToString();
            }
        }

        private static int CountTextElements(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            return new StringInfo(text).LengthInTextElements;
        }
    }

    internal sealed class PersonalPredictionCalibration
    {
        private const double LongTermSampleDecay = 0.98;
        private const double RecentSampleDecay = 0.90;
        private const double LongTermPredictionWeight = 0.65;
        private const double RecentPredictionWeight = 0.35;

        // ClampRatio 钳过的样本被认为是"边界外"，只贡献 1/4 权重，让真实分布能逐步逃出钳的天花板/地板
        private const double ClampedSampleWeightFactor = 0.25;
        private const double MinRatio = 0.25;
        private const double MaxRatio = 4.0;

        public int Count { get; set; }
        public int ObservedCharacters { get; set; }
        public double LongTermTimeRatio { get; set; }
        public double LongTermTimeWeight { get; set; }
        public double RecentTimeRatio { get; set; }
        public double RecentTimeWeight { get; set; }
        public double LongTermKeyRatio { get; set; }
        public double LongTermKeyWeight { get; set; }
        public double RecentKeyRatio { get; set; }
        public double RecentKeyWeight { get; set; }

        public double TimeFactor
        {
            get { return GetBlendedRatio(LongTermTimeRatio, LongTermTimeWeight, RecentTimeRatio, RecentTimeWeight); }
        }

        public double KeyFactor
        {
            get { return GetBlendedRatio(LongTermKeyRatio, LongTermKeyWeight, RecentKeyRatio, RecentKeyWeight); }
        }

        /// <summary>
        /// 累积一轮校准样本。
        ///
        /// 权重按 <paramref name="effectiveChars"/>（本轮实际跟打的有效字符数）线性加权——
        /// 短局少加权，长局多加权。同时把 ratio 钳过的样本权重再打 1/4 折扣，避免极端样本
        /// 把因子焊死在 0.25/4.0 上。
        /// </summary>
        public void Add(PersonalScorePredictionSnapshot snapshot, PersonalTypingRoundStats actual, int effectiveChars)
        {
            if (snapshot == null || actual == null || !snapshot.HasPrediction)
                return;

            double actualSeconds = actual.TotalSeconds;
            double actualHits = actual.TotalHits;
            if (actualSeconds <= 0 || actualHits <= 0)
                return;
            if (effectiveChars <= 0)
                return;

            bool timeClamped;
            double timeRatio = ClampRatio(actualSeconds / snapshot.PredictedSeconds, out timeClamped);
            bool keyClamped;
            double keyRatio = ClampRatio(actualHits / snapshot.PredictedTotalHits, out keyClamped);

            double timeWeight = effectiveChars * (timeClamped ? ClampedSampleWeightFactor : 1.0);
            double keyWeight = effectiveChars * (keyClamped ? ClampedSampleWeightFactor : 1.0);

            LongTermTimeRatio = LongTermTimeRatio * LongTermSampleDecay + timeRatio * timeWeight;
            LongTermTimeWeight = LongTermTimeWeight * LongTermSampleDecay + timeWeight;
            RecentTimeRatio = RecentTimeRatio * RecentSampleDecay + timeRatio * timeWeight;
            RecentTimeWeight = RecentTimeWeight * RecentSampleDecay + timeWeight;

            LongTermKeyRatio = LongTermKeyRatio * LongTermSampleDecay + keyRatio * keyWeight;
            LongTermKeyWeight = LongTermKeyWeight * LongTermSampleDecay + keyWeight;
            RecentKeyRatio = RecentKeyRatio * RecentSampleDecay + keyRatio * keyWeight;
            RecentKeyWeight = RecentKeyWeight * RecentSampleDecay + keyWeight;

            Count++;
            ObservedCharacters += effectiveChars;
        }

        public double ApplySeconds(double seconds)
        {
            return Math.Max(0, seconds) * TimeFactor;
        }

        public double ApplyKeys(double keys)
        {
            return Math.Max(0, keys) * KeyFactor;
        }

        private static double GetBlendedRatio(
            double longTermRatio,
            double longTermWeight,
            double recentRatio,
            double recentWeight)
        {
            double longTerm = longTermWeight > 0 ? longTermRatio / longTermWeight : 0;
            double recent = recentWeight > 0 ? recentRatio / recentWeight : 0;

            if (longTerm > 0 && recent > 0)
                return longTerm * LongTermPredictionWeight + recent * RecentPredictionWeight;
            if (longTerm > 0)
                return longTerm;
            if (recent > 0)
                return recent;
            return 1;
        }

        private static double ClampRatio(double ratio, out bool clamped)
        {
            clamped = false;
            if (double.IsNaN(ratio) || double.IsInfinity(ratio))
            {
                clamped = true;
                return 1;
            }
            if (ratio < MinRatio)
            {
                clamped = true;
                return MinRatio;
            }
            if (ratio > MaxRatio)
            {
                clamped = true;
                return MaxRatio;
            }
            return ratio;
        }
    }
}
