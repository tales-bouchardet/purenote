using System.Windows;

namespace PureNote
{
    public partial class MainWindow
    {
        private bool _lineNumbersEnabled;

        private void LineNumbersMenuItem_Click(object sender, RoutedEventArgs e)
        {
            _lineNumbersEnabled = LineNumbersMenuItem.IsChecked;
            LineNumberLayer.Visibility = _lineNumbersEnabled ? Visibility.Visible : Visibility.Collapsed;

            if (_lineNumbersEnabled)
            {
                LineNumberLayer.SetLineCount(_lineCount);
                LineNumberLayer.Refresh();
            }
        }

        private void LineNumbers_Invalidate()
        {
            if (!_lineNumbersEnabled) return;

            LineNumberLayer.Refresh();
        }
    }
}
