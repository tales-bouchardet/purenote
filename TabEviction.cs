using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Threading;

namespace PureNote
{
    public partial class MainWindow
    {
        private static readonly TimeSpan IdleBeforeEviction = TimeSpan.FromMinutes(2);

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

            if (releasedLarge) CompactLargeObjectHeap();
        }

        private void Evict(FileTab tab)
        {
            if (tab.IsDirty && !Spill(tab)) return;

            tab.SourceStamp = tab.IsDirty ? 0 : StampOf(tab.FullPath);

            SpillHistory(tab);

            tab.State = null;
        }

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


        private string _spillFolder;

        private string SpillFolder()
        {
            if (_spillFolder == null)
            {
                _spillFolder = Path.Combine(Path.GetTempPath(),
                    "purenote-session-" + System.Diagnostics.Process.GetCurrentProcess().Id
                    + "-" + Guid.NewGuid().ToString("N"));

                Directory.CreateDirectory(_spillFolder);
            }

            return _spillFolder;
        }

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


        private const int HistoryFormat = 1;

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

            RestoreHistory(tab, state);

            return state;
        }

        private static TextView.EditorState EmptyState()
        {
            return new TextView.EditorState { Document = new TextDocument() };
        }


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
