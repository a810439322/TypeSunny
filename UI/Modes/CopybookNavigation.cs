using System;
using System.Collections.Generic;

namespace TypeSunny.UI.Modes
{
    internal static class CopybookNavigation
    {
        public static int FindEndTargetWithinTypedLine(
            int currentIndex,
            int totalCount,
            IEnumerable<int> lineIndexes,
            Func<int, bool> isTyped)
        {
            var indexes = new HashSet<int>(lineIndexes);
            int lastTyped = -1;

            foreach (int index in indexes)
            {
                if (index >= 0 && index < totalCount && isTyped(index) && index > lastTyped)
                    lastTyped = index;
            }

            if (lastTyped < 0)
                return currentIndex;

            int continuation = lastTyped + 1;
            if (continuation < totalCount && indexes.Contains(continuation))
                return continuation;

            return lastTyped;
        }
    }
}
