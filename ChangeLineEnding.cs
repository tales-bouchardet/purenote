using System.Windows;
using System.Windows.Controls;

namespace PureNote
{
    public partial class MainWindow
    {
        private void LineEndingMenuItem_Click(object sender, RoutedEventArgs e)
        {
            string ending = (string)((MenuItem)sender).Header;

            if (ending == _lineEnding)
            {
                SetLineEndingChecked(_lineEnding);
                return;
            }

            _lineEnding = ending;
            _isDirty = true;

            UpdatePathDisplay();
            SetLineEndingChecked(ending);
        }

        private void SetLineEndingChecked(string ending)
        {
            CheckMenuItem(LineEndingMenu.Items, ending);
            LineEndingText.Text = ending;
        }
    }
}
