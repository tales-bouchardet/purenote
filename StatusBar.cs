using System.IO;

namespace PureNote
{
    public partial class MainWindow
    {
        private const string NoFileLabel = "No file";

        private void UpdateCounts()
        {
            int lines = Editor.LineCount;
            if (lines < 1) lines = 1;

            LineCountText.Text = $"{lines} ln";
            CharCountText.Text = $"{Editor.Text.Length} ch";
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
