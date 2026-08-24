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

            if (discardingLarge) CompactLargeObjectHeap();
        }
    }
}
