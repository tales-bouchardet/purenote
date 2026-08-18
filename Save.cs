using System;
using System.IO;
using System.Security;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;

namespace PureNote
{
    public partial class MainWindow
    {
        private void Save_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Save_Click(sender, new RoutedEventArgs());
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentFilePath))
            {
                SaveAs_Click(sender, e);
                return;
            }

            SaveToFile(_currentFilePath);
        }

        private void SaveAs_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Filter = "All files (*.*)|*.*";

            if (!string.IsNullOrEmpty(_currentFilePath))
            {
                dialog.FileName = Path.GetFileName(_currentFilePath);
            }

            if (dialog.ShowDialog() == true)
            {
                SaveToFile(dialog.FileName);
            }
        }

        private bool SaveToFile(string path)
        {
            string text = LineEndings.Convert(Editor.Text, _lineEnding);

            try
            {
                File.WriteAllText(path, text, _currentEncoding);
            }
            catch (UnauthorizedAccessException)
            {
                ReportSaveDenied(path);
                return false;
            }
            catch (SecurityException)
            {
                ReportSaveDenied(path);
                return false;
            }
            catch (IOException ex)
            {
                AppMessageBox.ShowError(this, $"Could not save the file:\n{path}\n\n{ex.Message}");
                return false;
            }

            _currentFilePath = path;
            _isDirty = false;
            UpdatePathDisplay();
            return true;
        }

        private void ReportSaveDenied(string path)
        {
            AppMessageBox.ShowError(this,
                $"You do not have permission to write to:\n{path}\n\n" +
                "Save somewhere else, or reopen purenote as administrator.");
        }
    }
}
