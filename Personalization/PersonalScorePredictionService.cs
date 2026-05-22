using System;
using System.Collections.Generic;

namespace TypeSunny.Personalization
{
    internal sealed class PersonalScorePredictionService
    {
        private readonly PersonalTypingProfileStore store;
        private readonly Func<string, double> baseDifficultyCalculator;
        private readonly Func<string, IEnumerable<string>> difficultySegmenter;

        public PersonalScorePredictionService(
            PersonalTypingProfileStore store,
            Func<string, double> baseDifficultyCalculator,
            Func<string, IEnumerable<string>> difficultySegmenter = null)
        {
            this.store = store ?? new PersonalTypingProfileStore();
            this.baseDifficultyCalculator = baseDifficultyCalculator ?? (_ => 0);
            this.difficultySegmenter = difficultySegmenter ?? (_ => new string[0]);
        }

        public void Train(
            string targetText,
            IEnumerable<string> commitTexts,
            IEnumerable<long> commitTimes,
            IEnumerable<long> keyTimes,
            PersonalTypingRoundStats stats)
        {
            try
            {
                PersonalTypingSession session = PersonalTypingSessionBuilder.Build(
                    targetText,
                    commitTexts,
                    commitTimes,
                    keyTimes,
                    difficultySegmenter(targetText ?? ""));

                if (session.EffectiveStatCharacters <= 0)
                    return;

                PersonalTypingProfile profile = store.Load();
                profile.Update(session, stats);
                store.Save(profile);
            }
            catch
            {
            }
        }

        public PersonalScorePredictionSnapshot CreateSnapshot(string text, string baseDifficultyText)
        {
            try
            {
                PersonalTypingProfile profile = store.Load();
                if (profile.EffectiveStatCharacters <= 0)
                    return new PersonalScorePredictionSnapshot();

                double baseScore = GetBaseDifficultyScore(text, baseDifficultyText);
                PersonalScorePrediction prediction = PersonalScorePredictor.Predict(
                    text,
                    profile,
                    baseScore,
                    difficultySegmenter(text ?? ""));
                return PersonalScorePredictionSnapshot.FromPrediction(text, baseScore, prediction);
            }
            catch
            {
                return new PersonalScorePredictionSnapshot();
            }
        }

        public void Calibrate(PersonalScorePredictionSnapshot snapshot, PersonalTypingRoundStats actual)
        {
            try
            {
                if (snapshot == null || actual == null || !snapshot.HasPrediction)
                    return;

                PersonalTypingProfile profile = store.Load();
                profile.UpdateCalibration(snapshot, actual);
                store.Save(profile);
            }
            catch
            {
            }
        }

        public string AppendPrediction(string text, string baseDifficultyText)
        {
            return AppendPrediction(text, baseDifficultyText, true, null, null);
        }

        public string AppendPrediction(
            string text,
            string baseDifficultyText,
            bool enabled,
            IEnumerable<string> order,
            Func<string, bool> isVisible)
        {
            try
            {
                if (!enabled)
                    return baseDifficultyText;

                PersonalScorePrediction prediction = Predict(text, baseDifficultyText);
                if (prediction.PredictedSpeed <= 0)
                    return baseDifficultyText;

                string prefix = string.IsNullOrWhiteSpace(baseDifficultyText)
                    ? ""
                    : baseDifficultyText.Trim() + " ";
                return prefix + PersonalScorePredictionFormatter.Format(prediction, order, isVisible);
            }
            catch
            {
                return baseDifficultyText;
            }
        }

        public string AppendPredictionSnapshot(
            string baseDifficultyText,
            PersonalScorePredictionSnapshot snapshot,
            bool enabled,
            IEnumerable<string> order,
            Func<string, bool> isVisible)
        {
            try
            {
                if (!enabled || snapshot == null || !snapshot.HasPrediction)
                    return baseDifficultyText;

                string predictionText = PersonalScorePredictionFormatter.Format(
                    snapshot.ToPrediction(),
                    order,
                    isVisible);
                if (string.IsNullOrWhiteSpace(predictionText))
                    return baseDifficultyText;

                string prefix = string.IsNullOrWhiteSpace(baseDifficultyText)
                    ? ""
                    : baseDifficultyText.Trim() + " ";
                return prefix + predictionText;
            }
            catch
            {
                return baseDifficultyText;
            }
        }

        public PersonalScorePrediction Predict(string text, string baseDifficultyText)
        {
            PersonalTypingProfile profile = store.Load();
            if (profile.EffectiveStatCharacters <= 0)
                return new PersonalScorePrediction();

            double baseScore = GetBaseDifficultyScore(text, baseDifficultyText);
            return PersonalScorePredictor.Predict(
                text,
                profile,
                baseScore,
                difficultySegmenter(text ?? ""));
        }

        private double GetBaseDifficultyScore(string text, string baseDifficultyText)
        {
            double baseScore = ExtractDifficultyScore(baseDifficultyText);
            if (baseScore <= 0)
                baseScore = Math.Max(0.01, baseDifficultyCalculator(text ?? ""));
            return baseScore;
        }

        internal static double ExtractDifficultyScore(string difficultyText)
        {
            if (string.IsNullOrWhiteSpace(difficultyText))
                return 0;

            int open = difficultyText.LastIndexOf('(');
            int close = difficultyText.LastIndexOf(')');
            if (open < 0 || close <= open)
                return 0;

            string raw = difficultyText.Substring(open + 1, close - open - 1);
            double value;
            if (double.TryParse(raw, out value))
                return value;
            return 0;
        }
    }
}
