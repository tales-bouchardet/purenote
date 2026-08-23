using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace PureNote
{
    public partial class MainWindow : FluentWindow
    {
        // Called by the crash handler to dump unsaved work before exiting.
        //
        // Streamed straight out of the document rather than through a string of
        // it, which would be a second full copy. That allocation is the one
        // certain to fail in the case this exists for: an OutOfMemoryException is
        // what a large document crashes with, and asking for another copy of it
        // is how the rescue itself would crash.
        internal bool TryWriteRecovery(string path)
        {
            if (Editor == null || Editor.Document.Length == 0) return false;

            using (StreamWriter writer = new StreamWriter(path, false, EncodingDetector.Utf8NoBom))
            {
                Editor.Document.WriteTo(writer, _lineEnding);
            }

            return true;
        }

        private string _currentFilePath;
        private Encoding _currentEncoding = EncodingDetector.Utf8NoBom;
        private string _lineEnding = LineEndings.Crlf;
        private bool _isDirty;

        public MainWindow()
        {
            InitializeComponent();

            SimplifiedMatchRadio.IsChecked = true;
            ReplaceSimpleRadio.IsChecked = true;
            ElevationText.Text = ElevationDetector.Detect();
            SetEncodingChecked(_currentEncoding);
            SetLineEndingChecked(_lineEnding);

            Editor.MatchBrush = Theme.AllMatches;
            Editor.CurrentMatchBrush = Theme.CurrentMatchFill;
            Editor.CurrentMatchStroke = Theme.CurrentMatchStroke;

            Editor.DocumentChanged += Editor_DocumentChanged;
            Editor.EditRefused += (s, e) => ReportOutOfMemory("make this edit");

            // The gutter draws line numbers and nothing that depends on where the
            // caret is, so it is told only when the range of lines on screen may
            // have moved. Moving the caret used to repaint it too - one
            // FormattedText per visible number, forty of them, for numbers that
            // had not changed - and a caret move that does scroll the view raises
            // ViewChanged anyway.
            Editor.ViewChanged += (s, e) => LineNumbers_Invalidate();

            LineNumberLayer.Attach(Editor);

            PopupDrag.Attach(FindPopup, FindHeader, this);
            PopupDrag.Attach(ReplacePopup, ReplaceHeader, this);

            UpdateCounts();

            Loaded += MainWindow_Loaded;
            SourceInitialized += Window_SourceInitialized;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= MainWindow_Loaded;

            Editor.Focus();

            string[] args = Environment.GetCommandLineArgs();
            if (args.Length < 2) return;

            string path = args[1];
            if (!File.Exists(path)) return;

            LoadFile(path);
        }

        // The one place an edit is heard about. There is no longer any question
        // of whether the document is mid-load and only partly present: loading
        // builds the document before the view is ever shown it, so anything that
        // reaches here is a real edit by the user.
        private void Editor_DocumentChanged(object sender, EventArgs e)
        {
            UpdateCounts();
            LineNumbers_Invalidate();

            if (!_isDirty)
            {
                _isDirty = true;
                UpdatePathDisplay();
            }

            if (!FindPopup.IsOpen) return;

            // The offsets in hand describe the document as it stood before this
            // edit, and everything after the edit point has just moved. Dropping
            // the highlighting now rather than leaving it to the recompute is the
            // difference between it going briefly absent and it sitting visibly
            // over the wrong words - which is what it did before this line
            // existed, for the length of the debounce plus the scan.
            Editor.ClearMatches();

            // Debounced because each recompute walks the whole document, and a
            // burst of keystrokes would otherwise pay for one scan each.
            DebounceFind(UpdateFindMatches);
        }

        private static void CheckMenuItem(ItemCollection items, string header)
        {
            foreach (object obj in items)
            {
                if (obj is System.Windows.Controls.MenuItem item)
                {
                    item.IsChecked = (string)item.Header == header;
                }
            }
        }
    }
}
