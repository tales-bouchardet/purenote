using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace PureNote
{
    public partial class MainWindow
    {
        private readonly List<int> _replaceMatches = new List<int>();

        private void Replace_Click(object sender, RoutedEventArgs e)
        {
            ReplacePopup.IsOpen = !ReplacePopup.IsOpen;

            if (ReplacePopup.IsOpen)
            {
                FindPopup.IsOpen = false;
                HighlightLayer.Children.Clear();
                ReplaceStatusText.Text = "";
                ReplaceFindTextBox.Focus();
                ReplaceFindTextBox.SelectAll();
            }
        }

        private void ReplaceClose_Click(object sender, RoutedEventArgs e)
        {
            ReplacePopup.IsOpen = false;
        }

        private void ReplaceTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ReplaceNext_Click(sender, e);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                ReplaceClose_Click(sender, e);
                e.Handled = true;
            }
        }

        private void ReplaceNext_Click(object sender, RoutedEventArgs e)
        {
            string term = ReplaceFindTextBox.Text;
            if (string.IsNullOrEmpty(term)) return;

            bool exact = ReplaceExactRadio.IsChecked == true;
            string replacement = ReplaceWithTextBox.Text;

            int index = TextSearch.IndexOf(Editor.Text, term, Editor.SelectionStart, exact);
            if (index < 0)
            {
                index = TextSearch.IndexOf(Editor.Text, term, 0, exact);
            }

            if (index < 0)
            {
                ReplaceStatusText.Text = "No matches";
                return;
            }

            Editor.Select(index, term.Length);
            Editor.SelectedText = replacement;
            Editor.Select(index + replacement.Length, 0);
            ScrollToOffset(index);

            ReplaceStatusText.Text = "1 replaced";
        }

        private void ReplaceAll_Click(object sender, RoutedEventArgs e)
        {
            string term = ReplaceFindTextBox.Text;
            if (string.IsNullOrEmpty(term)) return;

            bool exact = ReplaceExactRadio.IsChecked == true;
            string replacement = ReplaceWithTextBox.Text;
            string text = Editor.Text;

            TextSearch.FindAll(text, term, exact, _replaceMatches);

            if (_replaceMatches.Count == 0)
            {
                ReplaceStatusText.Text = "No matches";
                return;
            }

            StringBuilder sb = new StringBuilder(text.Length);
            int copiedUpTo = 0;

            foreach (int start in _replaceMatches)
            {
                sb.Append(text, copiedUpTo, start - copiedUpTo);
                sb.Append(replacement);
                copiedUpTo = start + term.Length;
            }

            sb.Append(text, copiedUpTo, text.Length - copiedUpTo);

            int caret = Editor.SelectionStart;
            Editor.SelectAll();
            Editor.SelectedText = sb.ToString();
            Editor.Select(caret < Editor.Text.Length ? caret : Editor.Text.Length, 0);

            ReplaceStatusText.Text = $"{_replaceMatches.Count} replaced";
        }
    }
}
