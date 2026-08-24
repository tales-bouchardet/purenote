using System;
using System.Collections.Generic;

namespace PureNote
{
    internal sealed partial class TextView
    {
        private const int MaxUndoChars = 8 * 1024 * 1024;
        private const int MaxUndoEntries = 256;

        internal sealed class UndoEntry
        {
            public int Offset;
            public string Removed;
            public string Inserted;
            public int CaretBefore;
            public int AnchorBefore;

            public bool OpenForTyping;

            public int Cost
            {
                get { return Removed.Length + Inserted.Length + 64; }
            }
        }

        private List<UndoEntry> _undo = new List<UndoEntry>();
        private List<UndoEntry> _redo = new List<UndoEntry>();
        private int _undoChars;

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

        public void ReplaceRange(int offset, int length, string text)
        {
            if (IsReadOnly) return;

            offset = Clamp(offset, 0, _document.Length);
            length = Clamp(length, 0, _document.Length - offset);

            if (length == 0 && string.IsNullOrEmpty(text)) return;

            string inserted = text ?? string.Empty;

            if (!FitsInUndoBudget(length, inserted.Length))
            {
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

                OpenForTyping = length == 0
                    && inserted.Length == 1
                    && inserted[0] != '\n'
                    && inserted[0] != '\t'
                    && !IsWordSeparator(inserted[0]),
            };

            Apply(offset, length, entry.Inserted);
            PushUndo(entry);

            MoveCaret(offset + entry.Inserted.Length, extend: false);
            AfterEdit();
        }

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
            _document.Replace(offset, length, text);

            InvalidateLineCache();

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
            _redo.Clear();

            if (TryMergeTyping(entry)) return;

            _undo.Add(entry);
            _undoChars += entry.Cost;

            TrimUndo();
        }

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

            if (IsWordSeparator(typed)) previous.OpenForTyping = false;

            return true;
        }

        private static bool IsWordSeparator(char c)
        {
            return c == ' ' || c == '\t' || c == '.' || c == ',' || c == ';' || c == ':'
                || c == '(' || c == ')' || c == '[' || c == ']' || c == '{' || c == '}'
                || c == '/' || c == '\\' || c == '-' || c == '=' || c == '"' || c == '\'';
        }

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
