using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace PureNote
{
    internal sealed class PopupDrag
    {
        private readonly Popup _popup;
        private readonly UIElement _header;
        private readonly UIElement _reference;

        private bool _dragging;
        private Point _start;
        private double _startOffsetX;
        private double _startOffsetY;

        private PopupDrag(Popup popup, UIElement header, UIElement reference)
        {
            _popup = popup;
            _header = header;
            _reference = reference;

            header.MouseLeftButtonDown += OnMouseDown;
            header.MouseMove += OnMouseMove;
            header.MouseLeftButtonUp += OnMouseUp;
        }

        public static void Attach(Popup popup, UIElement header, UIElement reference)
        {
            new PopupDrag(popup, header, reference);
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            _dragging = true;
            _start = e.GetPosition(_reference);
            _startOffsetX = _popup.HorizontalOffset;
            _startOffsetY = _popup.VerticalOffset;
            _header.CaptureMouse();
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!_dragging) return;

            Point current = e.GetPosition(_reference);
            _popup.HorizontalOffset = _startOffsetX + (current.X - _start.X);
            _popup.VerticalOffset = _startOffsetY + (current.Y - _start.Y);
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            _dragging = false;
            _header.ReleaseMouseCapture();
        }
    }
}
