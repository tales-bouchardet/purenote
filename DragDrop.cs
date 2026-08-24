using System;
using System.IO;
using System.Windows;

namespace PureNote
{
    public partial class MainWindow
    {
        private void Window_DragOver(object sender, DragEventArgs e)
        {
            if (GetDroppedFile(e) == null) return;

            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            string path = GetDroppedFile(e);
            if (path == null) return;

            e.Handled = true;

            Dispatcher.BeginInvoke(new Action(() => LoadFile(path)));
        }

        private static string GetDroppedFile(DragEventArgs e)
        {
            try
            {
                if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return null;

                string[] paths = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (paths == null || paths.Length == 0) return null;

                return File.Exists(paths[0]) ? paths[0] : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
