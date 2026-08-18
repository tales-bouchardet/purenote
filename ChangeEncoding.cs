using System.Text;
using System.Windows;
using MenuItem = System.Windows.Controls.MenuItem;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxResult = System.Windows.MessageBoxResult;

namespace PureNote
{
    public partial class MainWindow
    {
        private void EncodingMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MenuItem clicked = sender as MenuItem;
            if (clicked == null) return;

            string displayName = (string)clicked.Header;
            Encoding newEncoding = EncodingDetector.FromDisplayName(displayName);

            if (displayName == EncodingDetector.GetDisplayName(_currentEncoding))
            {
                SetEncodingChecked(_currentEncoding);
                return;
            }

            string question = EncodingDetector.CanRepresent(Editor.Text, newEncoding)
                ? $"Are you sure you want to change the encoding to {displayName}?"
                : $"Some characters in this text cannot be represented in {displayName} " +
                  "and will be replaced with '?' when saving.\n\nChange the encoding anyway?";

            MessageBoxResult result = AppMessageBox.Show(this, question,
                "Change encoding", MessageBoxButton.YesNo);

            if (result != MessageBoxResult.Yes)
            {
                SetEncodingChecked(_currentEncoding);
                return;
            }

            _currentEncoding = newEncoding;
            _isDirty = true;
            UpdatePathDisplay();
            SetEncodingChecked(newEncoding);
        }

        private void SetEncodingChecked(Encoding encoding)
        {
            string displayName = EncodingDetector.GetDisplayName(encoding);

            foreach (object obj in EncodingMenu.Items)
            {
                if (obj is MenuItem item)
                {
                    item.IsChecked = (string)item.Header == displayName;
                }
            }

            EncodingText.Text = displayName;
        }
    }
}
