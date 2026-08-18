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

        public static string Normalize(string input)
        {
            StringBuilder sb = new StringBuilder(input.Length);

            foreach (char c in input)
            {
                if (c < 128)
                {
                    sb.Append(char.ToLowerInvariant(c));
                    continue;
                }

                string decomposed = c.ToString().Normalize(NormalizationForm.FormD);
                sb.Append(char.ToLowerInvariant(decomposed[0]));
            }

            return sb.ToString();
        }
    }
}
