using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

namespace PureNote
{
    // The editor: a virtualised view over a TextDocument.
    //
    // The whole design turns on one number. A scrolling control has to report
    // how tall its content is, and a WPF TextBox answers that question by laying
    // out every line in the document - eighteen seconds of it, on the file that
    // prompted this. Here every line is the same height by construction, so the
    // answer is LineCount * LineHeight: arithmetic, no measuring, no matter how
    // large the file. The same trick is what makes VS Code and RichEdit open
    // instantly, and it is the only reason any of the rest of this is worth
    // writing.
    //
    // What follows from that is that only the lines about to be painted are ever
    // formatted - twenty or so, once per frame - and that they are found by
    // dividing the scroll offset by the line height rather than by walking the
    // document. Scrolling to the end of a three-million-line file touches three
    // million characters exactly never.
    //
    // It also means the drift that EditorLineLayout existed to correct is simply
    // gone: there is no second opinion about where a line sits, because this is
    // the only thing that decides.
    internal sealed partial class TextView : FrameworkElement, IScrollInfo
    {
        private TextDocument _document = new TextDocument();
        private TextLineRenderer _renderer;

        private double _horizontalOffset;
        private double _verticalOffset;
        private Size _viewport;

        // Never shrinks, and is an estimate until the widest line has been seen.
        // Measuring every line to get this exactly right is the one thing this
        // control refuses to do, so it starts from the longest line in characters
        // and grows as real lines are formatted. The scrollbar can therefore be a
        // little short until the widest line has been on screen once - the same
        // compromise AvalonEdit and VS Code make.
        private double _extentWidth;
        private int _longestLineChars;

        private int _caretOffset;
        private int _selectionAnchor;

        // The column the caret would like to be in, kept across vertical moves so
        // that going down through a short line and out the other side comes back
        // to where it started rather than sticking to the short line's end.
        private int _preferredColumn = -1;
        private bool _preferredColumnValid;

        private bool _caretOn = true;
        private DispatcherTimer _caretTimer;

        public TextView()
        {
            Focusable = true;
            FocusVisualStyle = null;

            // Without this the element is transparent to the mouse wherever it
            // has not drawn anything, and clicking past the end of a short line
            // would do nothing.
            ClipToBounds = true;

            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);

            InstallCommands();
        }

        public event EventHandler DocumentChanged;
        public event EventHandler CaretChanged;

        // Raised when an edit could not be made because there was no memory for
        // it. The document is left exactly as it was; the window turns this into
        // a message, because a keystroke that quietly does nothing reads as a
        // broken editor rather than as a full one.
        public event EventHandler EditRefused;

        // Raised whenever the range of lines on screen may have moved. The line
        // number gutter draws against the same arithmetic this view does, so it
        // only ever needs telling that the range changed, never what it is.
        public event EventHandler ViewChanged;

        // The gutter places its numbers with the same expression the view places
        // its lines with, and this is the one term of it that is not already
        // public.
        public double EditorPaddingTop
        {
            get { return Padding.Top; }
        }

        private void RaiseViewChanged()
        {
            EventHandler handler = ViewChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        public TextDocument Document
        {
            get { return _document; }
        }

        public bool IsReadOnly { get; set; }

        public Brush Background { get; set; }
        public Brush Foreground { get; set; }
        public Brush SelectionBrush { get; set; }
        public Brush CaretBrush { get; set; }
        public Brush MatchBrush { get; set; }
        public Brush CurrentMatchBrush { get; set; }
        public Brush CurrentMatchStroke { get; set; }

        public FontFamily FontFamily { get; set; }
        public double FontSize { get; set; }
        public Thickness Padding { get; set; }

        public int LineCount
        {
            get { return _document.LineCount; }
        }

        public int TextLength
        {
            get { return _document.Length; }
        }

        public double LineHeight
        {
            get { return Renderer.LineHeight; }
        }

        // ---- find highlighting ----------------------------------------------
        //
        // Drawn here rather than by a canvas of Rectangles laid over the top. The
        // overlay had to be told where every line sat, which is what forced a
        // second, drifting opinion about layout into the program; here the answer
        // is already in hand at the moment the line is drawn.

        private List<int> _matches;
        private int _matchLength;
        private int _currentMatch = -1;
        private bool _highlightAll;

        public void SetMatches(List<int> matches, int length, int current, bool highlightAll)
        {
            _matches = matches;
            _matchLength = length;
            _currentMatch = current;
            _highlightAll = highlightAll;

            InvalidateVisual();
        }

        public void ClearMatches()
        {
            _matches = null;
            _currentMatch = -1;

            InvalidateVisual();
        }

        // ---- document --------------------------------------------------------

        public void SetDocument(TextDocument document)
        {
            _document = document ?? new TextDocument();

            ResetUndo();

            _caretOffset = 0;
            _selectionAnchor = 0;
            _horizontalOffset = 0;
            _verticalOffset = 0;
            _matches = null;
            _currentMatch = -1;

            InvalidateLineCache();
            MeasureLongestLine();
            InvalidateScrollInfo();
            InvalidateVisual();

            RaiseDocumentChanged();
        }

        public void Clear()
        {
            SetDocument(new TextDocument());
        }

        // Proportional to the number of lines, not to the size of the document:
        // it reads the line index and never the text. Six milliseconds on a
        // three-million-line file, once, at load.
        private void MeasureLongestLine()
        {
            int longest = 0;
            int lines = _document.LineCount;

            for (int i = 0; i < lines; i++)
            {
                int length = _document.GetLineLength(i);
                if (length > longest) longest = length;
            }

            _longestLineChars = longest;
            _extentWidth = EstimatedWidthFor(longest);
        }

        private double EstimatedWidthFor(int characters)
        {
            return characters * Renderer.CharacterWidth + Padding.Left + Padding.Right;
        }

        private TextLineRenderer Renderer
        {
            get
            {
                if (_renderer == null)
                {
                    FontFamily family = FontFamily ?? SystemFonts.MessageFontFamily;
                    Typeface typeface = new Typeface(family, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

                    _renderer = new TextLineRenderer(typeface, FontSize > 0 ? FontSize : 14,
                        VisualTreeHelper.GetDpi(this).PixelsPerDip, Foreground ?? Brushes.Gainsboro);
                }

                return _renderer;
            }
        }

        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
        {
            base.OnDpiChanged(oldDpi, newDpi);

            _renderer = null;
            _currentMatchPen = null;
            InvalidateLineCache();
            InvalidateVisual();
        }

        // ---- caret and selection --------------------------------------------

        public int CaretOffset
        {
            get { return _caretOffset; }
            set { Select(value, 0); }
        }

        public int SelectionStart
        {
            get { return Math.Min(_caretOffset, _selectionAnchor); }
        }

        public int SelectionLength
        {
            get { return Math.Abs(_caretOffset - _selectionAnchor); }
        }

        public string SelectedText
        {
            get { return _document.GetText(SelectionStart, SelectionLength); }
        }

        public void Select(int start, int length)
        {
            start = Clamp(start, 0, _document.Length);
            length = Clamp(length, 0, _document.Length - start);

            _selectionAnchor = start;
            _caretOffset = start + length;
            _preferredColumnValid = false;

            RestartCaretBlink();
            BringCaretIntoView();
            InvalidateVisual();

            RaiseCaretChanged();
        }

        public void SelectAll()
        {
            _selectionAnchor = 0;
            _caretOffset = _document.Length;
            _preferredColumnValid = false;

            InvalidateVisual();
            RaiseCaretChanged();
        }

        // extend: a shift-modified move, which drags the far end of the selection
        // along instead of collapsing it.
        private void MoveCaret(int offset, bool extend)
        {
            _caretOffset = Clamp(offset, 0, _document.Length);
            if (!extend) _selectionAnchor = _caretOffset;

            RestartCaretBlink();
            BringCaretIntoView();
            InvalidateVisual();

            RaiseCaretChanged();
        }

        private static int Clamp(int value, int low, int high)
        {
            return value < low ? low : value > high ? high : value;
        }

        public int CaretLine
        {
            get { return _document.GetLineFromOffset(_caretOffset); }
        }

        public int CaretColumn
        {
            get
            {
                int line = CaretLine;
                return _caretOffset - _document.GetLineStart(line);
            }
        }

        private void RaiseDocumentChanged()
        {
            EventHandler handler = DocumentChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void RaiseCaretChanged()
        {
            EventHandler handler = CaretChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        // ---- caret blink -----------------------------------------------------

        protected override void OnGotKeyboardFocus(System.Windows.Input.KeyboardFocusChangedEventArgs e)
        {
            base.OnGotKeyboardFocus(e);
            RestartCaretBlink();
            InvalidateVisual();
        }

        protected override void OnLostKeyboardFocus(System.Windows.Input.KeyboardFocusChangedEventArgs e)
        {
            base.OnLostKeyboardFocus(e);

            if (_caretTimer != null) _caretTimer.Stop();

            _caretOn = false;
            InvalidateVisual();
        }

        // WPF has no equivalent of this - SystemParameters carries no caret blink
        // rate - so it comes from Win32, which is where the control panel setting
        // that governs it lives. A user who has turned blinking off gets INFINITE
        // back, and a caret that simply stays put.
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern uint GetCaretBlinkTime();

        private static double CaretBlinkMilliseconds()
        {
            uint blink = GetCaretBlinkTime();

            if (blink == 0 || blink == uint.MaxValue) return int.MaxValue;

            return Math.Max(200, blink);
        }

        private void RestartCaretBlink()
        {
            _caretOn = true;

            if (_caretTimer == null)
            {
                _caretTimer = new DispatcherTimer(DispatcherPriority.Background)
                {
                    Interval = TimeSpan.FromMilliseconds(CaretBlinkMilliseconds())
                };

                _caretTimer.Tick += (s, e) =>
                {
                    _caretOn = !_caretOn;
                    InvalidateVisual();
                };
            }

            _caretTimer.Stop();
            if (IsKeyboardFocused) _caretTimer.Start();
        }

        // ---- layout ----------------------------------------------------------

        protected override Size MeasureOverride(Size availableSize)
        {
            Size viewport = new Size(
                double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width,
                double.IsInfinity(availableSize.Height) ? 0 : availableSize.Height);

            if (viewport != _viewport)
            {
                _viewport = viewport;

                // How many columns a slice holds is how many the viewport shows.
                InvalidateLineCache();

                InvalidateScrollInfo();
                RaiseViewChanged();
            }

            return viewport;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (finalSize != _viewport)
            {
                _viewport = finalSize;
                InvalidateLineCache();
                InvalidateScrollInfo();
            }

            CoerceOffsets();
            return finalSize;
        }

        // A line is drawn at (line * LineHeight + Padding.Top - VerticalOffset),
        // so this is that expression solved for the line at y = 0. The padding
        // term is easy to drop and doing so puts the gutter half a row out of
        // step with the text beside it, which is exactly the class of bug the
        // old drifting layout used to produce.
        public int FirstVisibleLine
        {
            get { return Math.Max(0, (int)((_verticalOffset - Padding.Top) / LineHeight)); }
        }

        public int LastVisibleLine
        {
            get
            {
                // One extra: the top row is normally cut off by the viewport edge,
                // so the last one is too, and both have to be drawn to be clipped.
                int last = FirstVisibleLine + (int)Math.Ceiling(_viewport.Height / LineHeight) + 1;
                return Math.Min(last, _document.LineCount - 1);
            }
        }

        // ---- the visible part of a line --------------------------------------
        //
        // What of one line this frame is going to look at, prepared once and
        // handed to everything that draws on that row.
        //
        // Before this the line was read out of the document, had its tabs
        // expanded and was formatted once for the text itself, and then again by
        // each of the selection, the match highlighting and the caret asking
        // where a column sat - the same string laid out up to six times to paint
        // one row, and all of it again half a second later when the caret
        // blinked. Now it is prepared once, kept while it stays on screen, and
        // the blink costs a DrawText.
        //
        // It is also where a long line stops being a whole line. Only the columns
        // the viewport can show are ever read out of the document, so a
        // three-megabyte minified line costs what an eighty-column one costs.
        private sealed class VisibleLine
        {
            public int Line;
            public int Start;         // document offset of the line's first character
            public int Length;        // characters in the whole line, not in the slice
            public string Text;       // the part of it being drawn
            public string Expanded;   // Text with tabs expanded
            public int FirstColumn;   // display column Text begins at
            public bool Uniform;      // every column advances by exactly CharacterWidth
            public FormattedText Formatted;
            public double Width;      // of the whole line, as far as it can be known
        }

        private readonly Dictionary<int, VisibleLine> _lineCache = new Dictionary<int, VisibleLine>();

        // Columns kept beyond each edge of the viewport, so a character half
        // scrolled off is drawn and clipped rather than simply missing.
        private const int SliceMargin = 8;

        // Called by everything that changes what a prepared line would come out
        // as: an edit, a new document, a different font, and a horizontal scroll,
        // which moves the slice each long line was cut to. A vertical scroll does
        // not: the lines it brings in are new keys, and the ones it takes away are
        // still correct if they come back.
        private void InvalidateLineCache()
        {
            if (_lineCache.Count > 0) _lineCache.Clear();
        }

        private VisibleLine GetVisibleLine(int line)
        {
            VisibleLine prepared;
            if (_lineCache.TryGetValue(line, out prepared)) return prepared;

            prepared = BuildVisibleLine(line);
            _lineCache[line] = prepared;

            return prepared;
        }

        private VisibleLine BuildVisibleLine(int line)
        {
            TextLineRenderer renderer = Renderer;

            VisibleLine v = new VisibleLine();
            v.Line = line;
            v.Start = _document.GetLineStart(line);
            v.Length = _document.GetLineLength(line);

            int firstColumn = (int)(_horizontalOffset / renderer.CharacterWidth) - SliceMargin;
            if (firstColumn < 0) firstColumn = 0;

            int columns = (int)(_viewport.Width / renderer.CharacterWidth) + SliceMargin * 2;
            if (columns < 1) columns = 1;

            // Whether every character up to the right edge of the viewport is one
            // this font draws at a single cell's width. Only what is to the left
            // of the edge can move what is inside it, so this reads a prefix
            // proportional to how far the view is scrolled rather than to how long
            // the line is - and at offset zero it reads nothing at all.
            v.Uniform = renderer.IsMonospaced && IsUniformRun(v.Start, Math.Min(v.Length, firstColumn + columns));

            if (v.Uniform)
            {
                // Column and offset are the same number, so the slice comes
                // straight out of the document without reading a character before
                // it. This is the path a minified file takes.
                int from = Math.Min(firstColumn, v.Length);
                int take = Math.Min(columns, v.Length - from);

                v.FirstColumn = from;
                v.Text = _document.GetText(v.Start + from, take);
                v.Expanded = v.Text;
                v.Width = v.Length * renderer.CharacterWidth;
            }
            else
            {
                // Something on the line is not one cell wide, so no arithmetic
                // finds the slice and there is no budget to measure the way to it.
                // The line is taken from the start and cut at what FormattedText
                // will accept - which for an ordinary line of prose or code is the
                // whole of it, and everything below then measures exactly as it
                // always did.
                int take = Math.Min(v.Length, renderer.MaxFormattable);

                v.FirstColumn = 0;
                v.Text = _document.GetText(v.Start, take);
                v.Expanded = TextLineRenderer.Expand(v.Text);

                // Tabs expand, so a line made of them can come back out of Expand
                // longer than what went in and past the limit again.
                if (v.Expanded.Length > renderer.MaxFormattable)
                {
                    v.Expanded = v.Expanded.Substring(0, renderer.MaxFormattable);
                }
            }

            if (v.Expanded.Length > 0)
            {
                v.Formatted = renderer.Format(v.Expanded);

                if (!v.Uniform) v.Width = v.Formatted.WidthIncludingTrailingWhitespace;
            }

            return v;
        }

        // Whether this font draws every character in the range at exactly one
        // cell's width. Read straight out of the document rather than out of a
        // string of the line, so answering it allocates nothing.
        private bool IsUniformRun(int start, int count)
        {
            TextLineRenderer renderer = Renderer;

            for (int i = 0; i < count; i++)
            {
                if (!renderer.IsUniformWidth(_document[start + i])) return false;
            }

            return true;
        }

        // Where a column of this line sits, measured from the start of the line's
        // text - the slice's own offset is already inside the arithmetic.
        private static double ColumnX(TextLineRenderer renderer, VisibleLine v, int column)
        {
            if (v.Uniform) return column * renderer.CharacterWidth;

            return renderer.WidthOf(v.Expanded, column, false);
        }

        // Offset within the line to display column. The same number when the line
        // is uniform - no tabs, one cell per character - which is what lets the
        // slice be cut by offset in the first place.
        private static int ColumnOf(VisibleLine v, int offsetInLine)
        {
            if (v.Uniform) return offsetInLine;

            return TextLineRenderer.ToColumn(v.Text, Math.Min(offsetInLine, v.Text.Length));
        }

        // ---- rendering -------------------------------------------------------

        protected override void OnRender(DrawingContext dc)
        {
            double width = Math.Max(_viewport.Width, RenderSize.Width);
            double height = Math.Max(_viewport.Height, RenderSize.Height);

            if (Background != null) dc.DrawRectangle(Background, null, new Rect(0, 0, width, height));

            TextLineRenderer renderer = Renderer;

            int first = FirstVisibleLine;
            int last = LastVisibleLine;
            if (last < first) return;

            int selectionStart = SelectionStart;
            int selectionEnd = selectionStart + SelectionLength;
            int caretLine = _document.GetLineFromOffset(_caretOffset);

            double x0 = Padding.Left - _horizontalOffset;
            bool widthGrew = false;

            for (int line = first; line <= last; line++)
            {
                double y = line * renderer.LineHeight - _verticalOffset + Padding.Top;
                VisibleLine v = GetVisibleLine(line);

                if (selectionEnd > selectionStart)
                {
                    DrawSelection(dc, renderer, v, selectionStart, selectionEnd, x0, y);
                }

                DrawMatches(dc, renderer, v, x0, y);

                if (v.Formatted != null)
                {
                    dc.DrawText(v.Formatted, new Point(x0 + ColumnX(renderer, v, v.FirstColumn), y));

                    // The extent is an estimate until the widest line has been
                    // prepared at least once; this is where it stops being one.
                    double lineWidth = v.Width + Padding.Left + Padding.Right;
                    if (lineWidth > _extentWidth) { _extentWidth = lineWidth; widthGrew = true; }
                }

                if (_caretOn && IsKeyboardFocused && line == caretLine)
                {
                    DrawCaret(dc, renderer, v, x0, y);
                }
            }

            // Bounded to what has recently been on screen: scrolling a large file
            // end to end would otherwise leave one entry per line visited, and
            // each of them holds a FormattedText.
            if (_lineCache.Count > 512) PruneLineCache(first, last);

            if (widthGrew) InvalidateScrollInfo();
        }

        private void PruneLineCache(int first, int last)
        {
            List<int> stale = new List<int>();

            foreach (int line in _lineCache.Keys)
            {
                if (line < first || line > last) stale.Add(line);
            }

            foreach (int line in stale) _lineCache.Remove(line);
        }

        // Clipped to the viewport before it is handed over. A selection running
        // the length of a minified line is millions of pixels wide, and none of it
        // past the edge was going to be seen.
        private void DrawSpan(DrawingContext dc, Brush brush, Pen pen,
            double left, double right, double x0, double y, double minimumWidth)
        {
            double lo = -x0;
            double hi = lo + _viewport.Width;

            if (right < lo || left > hi) return;

            if (left < lo) left = lo;
            if (right > hi) right = hi;

            dc.DrawRectangle(brush, pen,
                new Rect(x0 + left, y, Math.Max(minimumWidth, right - left), Renderer.LineHeight));
        }

        private void DrawSelection(DrawingContext dc, TextLineRenderer renderer, VisibleLine v,
            int selectionStart, int selectionEnd, double x0, double y)
        {
            int lineEnd = v.Start + v.Length;
            if (selectionEnd <= v.Start || selectionStart > lineEnd) return;

            int from = Math.Max(selectionStart, v.Start) - v.Start;
            int to = Math.Min(selectionEnd, lineEnd) - v.Start;

            double left = ColumnX(renderer, v, ColumnOf(v, from));
            double right = ColumnX(renderer, v, ColumnOf(v, to));

            // A selection that runs past the end of this line covers the break as
            // well, and showing that as a sliver past the last character is what
            // makes a multi-line selection read as continuous.
            if (selectionEnd > lineEnd && v.Line + 1 < _document.LineCount)
            {
                right += renderer.CharacterWidth / 2;
            }

            DrawSpan(dc, SelectionBrush, null, left, right, x0, y, 1);
        }

        private void DrawMatches(DrawingContext dc, TextLineRenderer renderer, VisibleLine v, double x0, double y)
        {
            if (_matches == null || _matches.Count == 0 || _matchLength <= 0) return;

            // The list is ascending, so the first match that could touch the part
            // of the line being drawn is found rather than scanned to - and it is
            // the drawn part, not the whole line. A common term in a minified file
            // puts millions of matches on one line, and walking the ones off to
            // the left of the viewport would make scrolling stutter even with the
            // line itself already cut down.
            int windowStart = v.Start + (v.Uniform ? v.FirstColumn : 0);
            int windowEnd = windowStart + v.Text.Length;

            int i = _matches.BinarySearch(windowStart - _matchLength);
            if (i < 0) i = ~i;

            int lineEnd = v.Start + v.Length;

            for (; i < _matches.Count; i++)
            {
                int start = _matches[i];
                if (start > windowEnd) break;

                bool current = i == _currentMatch;
                if (!current && !_highlightAll) continue;

                int end = start + _matchLength;
                if (end < v.Start) continue;

                int from = Math.Max(start, v.Start) - v.Start;
                int to = Math.Min(end, lineEnd) - v.Start;
                if (to <= from) continue;

                double left = ColumnX(renderer, v, ColumnOf(v, from));
                double right = ColumnX(renderer, v, ColumnOf(v, to));

                DrawSpan(dc, current ? CurrentMatchBrush : MatchBrush,
                    current && CurrentMatchStroke != null ? CurrentMatchPen() : null, left, right, x0, y, 2);
            }
        }

        private Pen _currentMatchPen;

        // Built once and frozen, rather than one per match per line per frame.
        private Pen CurrentMatchPen()
        {
            if (_currentMatchPen == null)
            {
                _currentMatchPen = new Pen(CurrentMatchStroke, 1.5);
                _currentMatchPen.Freeze();
            }

            return _currentMatchPen;
        }

        private void DrawCaret(DrawingContext dc, TextLineRenderer renderer, VisibleLine v, double x0, double y)
        {
            double x = x0 + ColumnX(renderer, v, ColumnOf(v, _caretOffset - v.Start));

            // Snapped to the pixel grid: a caret on a fractional boundary is drawn
            // across two columns of pixels and reads as blurred.
            x = Math.Round(x);

            dc.DrawRectangle(CaretBrush ?? Foreground, null, new Rect(x, y, 1, renderer.LineHeight));
        }

        // ---- offsets and points ---------------------------------------------

        public Point GetPointForOffset(int offset)
        {
            TextLineRenderer renderer = Renderer;

            int line = _document.GetLineFromOffset(offset);
            VisibleLine v = GetVisibleLine(line);

            return new Point(
                Padding.Left - _horizontalOffset + ColumnX(renderer, v, ColumnOf(v, offset - v.Start)),
                line * renderer.LineHeight - _verticalOffset + Padding.Top);
        }

        public int GetOffsetFromPoint(Point point)
        {
            TextLineRenderer renderer = Renderer;

            int line = (int)((point.Y - Padding.Top + _verticalOffset) / renderer.LineHeight);
            line = Clamp(line, 0, _document.LineCount - 1);

            VisibleLine v = GetVisibleLine(line);
            double x = point.X - Padding.Left + _horizontalOffset;

            // The arithmetic is absolute, so a click far to the right of the slice
            // still lands on the right character - which is the whole reason a
            // minified line can be clicked in at all. The measuring path can only
            // answer for what it was given, and past that it clamps to the cut.
            if (v.Uniform) return v.Start + Clamp((int)Math.Round(x / renderer.CharacterWidth), 0, v.Length);

            return v.Start + TextLineRenderer.ToOffset(v.Text, renderer.ColumnAt(v.Expanded, x, false));
        }

        // ---- scrolling -------------------------------------------------------

        public ScrollViewer ScrollOwner { get; set; }
        public bool CanHorizontallyScroll { get; set; }
        public bool CanVerticallyScroll { get; set; }

        public double ExtentWidth
        {
            get { return Math.Max(_extentWidth, _viewport.Width); }
        }

        // The number this whole control exists to be able to answer without
        // laying anything out.
        public double ExtentHeight
        {
            get { return _document.LineCount * LineHeight + Padding.Top + Padding.Bottom; }
        }

        public double ViewportWidth { get { return _viewport.Width; } }
        public double ViewportHeight { get { return _viewport.Height; } }
        public double HorizontalOffset { get { return _horizontalOffset; } }
        public double VerticalOffset { get { return _verticalOffset; } }

        public void LineUp() { SetVerticalOffset(_verticalOffset - LineHeight); }
        public void LineDown() { SetVerticalOffset(_verticalOffset + LineHeight); }
        public void LineLeft() { SetHorizontalOffset(_horizontalOffset - Renderer.CharacterWidth * 4); }
        public void LineRight() { SetHorizontalOffset(_horizontalOffset + Renderer.CharacterWidth * 4); }

        public void PageUp() { SetVerticalOffset(_verticalOffset - _viewport.Height); }
        public void PageDown() { SetVerticalOffset(_verticalOffset + _viewport.Height); }
        public void PageLeft() { SetHorizontalOffset(_horizontalOffset - _viewport.Width); }
        public void PageRight() { SetHorizontalOffset(_horizontalOffset + _viewport.Width); }

        public void MouseWheelUp() { SetVerticalOffset(_verticalOffset - LineHeight * SystemParameters.WheelScrollLines); }
        public void MouseWheelDown() { SetVerticalOffset(_verticalOffset + LineHeight * SystemParameters.WheelScrollLines); }
        public void MouseWheelLeft() { LineLeft(); }
        public void MouseWheelRight() { LineRight(); }

        public void SetHorizontalOffset(double offset)
        {
            double clamped = Clamp(offset, 0, Math.Max(0, ExtentWidth - _viewport.Width));
            if (clamped == _horizontalOffset) return;

            _horizontalOffset = clamped;

            // Every long line was cut to a slice chosen for the old offset.
            InvalidateLineCache();

            InvalidateScrollInfo();
            InvalidateVisual();
            RaiseViewChanged();
        }

        public void SetVerticalOffset(double offset)
        {
            double clamped = Clamp(offset, 0, Math.Max(0, ExtentHeight - _viewport.Height));
            if (clamped == _verticalOffset) return;

            _verticalOffset = clamped;
            InvalidateScrollInfo();
            InvalidateVisual();
            RaiseViewChanged();
        }

        private static double Clamp(double value, double low, double high)
        {
            return value < low ? low : value > high ? high : value;
        }

        private void CoerceOffsets()
        {
            SetVerticalOffset(_verticalOffset);
            SetHorizontalOffset(_horizontalOffset);
        }

        public Rect MakeVisible(Visual visual, Rect rectangle)
        {
            return rectangle;
        }

        private void InvalidateScrollInfo()
        {
            if (ScrollOwner != null) ScrollOwner.InvalidateScrollInfo();
        }

        public void ScrollToLine(int line)
        {
            line = Clamp(line, 0, _document.LineCount - 1);
            SetVerticalOffset(line * LineHeight);
        }

        // Only moves when the target is off screen, so stepping between find
        // matches that already share the viewport does not yank the view around.
        public void ScrollIntoView(int offset)
        {
            int line = _document.GetLineFromOffset(offset);

            if (line >= FirstVisibleLine && line <= LastVisibleLine - 1) return;

            // Landed a third of the way down rather than at the very top, so
            // there is context above the thing being looked at.
            SetVerticalOffset(Math.Max(0, (line - _viewport.Height / LineHeight / 3) * LineHeight));
        }

        private void BringCaretIntoView()
        {
            if (_viewport.Height <= 0) return;

            TextLineRenderer renderer = Renderer;
            int line = _document.GetLineFromOffset(_caretOffset);

            // Same expression the renderer uses, padding included, so the caret
            // is brought to where it will actually be drawn rather than to a
            // position a padding's worth above it.
            double top = line * renderer.LineHeight + Padding.Top;
            double bottom = top + renderer.LineHeight;

            if (top < _verticalOffset) SetVerticalOffset(top);
            else if (bottom > _verticalOffset + _viewport.Height) SetVerticalOffset(bottom - _viewport.Height);

            VisibleLine v = GetVisibleLine(line);
            double x = ColumnX(renderer, v, ColumnOf(v, _caretOffset - v.Start));

            if (x < _horizontalOffset) SetHorizontalOffset(x - renderer.CharacterWidth * 4);
            else if (x > _horizontalOffset + _viewport.Width - Padding.Left - Padding.Right)
            {
                SetHorizontalOffset(x - _viewport.Width + Padding.Left + Padding.Right + renderer.CharacterWidth * 4);
            }
        }
    }
}
