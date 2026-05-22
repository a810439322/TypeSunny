using System;

namespace TypeSunny.Personalization
{
    internal static class QingDifficultyScale
    {
        public static string GetLabel(double score)
        {
            if (score < 0.1)
                return "淼";
            if (score < 0.3)
                return "水";
            if (score < 0.8)
                return "易";
            if (score < 5.0)
                return "普";
            if (score < 15.0)
                return "难";
            return "虐";
        }

        public static string Format(double score)
        {
            double rounded = Math.Round(score, 2);
            return GetLabel(rounded) + "(" + rounded.ToString("0.00") + ")";
        }
    }
}
