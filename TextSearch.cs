using System;
using System.Collections.Generic;
using System.Text;

namespace PureNote
{
    public static class TextSearch
    {
        internal static void FindAll(TextDocument document, string term, bool exact, List<int> results)
        {
            results.Clear();

            if (document == null || string.IsNullOrEmpty(term)) return;

            int length = document.Length;
            int termLength = term.Length;
            if (termLength > length) return;

            char[] needle = BuildNeedle(term, exact);

            for (int i = 0; i + termLength <= length; i++)
            {
                if (!MatchesAt(document, i, needle, exact)) continue;

                results.Add(i);

                i += termLength - 1;
            }
        }

        internal static int IndexOf(TextDocument document, string term, int startIndex, bool exact)
        {
            if (document == null || string.IsNullOrEmpty(term)) return -1;

            int length = document.Length;
            int termLength = term.Length;

            if (startIndex < 0) startIndex = 0;
            if (startIndex + termLength > length) return -1;

            char[] needle = BuildNeedle(term, exact);

            for (int i = startIndex; i + termLength <= length; i++)
            {
                if (MatchesAt(document, i, needle, exact)) return i;
            }

            return -1;
        }

        private static bool MatchesAt(TextDocument document, int offset, char[] needle, bool exact)
        {
            for (int j = 0; j < needle.Length; j++)
            {
                if (CharAt(document, offset + j, exact) != needle[j]) return false;
            }

            return true;
        }

        private static char[] BuildNeedle(string term, bool exact)
        {
            char[] needle = new char[term.Length];

            for (int i = 0; i < term.Length; i++)
            {
                needle[i] = exact ? term[i] : Fold(term[i]);
            }

            return needle;
        }

        private static char CharAt(TextDocument document, int offset, bool exact)
        {
            char c = document[offset];
            return exact ? c : Fold(c);
        }

        private static char Fold(char c)
        {
            if (c < 128) return c >= 'A' && c <= 'Z' ? (char)(c + 32) : c;

            return FoldNonAscii(c);
        }

        private static char[] _foldMap;

        private static char FoldNonAscii(char c)
        {
            if (char.IsSurrogate(c)) return c;

            if (_foldMap == null) _foldMap = new char[char.MaxValue + 1];

            char cached = _foldMap[c];
            if (cached != '\0') return cached;

            string decomposed = c.ToString().Normalize(NormalizationForm.FormD);
            char folded = char.ToLowerInvariant(decomposed[0]);

            _foldMap[c] = folded;
            return folded;
        }
    }
}
