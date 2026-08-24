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

        // A new document opens in a tab of its own rather than over the one in
        // front. Nothing is discarded, so nothing has to be confirmed.
        private void New_Click(object sender, RoutedEventArgs e)
        {
            bool discardingLarge = IsLargeDocument;

            DropFindMatches();

            if (!CanReuseActiveTab())
            {
                Detach(_active);

                _active = NewTab(null, EncodingDetector.Utf8NoBom, LineEndings.Crlf);
                _active.IsActive = true;

                _switching = true;
                try
                {
                    Editor.Clear();
                }
                finally
                {
                    _switching = false;
                }
            }

            _isDirty = false;

            UpdatePathDisplay();
            UpdateCounts();
            SetEncodingChecked(_currentEncoding);
            SetLineEndingChecked(_lineEnding);

            Editor.Focus();

            // The one moment a large document is known to have been let go with
            // nothing waiting on the latency. Compacting here is what stops the
            // space it occupied from being unusable to the next large file.
            if (discardingLarge) CompactLargeObjectHeap();
        }
    }
}
