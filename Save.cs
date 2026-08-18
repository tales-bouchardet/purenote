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
            string text = LineEndings.Convert(Editor.Text, _lineEnding);

            // Encodings like Windows-1252 or ASCII silently substitute '?' for
            // anything they can't express, so the loss is invisible until the file
            // is reopened. Let the user back out while the text is still in memory.
            if (!EncodingDetector.CanRepresent(text, _currentEncoding))
            {
                MessageBoxResult answer = AppMessageBox.Show(this,
                    $"Some characters cannot be written as {EncodingDetector.GetDisplayName(_currentEncoding)} " +
                    "and will be replaced with '?'.\n\nSave anyway?",
                    "Unsupported characters", MessageBoxButton.YesNo);

                if (answer != MessageBoxResult.Yes) return;
            }

            if (!WriteFile(path, text)) return;

            _currentFilePath = path;
            _isDirty = false;
            UpdatePathDisplay();
        }

        // Writes through a temporary file in the same directory and swaps it into
        // place, so a write that fails partway (disk full, drive removed) leaves
        // the original file intact instead of truncated.
        private bool WriteFile(string path, string text)
        {
            string temp = path + ".purenote-tmp";

            try
            {
                File.WriteAllText(temp, text, _currentEncoding);

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
