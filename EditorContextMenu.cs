using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace PureNote
{
    public partial class MainWindow
    {
        // A selection can be the whole document. What arrives here is on its way
        // into a single-line text box and then into a search, so past a certain
        // size it has stopped being a search term and become a mistake.
        private const int MaxTermFromSelection = 2048;

        // Which items make sense is decided each time the menu opens rather than
        // tracked as the selection changes: it is asked once per right click, and
        // anything kept in step continuously would be one more thing that can
        // fall out of step.
        private void EditorContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            bool hasSelection = Editor.SelectionLength > 0;

            ContextCopy.IsEnabled = hasSelection;
            ContextCut.IsEnabled = hasSelection;
            ContextSearch.IsEnabled = hasSelection;
            ContextReplace.IsEnabled = hasSelection;

            // Paste depends on what is on the clipboard, not on the selection -
            // it replaces one when there is one and inserts at the caret when
            // there is not.
            ContextPaste.IsEnabled = ClipboardHasText();
        }

        // Reading the clipboard can fail outright: it is a shared system resource
        // and another process may hold it open at the moment this is asked.
        // Offering Paste and having it do nothing is better than throwing while
        // opening a menu.
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

            // Opened before the term goes in: the search publishes its highlights
            // to the view, and the edit path checks whether find is open to decide
            // whether they need dropping again.
            FindPopup.IsOpen = true;
            FindTextBox.Text = term;

            // Setting the text queues a debounced search, which is right when the
            // term is being typed and wrong here - this is one deliberate action,
            // and waiting out a timer for it would be latency for its own sake.
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

            // The term is already known, so the box worth landing in is the other
            // one - the only thing still missing is what to replace it with.
            FocusLater(ReplaceWithTextBox);
        }

        private string TermFromSelection()
        {
            if (Editor.SelectionLength == 0) return null;

            string term = Editor.Document.GetText(Editor.SelectionStart,
                Math.Min(Editor.SelectionLength, MaxTermFromSelection));

            return term.Length == 0 ? null : term;
        }

        // Deferred for the same reason the Find and Replace menu items defer it:
        // the menu still holds keyboard focus as it unwinds, and taking focus
        // before it has finished closing means losing it again a moment later.
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
