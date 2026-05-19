using System;
using System.Collections.Generic;

namespace TypeSunny.UI.Modes
{
    internal enum CompositionCodeGlyphState
    {
        Neutral,
        Matched,
        Mismatched
    }

    internal readonly struct CompositionCodeGlyph
    {
        public CompositionCodeGlyph(char value, CompositionCodeGlyphState state)
        {
            Value = value;
            State = state;
        }

        public char Value { get; }

        public CompositionCodeGlyphState State { get; }
    }

    internal static class CompositionCodeFeedback
    {
        public static IReadOnlyList<CompositionCodeGlyph> BuildGlyphs(string composition, string expectedCode)
        {
            var glyphs = new List<CompositionCodeGlyph>();
            if (string.IsNullOrEmpty(composition))
                return glyphs;

            bool hasTarget = !string.IsNullOrEmpty(expectedCode);
            bool hasMismatch = false;
            for (int i = 0; i < composition.Length; i++)
            {
                char value = composition[i];
                CompositionCodeGlyphState state;
                if (!hasTarget)
                {
                    state = CompositionCodeGlyphState.Neutral;
                }
                else if (!hasMismatch
                         && i < expectedCode.Length
                         && char.ToUpperInvariant(value) == char.ToUpperInvariant(expectedCode[i]))
                {
                    state = CompositionCodeGlyphState.Matched;
                }
                else
                {
                    hasMismatch = true;
                    state = CompositionCodeGlyphState.Mismatched;
                }

                glyphs.Add(new CompositionCodeGlyph(value, state));
            }

            return glyphs;
        }
    }
}
