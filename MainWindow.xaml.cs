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
        internal bool TryWriteRecovery(string path)
        {
            if (Editor == null || Editor.Document.Length == 0) return false;

            using (StreamWriter writer = new StreamWriter(path, false, EncodingDetector.Utf8NoBom))
            {
                Editor.Document.WriteTo(writer, _lineEnding);
            }

            return true;
        }

        public MainWindow()
        {
            InitializeComponent();

            SimplifiedMatchRadio.IsChecked = true;
            ReplaceSimpleRadio.IsChecked = true;

            InitialiseTabs();
            ElevationText.Text = ElevationDetector.Detect();
            SetEncodingChecked(_currentEncoding);
            SetLineEndingChecked(_lineEnding);

            Editor.MatchBrush = Theme.AllMatches;
            Editor.CurrentMatchBrush = Theme.CurrentMatchFill;
            Editor.CurrentMatchStroke = Theme.CurrentMatchStroke;

            Editor.DocumentChanged += Editor_DocumentChanged;
            Editor.EditRefused += (s, e) => ReportOutOfMemory("make this edit");

            Editor.ViewChanged += (s, e) => LineNumbers_Invalidate();

            LineNumberLayer.Attach(Editor);

            PopupDrag.Attach(FindPopup, FindHeader, this);
            PopupDrag.Attach(ReplacePopup, ReplaceHeader, this);

            UpdateCounts();

            UpdatePathDisplay();

            Loaded += MainWindow_Loaded;
            SourceInitialized += Window_SourceInitialized;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= MainWindow_Loaded;

            Editor.Focus();

            string[] args = Environment.GetCommandLineArgs();

            for (int i = 1; i < args.Length; i++)
            {
                if (File.Exists(args[i])) LoadFile(args[i]);
            }
        }

        private void Editor_DocumentChanged(object sender, EventArgs e)
        {
            UpdateCounts();
            LineNumbers_Invalidate();

            if (!_switching && !_isDirty)
            {
                _isDirty = true;
                UpdatePathDisplay();
            }

            if (!FindPopup.IsOpen) return;

            Editor.ClearMatches();

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
