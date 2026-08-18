using System.Windows;

namespace PureNote
{
    public partial class MainWindow
    {
        private bool ConfirmDiscardChanges()
        {
            if (!_isDirty) return true;

            MessageBoxResult result = AppMessageBox.Show(this,
                "There are unsaved changes. Do you want to save them first?",
                "Unsaved changes", MessageBoxButton.YesNoCancel);

            // Anything that isn't an explicit "discard" answer — Cancel, or the
            // dialog being dismissed with Alt+F4 (MessageBoxResult.None) — has to
            // block the operation. Falling through to "proceed" would throw away
            // the unsaved buffer on a keystroke users press to back out.
            if (result == MessageBoxResult.Yes)
            {
                Save_Click(this, new RoutedEventArgs());

                return !_isDirty;
            }

            return result == MessageBoxResult.No;
        }
    }
}
