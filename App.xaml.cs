using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Wpf.Ui.Appearance;

namespace PureNote
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            ApplyAccent();
            DispatcherUnhandledException += OnUnhandledException;
        }

        private void ApplyAccent()
        {
            SolidColorBrush accent = Resources["Accent"] as SolidColorBrush;
            if (accent == null) return;

            ApplicationAccentColorManager.Apply(accent.Color, ApplicationTheme.Dark);
        }

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

                string path = Path.Combine(Path.GetTempPath(),
                    "purenote-recovery-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");

                return window.TryWriteRecovery(path) ? path : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
