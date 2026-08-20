using System.Windows;
using System.Windows.Input;

namespace PureNote
{
    public partial class MainWindow
    {
        private void New_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            New_Click(sender, new RoutedEventArgs());
        }

        private void New_Click(object sender, RoutedEventArgs e)
        {
            if (!ConfirmDiscardChanges()) return;

            CancelLoad();

            Editor.IsUndoEnabled = false;
            Editor.IsReadOnly = false;
            Editor.Clear();
            Editor.IsUndoEnabled = true;
            Editor.ScrollToHome();
            ResetCounts(0);

            _currentFilePath = null;
            _currentEncoding = EncodingDetector.Utf8NoBom;
            _lineEnding = LineEndings.Crlf;
            _isDirty = false;

            UpdatePathDisplay();
            SetEncodingChecked(_currentEncoding);
            SetLineEndingChecked(_lineEnding);
        }
    }
}
