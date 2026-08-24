using System;
using System.Collections.Generic;

namespace PureNote
{
    // Editing and undo. Every change to the document in the program goes through
    // ReplaceRange below - there is no other mutation point - which is what lets
    // undo be recorded in one place and lets everything downstream of an edit be
    // notified in one place.
    internal sealed partial class TextView
    {
        // Each entry holds the text it replaced, so an unbounded stack on a large
        // document is an unbounded number of copies of parts of it. Bounded by
        // characters rather than by entries because that is the thing that
        // actually runs out: a hundred one-character edits cost nothing, and one
        // replace-all across a hundred-megabyte file costs everything.
        private const int MaxUndoChars = 8 * 1024 * 1024;
        private const int MaxUndoEntries = 256;

        internal sealed class UndoEntry
        {
            public int Offset;
            public string Removed;
            public string Inserted;
            public int CaretBefore;
            public int AnchorBefore;

            // Cleared once anything else happens, so that a run of typing
            // collapses into one undo but a run interrupted by a click does not.
            public bool OpenForTyping;

            public int Cost
            {
                get { return Removed.Length + Inserted.Length + 64; }
            }
        }

        private List<UndoEntry> _undo = new List<UndoEntry>();
        private List<UndoEntry> _redo = new List<UndoEntry>();
        private int _undoChars;

        // Everything the editor is holding on behalf of one document, lifted out
        // whole so a second document can be put in and this one put back exactly
        // as it was.
        //
        // The undo history is the reason this exists. SetDocument resets it,
        // which is right when a file is being opened over another and wrong when
        // a tab is being stepped away from - without this, coming back to a tab
        // would find it unable to undo anything that happened before you left.
        //
        // Nested so it can name UndoEntry, and so that what a tab is holding on
        // the editor's behalf stays the editor's own shape rather than something
        // the rest of the program can take apart.
        internal sealed class EditorState
        {
            internal TextDocument Document;
            internal List<UndoEntry> Undo;
            internal List<UndoEntry> Redo;
            internal int UndoChars;
            internal int CaretOffset;
            internal int SelectionAnchor;
            internal double VerticalOffset;
            internal double HorizontalOffset;
        }

        // The lists are handed over rather than copied - the caller is taking
        // them, not borrowing them - and fresh ones are left behind for whatever
        // document arrives next.
        public EditorState CaptureState()
        {
            EditorState state = new EditorState
            {
                Document = _document,
                Undo = _undo,
                Redo = _redo,
                UndoChars = _undoChars,
                CaretOffset = _caretOffset,
                SelectionAnchor = _selectionAnchor,
                VerticalOffset = _verticalOffset,
                HorizontalOffset = _horizontalOffset
            };

            _undo = new List<UndoEntry>();
            _redo = new List<UndoEntry>();
            _undoChars = 0;

            return state;
        }

        public void RestoreState(EditorState state)
        {
            _document = state.Document ?? new TextDocument();
            _undo = state.Undo ?? new List<UndoEntry>();
            _redo = state.Redo ?? new List<UndoEntry>();
            _undoChars = state.UndoChars;

            _caretOffset = Clamp(state.CaretOffset, 0, _document.Length);
            _selectionAnchor = Clamp(state.SelectionAnchor, 0, _document.Length);
            _preferredColumnValid = false;

            _matches = null;
            _currentMatch = -1;

            // Both offsets are set through the setters, which clamp against an
            // extent that only makes sense once the new document is in place.
            _verticalOffset = 0;
            _horizontalOffset = 0;

            InvalidateLineCache();
            MeasureLongestLine();
            InvalidateScrollInfo();

            SetVerticalOffset(state.VerticalOffset);
            SetHorizontalOffset(state.HorizontalOffset);

            InvalidateVisual();
            RaiseDocumentChanged();
            RaiseCaretChanged();
        }

        public bool CanUndo { get { return _undo.Count > 0; } }
        public bool CanRedo { get { return _redo.Count > 0; } }

        // Whether an edit of this shape can be taken back within the budget.
        // Public so that the one operation able to exceed it can warn first.
        public static bool FitsInUndoBudget(int removedLength, int insertedLength)
        {
            return (long)removedLength + insertedLength <= MaxUndoChars;
        }

        private void ResetUndo()
        {
            _undo.Clear();
            _redo.Clear();
            _undoChars = 0;
        }

        // The single point at which the document changes.
        public void ReplaceRange(int offset, int length, string text)
        {
            if (IsReadOnly) return;

            offset = Clamp(offset, 0, _document.Length);
            length = Clamp(length, 0, _document.Length - offset);

            if (length == 0 && string.IsNullOrEmpty(text)) return;

            string inserted = text ?? string.Empty;

            // An edit whose undo record would not fit in the budget is not
            // recorded at all, and takes the existing history with it. The
            // alternative is to capture the replaced text anyway - a full copy of
            // whatever is being replaced, taken purely to be thrown away by the
            // next trim - which on a replace-all across a large document is the
            // allocation most likely to be the one that fails.
            //
            // Callers that can reach this size say so before they do it; see
            // ReplaceAll.
            if (!FitsInUndoBudget(length, inserted.Length))
            {
                // Applied before the history is dropped rather than after, so an
                // edit that turns out to have no room leaves both the document
                // and the history exactly as they were.
                Apply(offset, length, inserted);
                ResetUndo();

                MoveCaret(offset + inserted.Length, extend: false);
                AfterEdit();
                return;
            }

            UndoEntry entry = new UndoEntry
            {
                Offset = offset,
                Removed = length > 0 ? _document.GetText(offset, length) : string.Empty,
                Inserted = inserted,
                CaretBefore = _caretOffset,
                AnchorBefore = _selectionAnchor,

                // Whether the character after this one may join it into the same
                // undo step. Only a plain single character typed over nothing
                // starts a run - a paste, a deletion, or typing over a selection
                // is a step in its own right - and a separator closes the run it
                // is part of rather than opening a new one.
                OpenForTyping = length == 0
                    && inserted.Length == 1
                    && inserted[0] != '\n'
                    && inserted[0] != '\t'
                    && !IsWordSeparator(inserted[0]),
            };

            // Applied before the entry is pushed, for the same reason: an edit
            // that could not find the room leaves no record of having happened,
            // and so nothing for Ctrl+Z to undo that was never done.
            Apply(offset, length, entry.Inserted);
            PushUndo(entry);

            MoveCaret(offset + entry.Inserted.Length, extend: false);
            AfterEdit();
        }

        // What every edit the user makes by hand goes through. At the ceiling an
        // edit can genuinely not fit, and a keystroke is not a thing that should
        // be able to close the program: the document is left as it was and the
        // window is told once, which it turns into a message. Typing that quietly
        // does nothing reads as a broken editor.
        //
        // ReplaceRange itself still throws, because ReplaceAll wants to report
        // its own failure in its own words.
        private bool TryReplaceRange(int offset, int length, string text)
        {
            try
            {
                ReplaceRange(offset, length, text);
                return true;
            }
            catch (OutOfMemoryException)
            {
                RaiseEditRefused();
                return false;
            }
        }

        // The same guarantee for the two edits that do not go through
        // ReplaceRange: undo and redo also grow the document when the text they
        // put back is longer than the text they take out.
        private bool TryApply(int offset, int length, string text)
        {
            try
            {
                Apply(offset, length, text);
                return true;
            }
            catch (OutOfMemoryException)
            {
                RaiseEditRefused();
                return false;
            }
        }

        private void RaiseEditRefused()
        {
            EventHandler handler = EditRefused;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void Apply(int offset, int length, string text)
        {
            // One step rather than a removal and then an insertion: at the
            // ceiling the insertion can fail, and the removal must not already
            // have happened when it does.
            _document.Replace(offset, length, text);

            // Every line at or after the edit now holds different text, and every
            // line after it has a different number.
            InvalidateLineCache();

            // A line longer than any seen so far widens the scrollable area, and
            // the estimate has to keep up or the end of the line becomes
            // unreachable by scrolling.
            int line = _document.GetLineFromOffset(offset);
            int lineLength = _document.GetLineLength(line);

            if (lineLength > _longestLineChars)
            {
                _longestLineChars = lineLength;

                double estimate = EstimatedWidthFor(lineLength);
                if (estimate > _extentWidth) _extentWidth = estimate;
            }
        }

        private void AfterEdit()
        {
            InvalidateScrollInfo();
            InvalidateVisual();
            RaiseDocumentChanged();
        }

        private void PushUndo(UndoEntry entry)
        {
            // Anything that could be redone described a future that this edit has
            // just replaced.
            _redo.Clear();

            if (TryMergeTyping(entry)) return;

            _undo.Add(entry);
            _undoChars += entry.Cost;

            TrimUndo();
        }

        // A run of ordinary typing becomes one undo step rather than one per
        // character. The run ends at a word separator, at any edit that is not a
        // plain single-character insertion at the caret, and at anything that
        // moves the caret by itself - see EndTypingRun.
        private bool TryMergeTyping(UndoEntry entry)
        {
            if (_undo.Count == 0) return false;
            if (entry.Removed.Length != 0 || entry.Inserted.Length != 1) return false;

            char typed = entry.Inserted[0];
            if (typed == '\n' || typed == '\t') return false;

            UndoEntry previous = _undo[_undo.Count - 1];

            if (!previous.OpenForTyping) return false;
            if (previous.Removed.Length != 0) return false;
            if (previous.Offset + previous.Inserted.Length != entry.Offset) return false;

            previous.Inserted += typed;
            _undoChars++;

            // Ending the run on the separator rather than after it keeps the
            // space at the end of the word being undone with it, which is what
            // makes one Ctrl+Z take back one word.
            if (IsWordSeparator(typed)) previous.OpenForTyping = false;

            return true;
        }

        private static bool IsWordSeparator(char c)
        {
            return c == ' ' || c == '\t' || c == '.' || c == ',' || c == ';' || c == ':'
                || c == '(' || c == ')' || c == '[' || c == ']' || c == '{' || c == '}'
                || c == '/' || c == '\\' || c == '-' || c == '=' || c == '"' || c == '\'';
        }

        // Called by anything that means the next character typed starts a new
        // undo step: a click, an arrow key, a paste, losing focus.
        private void EndTypingRun()
        {
            if (_undo.Count > 0) _undo[_undo.Count - 1].OpenForTyping = false;
        }

        private void TrimUndo()
        {
            while (_undo.Count > MaxUndoEntries || (_undoChars > MaxUndoChars && _undo.Count > 1))
            {
                _undoChars -= _undo[0].Cost;
                _undo.RemoveAt(0);
            }
        }

        public void Undo()
        {
            if (_undo.Count == 0) return;

            UndoEntry entry = _undo[_undo.Count - 1];

            // Applied before the stacks move, so an undo that cannot find the
            // room leaves the history where it was instead of swallowing a step
            // that never happened.
            if (!TryApply(entry.Offset, entry.Inserted.Length, entry.Removed)) return;

            _undo.RemoveAt(_undo.Count - 1);
            _undoChars -= entry.Cost;
            _redo.Add(entry);

            _selectionAnchor = Clamp(entry.AnchorBefore, 0, _document.Length);
            _caretOffset = Clamp(entry.CaretBefore, 0, _document.Length);

            BringCaretIntoView();
            AfterEdit();
            RaiseCaretChanged();
        }

        public void Redo()
        {
            if (_redo.Count == 0) return;

            UndoEntry entry = _redo[_redo.Count - 1];

            if (!TryApply(entry.Offset, entry.Removed.Length, entry.Inserted)) return;

            _redo.RemoveAt(_redo.Count - 1);

            entry.OpenForTyping = false;
            _undo.Add(entry);
            _undoChars += entry.Cost;

            _caretOffset = _selectionAnchor = Clamp(entry.Offset + entry.Inserted.Length, 0, _document.Length);

            BringCaretIntoView();
            AfterEdit();
            RaiseCaretChanged();
        }

        // ---- editing operations ---------------------------------------------

        public void InsertText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            TryReplaceRange(SelectionStart, SelectionLength, text);
        }

        public void DeleteSelection()
        {
            if (SelectionLength == 0) return;

            EndTypingRun();
            TryReplaceRange(SelectionStart, SelectionLength, string.Empty);
        }

        public void Backspace()
        {
            if (SelectionLength > 0) { DeleteSelection(); return; }
            if (_caretOffset == 0) return;

            EndTypingRun();

            // Steps over a surrogate pair as one character, so backspacing an
            // emoji does not leave half of it behind.
            int count = _caretOffset >= 2 && char.IsLowSurrogate(_document[_caretOffset - 1])
                && char.IsHighSurrogate(_document[_caretOffset - 2]) ? 2 : 1;

            TryReplaceRange(_caretOffset - count, count, string.Empty);
        }

        public void DeleteForward()
        {
            if (SelectionLength > 0) { DeleteSelection(); return; }
            if (_caretOffset >= _document.Length) return;

            EndTypingRun();

            int count = _caretOffset + 1 < _document.Length && char.IsHighSurrogate(_document[_caretOffset])
                && char.IsLowSurrogate(_document[_caretOffset + 1]) ? 2 : 1;

            TryReplaceRange(_caretOffset, count, string.Empty);
        }
    }
}
