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

            string haystack = exact ? text : FoldDocument(text);
            string needle = exact ? term : Fold(term);

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

            string haystack = exact ? text : FoldDocument(text);
            string needle = exact ? term : Fold(term);

            if (startIndex < 0) startIndex = 0;
            if (startIndex > haystack.Length - needle.Length) return -1;

            return haystack.IndexOf(needle, startIndex, StringComparison.Ordinal);
        }

        // Folding the document is proportional to its size, and every keystroke in
        // the search box re-runs the search over the same unchanged text. The
        // editor hands out one stable string per edit, so reference equality is
        // enough to tell "same document" from "edited since".
        private static string _foldedSource;
        private static string _foldedDocument;

        private static string FoldDocument(string text)
        {
            if (ReferenceEquals(text, _foldedSource)) return _foldedDocument;

            _foldedDocument = Fold(text);
            _foldedSource = text;
            return _foldedDocument;
        }

        // Folds accents and case so "Ação" matches "acao". Every branch must map
        // exactly one char per input char: callers select by the returned index
        // against the raw text, so the mapping has to stay 1:1.
        private static string Fold(string input)
        {
            char[] buffer = new char[input.Length];

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                buffer[i] = c < 128 ? ToLowerAscii(c) : FoldNonAscii(c);
            }

            return new string(buffer);
        }

        private static char ToLowerAscii(char c)
        {
            return c >= 'A' && c <= 'Z' ? (char)(c + 32) : c;
        }

        // Memoised because the decompose-and-lowercase path allocates two strings
        // per character, and a document is overwhelmingly made of a small set of
        // repeated ones. '\0' marks an entry as not yet computed — folding never
        // produces it from a non-ASCII char, so it is safe as the empty slot.
        private static char[] _foldMap;

        private static char FoldNonAscii(char c)
        {
            // Half of a surrogate pair is not a valid string on its own —
            // string.Normalize throws on it, which used to take the whole app
            // down whenever a document or search term contained an emoji.
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
