using System;
using System.Collections.Generic;
using System.Text;

namespace PureNote
{
    public static class TextSearch
    {
        public static void FindAll(string text, string term, bool exact, List<int> results)
        {
            results.Clear();

            if (string.IsNullOrEmpty(term)) return;

            string haystack = exact ? text : Normalize(text);
            string needle = exact ? term : Normalize(term);

            int index = 0;
            while (index <= haystack.Length - needle.Length)
            {
                int found = haystack.IndexOf(needle, index, StringComparison.Ordinal);
                if (found < 0) break;

                results.Add(found);
                index = found + needle.Length;
            }
        }

        public static int IndexOf(string text, string term, int startIndex, bool exact)
        {
            if (string.IsNullOrEmpty(term)) return -1;

            string haystack = exact ? text : Normalize(text);
            string needle = exact ? term : Normalize(term);

            if (startIndex < 0) startIndex = 0;
            if (startIndex > haystack.Length - needle.Length) return -1;

            return haystack.IndexOf(needle, startIndex, StringComparison.Ordinal);
        }

        // Folds accents and case so "Ação" matches "acao". Every branch must append
        // exactly one char per input char: callers select by the returned index
        // against the raw text, so the mapping has to stay 1:1.
        private static string Normalize(string input)
        {
            StringBuilder sb = new StringBuilder(input.Length);

            foreach (char c in input)
            {
                if (c < 128)
                {
                    sb.Append(char.ToLowerInvariant(c));
                    continue;
                }

                // Half of a surrogate pair is not a valid string on its own —
                // string.Normalize throws on it, which used to take the whole app
                // down whenever a document or search term contained an emoji.
                if (char.IsSurrogate(c))
                {
                    sb.Append(c);
                    continue;
                }

                string decomposed = c.ToString().Normalize(NormalizationForm.FormD);
                sb.Append(char.ToLowerInvariant(decomposed[0]));
            }

            return sb.ToString();
        }
    }
}
