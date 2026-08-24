using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Threading;

namespace PureNote
{
    public partial class MainWindow
    {
        // How long a tab may sit unselected before it is emptied. Long enough
        // that stepping between two files while working on both never costs a
        // reload, short enough that a morning's worth of opened-and-forgotten
        // files is not still resident by lunch.
        private static readonly TimeSpan IdleBeforeEviction = TimeSpan.FromMinutes(2);

        // The sweep only has to be roughly on time - it is deciding whether a
        // couple of minutes have passed, not measuring them - so it runs rarely
        // and at the priority the window uses when it has nothing else to do.
        private static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(30);

        private DispatcherTimer _evictionTimer;

        private void StartEvictionSweep()
        {
            _evictionTimer = new DispatcherTimer(DispatcherPriority.ApplicationIdle)
            {
                Interval = SweepInterval
            };

            _evictionTimer.Tick += (s, e) => Sweep();
            _evictionTimer.Start();
        }

        private void Sweep()
        {
            // One tab is the tab in front, and the tab in front is never a
            // candidate. Nothing to walk.
            if (_tabs.Count < 2) return;

            DateTime now = DateTime.UtcNow;
            bool releasedLarge = false;

            foreach (FileTab tab in _tabs)
            {
                if (tab.IsActive || tab.IsEvicted) continue;
                if (now - tab.LastActive < IdleBeforeEviction) continue;

                bool large = tab.State.Document.Length >= LargeDocumentChars;

                Evict(tab);

                if (large && tab.IsEvicted) releasedLarge = true;
            }

            // Dropping the last reference to a document does not hand the memory
            // back - it makes it collectable, and a document the size of a file
            // went to the large object heap, which returns nothing to the process
            // until a collection runs. Without this the sweep would free memory
            // in the sense that mattered to the garbage collector and in no sense
            // that mattered to the machine.
            //
            // Only when something large actually went, and this is the one place
            // in the program where a pause is free: the sweep fires when the
            // window has nothing else to do.
            if (releasedLarge) CompactLargeObjectHeap();
        }

        // Lets go of a tab's document, its undo history and its line index.
        //
        // Two cases, and the difference between them is whether the file on disk
        // already says what the tab says. If it does, the tab needs to remember
        // nothing: the file is the copy, and reading it again is what a click
        // will do. If it does not, the changes are the only copy there is, and
        // they go to a file of their own first.
        private void Evict(FileTab tab)
        {
            if (tab.IsDirty && !Spill(tab)) return;

            // A clean tab is reloaded from the file it came from, so its history
            // is still a list of positions in the right text and is worth taking
            // down with it. Which file it will be read back from is what decides
            // whether that stays true, so the stamp is taken now.
            tab.SourceStamp = tab.IsDirty ? 0 : StampOf(tab.FullPath);

            SpillHistory(tab);

            tab.State = null;
        }

        // Length and last-written time folded into one number. Zero when there is
        // no file to ask about, which reads as "no claim" and costs the history.
        private static long StampOf(string path)
        {
            if (path == null) return 0;

            try
            {
                FileInfo info = new FileInfo(path);
                if (!info.Exists) return 0;

                return info.Length ^ info.LastWriteTimeUtc.Ticks;
            }
            catch (IOException)
            {
                return 0;
            }
            catch (UnauthorizedAccessException)
            {
                return 0;
            }
        }

        // ---- spilling --------------------------------------------------------

        private string _spillFolder;

        private string SpillFolder()
        {
            if (_spillFolder == null)
            {
                // One folder per run, so two purenotes open at once do not write
                // over each other, and so what a crash leaves behind is
                // identifiable as one session's worth of unsaved work.
                _spillFolder = Path.Combine(Path.GetTempPath(),
                    "purenote-session-" + Process().Id.ToString());

                Directory.CreateDirectory(_spillFolder);
            }

            return _spillFolder;
        }

        private static System.Diagnostics.Process Process()
        {
            return System.Diagnostics.Process.GetCurrentProcess();
        }

        // Written as UTF-16 with LF breaks, which is exactly how the document is
        // held in memory: two bytes a character, no encoding that could fail to
        // represent something, and no line ending to convert and convert back.
        // The file's own encoding and line ending are metadata on the tab and are
        // applied when the document is really saved, not here.
        //
        // Returns false when the spill could not be written, and the caller then
        // leaves the tab in memory - holding on to a document is a great deal
        // better than dropping work that has nowhere else to be.
        private bool Spill(FileTab tab)
        {
            try
            {
                string path = tab.SpillPath ?? Path.Combine(SpillFolder(),
                    Guid.NewGuid().ToString("N") + ".spill");

                using (StreamWriter writer = new StreamWriter(path, false, new UnicodeEncoding(false, true)))
                {
                    tab.State.Document.WriteTo(writer, LineEndings.Lf);
                }

                tab.SpillPath = path;
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (OutOfMemoryException)
            {
                return false;
            }
        }

        // ---- the undo history ------------------------------------------------

        // Bumped if the shape below ever changes. A file that does not say this
        // is not read, which is the whole of the compatibility story: history is
        // worth keeping and never worth guessing at.
        private const int HistoryFormat = 1;

        // Written as UTF-16 for the same reason the document is: the strings in
        // an undo entry came out of the document, and a lone surrogate in one of
        // them must survive the trip. UTF-8 would quietly turn it into U+FFFD and
        // the text undo put back would not be the text it took.
        private bool SpillHistory(FileTab tab)
        {
            TextView.EditorState state = tab.State;
            if (state == null) return false;

            if (state.Undo.Count == 0 && state.Redo.Count == 0) return false;

            try
            {
                string path = tab.HistoryPath ?? Path.Combine(SpillFolder(),
                    Guid.NewGuid().ToString("N") + ".undo");

                using (FileStream file = new FileStream(path, FileMode.Create, FileAccess.Write))
                using (BinaryWriter writer = new BinaryWriter(file, new UnicodeEncoding(false, false)))
                {
                    writer.Write(HistoryFormat);
                    writer.Write(state.UndoChars);

                    WriteEntries(writer, state.Undo);
                    WriteEntries(writer, state.Redo);
                }

                tab.HistoryPath = path;
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static void WriteEntries(BinaryWriter writer, List<TextView.UndoEntry> entries)
        {
            writer.Write(entries.Count);

            foreach (TextView.UndoEntry entry in entries)
            {
                writer.Write(entry.Offset);
                writer.Write(entry.Removed);
                writer.Write(entry.Inserted);
                writer.Write(entry.CaretBefore);
                writer.Write(entry.AnchorBefore);
                writer.Write(entry.OpenForTyping);
            }
        }

        // Puts the history back onto a state that has just been read.
        //
        // Refuses in three cases, and quietly, because a tab that comes back
        // without its history is a small loss and a tab that comes back with the
        // wrong history is a corrupted document: the file is not there, it is not
        // a shape this version wrote, or - for a clean tab - the file it was read
        // from is not the file the history was recorded against.
        private static void RestoreHistory(FileTab tab, TextView.EditorState state)
        {
            if (tab.HistoryPath == null || !File.Exists(tab.HistoryPath)) return;

            bool fromSpill = tab.SpillPath != null;
            if (!fromSpill && StampOf(tab.FullPath) != tab.SourceStamp) return;

            try
            {
                using (FileStream file = new FileStream(tab.HistoryPath, FileMode.Open, FileAccess.Read))
                using (BinaryReader reader = new BinaryReader(file, new UnicodeEncoding(false, false)))
                {
                    if (reader.ReadInt32() != HistoryFormat) return;

                    int undoChars = reader.ReadInt32();

                    List<TextView.UndoEntry> undo = ReadEntries(reader);
                    List<TextView.UndoEntry> redo = ReadEntries(reader);

                    state.Undo = undo;
                    state.Redo = redo;
                    state.UndoChars = undoChars;
                }
            }
            catch (IOException)
            {
                // EndOfStreamException lands here too, which is the case that
                // matters: a history file cut short by a crash mid-write reads
                // back as far as it can and then stops, and the tab comes back
                // without a history rather than with half of one.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static List<TextView.UndoEntry> ReadEntries(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            List<TextView.UndoEntry> entries = new List<TextView.UndoEntry>(count);

            for (int i = 0; i < count; i++)
            {
                entries.Add(new TextView.UndoEntry
                {
                    Offset = reader.ReadInt32(),
                    Removed = reader.ReadString(),
                    Inserted = reader.ReadString(),
                    CaretBefore = reader.ReadInt32(),
                    AnchorBefore = reader.ReadInt32(),
                    OpenForTyping = reader.ReadBoolean()
                });
            }

            return entries;
        }

        // ---- reading a tab back ---------------------------------------------

        // Called when an emptied tab is clicked. Takes the spill file if there is
        // one, because that is where the unsaved work went; otherwise the
        // original file, because the tab was clean and the file is the copy.
        private bool Restore(FileTab tab)
        {
            try
            {
                if (tab.SpillPath != null && File.Exists(tab.SpillPath))
                {
                    tab.State = ReadIntoState(tab, tab.SpillPath, Encoding.Unicode);
                    DiscardSpill(tab);
                    return true;
                }

                if (tab.FullPath == null || !File.Exists(tab.FullPath))
                {
                    // Nothing to read from at all. An untitled tab that was never
                    // dirty holds nothing worth reading anyway; a file that has
                    // gone missing is worth saying so about.
                    if (tab.FullPath != null)
                    {
                        AppMessageBox.ShowError(this,
                            "Could not reopen:\n" + tab.FullPath + "\n\nThe file is no longer there.");
                        return false;
                    }

                    tab.State = EmptyState();
                    DiscardSpill(tab);
                    return true;
                }

                tab.State = ReadIntoState(tab, tab.FullPath, tab.Encoding);
                DiscardSpill(tab);
                return true;
            }
            catch (OutOfMemoryException)
            {
                ReportTooLarge(tab.FullPath ?? tab.Name);
                return false;
            }
            catch (IOException ex)
            {
                AppMessageBox.ShowError(this, "Could not reopen:\n" + tab.Name + "\n\n" + ex.Message);
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                ReportReadDenied(tab.FullPath ?? tab.Name);
                return false;
            }
        }

        private TextView.EditorState ReadIntoState(FileTab tab, string path, Encoding encoding)
        {
            TextDocument document;

            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.Read, StreamBuffer, FileOptions.SequentialScan))
            {
                int preamble = encoding.GetPreamble().Length;

                DocumentShape shape;
                if (!DocumentLoader.TryMeasure(stream, encoding, preamble, out shape))
                {
                    throw new OutOfMemoryException();
                }

                document = DocumentLoader.Load(stream, encoding, preamble, shape);
            }

            TextView.EditorState state = new TextView.EditorState
            {
                Document = document,
                CaretOffset = Math.Min(tab.CaretOffset, document.Length),
                SelectionAnchor = Math.Min(tab.SelectionAnchor, document.Length),
                VerticalOffset = tab.VerticalOffset,
                HorizontalOffset = tab.HorizontalOffset
            };

            // Before the spill files are let go of, which is what the caller does
            // next.
            RestoreHistory(tab, state);

            return state;
        }

        private static TextView.EditorState EmptyState()
        {
            return new TextView.EditorState { Document = new TextDocument() };
        }

        // ---- cleaning up -----------------------------------------------------

        // Both files a tab can have left on disk, and the claim that went with
        // them.
        private static void DiscardSpill(FileTab tab)
        {
            Delete(tab.SpillPath);
            Delete(tab.HistoryPath);

            tab.SpillPath = null;
            tab.HistoryPath = null;
            tab.SourceStamp = 0;
        }

        private static void Delete(string path)
        {
            if (path == null) return;

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

        // On the way out, and only on the way out. A session that crashes leaves
        // its folder behind on purpose: those files are the only copy of work
        // that was never saved, and deleting them to be tidy would be deleting
        // the very thing they exist to hold.
        private void DiscardAllSpills()
        {
            foreach (FileTab tab in _tabs) DiscardSpill(tab);

            try
            {
                if (_spillFolder != null && Directory.Exists(_spillFolder)) Directory.Delete(_spillFolder, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
