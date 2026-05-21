using TypeSunny.Core;

namespace TypeSunny.UI
{
    internal static class StopInputWordCountPolicy
    {
        public static int Resolve(TxtSource txtSource, int scoreInputWordsBeforeRefresh, int inputTextElements)
        {
            if (txtSource == TxtSource.trainer && inputTextElements == 0 && scoreInputWordsBeforeRefresh > 0)
                return scoreInputWordsBeforeRefresh;

            return inputTextElements;
        }
    }
}
