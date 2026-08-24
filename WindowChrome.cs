using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace PureNote
{
    public partial class MainWindow
    {
        private const int DwmwaUseImmersiveDarkMode = 20;
        private const int DwmwaUseImmersiveDarkModeLegacy = 19;

        private void TopBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                ToggleMaximised();
                return;
            }

            if (WindowState == WindowState.Maximized) RestoreUnderCursor(e);

            try
            {
                DragMove();
            }
            catch (InvalidOperationException)
            {
            }
        }

        private void RestoreUnderCursor(MouseButtonEventArgs e)
        {
            Point inWindow = e.GetPosition(this);
            if (ActualWidth <= 0) return;

            double across = inWindow.X / ActualWidth;

            DpiScale dpi = VisualTreeHelper.GetDpi(this);
            Point onScreen = PointToScreen(inWindow);

            double restoredWidth = RestoreBounds.Width;

            WindowState = WindowState.Normal;

            Left = onScreen.X / dpi.DpiScaleX - restoredWidth * across;
            Top = onScreen.Y / dpi.DpiScaleY - inWindow.Y;
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            ToggleMaximised();
        }

        private void ToggleMaximised()
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
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
                return;
            }

            DiscardAllSpills();
        }

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
