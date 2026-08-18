using System.Windows.Input;

namespace PureNote
{
    public partial class MainWindow
    {
        private void Editor_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (IsWordSeparator(e.Key))
            {
                Editor.LockCurrentUndoUnit();
            }
        }

        private static bool IsWordSeparator(Key key)
        {
            return key == Key.Space
                || key == Key.Tab
                || key == Key.Enter
                || key == Key.OemPeriod
                || key == Key.OemComma
                || key == Key.OemSemicolon
                || key == Key.OemQuestion
                || key == Key.OemMinus;
        }
    }
}
