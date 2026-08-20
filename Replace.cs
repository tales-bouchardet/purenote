using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace PureNote
{
    public partial class MainWindow
    {
        private void Replace_Click(object sender, RoutedEventArgs e)
        {
            if (IsLoading)
            {
                ReportBusyLoading();
                return;
            }

            if (ReplacePopup.IsOpen)
            {
                ReplaceClose_Click(sender, e);
                return;
            }

            ReplacePopup.IsOpen = true;
            FindPopup.IsOpen = false;
            ClearHighlights();
            ReplaceStatusText.Text = "";

            // Deferred: a top-level MenuItem click leaves the menu holding keyboard
            // focus as it unwinds, which would take focus straight back off the box.
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                ReplaceFindTextBox.Focus();
                ReplaceFindTextBox.SelectAll();
            }));
        }

        private void ReplaceClose_Click(object sender, RoutedEventArgs e)
        {
            ReplacePopup.IsOpen = false;
            ReplaceStatusText.Text = "";
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

            int index = TextSearch.IndexOf(DocumentText, term, Editor.SelectionStart, exact);
            if (index < 0)
            {
                index = TextSearch.IndexOf(DocumentText, term, 0, exact);
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
            string text = DocumentText;

            List<int> matches = new List<int>();
            TextSearch.FindAll(text, term, exact, matches);

            if (matches.Count == 0)
            {
                ReplaceStatusText.Text = "No matches";
                return;
            }

            StringBuilder sb = new StringBuilder(text.Length);
            int copiedUpTo = 0;

            foreach (int start in matches)
            {
                sb.Append(text, copiedUpTo, start - copiedUpTo);
                sb.Append(replacement);
                copiedUpTo = start + term.Length;
            }

            sb.Append(text, copiedUpTo, text.Length - copiedUpTo);

            int caret = Editor.SelectionStart;
            Editor.SelectAll();
            Editor.SelectedText = sb.ToString();
            Editor.Select(Math.Min(caret, _rawLength), 0);

            ReplaceStatusText.Text = $"{matches.Count} replaced";
        }
    }
}
