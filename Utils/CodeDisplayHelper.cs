using System;

namespace TypeSunny.Utils
{
    internal static class CodeDisplayHelper
    {
        internal static string TryGetTailBadgeText(string rawCode)
        {
            if (string.IsNullOrWhiteSpace(rawCode))
                return "";

            int separatorIndex = rawCode.IndexOf('·');
            string code = separatorIndex >= 0 ? rawCode.Substring(0, separatorIndex) : rawCode;
            code = code.Trim();

            if (code.Length == 0)
                return "";

            char tail = code[code.Length - 1];
            if (tail == '0' || (tail >= '2' && tail <= '9'))
                return tail.ToString();

            return "";
        }
    }
}
