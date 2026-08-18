using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;

namespace PureNote
{
    public partial class MainWindow
    {
        private void Editor_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            RefreshHighlights();
        }

        // Vertical scrolling now happens on EditorScroll, not on Editor itself.
        private void EditorScroll_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            RefreshHighlights();
        }

        private void Editor_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            RefreshHighlights();
        }

        private void RefreshHighlights()
        {
            // Checked before touching the canvas: these events fire constantly, and
            // when Find has never been opened there is nothing to clear.
            if (!FindPopup.IsOpen)
            {
                if (HighlightLayer.Children.Count > 0) HighlightLayer.Children.Clear();
                return;
            }

            HighlightLayer.Children.Clear();

            if (_findMatches.Count == 0) return;

            int length = FindTextBox.Text.Length;
            if (length == 0) return;

            if (HighlightAllCheck.IsChecked == true)
            {
                AddAllMatchHighlights(length);
            }

            if (_findCurrentIndex >= 0)
            {
                AddHighlight(_findMatches[_findCurrentIndex], length, isCurrent: true);
            }
        }

        private void AddAllMatchHighlights(int length)
        {
            if (!TryGetVisibleCharacterRange(out int visibleStart, out int visibleEnd)) return;

            // Matches are collected in ascending order, so jump straight to the
            // first one that can be on screen. Searching a common substring in a
            // large file yields tens of thousands of matches, and walking past all
            // of them from index 0 on every scroll event is what made scrolling
            // stutter once the view was far down the document.
            int i = _findMatches.BinarySearch(visibleStart - length);
            if (i < 0) i = ~i;

            for (; i < _findMatches.Count; i++)
            {
                if (i == _findCurrentIndex) continue;

                int start = _findMatches[i];
                if (start > visibleEnd) break;

                AddHighlight(start, length, isCurrent: false);
            }
        }

        private void AddHighlight(int start, int length, bool isCurrent)
        {
            if (start < 0 || start + length > Editor.Text.Length) return;

            Rect startRect = Editor.GetRectFromCharacterIndex(start);
            Rect endRect = Editor.GetRectFromCharacterIndex(start + length);

            if (double.IsInfinity(startRect.X) || double.IsInfinity(endRect.X)) return;

            if (Math.Abs(startRect.Y - endRect.Y) > 1.0) return;

            Rectangle rect = new Rectangle
            {
                Width = Math.Max(2, endRect.X - startRect.X),
                Height = Math.Max(startRect.Height, endRect.Height),
                RadiusX = 3,
                RadiusY = 3,
                Fill = isCurrent ? Theme.CurrentMatchFill : Theme.AllMatches,
                Stroke = isCurrent ? Theme.CurrentMatchStroke : null,
                StrokeThickness = isCurrent ? 1.5 : 0
            };

            Canvas.SetLeft(rect, startRect.X);
            Canvas.SetTop(rect, Math.Min(startRect.Y, endRect.Y));

            HighlightLayer.Children.Add(rect);
        }

        // False when the visible range can't be determined. Callers must skip
        // highlighting entirely in that case: falling back to the whole document
        // would build a Rectangle per match, which on a large file with a common
        // search term means tens of thousands of visuals in one canvas.
        //
        // Editor.GetFirstVisibleLineIndex/GetLastVisibleLineIndex only make sense
        // when Editor scrolls itself; now that EditorScroll does the scrolling,
        // Editor renders its full, unclipped height, so those two would just
        // report the entire document as "visible". Ask EditorScroll's own
        // viewport instead, translated to characters via the same point Editor
        // sits at (Editor never scrolls vertically on its own, so its local Y
        // coordinates line up 1:1 with EditorScroll's content coordinates).
        private bool TryGetVisibleCharacterRange(out int start, out int end)
        {
            start = 0;
            end = 0;

            double viewTop = EditorScroll.VerticalOffset;
            double viewBottom = viewTop + EditorScroll.ViewportHeight;

            int startIndex = Editor.GetCharacterIndexFromPoint(new Point(0, viewTop), true);
            int endIndex = Editor.GetCharacterIndexFromPoint(new Point(0, viewBottom), true);

            if (startIndex < 0 || endIndex < 0) return false;

            int firstLine = Editor.GetLineIndexFromCharacterIndex(startIndex);
            int lastLine = Editor.GetLineIndexFromCharacterIndex(endIndex);

            if (firstLine < 0 || lastLine < firstLine) return false;

            start = Editor.GetCharacterIndexFromLineIndex(firstLine);
            end = Editor.GetCharacterIndexFromLineIndex(lastLine) + Editor.GetLineLength(lastLine);
            return true;
        }
    }
}
