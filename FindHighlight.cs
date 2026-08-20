using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace PureNote
{
    public partial class MainWindow
    {
        // Rectangles are reused rather than rebuilt. Scrolling fires a stream of
        // these events, and tearing down and re-creating a canvas full of shapes
        // on each one is a steady churn of allocations and visual-tree edits.
        private readonly List<Rectangle> _highlightPool = new List<Rectangle>();
        private int _highlightsUsed;
        private bool _highlightRefreshQueued;

        private void Editor_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            QueueHighlightRefresh();
            LineNumbers_Invalidate();
        }

        private void Editor_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            QueueHighlightRefresh();
            LineNumbers_Invalidate();
        }

        // A drag-select or a wheel spin produces many scroll events between two
        // frames; collapsing them into one repaint at render time means the work
        // happens once per frame instead of once per event.
        private void QueueHighlightRefresh()
        {
            if (_highlightRefreshQueued) return;

            _highlightRefreshQueued = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
            {
                _highlightRefreshQueued = false;
                RefreshHighlights();
            }));
        }

        private void RefreshHighlights()
        {
            _highlightsUsed = 0;

            // Checked before touching the canvas: these events fire constantly, and
            // when Find has never been opened there is nothing to draw.
            if (!FindPopup.IsOpen || _findMatches.Count == 0)
            {
                HideUnusedHighlights();
                return;
            }

            int length = FindTextBox.Text.Length;
            EditorLineLayout layout;

            if (length == 0 || !EditorLineLayout.TryCapture(Editor, out layout))
            {
                HideUnusedHighlights();
                return;
            }

            if (HighlightAllCheck.IsChecked == true)
            {
                AddAllMatchHighlights(layout, length);
            }

            if (_findCurrentIndex >= 0)
            {
                AddHighlight(layout, _findMatches[_findCurrentIndex], length, isCurrent: true);
            }

            HideUnusedHighlights();
        }

        private void ClearHighlights()
        {
            _highlightsUsed = 0;
            HideUnusedHighlights();
        }

        private void AddAllMatchHighlights(EditorLineLayout layout, int length)
        {
            if (!TryGetVisibleCharacterRange(layout, out int visibleStart, out int visibleEnd)) return;

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

                AddHighlight(layout, start, length, isCurrent: false);
            }
        }

        private void AddHighlight(EditorLineLayout layout, int start, int length, bool isCurrent)
        {
            if (start < 0 || start + length > _rawLength) return;

            Rect startRect = Editor.GetRectFromCharacterIndex(start);
            Rect endRect = Editor.GetRectFromCharacterIndex(start + length);

            if (double.IsInfinity(startRect.X) || double.IsInfinity(endRect.X)) return;

            // Only the two X values are taken from these rects — vertically they
            // carry the same drift EditorLineLayout exists to correct, so the row
            // comes from the layout instead. The Ys are still worth comparing to
            // each other: both drift alike, so a difference means the match runs
            // across a line break and there is no single row to draw it on.
            if (Math.Abs(startRect.Y - endRect.Y) > 1.0) return;

            int line = Editor.GetLineIndexFromCharacterIndex(start);
            if (line < 0) return;

            Rectangle rect = TakeHighlight();
            rect.Width = Math.Max(2, endRect.X - startRect.X);
            rect.Height = layout.LineHeight;
            rect.Fill = isCurrent ? Theme.CurrentMatchFill : Theme.AllMatches;
            rect.Stroke = isCurrent ? Theme.CurrentMatchStroke : null;
            rect.StrokeThickness = isCurrent ? 1.5 : 0;

            Canvas.SetLeft(rect, startRect.X);
            Canvas.SetTop(rect, layout.YForLine(line));
        }

        private Rectangle TakeHighlight()
        {
            if (_highlightsUsed == _highlightPool.Count)
            {
                Rectangle created = new Rectangle { RadiusX = 3, RadiusY = 3 };
                _highlightPool.Add(created);
                HighlightLayer.Children.Add(created);
            }

            Rectangle rect = _highlightPool[_highlightsUsed++];
            rect.Visibility = Visibility.Visible;
            return rect;
        }

        // Collapsed rather than removed: the pool settles at however many matches
        // fit on screen, so after the first few repaints nothing is allocated.
        private void HideUnusedHighlights()
        {
            for (int i = _highlightsUsed; i < _highlightPool.Count; i++)
            {
                if (_highlightPool[i].Visibility == Visibility.Visible)
                {
                    _highlightPool[i].Visibility = Visibility.Collapsed;
                }
            }
        }

        // False when the visible range can't be determined. Callers must skip
        // highlighting entirely in that case: falling back to the whole document
        // would build a Rectangle per match, which on a large file with a common
        // search term means tens of thousands of visuals in one canvas.
        private bool TryGetVisibleCharacterRange(EditorLineLayout layout, out int start, out int end)
        {
            start = 0;
            end = 0;

            int lineCount = Editor.LineCount;
            if (lineCount < 1) return false;

            int lastLine = layout.LastLineIn(Editor.ViewportHeight, lineCount);
            if (lastLine < layout.TopLine) return false;

            start = Editor.GetCharacterIndexFromLineIndex(layout.TopLine);
            end = Editor.GetCharacterIndexFromLineIndex(lastLine) + Editor.GetLineLength(lastLine);
            return true;
        }
    }
}
