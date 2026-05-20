using System;
using System.Collections.Generic;
using TypeSunny.UI.Modes;

namespace TypeSunny.Tests
{
    internal static class CompositionCodeFeedbackTests
    {
        private static int _failures;

        private static int Main()
        {
            Run("matching prefix is marked matched", MatchingPrefixIsMarkedMatched);
            Run("first mismatch and following glyphs are marked mismatched", FirstMismatchAndFollowingGlyphsAreMarkedMismatched);
            Run("missing target code uses neutral state", MissingTargetCodeUsesNeutralState);
            Run("matching is case insensitive", MatchingIsCaseInsensitive);
            Run("lower code label shows matched prefix and neutral tail", LowerCodeLabelShowsMatchedPrefixAndNeutralTail);
            Run("lower code label refreshes after delete", LowerCodeLabelRefreshesAfterDelete);
            Run("lower code label marks first mismatch and typed suffix", LowerCodeLabelMarksMismatchAndTypedSuffix);
            Run("lower code label marks extra input as mismatch", LowerCodeLabelMarksExtraInputAsMismatch);
            Run("committed correct label remains fully matched", CommittedCorrectLabelRemainsFullyMatched);

            if (_failures == 0)
            {
                Console.WriteLine("All CompositionCodeFeedback tests passed.");
                return 0;
            }

            Console.WriteLine(_failures + " CompositionCodeFeedback test(s) failed.");
            return 1;
        }

        private static void MatchingPrefixIsMarkedMatched()
        {
            var glyphs = CompositionCodeFeedback.BuildGlyphs("ab", "abcd");

            AssertGlyphs(glyphs,
                Glyph('a', CompositionCodeGlyphState.Matched),
                Glyph('b', CompositionCodeGlyphState.Matched));
        }

        private static void FirstMismatchAndFollowingGlyphsAreMarkedMismatched()
        {
            var glyphs = CompositionCodeFeedback.BuildGlyphs("axd", "abcd");

            AssertGlyphs(glyphs,
                Glyph('a', CompositionCodeGlyphState.Matched),
                Glyph('x', CompositionCodeGlyphState.Mismatched),
                Glyph('d', CompositionCodeGlyphState.Mismatched));
        }

        private static void MissingTargetCodeUsesNeutralState()
        {
            var glyphs = CompositionCodeFeedback.BuildGlyphs("ab", "");

            AssertGlyphs(glyphs,
                Glyph('a', CompositionCodeGlyphState.Neutral),
                Glyph('b', CompositionCodeGlyphState.Neutral));
        }

        private static void MatchingIsCaseInsensitive()
        {
            var glyphs = CompositionCodeFeedback.BuildGlyphs("AB", "ab");

            AssertGlyphs(glyphs,
                Glyph('A', CompositionCodeGlyphState.Matched),
                Glyph('B', CompositionCodeGlyphState.Matched));
        }

        private static void LowerCodeLabelShowsMatchedPrefixAndNeutralTail()
        {
            var glyphs = CompositionCodeFeedback.BuildLabelGlyphs("abcd", "ab");

            AssertGlyphs(glyphs,
                Glyph('a', CompositionCodeGlyphState.Matched),
                Glyph('b', CompositionCodeGlyphState.Matched),
                Glyph('c', CompositionCodeGlyphState.Neutral),
                Glyph('d', CompositionCodeGlyphState.Neutral));
        }

        private static void LowerCodeLabelRefreshesAfterDelete()
        {
            var glyphs = CompositionCodeFeedback.BuildLabelGlyphs("abcd", "a");

            AssertGlyphs(glyphs,
                Glyph('a', CompositionCodeGlyphState.Matched),
                Glyph('b', CompositionCodeGlyphState.Neutral),
                Glyph('c', CompositionCodeGlyphState.Neutral),
                Glyph('d', CompositionCodeGlyphState.Neutral));
        }

        private static void LowerCodeLabelMarksMismatchAndTypedSuffix()
        {
            var glyphs = CompositionCodeFeedback.BuildLabelGlyphs("abcd", "axd");

            AssertGlyphs(glyphs,
                Glyph('a', CompositionCodeGlyphState.Matched),
                Glyph('b', CompositionCodeGlyphState.Mismatched),
                Glyph('c', CompositionCodeGlyphState.Mismatched),
                Glyph('d', CompositionCodeGlyphState.Neutral));
        }

        private static void LowerCodeLabelMarksExtraInputAsMismatch()
        {
            var glyphs = CompositionCodeFeedback.BuildLabelGlyphs("ab", "abc");

            AssertGlyphs(glyphs,
                Glyph('a', CompositionCodeGlyphState.Matched),
                Glyph('b', CompositionCodeGlyphState.Mismatched));
        }

        private static void CommittedCorrectLabelRemainsFullyMatched()
        {
            string finalTypedText = CompositionCodeFeedback.GetCommittedLabelText("ab", "abc", isCorrect: true);
            var glyphs = CompositionCodeFeedback.BuildLabelGlyphs("ab", finalTypedText);

            AssertGlyphs(glyphs,
                Glyph('a', CompositionCodeGlyphState.Matched),
                Glyph('b', CompositionCodeGlyphState.Matched));
        }

        private static CompositionCodeGlyph Glyph(char value, CompositionCodeGlyphState state)
        {
            return new CompositionCodeGlyph(value, state);
        }

        private static void AssertGlyphs(
            IReadOnlyList<CompositionCodeGlyph> actual,
            params CompositionCodeGlyph[] expected)
        {
            AssertEqual(expected.Length, actual.Count);
            for (int i = 0; i < expected.Length; i++)
            {
                AssertEqual(expected[i].Value, actual[i].Value);
                AssertEqual(expected[i].State, actual[i].State);
            }
        }

        private static void Run(string name, Action test)
        {
            try
            {
                test();
                Console.WriteLine("PASS: " + name);
            }
            catch (Exception ex)
            {
                _failures++;
                Console.WriteLine("FAIL: " + name);
                Console.WriteLine(ex.Message);
            }
        }

        private static void AssertEqual<T>(T expected, T actual)
        {
            if (!object.Equals(expected, actual))
                throw new Exception("Expected " + expected + ", got " + actual + ".");
        }
    }
}
