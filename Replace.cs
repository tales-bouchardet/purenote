using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace PureNote
{
    public partial class MainWindow
    {
        // Ctrl+H, and the same reasoning as Find_Executed: the shortcut reopens
        // and refocuses rather than toggling.
        private void Replace_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            OpenReplace();
        }

        private void Replace_Click(object sender, RoutedEventArgs e)
        {
            if (ReplacePopup.IsOpen)
            {
                ReplaceClose_Click(sender, e);
                return;
            }

            OpenReplace();
        }

        private void OpenReplace()
        {
            FindPopup.IsOpen = false;
            DropFindMatches();

            ReplacePopup.IsOpen = true;
            ReplaceStatusText.Text = "";

            FocusLater(ReplaceFindTextBox);
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

            int index = TextSearch.IndexOf(Editor.Document, term, Editor.SelectionStart, exact);
            if (index < 0) index = TextSearch.IndexOf(Editor.Document, term, 0, exact);

            if (index < 0)
            {
                ReplaceStatusText.Text = "No matches";
                return;
            }

            try
            {
                Editor.ReplaceRange(index, term.Length, replacement);
            }
            catch (OutOfMemoryException)
            {
                ReportOutOfMemory("make this replacement");
                ReplaceStatusText.Text = "Out of memory";
                return;
            }

            Editor.ScrollIntoView(index);

            ReplaceStatusText.Text = "1 replaced";
        }

        private void ReplaceAll_Click(object sender, RoutedEventArgs e)
        {
            string term = ReplaceFindTextBox.Text;
            if (string.IsNullOrEmpty(term)) return;

            bool exact = ReplaceExactRadio.IsChecked == true;
            string replacement = ReplaceWithTextBox.Text;

            List<int> matches = new List<int>();
            TextSearch.FindAll(Editor.Document, term, exact, matches);

            if (matches.Count == 0)
            {
                ReplaceStatusText.Text = "No matches";
                return;
            }

            int length = Editor.Document.Length;
            long resulting = (long)length + (long)matches.Count * (replacement.Length - term.Length);

            if (resulting > int.MaxValue)
            {
                AppMessageBox.ShowError(this, "The result of replacing every match would be too large to hold.");
                return;
            }

            // This is the one edit that can exceed what undo will hold, and the
            // only honest thing to do is say so before doing it rather than let
            // Ctrl+Z quietly turn out to be unavailable afterwards.
            if (!TextView.FitsInUndoBudget(length, (int)resulting))
            {
                MessageBoxResult answer = AppMessageBox.Show(this,
                    string.Format("Replacing {0:N0} matches across a document this size is too large a change to keep " +
                                  "an undo record of.\n\nThis cannot be undone. Replace anyway?", matches.Count),
                    "Replace all", MessageBoxButton.YesNo);

                if (answer != MessageBoxResult.Yes) return;
            }

            try
            {
                Editor.ReplaceRange(0, length, BuildReplaced(matches, term, replacement, (int)resulting));

                ReplaceStatusText.Text = $"{matches.Count} replaced";
            }
            catch (OutOfMemoryException)
            {
                // Either the new text was never built or it has already been
                // taken; nothing here leaves the editor holding half a document.
                ReportOutOfMemory("replace that many matches");
                ReplaceStatusText.Text = "Out of memory";
            }
        }

        // Built into an array sized from the match count rather than through a
        // StringBuilder, so the result exists once instead of twice - the builder
        // and the string it was flattened into were both live at the moment the
        // document was about to allocate its own copy.
        private string BuildReplaced(List<int> matches, string term, string replacement, int resulting)
        {
            TextDocument document = Editor.Document;

            char[] result = new char[resulting];
            int written = 0;
            int copiedUpTo = 0;

            foreach (int start in matches)
            {
                int run = start - copiedUpTo;

                document.CopyTo(copiedUpTo, result, written, run);
                written += run;

                replacement.CopyTo(0, result, written, replacement.Length);
                written += replacement.Length;

                copiedUpTo = start + term.Length;
            }

            document.CopyTo(copiedUpTo, result, written, document.Length - copiedUpTo);

            return new string(result);
        }
    }
}
