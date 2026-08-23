using System;
using System.IO;

namespace PureNote
{
    // The document, held apart from anything that draws it.
    //
    // The editor used to be a WPF TextBox, and the whole document lived inside
    // it. That is what made opening a large file cost eighteen seconds: to
    // report a size, TextBoxView formats every line in the document, because the
    // width it has to report is the width of the longest one. Three million
    // lines get laid out to answer a question about one of them.
    //
    // Nothing here lays out anything. The text is a gap buffer, the line index
    // is an array of offsets, and both are built in a single pass over the
    // decoded characters. Whatever draws the document asks for the twenty-odd
    // lines it is about to paint and nothing else - see TextView, which derives
    // its scroll extent from the line count arithmetically rather than by
    // measuring, the way VS Code and RichEdit do.
    //
    // Line breaks are stored as a single '\n' regardless of what the file used.
    // The convention the file arrived in is remembered beside the document and
    // applied again on the way out, so nothing is lost - and in exchange every
    // offset in the program counts characters the way a person would, with no
    // CRLF pair to subtract back out.
    internal sealed class TextDocument
    {
        private const int MinGap = 1024;

        private char[] _buffer;
        private int _gapStart;
        private int _gapEnd;

        // Offset of the first character of each line. Entry 0 is always 0, so a
        // document always has at least one line, empty or not.
        private int[] _lineStarts;
        private int _lineCount;

        public TextDocument()
        {
            _buffer = new char[MinGap];
            _gapStart = 0;
            _gapEnd = _buffer.Length;
            _lineStarts = new int[] { 0 };
            _lineCount = 1;
        }

        // Takes ownership of both arrays rather than copying them: the loader
        // sizes them exactly and has no further use for them, and on a large file
        // a defensive copy here would be the largest allocation in the program.
        internal TextDocument(char[] buffer, int length, int[] lineStarts, int lineCount)
        {
            _buffer = buffer;
            _gapStart = length;
            _gapEnd = buffer.Length;
            _lineStarts = lineStarts;
            _lineCount = lineCount;
        }

        public int Length
        {
            get { return _buffer.Length - (_gapEnd - _gapStart); }
        }

        public int LineCount
        {
            get { return _lineCount; }
        }

        public char this[int offset]
        {
            get { return _buffer[offset < _gapStart ? offset : offset + (_gapEnd - _gapStart)]; }
        }

        public int GetLineStart(int line)
        {
            return _lineStarts[line];
        }

        // Where the line's text ends: at its '\n', or at the end of the document
        // for the last line. The break itself is not part of the line.
        public int GetLineEnd(int line)
        {
            return line + 1 < _lineCount ? _lineStarts[line + 1] - 1 : Length;
        }

        public int GetLineLength(int line)
        {
            return GetLineEnd(line) - _lineStarts[line];
        }

        public string GetLineText(int line)
        {
            int start = _lineStarts[line];
            return GetText(start, GetLineEnd(line) - start);
        }

        // Which line an offset falls on. Binary search rather than a scan: this
        // is asked once per caret move and once per visible line per repaint, and
        // a scan would make both proportional to how far down the file the user
        // has got.
        public int GetLineFromOffset(int offset)
        {
            if (offset <= 0) return 0;
            if (offset >= Length) return _lineCount - 1;

            int low = 0;
            int high = _lineCount - 1;

            while (low < high)
            {
                int middle = low + (high - low + 1) / 2;

                if (_lineStarts[middle] <= offset) low = middle; else high = middle - 1;
            }

            return low;
        }

        public void CopyTo(int offset, char[] destination, int destinationIndex, int count)
        {
            if (count <= 0) return;

            int gapSize = _gapEnd - _gapStart;

            // Wholly before the gap, wholly after it, or straddling it - the
            // third case is the only one that needs two copies, and it is the
            // reason callers cannot simply index the buffer directly.
            if (offset + count <= _gapStart)
            {
                Array.Copy(_buffer, offset, destination, destinationIndex, count);
            }
            else if (offset >= _gapStart)
            {
                Array.Copy(_buffer, offset + gapSize, destination, destinationIndex, count);
            }
            else
            {
                int before = _gapStart - offset;

                Array.Copy(_buffer, offset, destination, destinationIndex, before);
                Array.Copy(_buffer, _gapEnd, destination, destinationIndex + before, count - before);
            }
        }

        public string GetText(int offset, int count)
        {
            if (count <= 0) return string.Empty;

            char[] result = new char[count];
            CopyTo(offset, result, 0, count);

            return new string(result);
        }

        public string GetText()
        {
            return GetText(0, Length);
        }

        // Streams the document out in the given convention without ever building
        // a second copy of it. Saving used to convert the whole document into a
        // new string and hand that to the writer, which on a large file is one
        // more copy of it live at the moment the encoder is allocating its own.
        public void WriteTo(TextWriter writer, string lineEnding)
        {
            string target = lineEnding == LineEndings.Lf ? "\n"
                : lineEnding == LineEndings.Cr ? "\r"
                : "\r\n";

            char[] chunk = new char[64 * 1024];

            for (int line = 0; line < _lineCount; line++)
            {
                int start = _lineStarts[line];
                int remaining = GetLineEnd(line) - start;

                while (remaining > 0)
                {
                    int take = Math.Min(remaining, chunk.Length);

                    CopyTo(start, chunk, 0, take);
                    writer.Write(chunk, 0, take);

                    start += take;
                    remaining -= take;
                }

                // Every line but the last is followed by a break. The last one is
                // not: a document ending in a break has an empty final line, and
                // writing one for it too would grow the file by a line per save.
                if (line + 1 < _lineCount) writer.Write(target);
            }
        }

        public void Insert(int offset, string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            int line = GetLineFromOffset(offset);

            MoveGapTo(offset);
            EnsureGap(text.Length);

            text.CopyTo(0, _buffer, _gapStart, text.Length);
            _gapStart += text.Length;

            int breaks = CountBreaks(text);

            // Order matters: the new starts are written as absolute offsets that
            // are already correct, so the shift below must skip past them and
            // move only the lines that were there before.
            if (breaks > 0) InsertLines(line, offset, text, breaks);

            ShiftLineStarts(line + 1 + breaks, text.Length);
        }

        // Replaces a range in one step.
        //
        // Remove followed by Insert is not the same thing. The removal succeeds,
        // the insertion finds no room, and what was replaced is gone with nothing
        // put in its place - a document shorter than either the caller or the
        // user asked for, and an exception that says nothing about it. Finding
        // the room first means the one step that can fail happens while the
        // document is still whole.
        public void Replace(int offset, int count, string text)
        {
            int insert = string.IsNullOrEmpty(text) ? 0 : text.Length;

            // The removal hands `count` characters back to the gap, so that much
            // of the insertion is already paid for and only the excess has to be
            // found. Once this returns, the Insert below cannot need to grow.
            if (insert > count)
            {
                MoveGapTo(offset);
                EnsureGap(insert - count);
            }

            // The line index grows separately, and it would grow after the
            // removal had already happened. The cheap upper bound decides whether
            // counting the breaks exactly is worth a scan at all, so ordinary
            // typing never pays for one.
            if (insert > 0 && (long)_lineCount + insert > _lineStarts.Length)
            {
                EnsureLineCapacity(_lineCount + CountBreaks(text));
            }

            if (count > 0) Remove(offset, count);
            if (insert > 0) Insert(offset, text);
        }

        public void Remove(int offset, int count)
        {
            if (count <= 0) return;

            int firstLine = GetLineFromOffset(offset);
            int lastLine = GetLineFromOffset(offset + count);

            MoveGapTo(offset);
            _gapEnd += count;

            // Lines that began inside the removed range no longer begin anywhere.
            // The line the removal started on survives, holding what is left of
            // both ends joined together.
            if (lastLine > firstLine) RemoveLines(firstLine + 1, lastLine - firstLine);

            ShiftLineStarts(firstLine + 1, -count);
        }

        private static int CountBreaks(string text)
        {
            int count = 0;

            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n') count++;
            }

            return count;
        }

        private void InsertLines(int line, int offset, string text, int breaks)
        {
            EnsureLineCapacity(_lineCount + breaks);

            // Open a hole for the new starts and push what follows past it, then
            // fill the hole. Done in this order so nothing is overwritten before
            // it has been moved.
            Array.Copy(_lineStarts, line + 1, _lineStarts, line + 1 + breaks, _lineCount - line - 1);

            int written = line + 1;

            for (int i = 0; i < text.Length; i++)
            {
                // The line starts after the break, not on it.
                if (text[i] == '\n') _lineStarts[written++] = offset + i + 1;
            }

            _lineCount += breaks;
        }

        private void RemoveLines(int firstLine, int count)
        {
            Array.Copy(_lineStarts, firstLine + count, _lineStarts, firstLine, _lineCount - firstLine - count);
            _lineCount -= count;
        }

        // Every line after an edit begins that many characters further along.
        //
        // This is proportional to the number of lines below the caret, which on a
        // three-million-line file is a twelve-megabyte move per keystroke - about
        // two milliseconds, imperceptible, but it is the one part of editing here
        // that is not constant. If it ever needs to be, the fix is to hold the
        // pending shift as a pair (fromLine, delta) and apply it lazily, which
        // makes repeated typing on one line free.
        private void ShiftLineStarts(int fromLine, int delta)
        {
            for (int i = fromLine; i < _lineCount; i++)
            {
                _lineStarts[i] += delta;
            }
        }

        private void EnsureLineCapacity(int required)
        {
            if (required <= _lineStarts.Length) return;

            int capacity = Math.Max(required, _lineStarts.Length * 2);
            int[] grown = new int[capacity];

            Array.Copy(_lineStarts, grown, _lineCount);
            _lineStarts = grown;
        }

        private void MoveGapTo(int offset)
        {
            if (offset == _gapStart) return;

            if (offset < _gapStart)
            {
                int count = _gapStart - offset;

                Array.Copy(_buffer, offset, _buffer, _gapEnd - count, count);
                _gapEnd -= count;
                _gapStart = offset;
            }
            else
            {
                int count = offset - _gapStart;

                Array.Copy(_buffer, _gapEnd, _buffer, _gapStart, count);
                _gapStart += count;
                _gapEnd += count;
            }
        }

        private void EnsureGap(int required)
        {
            int available = _gapEnd - _gapStart;
            if (available >= required) return;

            // Both of these are computed as longs. Near the ceiling either can
            // pass what an int holds, and an overflow here asks for an array of
            // negative length - which throws something nothing up the call chain
            // is catching.
            long needed = (long)Length + required + MinGap;

            // Grown by half rather than doubled: these arrays are the size of the
            // document, and on a large one doubling means asking for a block the
            // size of the whole file in order to hold a few more characters.
            long wanted = Math.Max(needed, (long)_buffer.Length + _buffer.Length / 2);

            char[] grown = Allocate(wanted, needed);
            int capacity = grown.Length;

            Array.Copy(_buffer, 0, grown, 0, _gapStart);

            int after = _buffer.Length - _gapEnd;
            Array.Copy(_buffer, _gapEnd, grown, capacity - after, after);

            _buffer = grown;
            _gapEnd = capacity - after;
        }

        // A single object cannot exceed 2 GB on .NET Framework without
        // gcAllowVeryLargeObjects, which is half that many chars. The true
        // ceiling is a few chars under this - an array carries a header - so the
        // allocation below is still attempted rather than trusted to arithmetic.
        private const int MaxCapacity = 1024 * 1024 * 1024;

        // Room to spare is worth a great deal on an ordinary document and nothing
        // at all on one near the ceiling, where the half-again is the thing that
        // will not fit while the document itself still would. A document of 750
        // million characters used to close the program on the next keystroke: the
        // edit needed one more character, and the growth rule asked for 1.125
        // billion. Falling back to exactly what the edit needs trades that for
        // reallocating more often, at the only size it ever happens at.
        private static char[] Allocate(long wanted, long needed)
        {
            if (needed > MaxCapacity) throw new OutOfMemoryException();
            if (wanted > MaxCapacity) wanted = needed;

            try
            {
                return new char[wanted];
            }
            catch (OutOfMemoryException)
            {
                if (wanted <= needed) throw;

                return new char[needed];
            }
        }
    }
}
