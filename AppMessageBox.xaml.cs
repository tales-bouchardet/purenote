using System.Windows;
using System.Windows.Input;

namespace PureNote
{
    public partial class AppMessageBox : Window
    {
        private readonly MessageBoxButton _buttons;

        public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

        private AppMessageBox(string message, string title, MessageBoxButton buttons)
        {
            InitializeComponent();

            _buttons = buttons;
            TitleText.Text = title;
            MessageText.Text = message;

            bool hasCancel = buttons == MessageBoxButton.OKCancel || buttons == MessageBoxButton.YesNoCancel;
            bool hasYesNo = buttons == MessageBoxButton.YesNo || buttons == MessageBoxButton.YesNoCancel;
            bool hasOk = buttons == MessageBoxButton.OK || buttons == MessageBoxButton.OKCancel;

            YesButton.Visibility = Visible(hasYesNo);
            NoButton.Visibility = Visible(hasYesNo);
            CancelButton.Visibility = Visible(hasCancel);
            OkButton.Visibility = Visible(hasOk);
        }

        public static MessageBoxResult Show(Window owner, string message, string title, MessageBoxButton buttons)
        {
            AppMessageBox dialog = new AppMessageBox(message, title, buttons) { Owner = owner };
            dialog.ShowDialog();
            return dialog.Result;
        }

        public static void ShowError(Window owner, string message)
        {
            Show(owner, message, "Error", MessageBoxButton.OK);
        }

        private static Visibility Visible(bool visible)
        {
            return visible ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            if (_buttons == MessageBoxButton.OKCancel || _buttons == MessageBoxButton.YesNoCancel)
            {
                Result = MessageBoxResult.Cancel;
            }
            else if (_buttons == MessageBoxButton.OK)
            {
                Result = MessageBoxResult.OK;
            }
            else
            {
                Result = MessageBoxResult.No;
            }

            Close();
        }

        private void Yes_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.Yes;
            Close();
        }

        private void No_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.No;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.Cancel;
            Close();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.OK;
            Close();
        }
    }
}
