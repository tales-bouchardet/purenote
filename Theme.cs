using System.Windows;
using System.Windows.Media;

namespace PureNote
{
    internal static class Theme
    {
        private static SolidColorBrush _dirty;
        private static SolidColorBrush _allMatches;
        private static SolidColorBrush _currentMatchFill;
        private static SolidColorBrush _currentMatchStroke;
        private static SolidColorBrush _footerText;

        public static SolidColorBrush Dirty => _dirty ?? (_dirty = Frozen(AccentColor));

        public static SolidColorBrush AllMatches => _allMatches ?? (_allMatches = Frozen(WithAlpha(AccentColor, 70)));

        public static SolidColorBrush CurrentMatchFill => _currentMatchFill ?? (_currentMatchFill = Frozen(WithAlpha(AccentColor, 85)));
        public static SolidColorBrush CurrentMatchStroke => _currentMatchStroke ?? (_currentMatchStroke = Frozen(AccentColor));

        public static SolidColorBrush FooterText => _footerText ?? (_footerText = Palette("SecondaryText"));

        private static Color AccentColor => Palette("Accent").Color;

        private static SolidColorBrush Palette(string key)
        {
            return (SolidColorBrush)Application.Current.Resources[key];
        }

        private static Color WithAlpha(Color color, byte alpha)
        {
            return Color.FromArgb(alpha, color.R, color.G, color.B);
        }

        private static SolidColorBrush Frozen(Color color)
        {
            SolidColorBrush brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
    }
}
