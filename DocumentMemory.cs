using System;
using System.Runtime;

namespace PureNote
{
    public partial class MainWindow
    {
        private const int LargeDocumentChars = 4 * 1024 * 1024;

        private bool IsLargeDocument
        {
            get { return Editor != null && Editor.TextLength >= LargeDocumentChars; }
        }

        private static void CompactLargeObjectHeap()
        {
            try
            {
                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                GC.Collect();
            }
            catch (Exception)
            {
            }
        }

        private void ReportOutOfMemory(string operation)
        {
            AppMessageBox.ShowError(this,
                "Not enough memory to " + operation + ".\n\n" +
                "The document is too large for this operation to finish. What was " +
                "already in the editor has been left alone.");
        }
    }
}
