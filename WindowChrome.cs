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
            // Double click on the caption maximises and restores. Every window on
            // Windows does this, and a title bar that does not is the sort of
            // thing people notice without being able to say what is missing.
            if (e.ClickCount == 2)
            {
                ToggleMaximised();
                return;
            }

            if (WindowState == WindowState.Maximized) RestoreUnderCursor(e);

            // Throws if the button came up in between - which it can, because
            // restoring above takes long enough for a quick click to finish.
            try
            {
                DragMove();
            }
            catch (InvalidOperationException)
            {
            }
        }

        // Dragging a maximised window restores it and keeps the drag going,
        // rather than moving a full-screen window around. The restored window is
        // placed so the pointer stays at the same proportional spot along the
        // caption, which is what makes it feel like the window was picked up
        // rather than teleported.
        private void RestoreUnderCursor(MouseButtonEventArgs e)
        {
            Point inWindow = e.GetPosition(this);
            if (ActualWidth <= 0) return;

            double across = inWindow.X / ActualWidth;

            // PointToScreen answers in physical pixels while Left and Top are
            // set in device-independent ones, so on any display that is not at
            // 100% the two have to be reconciled or the window lands elsewhere.
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

            // Only once leaving is settled. The spill files hold work that was
            // emptied out of memory, and until the question above is answered
            // they are the only copy of it.
            DiscardAllSpills();
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
