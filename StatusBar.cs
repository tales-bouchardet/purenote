using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
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
            BreadcrumbBar.Children.Clear();

            if (string.IsNullOrEmpty(_currentFilePath))
            {
                BreadcrumbBar.Children.Add(MakeCrumb(NoFileLabel, Theme.Crumb, 0));
                BreadcrumbScroll.ToolTip = null;
                BreadcrumbScroll.ScrollToLeftEnd();
                return;
            }

            string[] parts = _currentFilePath.Split(
                new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < parts.Length; i++)
            {
                if (i > 0) BreadcrumbBar.Children.Add(MakeCrumb("›", Theme.Crumb, 6));

                bool leaf = i == parts.Length - 1;

                BreadcrumbBar.Children.Add(leaf
                    ? MakeLeafCrumb(parts[i])
                    : MakeCrumb(parts[i], Theme.Crumb, 0));
            }

            BreadcrumbScroll.ToolTip = _currentFilePath;

            // Measured before it is scrolled, or there is nothing to scroll yet.
            // A path that fits leaves this a no-op; one that does not gives up its
            // front rather than the name at its end.
            BreadcrumbScroll.UpdateLayout();
            BreadcrumbScroll.ScrollToRightEnd();
        }

        // The file's own name, and the only crumb the unsaved mark says anything
        // about.
        //
        // The mark trails the name rather than leading it, raised and small: set
        // level and in front, an asterisk pushes the name sideways every time the
        // document goes from clean to dirty and back, and the eye reads the
        // punctuation before the word it came for.
        private TextBlock MakeLeafCrumb(string name)
        {
            TextBlock crumb = MakeCrumb(string.Empty, _isDirty ? Theme.Dirty : Theme.CrumbLeaf, 0);

            crumb.Inlines.Add(new Run(name));

            if (_isDirty)
            {
                crumb.Inlines.Add(new Run("*")
                {
                    BaselineAlignment = BaselineAlignment.Superscript,
                    FontSize = 8
                });
            }

            return crumb;
        }

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
