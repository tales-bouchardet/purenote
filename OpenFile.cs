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

            // Detect returns null for bytes that match no BOM and are not valid
            // UTF-8. Opening still goes ahead on the UTF-8 assumption rather than
            // stopping to ask: it is right for all but a shrinking minority of
            // files, and the encoding menu is there for the rest.
            Encoding encoding = EncodingDetector.Detect(bytes) ?? EncodingDetector.Utf8NoBom;
            string text = EncodingDetector.Decode(bytes, encoding);

            _lineEnding = LineEndings.Detect(text);

            // Drop the previous document's undo history along with the document
            // itself, or Ctrl+Z would restore the old file's text while the path,
            // encoding and line ending all point at the new one — and saving then
            // writes the old content over the new file.
            string normalised = LineEndings.Convert(text, LineEndings.Crlf);

            // Puts the first screenful up straight away and feeds in the rest a
            // piece at a time; it also owns the undo reset and the tracked
            // length, which it can only settle once the whole file is in.
            BeginLoad(normalised);

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
    }
}
