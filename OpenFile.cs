using System;
using System.IO;
using System.Security;
using System.Text;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;

namespace PureNote
{
    public partial class MainWindow
    {
        private void Open_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Open_Click(sender, new RoutedEventArgs());
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            if (!ConfirmDiscardChanges()) return;

            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "All files (*.*)|*.*";

            if (dialog.ShowDialog() == true)
            {
                LoadFile(dialog.FileName);
            }
        }

        private void LoadFile(string path)
        {
            byte[] bytes = ReadFile(path);
            if (bytes == null) return;

            Encoding encoding = EncodingDetector.Detect(bytes) ?? AskEncoding();
            string text = EncodingDetector.Decode(bytes, encoding);

            _lineEnding = LineEndings.Detect(text);

            // Drop the previous document's undo history along with the document
            // itself, or Ctrl+Z would restore the old file's text while the path,
            // encoding and line ending all point at the new one — and saving then
            // writes the old content over the new file.
            Editor.IsUndoEnabled = false;
            Editor.Text = LineEndings.Convert(text, LineEndings.Crlf);
            Editor.IsUndoEnabled = true;
            EditorScroll.ScrollToVerticalOffset(0);

            _currentFilePath = path;
            _currentEncoding = encoding;
            _isDirty = false;

            UpdatePathDisplay();
            SetEncodingChecked(encoding);
            SetLineEndingChecked(_lineEnding);
        }

        private byte[] ReadFile(string path)
        {
            try
            {
                return File.ReadAllBytes(path);
            }
            catch (UnauthorizedAccessException)
            {
                ReportReadDenied(path);
            }
            catch (SecurityException)
            {
                ReportReadDenied(path);
            }
            catch (IOException ex)
            {
                AppMessageBox.ShowError(this, $"Could not open the file:\n{path}\n\n{ex.Message}");
            }

            return null;
        }

        private void ReportReadDenied(string path)
        {
            AppMessageBox.ShowError(this,
                $"You do not have permission to read:\n{path}\n\n" +
                "Reopen purenote as administrator to open this file.");
        }

        private Encoding AskEncoding()
        {
            MessageBoxResult result = AppMessageBox.Show(this,
                "Could not detect the file encoding.\n\n" +
                "Yes = UTF-8\nNo = Windows-1252\nCancel = ISO-8859-1",
                "Encoding", MessageBoxButton.YesNoCancel);

            if (result == MessageBoxResult.Yes) return EncodingDetector.Utf8NoBom;
            if (result == MessageBoxResult.No) return Encoding.GetEncoding(1252);
            return Encoding.GetEncoding(28591);
        }
    }
}
