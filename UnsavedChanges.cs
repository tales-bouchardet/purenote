using System.Collections.Generic;
using System.Windows;

namespace PureNote
{
    public partial class MainWindow
    {
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

            if (result != MessageBoxResult.Yes) return result == MessageBoxResult.No;

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
