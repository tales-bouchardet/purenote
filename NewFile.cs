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

            Editor.IsUndoEnabled = false;
            Editor.Clear();
            Editor.IsUndoEnabled = true;
            EditorScroll.ScrollToVerticalOffset(0);

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
