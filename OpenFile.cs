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
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "All files (*.*)|*.*";

            if (dialog.ShowDialog() == true)
            {
                LoadFile(dialog.FileName);
            }
        }

        internal void OpenFromExternal(string path)
        {
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;

            Activate();
            LoadFile(path);
        }

        private const int StreamBuffer = 4096;

        private void LoadFile(string path)
        {
            bool discardingLarge = IsLargeDocument;

            TextDocument document;
            Encoding encoding;
            DocumentShape shape;

            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                    FileShare.Read, StreamBuffer, FileOptions.SequentialScan))
                {
                    encoding = EncodingDetector.Detect(stream);

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

            if (!CanReuseActiveTab())
            {
                Detach(_active);
                _active = NewTab(path, encoding, shape.LineEnding);
                _active.IsActive = true;
            }

            _switching = true;
            try
            {
                Editor.SetDocument(document);
            }
            finally
            {
                _switching = false;
            }

            _currentFilePath = path;
            _currentEncoding = encoding;
            _lineEnding = shape.LineEnding;
            _isDirty = false;

            UpdatePathDisplay();
            UpdateCounts();
            SetEncodingChecked(encoding);
            SetLineEndingChecked(_lineEnding);
            Editor.Focus();

            if (discardingLarge) CompactLargeObjectHeap();

            if (FindPopup.IsOpen) UpdateFindMatches();
        }

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
