using System;
using System.Collections.Generic;
using System.Text;

namespace PureNote
{
    // Searching, straight over the document.
    //
    // This used to fold the whole document into a second, case-and-accent-
    // flattened copy of itself and run IndexOf over that. The copy was the size
    // of the document - 222 MB of it on the file that prompted the rewrite - and
    // it had to be cached, invalidated on every edit, and explicitly forgotten
    // when a file was closed, or it outlived the document it described.
    //
    // Folding a character at a time as the scan passes over it needs none of
    // that, and it turns out not to be a trade at all. Over a 12.8 M character
    // corpus: folding as it goes finds every match in 56 ms and allocates
    // nothing, where folding a copy first cost 144 ms to build the copy and 28
    // to search it - three times slower, for 24 MB. Giving up the vectorised
    // IndexOf loses less than building the string it would search costs.
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
            char first = needle[0];

            for (int i = 0; i + termLength <= length; i++)
            {
                if (CharAt(document, i, exact) != first) continue;

                int j = 1;
                while (j < termLength && CharAt(document, i + j, exact) == needle[j]) j++;

                if (j < termLength) continue;

                results.Add(i);

                // Matches do not overlap: the scan resumes after the one just
                // found, which is what makes replacing them all well defined.
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
            char first = needle[0];

            for (int i = startIndex; i + termLength <= length; i++)
            {
                if (CharAt(document, i, exact) != first) continue;

                int j = 1;
                while (j < termLength && CharAt(document, i + j, exact) == needle[j]) j++;

                if (j == termLength) return i;
            }

            return -1;
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

        // Folds accents and case so "Ação" matches "acao". Must map exactly one
        // char per input char: callers select by the returned offset against the
        // raw document, so the mapping has to stay 1:1.
        private static char Fold(char c)
        {
            if (c < 128) return c >= 'A' && c <= 'Z' ? (char)(c + 32) : c;

            return FoldNonAscii(c);
        }

        // Memoised because the decompose-and-lowercase path allocates two strings
        // per character, and a document is overwhelmingly made of a small set of
        // repeated ones. '\0' marks an entry as not yet computed - folding never
        // produces it from a non-ASCII char, so it is safe as the empty slot.
        private static char[] _foldMap;

        private static char FoldNonAscii(char c)
        {
            // Half of a surrogate pair is not a valid string on its own -
            // string.Normalize throws on one, which used to take the whole app
            // down whenever a document or a search term contained an emoji.
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
