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

            bool discardingLarge = IsLargeDocument;

            DropFindMatches();
            Editor.Clear();

            _currentFilePath = null;
            _currentEncoding = EncodingDetector.Utf8NoBom;
            _lineEnding = LineEndings.Crlf;
            _isDirty = false;

            UpdatePathDisplay();
            UpdateCounts();
            SetEncodingChecked(_currentEncoding);
            SetLineEndingChecked(_lineEnding);

            Editor.Focus();

            // The one moment a large document is known to have been let go with
            // nothing waiting on the latency: the editor is empty and the user is
            // starting from scratch. Compacting here is what stops the space it
            // occupied from being unusable to the next large file.
            if (discardingLarge) CompactLargeObjectHeap();
        }
    }
}
