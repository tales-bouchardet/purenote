using System;
using System.IO;

namespace PureNote
{
    public partial class MainWindow
    {
        private const string NoFileLabel = "No file";

        private void UpdateCounts()
        {
            LineCountText.Text = $"{CountLines(Editor.Text)} ln";
            CharCountText.Text = $"{CountDisplayCharacters()} ch";
        }

        // The editor normalises every line break to CRLF on load and types them
        // that way, but conventional character counts (Notepad, VS Code) treat a
        // line break as one character — so subtract the '\r' of each pair.
        private int CountDisplayCharacters()
        {
            int lineBreaks = CountLines(Editor.Text) - 1;
            return Math.Max(0, Editor.Text.Length - lineBreaks);
        }

        // Counted straight from the text instead of TextBox.LineCount: that
        // property only updates after WPF's next layout pass, so reading it from
        // a TextChanged handler reports the line count from before the keystroke.
        private static int CountLines(string text)
        {
            int count = 1;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n') count++;
            }
            return count;
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
