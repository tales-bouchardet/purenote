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

            DocumentShape shape;
            DocumentDecoder decoder;

            try
            {
                // Settles the line and character counts, and which line ending the
                // file arrived with, by decoding it once into a single reused
                // buffer. Nothing the size of the document is allocated here — the
                // bytes stay the only full copy until the editor builds its own.
                if (!DocumentDecoder.TryMeasure(bytes, encoding, out shape))
                {
                    ReportTooLarge(path);
                    return;
                }

                decoder = new DocumentDecoder(bytes, encoding);
            }
            catch (OutOfMemoryException)
            {
                // Left the current document alone, so there is something to go
                // back to. Without this the allocation takes the process down and
                // the crash handler tries to dump the buffer with no memory to do
                // it in.
                ReportTooLarge(path);
                return;
            }

            // Puts the first screenful up straight away and feeds in the rest a
            // slice at a time, decoding each one on its way in; it also owns the
            // undo reset and the tracked length, which it can only settle once the
            // whole file is in.
            BeginLoad(decoder, shape);

            _currentFilePath = path;
            _currentEncoding = encoding;
            _lineEnding = shape.LineEnding;
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
            catch (OutOfMemoryException)
            {
                ReportTooLarge(path);
            }
            catch (IOException ex)
            {
                AppMessageBox.ShowError(this, $"Could not open the file:\n{path}\n\n{ex.Message}");
            }

            return null;
        }

        // The editor holds the file as UTF-16 and builds a normalised copy beside
        // it, so what opening costs is a multiple of the size on disk rather than
        // the size on disk.
        private void ReportTooLarge(string path)
        {
            AppMessageBox.ShowError(this,
                $"Not enough memory to open:\n{path}\n\n" +
                "The file is too large for purenote to hold in memory.");
        }

        private void ReportReadDenied(string path)
        {
            AppMessageBox.ShowError(this,
                $"You do not have permission to read:\n{path}\n\n" +
                "Reopen purenote as administrator to open this file.");
        }
    }
}
