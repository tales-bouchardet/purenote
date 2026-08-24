using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PureNote
{
    public partial class MainWindow
    {
        private const string NoFileLabel = "No file";

        // Both counts are now properties of the document rather than numbers to
        // be kept in step with it.
        //
        // They used to be tracked by hand: a running character total adjusted by
        // every TextChange, and a line count read back from the editor's own
        // layout - which is why it could only be read after a layout pass had
        // run, and why the character count had to have the '\r' of every CRLF
        // pair subtracted back out of it to match what other editors report.
        // Holding the document directly, in LF, makes both exact and free.
        private int _lineCount = 1;
        private int _charCount = -1;

        // Called on every keystroke, so it formats only what actually moved.
        // Typing inside one line changes the character count and leaves the line
        // count alone, and each of these is a string built and a TextBlock
        // invalidated for a number that reads the same as it did before.
        private void UpdateCounts()
        {
            int lines = Editor.LineCount;
            int characters = Editor.TextLength;

            if (lines != _lineCount)
            {
                _lineCount = lines;
                LineCountText.Text = $"{lines} ln";
                LineNumberLayer.SetLineCount(lines);
            }

            if (characters != _charCount)
            {
                _charCount = characters;
                CharCountText.Text = $"{characters} ch";
            }
        }

        // The path, laid out as the trail of folders it is.
        //
        // Rebuilt whole rather than patched, because it is asked for only when the
        // path or the unsaved mark actually changes - opening, saving, a new file,
        // and the one keystroke that turns a clean document dirty. A handful of
        // TextBlocks at those moments costs nothing worth avoiding.
        private void UpdatePathDisplay()
        {
            UpdateTabs();

            BreadcrumbScroll.ToolTip = _currentFilePath;
            BuildBreadcrumbs();
        }

        // Rebuilt when the path changes and again when the bar changes width,
        // because how much of a path fits is a question about both.
        private void Breadcrumbs_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.WidthChanged) BuildBreadcrumbs();
        }

        private void BuildBreadcrumbs()
        {
            BreadcrumbBar.Children.Clear();

            if (string.IsNullOrEmpty(_currentFilePath))
            {
                BreadcrumbBar.Children.Add(MakeCrumb(NoFileLabel, Theme.Crumb, 0));
                return;
            }

            string[] parts = _currentFilePath.Split(
                new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < parts.Length; i++)
            {
                if (i > 0) BreadcrumbBar.Children.Add(MakeCrumb("›", Theme.Crumb, 6));

                bool leaf = i == parts.Length - 1;

                // Unsaved work shows as the colour of the file's own name and
                // nothing else. A mark beside it would be a second thing saying
                // what the colour already says, and the tab above carries its own
                // dot for anyone who reads that first.
                BreadcrumbBar.Children.Add(MakeCrumb(parts[i],
                    leaf ? (_isDirty ? Theme.Dirty : Theme.CrumbLeaf) : Theme.Crumb, 0));
            }

            TrimBreadcrumbs();
        }

        // Drops folders off the front until what is left fits, and says so with a
        // single ellipsis.
        //
        // The bar used to be scrolled to its end instead, which kept the file
        // name in view but cut the front of the trail mid-word - leaving a stray
        // half of a folder name and a separator pointing at nothing. A path is a
        // list of names, so the thing to drop is a name, not a number of pixels.
        private void TrimBreadcrumbs()
        {
            double available = BreadcrumbScroll.ActualWidth - CrumbSideMargins;

            // Before the first layout there is no width to fit into, and the
            // SizeChanged that follows will ask again.
            if (available <= 0) return;

            BreadcrumbScroll.UpdateLayout();
            if (BreadcrumbBar.ActualWidth <= available) return;

            BreadcrumbBar.Children.Insert(0, MakeCrumb("›", Theme.Crumb, 6));
            BreadcrumbBar.Children.Insert(0, MakeCrumb("…", Theme.Crumb, 0));

            // Index 0 is the ellipsis and 1 its separator; 2 is the first folder
            // still being shown, and 3 the separator after it. Below five
            // children there is nothing left to give up but the file name itself,
            // which is the one thing this exists to keep.
            while (BreadcrumbBar.Children.Count >= 5)
            {
                BreadcrumbScroll.UpdateLayout();
                if (BreadcrumbBar.ActualWidth <= available) return;

                BreadcrumbBar.Children.RemoveAt(2);
                BreadcrumbBar.Children.RemoveAt(2);
            }
        }

        // The panel's own left and right margin, which the width it has to fit
        // into does not include.
        private const double CrumbSideMargins = 24;

        private static TextBlock MakeCrumb(string text, Brush brush, double sideMargin)
        {
            return new TextBlock
            {
                Text = text,
                Foreground = brush,
                FontSize = 11.5,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(sideMargin, 0, sideMargin, 0)
            };
        }
    }
}
