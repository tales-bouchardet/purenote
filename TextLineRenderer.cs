using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace PureNote
{
    internal sealed class TextLineRenderer
    {
        public const int TabSize = 4;

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

            FormattedText probe = Format("0");
            CharacterWidth = probe.WidthIncludingTrailingWhitespace;

            LineHeight = Math.Ceiling(_typeface.FontFamily.LineSpacing * fontSize);
            if (LineHeight <= 0) LineHeight = Math.Ceiling(fontSize * 1.4);

            Baseline = _typeface.FontFamily.Baseline * fontSize;

            MaxFormattable = Math.Max(256, Math.Min(SafeFormattedChars, (int)(SafeFormattedWidth / Math.Max(1, CharacterWidth))));

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

        public int MaxFormattable { get; private set; }

        public bool IsMonospaced { get; private set; }

        private bool SameAdvance(char c)
        {
            ushort glyph;
            if (!_glyphs.CharacterToGlyphMap.TryGetValue(c, out glyph)) return false;

            return Math.Abs(_glyphs.AdvanceWidths[glyph] - _standardAdvance) < 0.0001;
        }

        private byte[] _uniformMap;

        public bool IsUniformWidth(char c)
        {
            if (!IsMonospaced) return false;

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

        public double WidthOf(string expanded, int column, bool uniform)
        {
            if (column <= 0) return 0;
            if (uniform) return column * CharacterWidth;

            if (column >= expanded.Length) return Format(expanded).WidthIncludingTrailingWhitespace;

            return Format(expanded.Substring(0, column)).WidthIncludingTrailingWhitespace;
        }

        public int ColumnAt(string expanded, double x, bool uniform)
        {
            if (x <= 0 || expanded.Length == 0) return 0;

            if (uniform)
            {
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
