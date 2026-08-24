using System.Collections.Generic;
using System.Windows;

namespace PureNote
{
    public partial class MainWindow
    {
        // Asked on the way out, and now on behalf of every tab rather than one
        // document. A tab that was emptied while carrying changes still counts:
        // its work is in a spill file, and a spill file is deleted on a clean
        // exit, so leaving without asking would lose it just the same.
        private bool ConfirmDiscardChanges()
        {
            List<FileTab> unsaved = new List<FileTab>();

            foreach (FileTab tab in _tabs)
            {
                if (tab.IsDirty) unsaved.Add(tab);
            }

            if (unsaved.Count == 0) return true;

            MessageBoxResult result = AppMessageBox.Show(this,
                unsaved.Count == 1
                    ? "There are unsaved changes in " + unsaved[0].Name + ". Do you want to save them first?"
                    : "There are unsaved changes in " + unsaved.Count + " files. Do you want to save them first?",
                "Unsaved changes", MessageBoxButton.YesNoCancel);

            // Anything that isn't an explicit "discard" answer - Cancel, or the
            // dialog being dismissed with Alt+F4 (MessageBoxResult.None) - has to
            // block the operation. Falling through to "proceed" would throw away
            // the unsaved buffer on a keystroke users press to back out.
            if (result != MessageBoxResult.Yes) return result == MessageBoxResult.No;

            // Each in turn, in front, because saving writes what the editor is
            // holding. A cancelled Save As stops the whole thing.
            foreach (FileTab tab in unsaved)
            {
                Activate(tab);
                Save_Click(this, new RoutedEventArgs());

                if (tab.IsDirty) return false;
            }

            return true;
        }
    }
}
