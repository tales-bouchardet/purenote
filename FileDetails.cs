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
            string encoding = EncodingDetector.GetDisplayName(_currentEncoding);
            string counts = $"Characters: {Editor.Text.Length}\n" +
                            $"Lines: {Editor.LineCount}";

            if (string.IsNullOrEmpty(_currentFilePath))
            {
                return "File has not been saved to disk yet.\n" +
                       $"Current encoding: {encoding}\n" +
                       counts;
            }

            try
            {
                FileInfo fileInfo = new FileInfo(_currentFilePath);

                if (!fileInfo.Exists)
                {
                    return $"Path: {_currentFilePath}\n" +
                           "The file no longer exists on disk.\n" +
                           $"Encoding: {encoding}\n" +
                           counts;
                }

                return $"Path: {_currentFilePath}\n" +
                       $"Size: {fileInfo.Length} bytes\n" +
                       $"Encoding: {encoding}\n" +
                       $"Last modified: {fileInfo.LastWriteTime}\n" +
                       counts;
            }
            catch (Exception ex)
            {
                return $"Path: {_currentFilePath}\n" +
                       $"Details unavailable: {ex.Message}\n" +
                       $"Encoding: {encoding}\n" +
                       counts;
            }
        }
    }
}
