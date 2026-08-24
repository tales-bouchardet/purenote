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

        // The tab the editor is showing.
        private FileTab _active;

        // Up while a document is being swapped in or out. Putting a document into
        // the editor raises DocumentChanged, and the window's handler reads that
        // as the user having typed - which would mark a tab dirty for the crime
        // of being opened.
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

        // ---- what the window used to keep on itself --------------------------
        //
        // These were four fields. They are one set per open document now, and the
        // window reads whichever tab is in front - which is why the rest of the
        // program did not have to be rewritten to know about tabs.

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

        // ---- switching -------------------------------------------------------

        private void Tab_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            FileTab tab = ((FrameworkElement)sender).DataContext as FileTab;

            if (tab != null && tab != _active)
            {
                Activate(tab);
                e.Handled = true;
            }
        }

        // Puts a tab in front. The one stepping away takes its document and undo
        // history back first, so returning to it later is a matter of handing
        // them over again rather than reading the file a second time.
        private void Activate(FileTab tab)
        {
            if (tab == null || tab == _active) return;

            Detach(_active);

            // An emptied tab reads itself back before it can be shown. This is
            // the one place a click can cost a file read, and it is what the tab
            // having cost nothing while it sat there was bought with.
            if (tab.IsEvicted && !Restore(tab))
            {
                // Nothing to show. Put the old tab back rather than leave the
                // window pointing at a tab it cannot display.
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

        // Takes the editor's state back into the tab it belongs to.
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

        // A brand new window has one empty untitled tab in it, and opening a file
        // into that rather than beside it is what stops every session from
        // beginning with a blank tab nobody asked for.
        private bool CanReuseActiveTab()
        {
            return _active != null
                && _active.FullPath == null
                && !_active.IsDirty
                && Editor.TextLength == 0;
        }

        // The name shown on a tab follows the path, and Save As is the one thing
        // that changes it under a document that is already open.
        private void UpdateTabs()
        {
            if (_active == null) return;

            _active.Name = _active.FullPath == null ? UntitledLabel : Path.GetFileName(_active.FullPath);
        }

        // ---- closing ---------------------------------------------------------

        private void TabClose_Click(object sender, RoutedEventArgs e)
        {
            FileTab tab = ((FrameworkElement)sender).DataContext as FileTab;
            if (tab != null) CloseTab(tab);

            // Or the click goes on to be read as a click on the tab it just shut.
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

            // Deliberately cleared: Activate refuses to act on the tab already in
            // front, and this one is on its way out.
            _active = null;

            if (_tabs.Count > 0)
            {
                Activate(_tabs[Math.Min(index, _tabs.Count - 1)]);
                Editor.Focus();
                return;
            }

            // Never no tabs at all - the editor has to be showing something, and
            // an empty untitled document is what it showed at startup.
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

            // Saving means having it in front: the save path writes whatever the
            // editor is holding.
            Activate(tab);
            Save_Click(this, new RoutedEventArgs());

            return !tab.IsDirty;
        }
    }
}
