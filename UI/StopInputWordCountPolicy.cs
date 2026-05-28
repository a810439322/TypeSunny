using TypeSunny.Core;

namespace TypeSunny.UI
{
    internal static class StopInputWordCountPolicy
    {
        public static int Resolve(
            TxtSource txtSource,
            int scoreInputWordsBeforeRefresh,
            int inputTextElements,
            bool preserveScoreWhenInputHidden = false)
        {
            if (preserveScoreWhenInputHidden)
                return scoreInputWordsBeforeRefresh;

            if (txtSource == TxtSource.trainer && inputTextElements == 0 && scoreInputWordsBeforeRefresh > 0)
                return scoreInputWordsBeforeRefresh;

            return inputTextElements;
        }
    }
}
