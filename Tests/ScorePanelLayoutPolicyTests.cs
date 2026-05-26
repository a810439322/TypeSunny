using System;
using TypeSunny.UI;

namespace TypeSunny.Tests
{
    internal static class ScorePanelLayoutPolicyTests
    {
        private static int Main()
        {
            try
            {
                UsesVisibleScoreRowsPlusOneInsteadOfLoadedThirtyRows();
                KeepsAtLeastOneResultRowWhenHeaderConsumesVisibleSpace();
                DoesNotExceedLoadedRowsOrDefaultLimit();
                AlignmentWindowDoesNotLimitRenderedRows();
                LoadMoreCountIgnoresRenderedRowsAndUsesLoadedRows();
                CalculatesAlignmentWindowStartFromFirstVisibleLine();
                ClampsAlignmentWindowStartToLoadedRows();
                DoesNotSubtractHeaderLinesAfterScrollingPastHeader();

                Console.WriteLine("All ScorePanelLayoutPolicy tests passed.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        private static void UsesVisibleScoreRowsPlusOneInsteadOfLoadedThirtyRows()
        {
            int rowsToRender = ScorePanelLayoutPolicy.CalculateRowsToRender(
                loadedRowCount: 30,
                visibleTextLineCount: 7,
                nonResultLineCount: 2);

            AssertEqual("visible score rows plus one", 6, rowsToRender);
        }

        private static void KeepsAtLeastOneResultRowWhenHeaderConsumesVisibleSpace()
        {
            int rowsToRender = ScorePanelLayoutPolicy.CalculateRowsToRender(
                loadedRowCount: 30,
                visibleTextLineCount: 1,
                nonResultLineCount: 2);

            AssertEqual("minimum result rows", 1, rowsToRender);
        }

        private static void DoesNotExceedLoadedRowsOrDefaultLimit()
        {
            AssertEqual(
                "loaded row cap",
                4,
                ScorePanelLayoutPolicy.CalculateRowsToRender(4, 20, 1));

            AssertEqual(
                "default row cap",
                ScorePanelLayoutPolicy.DefaultResultRowLimit,
                ScorePanelLayoutPolicy.CalculateRowsToRender(100, 100, 1));
        }

        private static void AlignmentWindowDoesNotLimitRenderedRows()
        {
            int rowsForAlignment = ScorePanelLayoutPolicy.CalculateRowsToRender(
                loadedRowCount: 30,
                visibleTextLineCount: 7,
                nonResultLineCount: 2);

            int renderedRows = ScorePanelLayoutPolicy.CalculateRowsToDisplay(
                loadedRowCount: 30);

            AssertEqual("alignment window rows", 6, rowsForAlignment);
            AssertEqual("rendered rows keep loaded rows", 30, renderedRows);
        }

        private static void LoadMoreCountIgnoresRenderedRowsAndUsesLoadedRows()
        {
            int remaining = ScorePanelLayoutPolicy.CalculateLoadMoreRemainingCount(
                totalRowCount: 80,
                loadedRowCount: 30);

            AssertEqual("load more remaining", 50, remaining);
        }

        private static void CalculatesAlignmentWindowStartFromFirstVisibleLine()
        {
            AssertEqual(
                "header line stays on first result",
                0,
                ScorePanelLayoutPolicy.CalculateFirstVisibleResultRowIndex(
                    firstVisibleLineIndex: 1,
                    nonResultLineCount: 2,
                    loadedRowCount: 30));

            AssertEqual(
                "visible result offset",
                3,
                ScorePanelLayoutPolicy.CalculateFirstVisibleResultRowIndex(
                    firstVisibleLineIndex: 5,
                    nonResultLineCount: 2,
                    loadedRowCount: 30));
        }

        private static void ClampsAlignmentWindowStartToLoadedRows()
        {
            AssertEqual(
                "negative line clamps to first result",
                0,
                ScorePanelLayoutPolicy.CalculateFirstVisibleResultRowIndex(
                    firstVisibleLineIndex: -4,
                    nonResultLineCount: 2,
                    loadedRowCount: 30));

            AssertEqual(
                "past loaded rows clamps to last result",
                4,
                ScorePanelLayoutPolicy.CalculateFirstVisibleResultRowIndex(
                    firstVisibleLineIndex: 50,
                    nonResultLineCount: 2,
                    loadedRowCount: 5));

            AssertEqual(
                "empty loaded rows has no visible result",
                0,
                ScorePanelLayoutPolicy.CalculateFirstVisibleResultRowIndex(
                    firstVisibleLineIndex: 50,
                    nonResultLineCount: 2,
                    loadedRowCount: 0));
        }

        private static void DoesNotSubtractHeaderLinesAfterScrollingPastHeader()
        {
            int visibleLineCount = 7;
            int nonResultLineCount = 2;
            int firstVisibleLineIndex = 5;
            int firstVisibleResultRowIndex = ScorePanelLayoutPolicy.CalculateFirstVisibleResultRowIndex(
                firstVisibleLineIndex,
                nonResultLineCount,
                loadedRowCount: 30);

            int visibleNonResultLines = ScorePanelLayoutPolicy.CalculateVisibleNonResultLineCount(
                firstVisibleLineIndex,
                nonResultLineCount);

            int rowsToRender = ScorePanelLayoutPolicy.CalculateRowsToRender(
                loadedRowCount: 30 - firstVisibleResultRowIndex,
                visibleTextLineCount: visibleLineCount,
                nonResultLineCount: visibleNonResultLines);

            AssertEqual("scrolled visible non-result lines", 0, visibleNonResultLines);
            AssertEqual("visible result rows plus one after scrolling", 8, rowsToRender);
        }

        private static void AssertEqual(string name, int expected, int actual)
        {
            if (expected != actual)
                throw new Exception(name + " expected " + expected + ", got " + actual + ".");
        }
    }
}
