using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace PureNote
{
    internal sealed class LineNumberGutter : FrameworkElement
    {
        private const double HorizontalPadding = 14;
        private const int MinimumDigits = 2;

        private TextView _editor;
        private FontFamily _typefaceSource;
        private Typeface _typeface;
        private double _pixelsPerDip = 1;
        private int _digits = MinimumDigits;

        public Brush Foreground { get; set; }
        public Brush Background { get; set; }
        public Brush DividerBrush { get; set; }

        public void Attach(TextView editor)
        {
            _editor = editor;
        }

        public void SetLineCount(int lineCount)
        {
            int digits = Math.Max(MinimumDigits, lineCount.ToString(CultureInfo.InvariantCulture).Length);
            if (digits == _digits) return;

            _digits = digits;
            InvalidateMeasure();
        }

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
            _pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            return new Size(Format(new string('9', _digits)).Width + HorizontalPadding * 2, 0);
        }

        protected override void OnRender(DrawingContext dc)
        {
            double width = RenderSize.Width;
            double height = RenderSize.Height;

            if (Background != null) dc.DrawRectangle(Background, null, new Rect(0, 0, width, height));

            if (DividerBrush != null) dc.DrawRectangle(DividerBrush, null, new Rect(width - 1, 0, 1, height));

            if (_editor == null || Foreground == null) return;

            double lineHeight = _editor.LineHeight;
            if (lineHeight <= 0) return;

            int first = _editor.FirstVisibleLine;
            int last = _editor.LastVisibleLine;

            double top = _editor.EditorPaddingTop - _editor.VerticalOffset;

            for (int line = first; line <= last; line++)
            {
                double y = top + line * lineHeight;
                if (y > height) break;

                FormattedText text = Format((line + 1).ToString(CultureInfo.InvariantCulture));
                dc.DrawText(text, new Point(width - HorizontalPadding - text.Width, y));
            }
        }

        private FormattedText Format(string value)
        {
            return new FormattedText(
                value,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                ResolveTypeface(),
                _editor != null && _editor.FontSize > 0 ? _editor.FontSize : 14,
                Foreground ?? Brushes.Gray,
                _pixelsPerDip);
        }

        private Typeface ResolveTypeface()
        {
            FontFamily family = _editor != null && _editor.FontFamily != null
                ? _editor.FontFamily
                : SystemFonts.MessageFontFamily;

            if (_typeface == null || !ReferenceEquals(family, _typefaceSource))
            {
                _typefaceSource = family;
                _typeface = new Typeface(family, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            }

            return _typeface;
        }
    }
}
