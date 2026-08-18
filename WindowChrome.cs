using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace PureNote
{
    public partial class MainWindow
    {
        private const int DwmwaUseImmersiveDarkMode = 20;
        private const int DwmwaUseImmersiveDarkModeLegacy = 19;

        private void TopBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void CloseWindow_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (!ConfirmDiscardChanges())
            {
                e.Cancel = true;
            }
        }

        // Without this, DWM has no idea this window is dark-themed and falls
        // back to its light-mode fill for chrome it composites itself (title
        // bar, and the placeholder it paints over freshly-exposed area while
        // live-resizing) — that's the white strip/flash during a drag-resize.
        private void Window_SourceInitialized(object sender, System.EventArgs e)
        {
            HwndSource source = (HwndSource)PresentationSource.FromVisual(this);
            if (source == null) return;

            int useDarkMode = 1;
            int result = DwmSetWindowAttribute(source.Handle, DwmwaUseImmersiveDarkMode, ref useDarkMode, sizeof(int));
            if (result != 0)
            {
                DwmSetWindowAttribute(source.Handle, DwmwaUseImmersiveDarkModeLegacy, ref useDarkMode, sizeof(int));
            }
        }

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(System.IntPtr hwnd, int attribute, ref int value, int valueSize);
    }
}
