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

            if (result == MessageBoxResult.Cancel) return false;

            if (result == MessageBoxResult.Yes)
            {
                Save_Click(this, new RoutedEventArgs());

                return !_isDirty;
            }

            return true;
        }
    }
}
