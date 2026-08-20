using System;
using System.Windows;
using System.Windows.Controls;

namespace PureNote
{
    // Where the editor's visible lines actually sit on screen.
    //
    // TextBox offers three ways to ask, and two of them lie on a large document.
    // GetFirstVisibleLineIndex and GetRectFromCharacterIndex share a scroll model
    // whose per-line rounding accumulates against the text the control really
    // paints: dead-on at the top of the file, seven lines out at the middle of a
    // 200,000-line one and fourteen out at the end. Hit-testing is the odd one
    // out, and it is the one that agrees with the pixels — it is what decides
    // where a mouse click lands. So identity and position both come from it here,
    // and everything drawn over the editor measures against this.
    internal struct EditorLineLayout
    {
        // Enough halvings to place a line within a third of a pixel of its row.
        private const int BoundarySteps = 7;

        public int TopLine;
        public double TopY;
        public double LineHeight;

        public double YForLine(int line)
        {
            return TopY + (line - TopLine) * LineHeight;
        }

        public int LastLineIn(double viewportHeight, int lineCount)
        {
            int last = TopLine + (int)Math.Ceiling((viewportHeight - TopY) / LineHeight);
            return last > lineCount - 1 ? lineCount - 1 : last;
        }

        public static bool TryCapture(TextBox editor, out EditorLineLayout layout)
        {
            layout = new EditorLineLayout();

            try
            {
                return TryMeasure(editor, ref layout);
            }
            catch (ArgumentOutOfRangeException)
            {
                // The editor re-measured mid-read, so the indices it handed out no
                // longer address anything. Callers redraw on the next pass.
                return false;
            }
        }

        private static bool TryMeasure(TextBox editor, ref EditorLineLayout layout)
        {
            int lineCount = editor.LineCount;
            if (lineCount < 1) return false;

            int topIndex = editor.GetCharacterIndexFromPoint(new Point(2, 0), true);
            if (topIndex < 0) return false;

            int topLine = editor.GetLineIndexFromCharacterIndex(topIndex);
            if (topLine < 0 || topLine > lineCount - 1) return false;

            // Only the height is taken from this rect. Its Y is subject to the
            // drift described above; the row's true position is measured below.
            Rect probe = editor.GetRectFromCharacterIndex(topIndex);
            double lineHeight = probe.Height;
            if (lineHeight <= 0 || double.IsInfinity(lineHeight)) return false;

            double topY;

            if (LineAt(editor, lineHeight) > topLine)
            {
                // The top row is normally cut off by the viewport edge. Bisect for
                // the y where the row below it begins, then step back one line.
                double above = 0;
                double below = lineHeight;

                for (int i = 0; i < BoundarySteps; i++)
                {
                    double middle = (above + below) / 2;
                    if (LineAt(editor, middle) > topLine) below = middle; else above = middle;
                }

                topY = below - lineHeight;
            }
            else
            {
                // No row starts within a line height of the top, so there is no
                // boundary to measure against — the editor is scrolled to the top
                // or showing its last line, and the drift is nil either way.
                if (probe.IsEmpty || double.IsInfinity(probe.Y)) return false;
                topY = probe.Y;
            }

            layout.TopLine = topLine;
            layout.TopY = topY;
            layout.LineHeight = lineHeight;
            return true;
        }

        private static int LineAt(TextBox editor, double y)
        {
            // x is kept just inside the left edge so the probe stays on the row
            // regardless of how far the editor is scrolled sideways.
            int index = editor.GetCharacterIndexFromPoint(new Point(2, y), true);
            return index < 0 ? -1 : editor.GetLineIndexFromCharacterIndex(index);
        }
    }
}
