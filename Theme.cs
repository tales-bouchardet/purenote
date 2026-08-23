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
        private static SolidColorBrush _crumb;
        private static SolidColorBrush _crumbLeaf;

        public static SolidColorBrush Dirty => _dirty ?? (_dirty = Frozen(AccentColor));

        public static SolidColorBrush AllMatches => _allMatches ?? (_allMatches = Frozen(WithAlpha(AccentColor, 70)));

        public static SolidColorBrush CurrentMatchFill => _currentMatchFill ?? (_currentMatchFill = Frozen(WithAlpha(AccentColor, 85)));
        public static SolidColorBrush CurrentMatchStroke => _currentMatchStroke ?? (_currentMatchStroke = Frozen(AccentColor));

        public static SolidColorBrush FooterText => _footerText ?? (_footerText = Palette("SecondaryText"));

        // The folders a file sits in, and the file itself. Two shades rather than
        // one: the trail is context and the name at the end of it is the answer,
        // and reading them at the same weight makes the eye hunt for the part it
        // came for.
        public static SolidColorBrush Crumb => _crumb ?? (_crumb = Palette("MutedText"));
        public static SolidColorBrush CrumbLeaf => _crumbLeaf ?? (_crumbLeaf = Palette("EditorText"));

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
