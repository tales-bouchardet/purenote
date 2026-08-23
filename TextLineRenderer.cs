using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace PureNote
{
    // Turns one line of the document into something that can be drawn and
    // measured. One line, never the document - which is the whole point of the
    // rewrite, so it is worth being blunt about it here: nothing in this file
    // may ever be handed more than a single line.
    //
    // Tabs are expanded to the next tab stop before the text is formatted,
    // because FormattedText has no notion of tab stops and would otherwise draw
    // a tab as nothing at all. That expansion is why display columns and
    // document offsets are different coordinates, and why the two mapping
    // functions below exist. Lines without tabs - nearly all of them - take the
    // identity path and allocate nothing extra.
    //
    // FormattedText cannot be handed a long line at all, at any speed. Past a
    // length its own formatter will not exceed - measured at about 9,600
    // characters of this font at this size - it silently wraps the line: the
    // width it reports stops growing, its height becomes several rows, and
    // drawing it paints the overflow down the page over whatever was below.
    // Everything here therefore works on a slice small enough to be safe, and
    // MaxFormattable says how large that is for the font actually in use.
    //
    // Where a column sits is answered two ways, and the cheap one is exact
    // whenever it applies. In a monospaced font every character the font holds
    // advances the pen by the same amount, so the x of a column is a
    // multiplication - no formatting, no allocation, and no length it stops
    // being true at. What breaks the rule is a character the font does not hold,
    // because the glyph then comes from a fallback face with its own advance.
    // Which path is in force is decided per character from the font's own glyph
    // table, never assumed from the font's name or from the character's range:
    // restricting it to ASCII would push every accented word in a Portuguese
    // document onto the slow path for no reason.
    internal sealed class TextLineRenderer
    {
        public const int TabSize = 4;

        // A comfortable margin under where FormattedText starts wrapping. Both a
        // width and a character count, whichever is smaller: the measurement
        // above pins the limit at a particular font and size, and taking the
        // lower of the two keeps this safe whichever of the two the formatter is
        // really counting.
        private const double SafeFormattedWidth = 32000;
        private const int SafeFormattedChars = 4000;

        private readonly Typeface _typeface;
        private readonly double _fontSize;
        private readonly double _pixelsPerDip;
        private readonly Brush _foreground;

        private readonly GlyphTypeface _glyphs;
        private readonly double _standardAdvance;

        public TextLineRenderer(Typeface typeface, double fontSize, double pixelsPerDip, Brush foreground)
        {
            _typeface = typeface;
            _fontSize = fontSize;
            _pixelsPerDip = pixelsPerDip;
            _foreground = foreground;

            // Measured from a character rather than assumed, so a proportional
            // font degrades into a wrong-looking estimate rather than a crash.
            // Everything that has to be exact re-measures the real text.
            FormattedText probe = Format("0");
            CharacterWidth = probe.WidthIncludingTrailingWhitespace;

            // Line height comes from the typeface, not from a formatted string:
            // it has to be identical for every line in the document, including
            // empty ones, or the arithmetic that replaces measuring falls apart.
            LineHeight = Math.Ceiling(_typeface.FontFamily.LineSpacing * fontSize);
            if (LineHeight <= 0) LineHeight = Math.Ceiling(fontSize * 1.4);

            Baseline = _typeface.FontFamily.Baseline * fontSize;

            MaxFormattable = Math.Max(256, Math.Min(SafeFormattedChars, (int)(SafeFormattedWidth / Math.Max(1, CharacterWidth))));

            // The glyph table is what makes the fast path a fact rather than a
            // hope: it says which characters this font actually holds and what
            // each one advances by. Without it - a composite family, a font that
            // will not resolve - the fast path is simply never taken.
            GlyphTypeface glyphs;
            if (_typeface.TryGetGlyphTypeface(out glyphs))
            {
                ushort zero;
                if (glyphs.CharacterToGlyphMap.TryGetValue('0', out zero))
                {
                    _glyphs = glyphs;
                    _standardAdvance = glyphs.AdvanceWidths[zero];
                    IsMonospaced = SameAdvance('i') && SameAdvance(' ') && SameAdvance('W')
                        && SameAdvance('M') && SameAdvance('.');
                }
            }
        }

        public double CharacterWidth { get; private set; }
        public double LineHeight { get; private set; }
        public double Baseline { get; private set; }

        // The most characters that may be handed to Format in one go.
        public int MaxFormattable { get; private set; }

        // Whether the arithmetic path is available at all. Probed against the
        // characters a proportional face draws at obviously different widths,
        // because "mono" in a family name is a promise nobody checks.
        public bool IsMonospaced { get; private set; }

        private bool SameAdvance(char c)
        {
            ushort glyph;
            if (!_glyphs.CharacterToGlyphMap.TryGetValue(c, out glyph)) return false;

            return Math.Abs(_glyphs.AdvanceWidths[glyph] - _standardAdvance) < 0.0001;
        }

        // Whether this font draws this character at exactly CharacterWidth.
        //
        // Memoised, for the same reason TextSearch memoises case folding: a
        // document is overwhelmingly made of a small set of repeated characters,
        // and this is asked once per character of every line prepared for a
        // paint. 0 is "not looked up yet", which is why the map is bytes rather
        // than bools.
        private byte[] _uniformMap;

        public bool IsUniformWidth(char c)
        {
            if (!IsMonospaced) return false;

            // Controls and tabs are not one cell wide whatever the font says, and
            // a lone surrogate is not a character the glyph table can answer for.
            if (c < ' ' || char.IsSurrogate(c)) return false;

            if (_uniformMap == null) _uniformMap = new byte[char.MaxValue + 1];

            byte cached = _uniformMap[c];
            if (cached != 0) return cached == 1;

            bool uniform = SameAdvance(c);
            _uniformMap[c] = uniform ? (byte)1 : (byte)2;

            return uniform;
        }

        public FormattedText Format(string text)
        {
            return new FormattedText(
                text ?? string.Empty,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                _typeface,
                _fontSize,
                _foreground,
                _pixelsPerDip);
        }

        public static bool HasTab(string line)
        {
            return line.IndexOf('\t') >= 0;
        }

        // What actually gets drawn: the line with every tab replaced by the
        // spaces that carry the text to its next tab stop.
        public static string Expand(string line)
        {
            if (!HasTab(line)) return line;

            System.Text.StringBuilder sb = new System.Text.StringBuilder(line.Length + TabSize);

            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] != '\t')
                {
                    sb.Append(line[i]);
                    continue;
                }

                sb.Append(' ', TabSize - sb.Length % TabSize);
            }

            return sb.ToString();
        }

        // Offset within the line to column in the expanded text.
        public static int ToColumn(string line, int offsetInLine)
        {
            if (!HasTab(line)) return offsetInLine;

            int column = 0;

            for (int i = 0; i < offsetInLine && i < line.Length; i++)
            {
                column = line[i] == '\t' ? column + (TabSize - column % TabSize) : column + 1;
            }

            return column;
        }

        // Column in the expanded text back to an offset within the line. A column
        // that lands inside the run of spaces a tab expanded into resolves to the
        // tab itself, so a click in the middle of a tab puts the caret on one
        // side of it rather than nowhere.
        public static int ToOffset(string line, int column)
        {
            if (!HasTab(line)) return Math.Min(column, line.Length);

            int current = 0;

            for (int i = 0; i < line.Length; i++)
            {
                int next = line[i] == '\t' ? current + (TabSize - current % TabSize) : current + 1;

                if (column < next) return i;

                current = next;
            }

            return line.Length;
        }

        // Where a column sits horizontally, relative to the start of `expanded`.
        //
        // uniform: the caller has established that every character up to this
        // column advances by exactly CharacterWidth - see TextView, which checks
        // it against the document one character at a time rather than guessing.
        // Then this is a multiplication and the FormattedText that measuring
        // would have built is never allocated. That matters more than it looks:
        // this is asked for the caret, for both ends of the selection and for
        // both ends of every match on every line of every frame, and the
        // measuring path formats a fresh copy of the prefix for each of them.
        public double WidthOf(string expanded, int column, bool uniform)
        {
            if (column <= 0) return 0;
            if (uniform) return column * CharacterWidth;

            if (column >= expanded.Length) return Format(expanded).WidthIncludingTrailingWhitespace;

            return Format(expanded.Substring(0, column)).WidthIncludingTrailingWhitespace;
        }

        // The reverse. Arithmetic when the run is uniform; otherwise bisection
        // over the same measurement, because FormattedText has no hit-testing of
        // its own. The bisection is bounded by MaxFormattable, so the handful of
        // measurements it costs is a handful of short ones.
        public int ColumnAt(string expanded, double x, bool uniform)
        {
            if (x <= 0 || expanded.Length == 0) return 0;

            if (uniform)
            {
                // Rounding rather than truncating is what puts the caret after a
                // character when the click landed past its halfway point.
                int column = (int)Math.Round(x / CharacterWidth);

                return column < 0 ? 0 : column > expanded.Length ? expanded.Length : column;
            }

            int low = 0;
            int high = expanded.Length;

            while (low < high)
            {
                int middle = (low + high + 1) / 2;

                if (WidthOf(expanded, middle, false) <= x) low = middle; else high = middle - 1;
            }

            // Past the halfway point of a character the caret belongs after it,
            // which is what makes clicking near the right edge of a glyph land
            // where the user meant.
            if (low < expanded.Length)
            {
                double before = WidthOf(expanded, low, false);
                double after = WidthOf(expanded, low + 1, false);

                if (x - before > (after - before) / 2) low++;
            }

            return low;
        }
    }
}
