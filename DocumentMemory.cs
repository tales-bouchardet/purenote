using System;
using System.Runtime;

namespace PureNote
{
    public partial class MainWindow
    {
        // A document big enough that one avoidable copy of it is worth going out
        // of the way for. Below this the amplification is real but too small to
        // be worth changing how anything behaves.
        private const int LargeDocumentChars = 4 * 1024 * 1024;

        private bool IsLargeDocument
        {
            get { return Editor != null && Editor.TextLength >= LargeDocumentChars; }
        }

        // Strings and buffers this size go straight to the large object heap, and
        // an ordinary collection sweeps it without compacting it. Opening and
        // closing a few large files leaves it holding enough free-but-fragmented
        // space that the next allocation fails with memory still on the machine.
        // One compaction at the points where a large document is known to have
        // just been let go is what keeps the third big file from failing where
        // the first did not.
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
