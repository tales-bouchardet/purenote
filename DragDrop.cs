using System;
using System.IO;
using System.Windows;

namespace PureNote
{
    public partial class MainWindow
    {
        private void Window_DragOver(object sender, DragEventArgs e)
        {
            // Only claim file drops. These are preview handlers on the window, so
            // handling everything would tunnel past the editor and silently kill
            // its built-in drag-and-drop of selected text.
            if (GetDroppedFile(e) == null) return;

            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            string path = GetDroppedFile(e);
            if (path == null) return;

            e.Handled = true;

            // The source application's drag loop is blocked until this handler
            // returns, so a modal prompt here would freeze Explorer. Let the drop
            // finish first, then ask.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (ConfirmDiscardChanges()) LoadFile(path);
            }));
        }

        private static string GetDroppedFile(DragEventArgs e)
        {
            try
            {
                if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return null;

                string[] paths = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (paths == null || paths.Length == 0) return null;

                // A folder would otherwise reach File.ReadAllBytes and surface as a
                // bogus "you do not have permission" error.
                return File.Exists(paths[0]) ? paths[0] : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
