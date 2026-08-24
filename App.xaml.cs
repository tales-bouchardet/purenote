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

        // The boxes and buttons in Find and Replace come from WPF-UI, and they
        // take their accent from brushes its own dictionaries resolve when those
        // dictionaries are parsed. A StaticResource inside that dictionary is
        // bound to the brush that was there at the time, so replacing the key
        // afterwards - which is what the accent overrides in App.xaml do - never
        // reaches them.
        //
        // Measured rather than assumed: with all twelve accent keys overridden to
        // amber, a TextBox, a RadioButton and a CheckBox still rendered in the
        // system blue. This is the door WPF-UI leaves for changing them, and it
        // is the only one that works.
        private void ApplyAccent()
        {
            SolidColorBrush accent = Resources["Accent"] as SolidColorBrush;
            if (accent == null) return;

            ApplicationAccentColorManager.Apply(accent.Color, ApplicationTheme.Dark);
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
