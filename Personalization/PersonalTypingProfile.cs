using System;
using System.Collections.Generic;
using System.Globalization;

namespace TypeSunny.Personalization
{
    internal sealed class PersonalTypingRoundStats
    {
        public int TotalWords { get; set; }
        public double TotalSeconds { get; set; }
        public int TotalHits { get; set; }
        public double Speed { get; set; }
        public double HitRate { get; set; }
        public double Kpw { get; set; }
        public double Accuracy { get; set; }
        public int Backs { get; set; }
        public double Correction { get; set; }
        public int WasteCodes { get; set; }
        public int Choose { get; set; }
    }

    internal sealed class PersonalTypingUnitSample
    {
        public PersonalTypingUnitSample(string text, double effectiveMilliseconds, double keyCount)
        {
            Text = text ?? "";
            EffectiveMilliseconds = Math.Max(0, effectiveMilliseconds);
            KeyCount = Math.Max(0, keyCount);
            CharacterCount = new StringInfo(Text).LengthInTextElements;
        }

        public string Text { get; private set; }
        public double EffectiveMilliseconds { get; private set; }
        public double KeyCount { get; private set; }
        public int CharacterCount { get; private set; }
    }

    internal sealed class PersonalTypingSession
    {
        public int EffectiveStatCharacters { get; set; }
        public double EffectiveMilliseconds { get; set; }
        public List<PersonalTypingUnitSample> Samples { get; private set; }

        public PersonalTypingSession()
        {
            Samples = new List<PersonalTypingUnitSample>();
        }
    }

    internal sealed class PersonalTypingUnitStats
    {
        private const double LongTermSampleDecay = 0.98;
        private const double RecentSampleDecay = 0.90;
        private const double LongTermPredictionWeight = 0.65;
        private const double RecentPredictionWeight = 0.35;

        public PersonalTypingUnitStats()
        {
        }

        public PersonalTypingUnitStats(string text)
        {
            Text = text ?? "";
        }

        public string Text { get; set; }
        public int Count { get; set; }
        public int ObservedCharacters { get; set; }
        public double TotalMilliseconds { get; set; }
        public double TotalKeys { get; set; }
        public double LongTermWeightedMilliseconds { get; set; }
        public double LongTermWeightedKeys { get; set; }
        public double LongTermWeight { get; set; }
        public double RecencyWeightedMilliseconds { get; set; }
        public double RecencyWeightedKeys { get; set; }
        public double RecencyWeight { get; set; }

        public double AverageMilliseconds
        {
            get
            {
                double longTerm = LongTermAverageMilliseconds;
                double recent = RecentAverageMilliseconds;
                if (longTerm > 0 && recent > 0)
                    return longTerm * LongTermPredictionWeight + recent * RecentPredictionWeight;
                if (longTerm > 0)
                    return longTerm;
                if (recent > 0)
                    return recent;
                return Count > 0 ? TotalMilliseconds / Count : 0;
            }
        }

        public double AverageKeys
        {
            get
            {
                double longTerm = LongTermAverageKeys;
                double recent = RecentAverageKeys;
                if (longTerm > 0 && recent > 0)
                    return longTerm * LongTermPredictionWeight + recent * RecentPredictionWeight;
                if (longTerm > 0)
                    return longTerm;
                if (recent > 0)
                    return recent;
                return Count > 0 ? TotalKeys / Count : 0;
            }
        }

        public double LongTermAverageMilliseconds
        {
            get
            {
                if (LongTermWeight > 0)
                    return LongTermWeightedMilliseconds / LongTermWeight;
                return Count > 0 ? TotalMilliseconds / Count : 0;
            }
        }

        public double RecentAverageMilliseconds
        {
            get
            {
                if (RecencyWeight > 0)
                    return RecencyWeightedMilliseconds / RecencyWeight;
                return Count > 0 ? TotalMilliseconds / Count : 0;
            }
        }

        public double LongTermAverageKeys
        {
            get
            {
                if (LongTermWeight > 0)
                    return LongTermWeightedKeys / LongTermWeight;
                return Count > 0 ? TotalKeys / Count : 0;
            }
        }

        public double RecentAverageKeys
        {
            get
            {
                if (RecencyWeight > 0)
                    return RecencyWeightedKeys / RecencyWeight;
                return Count > 0 ? TotalKeys / Count : 0;
            }
        }

        public double MillisecondsPerCharacter
        {
            get { return ObservedCharacters > 0 ? TotalMilliseconds / ObservedCharacters : 0; }
        }

        public void Add(PersonalTypingUnitSample sample)
        {
            if (sample == null)
                return;

            if (string.IsNullOrEmpty(Text))
                Text = sample.Text;

            Count++;
            ObservedCharacters += sample.CharacterCount;
            TotalMilliseconds += sample.EffectiveMilliseconds;
            TotalKeys += sample.KeyCount;
            LongTermWeightedMilliseconds = LongTermWeightedMilliseconds * LongTermSampleDecay + sample.EffectiveMilliseconds;
            LongTermWeightedKeys = LongTermWeightedKeys * LongTermSampleDecay + sample.KeyCount;
            LongTermWeight = LongTermWeight * LongTermSampleDecay + 1;
            RecencyWeightedMilliseconds = RecencyWeightedMilliseconds * RecentSampleDecay + sample.EffectiveMilliseconds;
            RecencyWeightedKeys = RecencyWeightedKeys * RecentSampleDecay + sample.KeyCount;
            RecencyWeight = RecencyWeight * RecentSampleDecay + 1;
        }

    }

    internal sealed class PersonalTypingProfile
    {
        public int EffectiveStatCharacters { get; set; }
        public double BaselineSpeed { get; set; }
        public double BaselineHitRate { get; set; }
        public double BaselineKpw { get; set; }
        public double BaselineAccuracy { get; set; }
        public double BaselineBacksPerChar { get; set; }
        public double BaselineCorrectionPerChar { get; set; }
        public double BaselineWasteCodesPerChar { get; set; }
        public double BaselineChoosePerChar { get; set; }
        public PersonalPredictionCalibration Calibration { get; set; }
        public Dictionary<string, PersonalTypingUnitStats> Units { get; set; }

        public PersonalTypingProfile()
        {
            BaselineSpeed = 120;
            BaselineHitRate = 5;
            BaselineKpw = 4;
            BaselineAccuracy = 98;
            Calibration = new PersonalPredictionCalibration();
            Units = new Dictionary<string, PersonalTypingUnitStats>();
        }

        public void Update(PersonalTypingSession session, PersonalTypingRoundStats stats)
        {
            if (session == null || stats == null || session.EffectiveStatCharacters <= 0)
                return;

            EnsureCollections();

            int oldChars = EffectiveStatCharacters;
            int newChars = session.EffectiveStatCharacters;
            int totalChars = oldChars + newChars;

            BaselineSpeed = WeightedAverage(BaselineSpeed, oldChars, stats.Speed, newChars);
            BaselineHitRate = WeightedAverage(BaselineHitRate, oldChars, stats.HitRate, newChars);
            BaselineKpw = WeightedAverage(BaselineKpw, oldChars, stats.Kpw, newChars);
            BaselineAccuracy = WeightedAverage(BaselineAccuracy, oldChars, stats.Accuracy, newChars);
            BaselineBacksPerChar = WeightedAverage(BaselineBacksPerChar, oldChars, SafePerChar(stats.Backs, stats.TotalWords), newChars);
            BaselineCorrectionPerChar = WeightedAverage(BaselineCorrectionPerChar, oldChars, SafePerChar(stats.Correction, stats.TotalWords), newChars);
            BaselineWasteCodesPerChar = WeightedAverage(BaselineWasteCodesPerChar, oldChars, SafePerChar(stats.WasteCodes, stats.TotalWords), newChars);
            BaselineChoosePerChar = WeightedAverage(BaselineChoosePerChar, oldChars, SafePerChar(stats.Choose, stats.TotalWords), newChars);
            EffectiveStatCharacters = totalChars;

            foreach (PersonalTypingUnitSample sample in session.Samples)
            {
                if (string.IsNullOrEmpty(sample.Text) || sample.CharacterCount <= 0 || sample.CharacterCount > 4)
                    continue;

                PersonalTypingUnitStats unit;
                if (!Units.TryGetValue(sample.Text, out unit))
                {
                    unit = new PersonalTypingUnitStats(sample.Text);
                    Units[sample.Text] = unit;
                }
                unit.Add(sample);
            }
        }

        public void UpdateCalibration(PersonalScorePredictionSnapshot snapshot, PersonalTypingRoundStats actual)
        {
            if (snapshot == null || actual == null || !snapshot.HasPrediction)
                return;

            EnsureCollections();
            Calibration.Add(snapshot, actual);
        }

        private void EnsureCollections()
        {
            if (Units == null)
                Units = new Dictionary<string, PersonalTypingUnitStats>();
            if (Calibration == null)
                Calibration = new PersonalPredictionCalibration();

            RemoveInvalidUnits();
        }

        private void RemoveInvalidUnits()
        {
            if (Units == null || Units.Count == 0)
                return;

            var invalidKeys = new List<string>();
            foreach (string key in Units.Keys)
            {
                if (!IsValidPersonalUnit(key))
                    invalidKeys.Add(key);
            }

            foreach (string key in invalidKeys)
                Units.Remove(key);
        }

        private static bool IsValidPersonalUnit(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            var info = new StringInfo(text);
            int length = info.LengthInTextElements;
            if (length <= 0 || length > 4)
                return false;

            for (int i = 0; i < length; i++)
            {
                string element = info.SubstringByTextElements(i, 1);
                if (element.Length != 1 || !IsCjkUnifiedIdeograph(element[0]))
                    return false;
            }

            return true;
        }

        private static bool IsCjkUnifiedIdeograph(char ch)
        {
            return (ch >= '\u3400' && ch <= '\u9FFF') || (ch >= '\uF900' && ch <= '\uFAFF');
        }

        private static double WeightedAverage(double oldValue, int oldWeight, double newValue, int newWeight)
        {
            if (newWeight <= 0)
                return oldValue;
            if (oldWeight <= 0)
                return newValue;
            return (oldValue * oldWeight + newValue * newWeight) / (oldWeight + newWeight);
        }

        private static double SafePerChar(double value, int chars)
        {
            return chars > 0 ? value / chars : 0;
        }
    }
}
