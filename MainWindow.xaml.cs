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
        internal string EditorText
        {
            get { return Editor == null ? null : Editor.Text; }
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
            UpdateCounts();

            PopupDrag.Attach(FindPopup, FindHeader, this);
            PopupDrag.Attach(ReplacePopup, ReplaceHeader, this);

            Editor.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(Editor_ScrollChanged));
            Editor.SizeChanged += Editor_SizeChanged;
            Editor.SelectionChanged += Editor_SelectionChanged;

            Editor.SizeChanged += LineNumbers_Editor_SizeChanged;
            Editor.TextChanged += LineNumbers_Editor_TextChanged;

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

        private void ScrollToOffset(int offset)
        {
            ScrollRectIntoView(Editor.GetRectFromCharacterIndex(offset));
        }

        // Editor no longer scrolls itself (EditorScroll does, so the gutter scrolls
        // in lockstep with it), so the usual "typing/arrow keys keep the caret in
        // view" behavior a TextBox gives for free has to be done by hand here.
        private void Editor_SelectionChanged(object sender, RoutedEventArgs e)
        {
            ScrollRectIntoView(Editor.GetRectFromCharacterIndex(Editor.CaretIndex));
        }

        private void ScrollRectIntoView(Rect rect)
        {
            if (rect.IsEmpty || double.IsInfinity(rect.Top)) return;

            double viewTop = EditorScroll.VerticalOffset;
            double viewBottom = viewTop + EditorScroll.ViewportHeight;

            if (rect.Top < viewTop)
            {
                EditorScroll.ScrollToVerticalOffset(rect.Top);
            }
            else if (rect.Bottom > viewBottom)
            {
                EditorScroll.ScrollToVerticalOffset(rect.Bottom - EditorScroll.ViewportHeight);
            }
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
            UpdateCounts();

            // The footer only changes on the transition into the dirty state.
            if (!_isDirty)
            {
                _isDirty = true;
                UpdatePathDisplay();
            }

            if (FindPopup.IsOpen)
            {
                UpdateFindMatches();
            }
        }
    }
}
