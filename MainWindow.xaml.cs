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
        // Read by the crash handler so it can dump unsaved work before exiting.
        //
        // Mid-load this is only the part of the file that has arrived, and that is
        // the right answer: the load is read-only, so there is no unsaved work to
        // rescue, and the file it came from is still sitting on disk intact.
        internal string EditorText
        {
            get { return Editor == null ? null : Editor.Text; }
        }

        private string _currentFilePath;
        private Encoding _currentEncoding = EncodingDetector.Utf8NoBom;
        private string _lineEnding = LineEndings.Crlf;
        private bool _isDirty;

        // TextBox.Text rebuilds the whole document into a fresh string on every
        // read, so a multi-megabyte file means a multi-megabyte allocation each
        // time. Find, Replace, Save and the encoding check all want the same
        // snapshot, and between two edits it cannot change — so take one copy and
        // hand it out until the next edit drops it.
        private string _textSnapshot;

        private string DocumentText
        {
            get
            {
                if (_textSnapshot == null) _textSnapshot = Editor.Text;
                return _textSnapshot;
            }
        }

        public MainWindow()
        {
            InitializeComponent();

            SimplifiedMatchRadio.IsChecked = true;
            ReplaceSimpleRadio.IsChecked = true;
            ElevationText.Text = ElevationDetector.Detect();
            SetEncodingChecked(_currentEncoding);
            SetLineEndingChecked(_lineEnding);

            LineNumberLayer.Attach(Editor);
            QueueCountsUpdate();

            PopupDrag.Attach(FindPopup, FindHeader, this);
            PopupDrag.Attach(ReplacePopup, ReplaceHeader, this);

            Editor.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(Editor_ScrollChanged));
            Editor.SizeChanged += Editor_SizeChanged;

            Loaded += MainWindow_Loaded;
            SourceInitialized += Window_SourceInitialized;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= MainWindow_Loaded;

            string[] args = Environment.GetCommandLineArgs();
            if (args.Length < 2) return;

            string path = args[1];
            if (!File.Exists(path)) return;

            LoadFile(path);
        }

        // Only scrolls when the target is off screen, so stepping between matches
        // that already share the viewport does not yank the view around.
        private void ScrollToOffset(int offset)
        {
            int line = Editor.GetLineIndexFromCharacterIndex(offset);
            if (line < 0) return;

            EditorLineLayout layout;
            if (EditorLineLayout.TryCapture(Editor, out layout))
            {
                if (line >= layout.TopLine && line <= layout.LastLineIn(Editor.ViewportHeight, Editor.LineCount)) return;
            }

            Editor.ScrollToLine(line);

            // ScrollToLine aims with the same drifting scroll model EditorLineLayout
            // exists to work around, so on a large document it can settle a dozen
            // lines away from the match. Measure where it actually landed and close
            // the gap; one correction is enough, the residue being sub-line.
            Editor.UpdateLayout();

            EditorLineLayout landed;
            if (!EditorLineLayout.TryCapture(Editor, out landed) || landed.TopLine == line) return;

            Editor.ScrollToVerticalOffset(Editor.VerticalOffset + (line - landed.TopLine) * landed.LineHeight);
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

        private void Editor_TextChanged(object sender, TextChangedEventArgs e)
        {
            _textSnapshot = null;

            // Appends from a progressive load are not edits: the file is not
            // dirty, the counts would only churn, and CompleteLoad settles both
            // once the whole document has arrived. The gutter is left out of it
            // too — text lands at the end of the document, where it cannot
            // change which numbers are on screen, and the width was set from the
            // final line count before the first slice went in.
            if (IsLoading) return;

            LineNumbers_Invalidate();

            TrackLength(e);
            QueueCountsUpdate();

            // The footer only changes on the transition into the dirty state.
            if (!_isDirty)
            {
                _isDirty = true;
                UpdatePathDisplay();
            }

            // Debounced for the same reason typing in the search box is: each
            // recompute rescans the whole document, and an edit invalidates the
            // folded copy the search runs against, so every keystroke here would
            // otherwise pay for a fresh fold as well.
            if (FindPopup.IsOpen)
            {
                DebounceFind(UpdateFindMatches);
            }
        }
    }
}
