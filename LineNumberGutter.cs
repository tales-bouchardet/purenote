using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace PureNote
{
    // Draws only the line numbers that are currently on screen.
    //
    // The gutter used to be a second TextBox holding one number per line of the
    // whole document, parked next to the editor inside a shared ScrollViewer.
    // Neither box scrolled itself, so WPF had to format and render every line of
    // both on every layout pass — the cost that made pasting or selecting a large
    // block freeze the window. Now the editor scrolls itself (and so virtualises
    // again) and this element paints the twenty-odd numbers that are visible.
    internal sealed class LineNumberGutter : FrameworkElement
    {
        private const double HorizontalPadding = 14;
        private const int MinimumDigits = 2;

        private TextBox _editor;
        private FontFamily _typefaceSource;
        private Typeface _typeface;
        private double _pixelsPerDip = 1;
        private int _digits = MinimumDigits;
        private bool _repaintQueued;

        public Brush Foreground { get; set; }
        public Brush Background { get; set; }
        public Brush DividerBrush { get; set; }

        public void Attach(TextBox editor)
        {
            _editor = editor;
        }

        // The width is driven by the document's total line count, not by the
        // widest number on screen: sizing it to the visible range would make the
        // gutter jump wider and narrower as the user scrolls.
        public void SetLineCount(int lineCount)
        {
            int digits = Math.Max(MinimumDigits, lineCount.ToString(CultureInfo.InvariantCulture).Length);
            if (digits == _digits) return;

            _digits = digits;
            InvalidateMeasure();
        }

        // Called for every scroll, resize and edit. InvalidateVisual only marks
        // the element dirty, so a burst of events still costs a single repaint.
        public void Refresh()
        {
            InvalidateVisual();
        }

        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
        {
            base.OnDpiChanged(oldDpi, newDpi);
            _pixelsPerDip = newDpi.PixelsPerDip;
            InvalidateMeasure();
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            // Read here rather than on attach: the element is in the visual tree
            // by the time it is measured, so this picks up the monitor the window
            // actually opened on instead of falling back to the primary one.
            _pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            // Height is left to the parent Grid: the gutter always spans the same
            // row as the editor, which is what keeps their origins aligned.
            return new Size(Format(new string('9', _digits)).Width + HorizontalPadding * 2, 0);
        }

        protected override void OnRender(DrawingContext dc)
        {
            double width = RenderSize.Width;
            double height = RenderSize.Height;

            if (Background != null)
            {
                dc.DrawRectangle(Background, null, new Rect(0, 0, width, height));
            }

            if (DividerBrush != null)
            {
                dc.DrawRectangle(DividerBrush, null, new Rect(width - 1, 0, 1, height));
            }

            if (_editor == null || Foreground == null) return;

            EditorLineLayout layout;
            if (!EditorLineLayout.TryCapture(_editor, out layout))
            {
                RepaintWhenLayoutSettles();
                return;
            }

            int lineCount = _editor.LineCount;

            for (int row = 0; ; row++)
            {
                int line = layout.TopLine + row;
                if (line > lineCount - 1) break;

                double y = layout.TopY + row * layout.LineHeight;
                if (y > height) break;

                FormattedText text = Format((line + 1).ToString(CultureInfo.InvariantCulture));
                dc.DrawText(text, new Point(width - HorizontalPadding - text.Width, y));
            }
        }

        private void RepaintWhenLayoutSettles()
        {
            if (_repaintQueued) return;

            _repaintQueued = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                _repaintQueued = false;
                InvalidateVisual();
            }));
        }

        private FormattedText Format(string value)
        {
            return new FormattedText(
                value,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                ResolveTypeface(),
                _editor != null ? _editor.FontSize : 14,
                Foreground ?? Brushes.Gray,
                _pixelsPerDip);
        }

        private Typeface ResolveTypeface()
        {
            FontFamily family = _editor != null ? _editor.FontFamily : SystemFonts.MessageFontFamily;

            if (_typeface == null || !ReferenceEquals(family, _typefaceSource))
            {
                _typefaceSource = family;
                _typeface = new Typeface(family, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            }

            return _typeface;
        }
    }
}
