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

        // FileStream's own buffer, which every read here is far larger than -
        // past its size FileStream reads straight into the caller's array and
        // never touches this. Kept small so it is not a second megabyte
        // buffering a megabyte. It is only here because asking for
        // SequentialScan means using the overload that also takes a size.
        private const int StreamBuffer = 4096;

        // Opening is now one operation rather than a staged one.
        //
        // It used to be split across a first chunk, a dispatcher pump, an
        // adaptive slice size and a progress readout, all of it built to spread
        // out the eighteen seconds a TextBox spent laying the file out before it
        // would show anything. None of that work was the file's fault, and none
        // of it survives: the document is built by passes that read the file a
        // megabyte at a time, and the view never looks at more of it than fits on
        // screen. Measured on a 256 MB file, the whole open comes to about one
        // second and never holds more than the document itself.
        private void LoadFile(string path)
        {
            bool discardingLarge = IsLargeDocument;

            TextDocument document;
            Encoding encoding;
            DocumentShape shape;

            try
            {
                // One handle for all three passes rather than three opens. The
                // fill pass writes into an array the measure pass sized, so a
                // file that changed between them would be a buffer overrun -
                // and FileShare.Read, which is what File.ReadAllBytes used,
                // keeps writers out for as long as this is held.
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                    FileShare.Read, StreamBuffer, FileOptions.SequentialScan))
                {
                    // A file that matches no BOM and is not valid UTF-8 comes
                    // back as Windows-1252, which round-trips every byte value,
                    // so a file this guesses wrong is displayed wrong rather than
                    // damaged on the next save. Opening goes ahead on the guess
                    // rather than stopping to ask, and the encoding menu is there
                    // for the rest. See EncodingDetector.Ansi.
                    encoding = EncodingDetector.Detect(stream);

                    // Only ever a mark that was actually seen: Detect returns a
                    // preamble-carrying encoding only when it matched one.
                    int preamble = encoding.GetPreamble().Length;

                    if (!DocumentLoader.TryMeasure(stream, encoding, preamble, out shape))
                    {
                        ReportTooLarge(path);
                        return;
                    }

                    document = DocumentLoader.Load(stream, encoding, preamble, shape);
                }
            }
            catch (OutOfMemoryException)
            {
                // The current document is left alone, so there is something to go
                // back to. Without this the allocation takes the process down and
                // the crash handler tries to dump the buffer with no memory to do
                // it in.
                ReportTooLarge(path);
                return;
            }
            catch (UnauthorizedAccessException)
            {
                ReportReadDenied(path);
                return;
            }
            catch (SecurityException)
            {
                ReportReadDenied(path);
                return;
            }
            catch (IOException ex)
            {
                AppMessageBox.ShowError(this, $"Could not open the file:\n{path}\n\n{ex.Message}");
                return;
            }

            DropFindMatches();
            Editor.SetDocument(document);

            _currentFilePath = path;
            _currentEncoding = encoding;
            _lineEnding = shape.LineEnding;
            _isDirty = false;

            UpdatePathDisplay();
            UpdateCounts();
            SetEncodingChecked(encoding);
            SetLineEndingChecked(_lineEnding);

            // The document that was just replaced is the only large thing this
            // let go of - the file itself was never held. Letting go of it is not
            // the same as getting the memory back: it was one allocation the size
            // of the document, which put it on the large object heap, and nothing
            // there returns to the process until a collection runs.
            if (discardingLarge) CompactLargeObjectHeap();

            if (FindPopup.IsOpen) UpdateFindMatches();
        }

        // The document is held as UTF-16 with an index beside it, so what opening
        // costs is roughly twice the size on disk plus four bytes a line.
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
