using System.IO;

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

        private void UpdatePathDisplay()
        {
            bool hasFile = !string.IsNullOrEmpty(_currentFilePath);

            PathText.Text = hasFile ? Path.GetFileName(_currentFilePath) : NoFileLabel;

            string fullPath = hasFile ? _currentFilePath : NoFileLabel;
            PathFooterText.Text = _isDirty ? "* " + fullPath : fullPath;
            PathFooterText.Foreground = _isDirty ? Theme.Dirty : Theme.FooterText;
        }
    }
}
