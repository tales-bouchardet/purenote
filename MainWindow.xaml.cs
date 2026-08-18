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

            Loaded += MainWindow_Loaded;
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
            int line = Editor.GetLineIndexFromCharacterIndex(offset);
            if (line >= 0)
            {
                Editor.ScrollToLine(line);
            }
        }

        private void Editor_TextChanged(object sender, TextChangedEventArgs e)
        {
            _isDirty = true;
            UpdateCounts();
            UpdatePathDisplay();

            if (FindPopup.IsOpen)
            {
                UpdateFindMatches();
            }
        }
    }
}
