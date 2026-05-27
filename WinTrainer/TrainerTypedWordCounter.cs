using System.Collections.Generic;

namespace TypeSunny
{
    internal sealed class TrainerTypedWordCounter
    {
        private int countedFallbackWords;
        private int countedWords;

        public int AddFrom(
            IReadOnlyList<string> committedTexts,
            IReadOnlyList<string> targetWords,
            string fallbackInputText,
            int fallbackInputTextElements)
        {
            if (committedTexts == null || committedTexts.Count == 0)
                return AddFromFallback(targetWords, fallbackInputText, fallbackInputTextElements);

            int currentWords = TrainerActualWordCounter.CountPartialWords(
                committedTexts,
                targetWords,
                fallbackInputText,
                fallbackInputTextElements);
            if (currentWords <= countedWords)
                return 0;

            int delta = currentWords - countedWords;
            countedWords = currentWords;
            countedFallbackWords = countedWords;
            return delta;
        }

        public void Reset()
        {
            countedFallbackWords = 0;
            countedWords = 0;
        }

        private int AddFromFallback(
            IReadOnlyList<string> targetWords,
            string fallbackInputText,
            int fallbackInputTextElements)
        {
            int currentWords = TrainerActualWordCounter.CountPartialWords(
                null,
                targetWords,
                fallbackInputText,
                fallbackInputTextElements);
            if (currentWords <= countedFallbackWords)
                return 0;

            int delta = currentWords - countedFallbackWords;
            countedFallbackWords = currentWords;
            countedWords = countedFallbackWords;
            return delta;
        }
    }
}
