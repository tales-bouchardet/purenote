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

        private void Editor_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            RefreshHighlights();
        }

        private void RefreshHighlights()
        {
            HighlightLayer.Children.Clear();

            if (!FindPopup.IsOpen) return;
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
            GetVisibleCharacterRange(out int visibleStart, out int visibleEnd);

            for (int i = 0; i < _findMatches.Count; i++)
            {
                if (i == _findCurrentIndex) continue;

                int start = _findMatches[i];
                if (start + length < visibleStart) continue;
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

        private void GetVisibleCharacterRange(out int start, out int end)
        {
            start = 0;
            end = Editor.Text.Length;

            int firstLine = Editor.GetFirstVisibleLineIndex();
            int lastLine = Editor.GetLastVisibleLineIndex();

            if (firstLine < 0 || lastLine < firstLine || lastLine >= Editor.LineCount) return;

            start = Editor.GetCharacterIndexFromLineIndex(firstLine);
            end = Editor.GetCharacterIndexFromLineIndex(lastLine) + Editor.GetLineLength(lastLine);
        }
    }
}
