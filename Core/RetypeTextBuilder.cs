using Diff;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TypeSunny.Core
{
    internal static class RetypeTextBuilder
    {
        public static void ReplaceWithFinalWrongRecords(
            IDictionary<int, string> wrongRecords,
            IEnumerable<KeyValuePair<int, string>> finalWrongRecords,
            string wrongExclude)
        {
            wrongRecords.Clear();

            if (finalWrongRecords == null)
                return;

            foreach (var record in finalWrongRecords.OrderBy(r => r.Key))
            {
                string word = record.Value ?? "";
                if (word.Length == 0)
                    continue;

                if (!string.IsNullOrEmpty(wrongExclude) && wrongExclude.Contains(word))
                    continue;

                wrongRecords[record.Key] = word;
            }
        }

        public static string BuildCombinedRetypeText(
            IDictionary<int, string> wrongRecords,
            int wrongRepeatCount,
            IDictionary<int, string> slowRecords,
            int slowRepeatCount)
        {
            var sb = new StringBuilder();
            AppendRecords(sb, wrongRecords, wrongRepeatCount);
            AppendRecords(sb, slowRecords, slowRepeatCount);
            return sb.ToString();
        }

        public static Dictionary<int, string> BuildLookTypingDeletedWordRecords(
            string originalText,
            IEnumerable<DiffRes> diffs,
            string wrongExclude)
        {
            var records = new Dictionary<int, string>();
            if (string.IsNullOrEmpty(originalText) || diffs == null)
                return records;

            foreach (var diff in diffs)
            {
                if (diff.Type != DiffType.Delete)
                    continue;

                int textIndex = diff.OrigIndex - 1;
                if (textIndex < 0 || textIndex >= originalText.Length)
                    continue;

                string word = originalText.Substring(textIndex, 1);
                if (!string.IsNullOrEmpty(wrongExclude) && wrongExclude.Contains(word))
                    continue;

                records[textIndex] = word;
            }

            return records;
        }

        public static bool TryResolveBlindImeBackspaceWrongRecord(
            IList<string> words,
            int nextPendingWordIndex,
            string wrongExclude,
            out int position,
            out string word)
        {
            position = -1;
            word = null;
            return false;
        }

        private static void AppendRecords(StringBuilder sb, IDictionary<int, string> records, int repeatCount)
        {
            if (records == null || repeatCount <= 0)
                return;

            var orderedValues = records
                .OrderBy(r => r.Key)
                .Select(r => r.Value)
                .Where(v => !string.IsNullOrEmpty(v))
                .ToArray();

            for (int i = 0; i < repeatCount; i++)
            {
                foreach (string value in orderedValues)
                    sb.Append(value);
            }
        }
    }
}
