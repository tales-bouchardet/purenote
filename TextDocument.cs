using System;
using System.IO;

namespace PureNote
{
    internal sealed class TextDocument
    {
        private const int MinGap = 1024;

        private char[] _buffer;
        private int _gapStart;
        private int _gapEnd;

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

            if (breaks > 0) InsertLines(line, offset, text, breaks);

            ShiftLineStarts(line + 1 + breaks, text.Length);
        }

        public void Replace(int offset, int count, string text)
        {
            int insert = string.IsNullOrEmpty(text) ? 0 : text.Length;

            if (insert > count)
            {
                MoveGapTo(offset);
                EnsureGap(insert - count);
            }

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

            Array.Copy(_lineStarts, line + 1, _lineStarts, line + 1 + breaks, _lineCount - line - 1);

            int written = line + 1;

            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n') _lineStarts[written++] = offset + i + 1;
            }

            _lineCount += breaks;
        }

        private void RemoveLines(int firstLine, int count)
        {
            Array.Copy(_lineStarts, firstLine + count, _lineStarts, firstLine, _lineCount - firstLine - count);
            _lineCount -= count;
        }

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

            long needed = (long)Length + required + MinGap;

            long wanted = Math.Max(needed, (long)_buffer.Length + _buffer.Length / 2);

            char[] grown = Allocate(wanted, needed);
            int capacity = grown.Length;

            Array.Copy(_buffer, 0, grown, 0, _gapStart);

            int after = _buffer.Length - _gapEnd;
            Array.Copy(_buffer, _gapEnd, grown, capacity - after, after);

            _buffer = grown;
            _gapEnd = capacity - after;
        }

        private const int MaxCapacity = 1024 * 1024 * 1024;

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
