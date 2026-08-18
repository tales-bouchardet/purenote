using System;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PureNote
{
    public partial class MainWindow
    {
        private const double LineNumberGutterPadding = 14;

        private bool _lineNumbersEnabled;
        private int _lineNumbersRenderedCount = -1;

        private void LineNumbersMenuItem_Click(object sender, RoutedEventArgs e)
        {
            _lineNumbersEnabled = LineNumbersMenuItem.IsChecked;
            LineNumberLayer.Visibility = _lineNumbersEnabled ? Visibility.Visible : Visibility.Collapsed;
            LineNumberDivider.Visibility = _lineNumbersEnabled ? Visibility.Visible : Visibility.Collapsed;

            if (_lineNumbersEnabled)
            {
                RefreshLineNumbers();
            }
            else
            {
                LineNumberColumn.Width = new GridLength(0);

                // Forget the rendered count too: it is what gates recomputing the
                // gutter width, and without this a re-enable with an unchanged line
                // count would skip that and leave the column stuck at zero width.
                _lineNumbersRenderedCount = -1;
            }
        }

        private void LineNumbers_Editor_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_lineNumbersEnabled) return;
            RefreshLineNumbers();
        }

        private void LineNumbers_Editor_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!_lineNumbersEnabled) return;
            SyncGutterHeight();
        }

        // Gutter and editor are two TextBoxes in the same scrolled Grid, sharing
        // font/size/padding, so WPF's own layout keeps every number pinned to its
        // line for free — no per-line position math to get wrong. The one gap:
        // Editor's own horizontal scrollbar (shown once a line overflows the
        // viewport) adds height only to Editor, not to the gutter, which would
        // push the last few line numbers out of step with their lines. Mirror
        // that height as bottom padding so both text boxes still end level.
        private void SyncGutterHeight()
        {
            double reserve = Editor.ExtentWidth > Editor.ViewportWidth
                ? SystemParameters.HorizontalScrollBarHeight
                : 0;

            Thickness padding = LineNumberLayer.Padding;
            if (Math.Abs(padding.Bottom - reserve) > 0.5)
            {
                LineNumberLayer.Padding = new Thickness(padding.Left, padding.Top, padding.Right, reserve);
            }
        }

        private void RefreshLineNumbers()
        {
            int lineCount = CountLines(Editor.Text);

            if (lineCount != _lineNumbersRenderedCount)
            {
                _lineNumbersRenderedCount = lineCount;

                StringBuilder builder = new StringBuilder();
                for (int i = 1; i <= lineCount; i++)
                {
                    if (i > 1) builder.Append('\n');
                    builder.Append(i.ToString(CultureInfo.InvariantCulture));
                }

                LineNumberLayer.Text = builder.ToString();
                UpdateLineNumberGutterWidth(lineCount);
            }

            SyncGutterHeight();
        }

        private void UpdateLineNumberGutterWidth(int lineCount)
        {
            int digits = Math.Max(2, lineCount.ToString(CultureInfo.InvariantCulture).Length);

            FormattedText formatted = new FormattedText(
                new string('9', digits),
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(Editor.FontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                Editor.FontSize,
                Brushes.Black,
                VisualTreeHelper.GetDpi(Editor).PixelsPerDip);

            LineNumberColumn.Width = new GridLength(formatted.Width + LineNumberGutterPadding * 2);
        }
    }
}
