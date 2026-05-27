using System;
using System.Collections.Generic;
using System.Globalization;

namespace TypeSunny
{
    internal static class TrainerActualWordCounter
    {
        public static int CountPartialWords(
            IEnumerable<string> committedTexts,
            IReadOnlyList<string> targetWords,
            int fallbackInputTextElements)
        {
            return CountPartialWords(committedTexts, targetWords, null, fallbackInputTextElements);
        }

        public static int CountPartialWords(
            IEnumerable<string> committedTexts,
            IReadOnlyList<string> targetWords,
            string fallbackInputText,
            int fallbackInputTextElements)
        {
            int targetCount = targetWords?.Count ?? 0;
            if (targetCount <= 0)
                return 0;

            int committed = CountCommittedTargetSlots(committedTexts, targetWords);
            if (committed > 0)
                return Math.Min(committed, targetCount);

            if (!string.IsNullOrEmpty(fallbackInputText))
                return CountCommittedTargetSlots(new[] { fallbackInputText }, targetWords);

            return Math.Min(Math.Max(0, fallbackInputTextElements), targetCount);
        }

        public static int CountCommittedWords(
            IEnumerable<string> committedTexts,
            IReadOnlyList<string> targetWords,
            int targetStartIndex)
        {
            int targetCount = targetWords?.Count ?? 0;
            if (targetCount <= 0)
                return 0;

            int boundedStartIndex = Math.Max(0, Math.Min(targetStartIndex, targetCount - 1));
            return CountCommittedTargetSlots(committedTexts, targetWords, boundedStartIndex);
        }

        private static int CountCommittedTargetSlots(IEnumerable<string> committedTexts, IReadOnlyList<string> targetWords)
        {
            return CountCommittedTargetSlots(committedTexts, targetWords, 0);
        }

        private static int CountCommittedTargetSlots(IEnumerable<string> committedTexts, IReadOnlyList<string> targetWords, int targetStartIndex)
        {
            if (committedTexts == null)
                return 0;

            int slots = targetStartIndex;
            string pendingAsciiLetters = "";
            foreach (string text in committedTexts)
            {
                int textElements = CountTextElements(text);
                if (textElements <= 0)
                    continue;

                if (IsAsciiLetterText(text))
                {
                    pendingAsciiLetters += text;
                    continue;
                }

                slots = AddPendingAsciiLetters(slots, pendingAsciiLetters, targetWords);
                pendingAsciiLetters = "";

                slots += textElements;

                if (slots >= targetWords.Count)
                    return targetWords.Count - targetStartIndex;
            }

            slots = AddPendingAsciiLetters(slots, pendingAsciiLetters, targetWords);
            return Math.Min(slots, targetWords.Count) - targetStartIndex;
        }

        private static int AddPendingAsciiLetters(int slots, string pendingAsciiLetters, IReadOnlyList<string> targetWords)
        {
            if (string.IsNullOrEmpty(pendingAsciiLetters))
                return slots;

            int textElements = CountTextElements(pendingAsciiLetters);
            if (textElements <= 0)
                return slots;

            if (IsCjkTargetAt(targetWords, slots))
                slots += 1;
            else
                slots += textElements;

            return Math.Min(slots, targetWords.Count);
        }

        private static bool IsCjkTargetAt(IReadOnlyList<string> targetWords, int index)
        {
            if (targetWords == null || index < 0 || index >= targetWords.Count)
                return false;

            string target = targetWords[index];
            if (string.IsNullOrEmpty(target))
                return false;

            int code = char.ConvertToUtf32(target, 0);
            return (code >= 0x3400 && code <= 0x4DBF)
                || (code >= 0x4E00 && code <= 0x9FFF)
                || (code >= 0xF900 && code <= 0xFAFF)
                || (code >= 0x20000 && code <= 0x2A6DF)
                || (code >= 0x2A700 && code <= 0x2B73F)
                || (code >= 0x2B740 && code <= 0x2B81F)
                || (code >= 0x2B820 && code <= 0x2CEAF);
        }

        private static bool IsAsciiLetterText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            foreach (char ch in text)
            {
                if (!((ch >= 'A' && ch <= 'Z') || (ch >= 'a' && ch <= 'z')))
                    return false;
            }

            return true;
        }

        private static int CountTextElements(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            return new StringInfo(text).LengthInTextElements;
        }
    }
}
