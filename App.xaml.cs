using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace PureNote
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            DispatcherUnhandledException += OnUnhandledException;
        }

        // A crash would otherwise take the unsaved buffer with it silently. Dump
        // whatever is in the editor to disk first, then tell the user where it went.
        private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            string recoveryPath = TryWriteRecoveryFile();

            string message = "purenote hit an unexpected error and needs to close.\n\n" + e.Exception.Message;
            if (recoveryPath != null)
            {
                message += "\n\nYour text was saved to:\n" + recoveryPath;
            }

            MessageBox.Show(message, "purenote", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private string TryWriteRecoveryFile()
        {
            try
            {
                PureNote.MainWindow window = MainWindow as PureNote.MainWindow;
                if (window == null) return null;

                string text = window.EditorText;
                if (string.IsNullOrEmpty(text)) return null;

                string path = Path.Combine(Path.GetTempPath(),
                    "purenote-recovery-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");

                File.WriteAllText(path, text);
                return path;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
