using System;
using System.Windows;

namespace PureNote
{
    public partial class MainWindow
    {
        private void Window_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = HasSingleFile(e) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            string path = GetDroppedFile(e);
            if (path == null) return;

            e.Handled = true;

            if (!ConfirmDiscardChanges()) return;

            LoadFile(path);
        }

        private static bool HasSingleFile(DragEventArgs e)
        {
            return GetDroppedFile(e) != null;
        }

        private static string GetDroppedFile(DragEventArgs e)
        {
            try
            {
                if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return null;

                string[] paths = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (paths == null || paths.Length == 0) return null;

                return paths[0];
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
