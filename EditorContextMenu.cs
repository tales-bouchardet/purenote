using System;
using System.Windows;
using System.Windows.Threading;

namespace PureNote
{
    public partial class MainWindow
    {
        private const int MaxTermFromSelection = 2048;

        private void EditorContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            bool hasSelection = Editor.SelectionLength > 0;

            ContextCopy.IsEnabled = hasSelection;
            ContextCut.IsEnabled = hasSelection;
            ContextSearch.IsEnabled = hasSelection;
            ContextReplace.IsEnabled = hasSelection;

            ContextPaste.IsEnabled = ClipboardHasText();
        }

        private static bool ClipboardHasText()
        {
            try
            {
                return Clipboard.ContainsText();
            }
            catch (Exception)
            {
                return true;
            }
        }

        private void ContextCut_Click(object sender, RoutedEventArgs e)
        {
            Editor.Cut();
            Editor.Focus();
        }

        private void ContextCopy_Click(object sender, RoutedEventArgs e)
        {
            Editor.Copy();
            Editor.Focus();
        }

        private void ContextPaste_Click(object sender, RoutedEventArgs e)
        {
            Editor.Paste();
            Editor.Focus();
        }

        private void ContextSearch_Click(object sender, RoutedEventArgs e)
        {
            string term = TermFromSelection();
            if (term == null) return;

            ReplacePopup.IsOpen = false;

            FindPopup.IsOpen = true;
            FindTextBox.Text = term;

            FlushPendingFind();

            FocusLater(FindTextBox);
        }

        private void ContextReplace_Click(object sender, RoutedEventArgs e)
        {
            string term = TermFromSelection();
            if (term == null) return;

            FindPopup.IsOpen = false;
            DropFindMatches();

            ReplacePopup.IsOpen = true;
            ReplaceFindTextBox.Text = term;
            ReplaceStatusText.Text = "";

            FocusLater(ReplaceWithTextBox);
        }

        private string TermFromSelection()
        {
            if (Editor.SelectionLength == 0) return null;

            string term = Editor.Document.GetText(Editor.SelectionStart,
                Math.Min(Editor.SelectionLength, MaxTermFromSelection));

            return term.Length == 0 ? null : term;
        }

        private void FocusLater(IInputElement target)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                target.Focus();

                System.Windows.Controls.TextBox box = target as System.Windows.Controls.TextBox;
                if (box != null) box.SelectAll();
            }));
        }
    }
}
