using System;
using System.Collections.Generic;
using System.Globalization;

namespace TypeSunny.Personalization
{
    internal sealed class PersonalScorePredictionSnapshot
    {
        public PersonalScorePredictionSnapshot()
        {
            Units = new List<string>();
            CreatedAt = DateTime.Now;
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

        public void Add(PersonalScorePredictionSnapshot snapshot, PersonalTypingRoundStats actual)
        {
            if (snapshot == null || actual == null || !snapshot.HasPrediction)
                return;

            double actualSeconds = actual.TotalSeconds;
            double actualHits = actual.TotalHits;
            if (actualSeconds <= 0 || actualHits <= 0)
                return;

            double timeRatio = ClampRatio(actualSeconds / snapshot.PredictedSeconds);
            double keyRatio = ClampRatio(actualHits / snapshot.PredictedTotalHits);

            LongTermTimeRatio = LongTermTimeRatio * LongTermSampleDecay + timeRatio;
            LongTermTimeWeight = LongTermTimeWeight * LongTermSampleDecay + 1;
            RecentTimeRatio = RecentTimeRatio * RecentSampleDecay + timeRatio;
            RecentTimeWeight = RecentTimeWeight * RecentSampleDecay + 1;

            LongTermKeyRatio = LongTermKeyRatio * LongTermSampleDecay + keyRatio;
            LongTermKeyWeight = LongTermKeyWeight * LongTermSampleDecay + 1;
            RecentKeyRatio = RecentKeyRatio * RecentSampleDecay + keyRatio;
            RecentKeyWeight = RecentKeyWeight * RecentSampleDecay + 1;

            Count++;
            ObservedCharacters += snapshot.TargetCharacters;
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

        private static double ClampRatio(double ratio)
        {
            if (double.IsNaN(ratio) || double.IsInfinity(ratio))
                return 1;
            return Math.Max(0.25, Math.Min(4.0, ratio));
        }
    }
}
