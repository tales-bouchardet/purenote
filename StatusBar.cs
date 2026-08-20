using System;
using System.IO;
using System.Windows.Controls;
using System.Windows.Threading;

namespace PureNote
{
    public partial class MainWindow
    {
        private const string NoFileLabel = "No file";

        private int _rawLength;
        private int _lineCount = 1;
        private bool _countsUpdateQueued;

        // Kept in step with the editor instead of measured from it: TextChange
        // reports exactly how much went in and out, so the character count costs
        // nothing, where re-reading TextBox.Text on every keystroke of a large
        // file meant copying the whole document each time.
        private void TrackLength(TextChangedEventArgs e)
        {
            foreach (TextChange change in e.Changes)
            {
                _rawLength += change.AddedLength - change.RemovedLength;
            }

            if (_rawLength < 0) _rawLength = 0;
        }

        // Called when the document is replaced wholesale and the new text is
        // already in hand, so the tracked length starts from a known-good value.
        private void ResetCounts(int length)
        {
            _rawLength = length;
            QueueCountsUpdate();
        }

        // Coalesced onto the dispatcher for two reasons: a burst of edits then
        // refreshes the status bar once, and this runs after WPF's layout pass,
        // which is what makes Editor.LineCount readable below.
        //
        // Loaded rather than Background: both sit under Render and so see a
        // finished layout, but Background sits under Input too, and queued there
        // the counts simply stopped arriving while the pointer was moving over
        // the window.
        private void QueueCountsUpdate()
        {
            if (_countsUpdateQueued) return;

            _countsUpdateQueued = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(UpdateCounts));
        }

        private void UpdateCounts()
        {
            _countsUpdateQueued = false;

            // A load sets both counts from the string it is loading and shows its
            // own progress next to them; letting an update queued before it landed
            // overwrite that would replace the file's real size with the size of
            // however much of it has arrived.
            if (IsLoading) return;

            // WPF already walks the document to lay it out, and LineCount is the
            // result of that walk — free to read, where counting newlines here
            // repeated the same work. It reports -1 until the first layout pass,
            // which is the only time the document has to be scanned by hand.
            int lines = Editor.LineCount;
            _lineCount = lines > 0 ? lines : CountLines(Editor.Text);

            LineCountText.Text = $"{_lineCount} ln";
            CharCountText.Text = $"{CountDisplayCharacters()} ch";

            if (_lineNumbersEnabled) LineNumberLayer.SetLineCount(_lineCount);
        }

        // The editor normalises every line break to CRLF on load and types them
        // that way, but conventional character counts (Notepad, VS Code) treat a
        // line break as one character — so subtract the '\r' of each pair.
        private int CountDisplayCharacters()
        {
            return Math.Max(0, _rawLength - (_lineCount - 1));
        }

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
