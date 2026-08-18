using System;
using System.IO;
using System.Windows;

namespace PureNote
{
    public partial class MainWindow
    {
        private void Info_Click(object sender, RoutedEventArgs e)
        {
            AppMessageBox.Show(this, BuildFileDetails(), "File information", MessageBoxButton.OK);
        }

        private string BuildFileDetails()
        {
            return BuildFileHeader() + "\n" +
                   $"Encoding: {EncodingDetector.GetDisplayName(_currentEncoding)}\n" +
                   $"Characters: {CountDisplayCharacters()}\n" +
                   $"Lines: {CountLines(Editor.Text)}";
        }

        private string BuildFileHeader()
        {
            if (string.IsNullOrEmpty(_currentFilePath))
            {
                return "File has not been saved to disk yet.";
            }

            try
            {
                FileInfo fileInfo = new FileInfo(_currentFilePath);

                if (!fileInfo.Exists)
                {
                    return $"Path: {_currentFilePath}\n" +
                           "The file no longer exists on disk.";
                }

                return $"Path: {_currentFilePath}\n" +
                       $"Size: {fileInfo.Length} bytes\n" +
                       $"Last modified: {fileInfo.LastWriteTime}";
            }
            catch (Exception ex)
            {
                return $"Path: {_currentFilePath}\n" +
                       $"Details unavailable: {ex.Message}";
            }
        }
    }
}
