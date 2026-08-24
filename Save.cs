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

        private void SaveToFile(string path)
        {
            if (!EncodingDetector.CanRepresent(Editor.Document, _currentEncoding))
            {
                MessageBoxResult answer = AppMessageBox.Show(this,
                    $"Some characters cannot be written as {EncodingDetector.GetDisplayName(_currentEncoding)} " +
                    "and will be replaced with '?'.\n\nSave anyway?",
                    "Unsupported characters", MessageBoxButton.YesNo);

                if (answer != MessageBoxResult.Yes) return;
            }

            if (!WriteFile(path)) return;

            _currentFilePath = path;
            _isDirty = false;
            UpdatePathDisplay();
        }

        private bool WriteFile(string path)
        {
            string temp = path + ".purenote-tmp";

            try
            {
                using (StreamWriter writer = new StreamWriter(temp, false, _currentEncoding))
                {
                    Editor.Document.WriteTo(writer, _lineEnding);
                }

                if (File.Exists(path))
                {
                    File.Replace(temp, path, null);
                }
                else
                {
                    File.Move(temp, path);
                }

                return true;
            }
            catch (UnauthorizedAccessException)
            {
                ReportSaveDenied(path);
            }
            catch (SecurityException)
            {
                ReportSaveDenied(path);
            }
            catch (OutOfMemoryException)
            {
                ReportOutOfMemory("save this document");
            }
            catch (IOException ex)
            {
                AppMessageBox.ShowError(this, $"Could not save the file:\n{path}\n\n{ex.Message}");
            }
            finally
            {
                DeleteIfExists(temp);
            }

            return false;
        }

        private static void DeleteIfExists(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private void ReportSaveDenied(string path)
        {
            AppMessageBox.ShowError(this,
                $"You do not have permission to write to:\n{path}\n\n" +
                "Save somewhere else, or reopen purenote as administrator.");
        }
    }
}
