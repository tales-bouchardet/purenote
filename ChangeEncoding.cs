using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace PureNote
{
    public partial class MainWindow
    {
        private void EncodingMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // CanRepresent below has to scan the whole document, and half of it
            // is still on its way in.
            if (IsLoading)
            {
                SetEncodingChecked(_currentEncoding);
                ReportBusyLoading();
                return;
            }

            string displayName = (string)((MenuItem)sender).Header;
            Encoding newEncoding = EncodingDetector.FromDisplayName(displayName);

            if (displayName == EncodingDetector.GetDisplayName(_currentEncoding))
            {
                SetEncodingChecked(_currentEncoding);
                return;
            }

            string question = EncodingDetector.CanRepresent(DocumentText, newEncoding)
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

            CheckMenuItem(EncodingMenu.Items, displayName);
            EncodingText.Text = displayName;
        }
    }
}
