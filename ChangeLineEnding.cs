using System.Windows;
using MenuItem = System.Windows.Controls.MenuItem;

namespace PureNote
{
    public partial class MainWindow
    {
        private void LineEndingMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MenuItem clicked = sender as MenuItem;
            if (clicked == null) return;

            string ending = (string)clicked.Header;

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
            foreach (object obj in LineEndingMenu.Items)
            {
                if (obj is MenuItem item)
                {
                    item.IsChecked = (string)item.Header == ending;
                }
            }

            LineEndingText.Text = ending;
        }
    }
}
