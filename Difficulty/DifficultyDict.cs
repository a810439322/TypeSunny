using TypeSunny.Personalization;
using System.Collections.Generic;

namespace TypeSunny.Difficulty
{
    public class DifficultyDict
    {
        private readonly QingDifficultyScorer scorer;

        public DifficultyDict()
            : this(null)
        {
        }

        public DifficultyDict(string vocabPath)
        {
            scorer = new QingDifficultyScorer(vocabPath);
        }

        public double Calc(string text)
        {
            return scorer.Compute(text).Score;
        }

        public string DiffText(double diff)
        {
            if (diff < 0)
                return "";

            return QingDifficultyScale.Format(diff);
        }

        public string CalcText(string text)
        {
            return DiffText(Calc(text));
        }

        public IReadOnlyList<string> SegmentText(string text)
        {
            return scorer.SegmentText(text);
        }
    }
}
