using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

namespace PureNote
{
    internal sealed partial class TextView : FrameworkElement, IScrollInfo
    {
        private TextDocument _document = new TextDocument();
        private TextLineRenderer _renderer;

        private double _horizontalOffset;
        private double _verticalOffset;
        private Size _viewport;

        private double _extentWidth;
        private int _longestLineChars;

        private int _caretOffset;
        private int _selectionAnchor;

        private int _preferredColumn = -1;
        private bool _preferredColumnValid;

        private bool _caretOn = true;
        private DispatcherTimer _caretTimer;

        public TextView()
        {
            Focusable = true;
            FocusVisualStyle = null;

            ClipToBounds = true;

            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);

            InstallCommands();
        }

        public event EventHandler DocumentChanged;
        public event EventHandler CaretChanged;

        public event EventHandler EditRefused;

        public event EventHandler ViewChanged;

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


        protected override Size MeasureOverride(Size availableSize)
        {
            Size viewport = new Size(
                double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width,
                double.IsInfinity(availableSize.Height) ? 0 : availableSize.Height);

            if (viewport != _viewport)
            {
                _viewport = viewport;

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

        public int FirstVisibleLine
        {
            get { return Math.Max(0, (int)((_verticalOffset - Padding.Top) / LineHeight)); }
        }

        public int LastVisibleLine
        {
            get
            {
                int last = FirstVisibleLine + (int)Math.Ceiling(_viewport.Height / LineHeight) + 1;
                return Math.Min(last, _document.LineCount - 1);
            }
        }

        private sealed class VisibleLine
        {
            public int Line;
            public int Start;
            public int Length;
            public string Text;
            public string Expanded;
            public int FirstColumn;
            public bool Uniform;
            public FormattedText Formatted;
            public double Width;
        }

        private readonly Dictionary<int, VisibleLine> _lineCache = new Dictionary<int, VisibleLine>();

        private const int SliceMargin = 8;

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

            v.Uniform = renderer.IsMonospaced && IsUniformRun(v.Start, Math.Min(v.Length, firstColumn + columns));

            if (v.Uniform)
            {
                int from = Math.Min(firstColumn, v.Length);
                int take = Math.Min(columns, v.Length - from);

                v.FirstColumn = from;
                v.Text = _document.GetText(v.Start + from, take);
                v.Expanded = v.Text;
                v.Width = v.Length * renderer.CharacterWidth;
            }
            else
            {
                int take = Math.Min(v.Length, renderer.MaxFormattable);

                v.FirstColumn = 0;
                v.Text = _document.GetText(v.Start, take);
                v.Expanded = TextLineRenderer.Expand(v.Text);

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

        private bool IsUniformRun(int start, int count)
        {
            TextLineRenderer renderer = Renderer;

            for (int i = 0; i < count; i++)
            {
                if (!renderer.IsUniformWidth(_document[start + i])) return false;
            }

            return true;
        }

        private static double ColumnX(TextLineRenderer renderer, VisibleLine v, int column)
        {
            if (v.Uniform) return column * renderer.CharacterWidth;

            return renderer.WidthOf(v.Expanded, column, false);
        }

        private static int ColumnOf(VisibleLine v, int offsetInLine)
        {
            if (v.Uniform) return offsetInLine;

            return TextLineRenderer.ToColumn(v.Text, Math.Min(offsetInLine, v.Text.Length));
        }


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

                    double lineWidth = v.Width + Padding.Left + Padding.Right;
                    if (lineWidth > _extentWidth) { _extentWidth = lineWidth; widthGrew = true; }
                }

                if (_caretOn && IsKeyboardFocused && line == caretLine)
                {
                    DrawCaret(dc, renderer, v, x0, y);
                }
            }

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

            if (selectionEnd > lineEnd && v.Line + 1 < _document.LineCount)
            {
                right += renderer.CharacterWidth / 2;
            }

            DrawSpan(dc, SelectionBrush, null, left, right, x0, y, 1);
        }

        private void DrawMatches(DrawingContext dc, TextLineRenderer renderer, VisibleLine v, double x0, double y)
        {
            if (_matches == null || _matches.Count == 0 || _matchLength <= 0) return;

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

            x = Math.Round(x);

            dc.DrawRectangle(CaretBrush ?? Foreground, null, new Rect(x, y, 1, renderer.LineHeight));
        }


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

            if (v.Uniform) return v.Start + Clamp((int)Math.Round(x / renderer.CharacterWidth), 0, v.Length);

            return v.Start + TextLineRenderer.ToOffset(v.Text, renderer.ColumnAt(v.Expanded, x, false));
        }


        public ScrollViewer ScrollOwner { get; set; }
        public bool CanHorizontallyScroll { get; set; }
        public bool CanVerticallyScroll { get; set; }

        public double ExtentWidth
        {
            get { return Math.Max(_extentWidth, _viewport.Width); }
        }

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

        public void ScrollIntoView(int offset)
        {
            int line = _document.GetLineFromOffset(offset);

            if (line >= FirstVisibleLine && line <= LastVisibleLine - 1) return;

            SetVerticalOffset(Math.Max(0, (line - _viewport.Height / LineHeight / 3) * LineHeight));
        }

        private void BringCaretIntoView()
        {
            if (_viewport.Height <= 0) return;

            TextLineRenderer renderer = Renderer;
            int line = _document.GetLineFromOffset(_caretOffset);

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
