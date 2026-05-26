using System;

namespace TypeSunny.UI
{
    internal static class ScorePanelLayoutPolicy
    {
        public const int DefaultResultRowLimit = 30;

        public static int CalculateRowsToRender(
            int loadedRowCount,
            int visibleTextLineCount,
            int nonResultLineCount)
        {
            if (loadedRowCount <= 0)
                return 0;

            int visibleResultRows = Math.Max(0, visibleTextLineCount - Math.Max(0, nonResultLineCount));
            int requestedRows = Math.Max(1, visibleResultRows + 1);
            int cappedRows = Math.Min(requestedRows, DefaultResultRowLimit);
            return Math.Min(loadedRowCount, cappedRows);
        }

        public static int CalculateRowsToDisplay(int loadedRowCount)
        {
            return Math.Max(0, loadedRowCount);
        }

        public static int CalculateLoadMoreRemainingCount(int totalRowCount, int loadedRowCount)
        {
            return Math.Max(0, totalRowCount - Math.Max(0, loadedRowCount));
        }

        public static int CalculateFirstVisibleResultRowIndex(
            int firstVisibleLineIndex,
            int nonResultLineCount,
            int loadedRowCount)
        {
            if (loadedRowCount <= 0)
                return 0;

            int rawResultIndex = Math.Max(0, firstVisibleLineIndex) - Math.Max(0, nonResultLineCount);
            int clampedResultIndex = Math.Max(0, rawResultIndex);
            return Math.Min(clampedResultIndex, loadedRowCount - 1);
        }

        public static int CalculateVisibleNonResultLineCount(
            int firstVisibleLineIndex,
            int nonResultLineCount)
        {
            int safeNonResultLineCount = Math.Max(0, nonResultLineCount);
            int safeFirstVisibleLineIndex = Math.Max(0, firstVisibleLineIndex);
            return Math.Max(0, safeNonResultLineCount - safeFirstVisibleLineIndex);
        }

        public static int CalculateVisibleTextLineCount(double actualHeight, double fontSize)
        {
            if (actualHeight <= 0)
                return DefaultResultRowLimit;

            double lineHeight = GetEstimatedLineHeight(fontSize);
            if (lineHeight <= 0)
                return DefaultResultRowLimit;

            return Math.Max(1, (int)Math.Floor(actualHeight / lineHeight));
        }

        private static double GetEstimatedLineHeight(double fontSize)
        {
            double safeFontSize = fontSize > 0 ? fontSize : 15.0;
            return safeFontSize * 1.35;
        }
    }
}
