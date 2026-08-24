using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace PureNote
{
    public partial class MainWindow
    {
        private const string UntitledLabel = "Untitled";

        private readonly ObservableCollection<FileTab> _tabs = new ObservableCollection<FileTab>();

        private FileTab _active;

        private bool _switching;

        private void InitialiseTabs()
        {
            TabStrip.ItemsSource = _tabs;

            _active = NewTab(null, EncodingDetector.Utf8NoBom, LineEndings.Crlf);
            _active.State = Editor.CaptureState();
            _active.IsActive = true;

            StartEvictionSweep();
        }

        private FileTab NewTab(string path, Encoding encoding, string lineEnding)
        {
            FileTab tab = new FileTab
            {
                FullPath = path,
                Encoding = encoding,
                LineEnding = lineEnding,
                Name = path == null ? UntitledLabel : Path.GetFileName(path),
                LastActive = DateTime.UtcNow
            };

            _tabs.Add(tab);
            return tab;
        }


        private string _currentFilePath
        {
            get { return _active == null ? null : _active.FullPath; }
            set { if (_active != null) { _active.FullPath = value; _active.Name = value == null ? UntitledLabel : Path.GetFileName(value); } }
        }

        private Encoding _currentEncoding
        {
            get { return _active == null ? EncodingDetector.Utf8NoBom : _active.Encoding; }
            set { if (_active != null) _active.Encoding = value; }
        }

        private string _lineEnding
        {
            get { return _active == null ? LineEndings.Crlf : _active.LineEnding; }
            set { if (_active != null) _active.LineEnding = value; }
        }

        private bool _isDirty
        {
            get { return _active != null && _active.IsDirty; }
            set { if (_active != null) _active.IsDirty = value; }
        }


        private void Tab_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            FileTab tab = ((FrameworkElement)sender).DataContext as FileTab;

            if (tab != null && tab != _active)
            {
                Activate(tab);
                e.Handled = true;
            }
        }

        private void Activate(FileTab tab)
        {
            if (tab == null || tab == _active) return;

            Detach(_active);

            if (tab.IsEvicted && !Restore(tab))
            {
                if (_active != null) Show(_active);
                return;
            }

            Show(tab);
            Editor.Focus();
        }

        private void Show(FileTab tab)
        {
            _active = tab;
            _active.IsActive = true;
            _active.LastActive = DateTime.UtcNow;

            _switching = true;
            try
            {
                Editor.RestoreState(tab.State);
            }
            finally
            {
                _switching = false;
            }

            DropFindMatches();
            UpdatePathDisplay();
            UpdateCounts();
            SetEncodingChecked(tab.Encoding);
            SetLineEndingChecked(tab.LineEnding);
        }

        private void Detach(FileTab tab)
        {
            if (tab == null) return;

            tab.IsActive = false;
            tab.LastActive = DateTime.UtcNow;

            TextView.EditorState state = Editor.CaptureState();

            tab.State = state;
            tab.CaretOffset = state.CaretOffset;
            tab.SelectionAnchor = state.SelectionAnchor;
            tab.VerticalOffset = state.VerticalOffset;
            tab.HorizontalOffset = state.HorizontalOffset;
        }

        private bool CanReuseActiveTab()
        {
            return _active != null
                && _active.FullPath == null
                && !_active.IsDirty
                && Editor.TextLength == 0;
        }


        private void TabClose_Click(object sender, RoutedEventArgs e)
        {
            FileTab tab = ((FrameworkElement)sender).DataContext as FileTab;
            if (tab != null) CloseTab(tab);

            e.Handled = true;
        }

        private void CloseTab(FileTab tab)
        {
            if (tab.IsDirty && !ConfirmDiscardTab(tab)) return;

            DiscardSpill(tab);

            bool wasActive = tab == _active;
            int index = _tabs.IndexOf(tab);

            tab.State = null;
            _tabs.Remove(tab);

            if (!wasActive) return;

            _active = null;

            if (_tabs.Count > 0)
            {
                Activate(_tabs[Math.Min(index, _tabs.Count - 1)]);
                Editor.Focus();
                return;
            }

            Editor.Clear();

            FileTab fresh = NewTab(null, EncodingDetector.Utf8NoBom, LineEndings.Crlf);
            fresh.State = Editor.CaptureState();
            Show(fresh);
            Editor.Focus();
        }

        private bool ConfirmDiscardTab(FileTab tab)
        {
            MessageBoxResult answer = AppMessageBox.Show(this,
                "There are unsaved changes in " + tab.Name + ". Do you want to save them first?",
                "Unsaved changes", MessageBoxButton.YesNoCancel);

            if (answer != MessageBoxResult.Yes) return answer == MessageBoxResult.No;

            Activate(tab);
            Save_Click(this, new RoutedEventArgs());

            return !tab.IsDirty;
        }
    }
}
