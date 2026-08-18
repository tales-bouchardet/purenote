using System.Windows;
using System.Windows.Input;

namespace PureNote
{
    public partial class AppMessageBox : Window
    {
        private readonly MessageBoxResult _dismissResult;

        public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

        private AppMessageBox(string message, string title, MessageBoxButton buttons)
        {
            InitializeComponent();

            TitleText.Text = title;
            MessageText.Text = message;

            bool hasCancel = buttons == MessageBoxButton.OKCancel || buttons == MessageBoxButton.YesNoCancel;
            bool hasYesNo = buttons == MessageBoxButton.YesNo || buttons == MessageBoxButton.YesNoCancel;
            bool hasOk = buttons == MessageBoxButton.OK || buttons == MessageBoxButton.OKCancel;

            YesButton.Visibility = Visible(hasYesNo);
            NoButton.Visibility = Visible(hasYesNo);
            CancelButton.Visibility = Visible(hasCancel);
            OkButton.Visibility = Visible(hasOk);

            _dismissResult = hasCancel ? MessageBoxResult.Cancel
                : hasOk ? MessageBoxResult.OK
                : MessageBoxResult.No;

            // Dismissing the window itself (Alt+F4, Esc, the task bar) has to land
            // on the same answer as the close button, otherwise Result stays None
            // and callers can't tell "backed out" from "chose to proceed".
            Closing += (s, e) => { if (Result == MessageBoxResult.None) Result = _dismissResult; };
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
            Result = _dismissResult;
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
