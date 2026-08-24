using System;
using System.Windows;
using System.Windows.Input;

namespace PureNote
{
    internal sealed partial class TextView
    {
        private bool _dragging;

        private void InstallCommands()
        {
            Bind(ApplicationCommands.Copy, (s, e) => Copy());
            Bind(ApplicationCommands.Cut, (s, e) => Cut());
            Bind(ApplicationCommands.Paste, (s, e) => Paste());
            Bind(ApplicationCommands.SelectAll, (s, e) => SelectAll());
            Bind(ApplicationCommands.Undo, (s, e) => Undo());
            Bind(ApplicationCommands.Redo, (s, e) => Redo());
        }

        private void Bind(RoutedCommand command, ExecutedRoutedEventHandler handler)
        {
            CommandBindings.Add(new CommandBinding(command, handler));
        }


        protected override void OnTextInput(TextCompositionEventArgs e)
        {
            base.OnTextInput(e);

            if (string.IsNullOrEmpty(e.Text)) return;

            if (e.Text.Length == 1 && e.Text[0] < ' ' && e.Text[0] != '\t') return;

            InsertText(e.Text);
            e.Handled = true;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Handled) return;

            bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
            bool control = (Keyboard.Modifiers & ModifierKeys.Control) != 0;

            switch (e.Key)
            {
                case Key.Left:
                    EndTypingRun();
                    MoveCaretHorizontally(control ? PreviousWord(_caretOffset) : PreviousCaretStop(_caretOffset), shift);
                    break;

                case Key.Right:
                    EndTypingRun();
                    MoveCaretHorizontally(control ? NextWord(_caretOffset) : NextCaretStop(_caretOffset), shift);
                    break;

                case Key.Up:
                    EndTypingRun();
                    if (control) SetVerticalOffset(_verticalOffset - LineHeight); else MoveCaretVertically(-1, shift);
                    break;

                case Key.Down:
                    EndTypingRun();
                    if (control) SetVerticalOffset(_verticalOffset + LineHeight); else MoveCaretVertically(1, shift);
                    break;

                case Key.PageUp:
                    EndTypingRun();
                    MoveCaretVertically(-VisibleLineCount(), shift);
                    break;

                case Key.PageDown:
                    EndTypingRun();
                    MoveCaretVertically(VisibleLineCount(), shift);
                    break;

                case Key.Home:
                    EndTypingRun();
                    MoveCaretHorizontally(control ? 0 : HomeOf(CaretLine), shift);
                    break;

                case Key.End:
                    EndTypingRun();
                    MoveCaretHorizontally(control ? _document.Length : _document.GetLineEnd(CaretLine), shift);
                    break;

                case Key.Back:
                    Backspace();
                    break;

                case Key.Delete:
                    DeleteForward();
                    break;

                case Key.Return:
                    EndTypingRun();
                    InsertText("\n");
                    break;

                case Key.Tab:
                    EndTypingRun();
                    InsertText("\t");
                    break;

                case Key.Escape:
                    return;

                default:
                    return;
            }

            e.Handled = true;
        }

        private int VisibleLineCount()
        {
            return Math.Max(1, (int)(ViewportHeight / LineHeight) - 1);
        }

        private int HomeOf(int line)
        {
            int start = _document.GetLineStart(line);
            int end = _document.GetLineEnd(line);

            int firstText = start;
            while (firstText < end && (_document[firstText] == ' ' || _document[firstText] == '\t')) firstText++;

            return _caretOffset == firstText ? start : firstText;
        }

        private void MoveCaretHorizontally(int offset, bool extend)
        {
            _preferredColumnValid = false;
            MoveCaret(offset, extend);
        }

        private void MoveCaretVertically(int deltaLines, bool extend)
        {
            int line = CaretLine;

            if (!_preferredColumnValid)
            {
                VisibleLine current = GetVisibleLine(line);

                _preferredColumn = ColumnOf(current, _caretOffset - current.Start);
                _preferredColumnValid = true;
            }

            int target = Clamp(line + deltaLines, 0, _document.LineCount - 1);
            VisibleLine destination = GetVisibleLine(target);

            int offsetInLine = destination.Uniform
                ? Math.Min(_preferredColumn, destination.Length)
                : TextLineRenderer.ToOffset(destination.Text, _preferredColumn);

            _caretOffset = Clamp(destination.Start + offsetInLine, 0, _document.Length);
            if (!extend) _selectionAnchor = _caretOffset;

            RestartCaretBlink();
            BringCaretIntoView();
            InvalidateVisual();
            RaiseCaretChanged();
        }

        private int NextCaretStop(int offset)
        {
            if (offset >= _document.Length) return _document.Length;

            return offset + 1 < _document.Length
                && char.IsHighSurrogate(_document[offset])
                && char.IsLowSurrogate(_document[offset + 1]) ? offset + 2 : offset + 1;
        }

        private int PreviousCaretStop(int offset)
        {
            if (offset <= 0) return 0;

            return offset >= 2
                && char.IsLowSurrogate(_document[offset - 1])
                && char.IsHighSurrogate(_document[offset - 2]) ? offset - 2 : offset - 1;
        }

        private enum CharClass { Space, Word, Symbol }

        private CharClass ClassOf(int offset)
        {
            char c = _document[offset];

            if (c == ' ' || c == '\t' || c == '\n') return CharClass.Space;
            if (char.IsLetterOrDigit(c) || c == '_') return CharClass.Word;

            return CharClass.Symbol;
        }

        private int NextWord(int offset)
        {
            int length = _document.Length;
            if (offset >= length) return length;

            CharClass start = ClassOf(offset);
            while (offset < length && ClassOf(offset) == start) offset++;
            while (offset < length && ClassOf(offset) == CharClass.Space && _document[offset] != '\n') offset++;

            return offset;
        }

        private int PreviousWord(int offset)
        {
            if (offset <= 0) return 0;

            offset--;
            while (offset > 0 && ClassOf(offset) == CharClass.Space && _document[offset] != '\n') offset--;

            CharClass start = ClassOf(offset);
            while (offset > 0 && ClassOf(offset - 1) == start) offset--;

            return offset;
        }


        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);

            Focus();
            EndTypingRun();

            Point point = e.GetPosition(this);
            int offset = GetOffsetFromPoint(point);

            if (e.ClickCount == 2) { SelectWordAt(offset); e.Handled = true; return; }
            if (e.ClickCount >= 3) { SelectLineAt(offset); e.Handled = true; return; }

            MoveCaretHorizontally(offset, (Keyboard.Modifiers & ModifierKeys.Shift) != 0);

            _dragging = true;
            CaptureMouse();

            e.Handled = true;
        }

        protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseRightButtonDown(e);

            Focus();
            EndTypingRun();

            int offset = GetOffsetFromPoint(e.GetPosition(this));
            int start = SelectionStart;

            if (offset < start || offset > start + SelectionLength)
            {
                MoveCaretHorizontally(offset, extend: false);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (!_dragging) return;

            Point point = e.GetPosition(this);

            if (point.Y < 0) SetVerticalOffset(_verticalOffset - LineHeight);
            else if (point.Y > ViewportHeight) SetVerticalOffset(_verticalOffset + LineHeight);

            MoveCaretHorizontally(GetOffsetFromPoint(point), extend: true);
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);

            if (!_dragging) return;

            _dragging = false;
            ReleaseMouseCapture();
        }

        private void SelectWordAt(int offset)
        {
            if (_document.Length == 0) return;

            int at = Math.Min(offset, _document.Length - 1);

            CharClass klass = ClassOf(at);

            int start = at;
            while (start > 0 && ClassOf(start - 1) == klass && _document[start - 1] != '\n') start--;

            int end = at;
            while (end < _document.Length && ClassOf(end) == klass && _document[end] != '\n') end++;

            Select(start, end - start);
        }

        private void SelectLineAt(int offset)
        {
            int line = _document.GetLineFromOffset(offset);
            int start = _document.GetLineStart(line);

            int end = line + 1 < _document.LineCount ? _document.GetLineStart(line + 1) : _document.Length;

            Select(start, end - start);
        }


        public void Copy()
        {
            if (SelectionLength == 0) return;

            PutOnClipboard(SelectedText);
        }

        public void Cut()
        {
            if (SelectionLength == 0) return;

            if (PutOnClipboard(SelectedText)) DeleteSelection();
        }

        private bool PutOnClipboard(string text)
        {
            try
            {
                DataObject data = new DataObject();
                data.SetData(DataFormats.UnicodeText, text, false);

                Clipboard.SetDataObject(data, true);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public void Paste()
        {
            string incoming;

            try
            {
                if (!Clipboard.ContainsText()) return;

                incoming = Clipboard.GetText();
            }
            catch (Exception)
            {
                return;
            }

            if (string.IsNullOrEmpty(incoming)) return;

            EndTypingRun();

            InsertText(Normalise(incoming));
        }

        private static string Normalise(string text)
        {
            if (text.IndexOf('\r') < 0) return text;

            System.Text.StringBuilder sb = new System.Text.StringBuilder(text.Length);

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (c == '\r')
                {
                    sb.Append('\n');
                    if (i + 1 < text.Length && text[i + 1] == '\n') i++;
                }
                else
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }
    }
}
