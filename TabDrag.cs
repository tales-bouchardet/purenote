using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace PureNote
{
    public partial class MainWindow
    {
        private const double TearOffMargin = 50;

        private static readonly List<MainWindow> OpenWindows = new List<MainWindow>();

        private FileTab _draggingTab;
        private FrameworkElement _draggingElement;
        private Point _dragStartInStrip;
        private bool _dragArmed;

        private void Tab_MouseMove(object sender, MouseEventArgs e)
        {
            if (_draggingTab == null || e.LeftButton != MouseButtonState.Pressed) return;

            Point current = e.GetPosition(TabStrip);

            if (!_dragArmed)
            {
                double dx = current.X - _dragStartInStrip.X;
                double dy = current.Y - _dragStartInStrip.Y;

                if (Math.Abs(dx) < SystemParameters.MinimumHorizontalDragDistance
                    && Math.Abs(dy) < SystemParameters.MinimumVerticalDragDistance) return;

                _dragArmed = true;
                Mouse.OverrideCursor = Cursors.SizeAll;
            }

            if (IsOutsideStrip(current.Y)) return;

            ReorderTo(current.X);
        }

        private void Tab_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_draggingTab == null) return;

            FileTab tab = _draggingTab;
            FrameworkElement element = _draggingElement;
            bool armed = _dragArmed;

            _draggingTab = null;
            _draggingElement = null;
            _dragArmed = false;

            Mouse.OverrideCursor = null;
            element.ReleaseMouseCapture();

            if (!armed) return;

            Point screenPoint = element.PointToScreen(e.GetPosition(element));

            double xInTarget;
            MainWindow target = FindWindowUnderTabStrip(this, screenPoint, out xInTarget);

            if (target != null)
            {
                DetachTabForTransfer(tab);
                target.ReceiveTab(tab, xInTarget);
                target.Activate();
                return;
            }

            if (IsOutsideStrip(e.GetPosition(TabStrip).Y))
            {
                TearOff(tab, screenPoint);
            }
        }

        private void Tab_LostMouseCapture(object sender, MouseEventArgs e)
        {
            _draggingTab = null;
            _draggingElement = null;
            _dragArmed = false;
            Mouse.OverrideCursor = null;
        }

        private bool IsOutsideStrip(double yInStrip)
        {
            return yInStrip < -TearOffMargin || yInStrip > TabStrip.ActualHeight + TearOffMargin;
        }

        private void ReorderTo(double xInStrip)
        {
            int from = _tabs.IndexOf(_draggingTab);
            FileTab over = TabUnderX(xInStrip);
            if (over == null || over == _draggingTab) return;

            int to = _tabs.IndexOf(over);
            if (from < 0 || to < 0 || from == to) return;

            FrameworkElement container = TabStrip.ItemContainerGenerator.ContainerFromItem(over) as FrameworkElement;
            if (container == null) return;

            double mid = container.TranslatePoint(new Point(0, 0), TabStrip).X + container.ActualWidth / 2;

            if (to > from && xInStrip < mid) return;
            if (to < from && xInStrip > mid) return;

            _tabs.Move(from, to);
        }

        private FileTab TabUnderX(double xInStrip)
        {
            foreach (FileTab tab in _tabs)
            {
                FrameworkElement container = TabStrip.ItemContainerGenerator.ContainerFromItem(tab) as FrameworkElement;
                if (container == null) continue;

                double left = container.TranslatePoint(new Point(0, 0), TabStrip).X;
                if (xInStrip >= left && xInStrip <= left + container.ActualWidth) return tab;
            }

            return null;
        }

        private static MainWindow FindWindowUnderTabStrip(MainWindow exclude, Point screenPoint, out double xInStrip)
        {
            foreach (MainWindow window in OpenWindows)
            {
                if (window == exclude) continue;
                if (window.TryScreenPointToTabStrip(screenPoint, out xInStrip)) return window;
            }

            xInStrip = 0;
            return null;
        }

        private bool TryScreenPointToTabStrip(Point screenPoint, out double xInStrip)
        {
            xInStrip = 0;
            if (!IsVisible || WindowState == WindowState.Minimized) return false;

            DpiScale dpi = VisualTreeHelper.GetDpi(this);
            Point origin = TabStripRow.PointToScreen(new Point(0, 0));
            Rect bounds = new Rect(origin,
                new Size(TabStripRow.ActualWidth * dpi.DpiScaleX, TabStripRow.ActualHeight * dpi.DpiScaleY));

            if (!bounds.Contains(screenPoint)) return false;

            xInStrip = (screenPoint.X - origin.X) / dpi.DpiScaleX;
            return true;
        }

        private void ReceiveTab(FileTab tab, double xInStrip)
        {
            int index = InsertionIndexForX(xInStrip);
            _tabs.Insert(Math.Min(index, _tabs.Count), tab);

            Activate(tab);
            Editor.Focus();
        }

        private int InsertionIndexForX(double xInStrip)
        {
            for (int i = 0; i < _tabs.Count; i++)
            {
                FrameworkElement container = TabStrip.ItemContainerGenerator.ContainerFromItem(_tabs[i]) as FrameworkElement;
                if (container == null) continue;

                double left = container.TranslatePoint(new Point(0, 0), TabStrip).X;
                if (xInStrip < left + container.ActualWidth / 2) return i;
            }

            return _tabs.Count;
        }

        private void TearOff(FileTab tab, Point screenPoint)
        {
            if (_tabs.Count <= 1) return;

            DpiScale dpi = VisualTreeHelper.GetDpi(this);

            DetachTabForTransfer(tab);

            MainWindow window = new MainWindow(tab)
            {
                Left = screenPoint.X / dpi.DpiScaleX,
                Top = screenPoint.Y / dpi.DpiScaleY,
                Width = ActualWidth,
                Height = ActualHeight
            };

            window.Show();
        }
    }
}
