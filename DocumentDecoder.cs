using System;
using System.Text;

namespace PureNote
{
    // Turns a file's bytes into the editor's text a slice at a time.
    //
    // Opening used to decode the whole file, then rewrite every line ending in
    // it, and only then start handing the result to the editor. That cost three
    // full copies of the document on top of the bytes themselves — the decoded
    // string, the builder that rewrote its line endings, and the string that
    // builder was flattened into — and all three were alive at the same moment,
    // which was also the moment the editor was about to allocate its own copy.
    // On a 111 MB file the process peaked near a gigabyte, and it peaked there
    // before a single character had been shown.
    //
    // Here the bytes stay the backing store and each slice is decoded and
    // normalised on its way into the editor, through two buffers that are sized
    // once and reused for every slice after. What is alive at any moment is the
    // file's bytes, the editor's own copy, and a few hundred kilobytes of
    // working room.
    //
    // Slicing bytes rather than characters is what makes this safe to do at all.
    // A Decoder carries its state between calls, so a multi-byte sequence split
    // across two slices is held back and completed by the next one instead of
    // decoding to a replacement character — and because a four-byte sequence
    // yields both halves of its surrogate pair in one go, no slice can end on
    // half a character either.
    internal sealed class DocumentDecoder
    {
        private readonly byte[] _bytes;
        private readonly Encoding _encoding;
        private readonly Decoder _decoder;
        private readonly int _start;

        private int _position;
        private char[] _decoded = new char[0];
        private char[] _normalised = new char[0];

        // A slice that ends on '\r' has already had a CRLF written for it, on the
        // assumption it was a lone carriage return. If the next slice opens with
        // the '\n' that belonged to it, that newline has already been accounted
        // for and has to be dropped rather than turned into a second break.
        private bool _skipLeadingNewline;

        public DocumentDecoder(byte[] bytes, Encoding encoding)
        {
            _bytes = bytes;
            _encoding = encoding;
            _decoder = encoding.GetDecoder();
            _start = EncodingDetector.PreambleLength(bytes, encoding);
            _position = _start;
        }

        // Characters written out so far, after normalisation. The editor's tracked
        // length comes from this rather than from the measured total, so the two
        // cannot drift apart even if they ever disagreed.
        public int CharsProduced { get; private set; }

        public int ByteLength
        {
            get { return _bytes.Length - _start; }
        }

        public int BytesRead
        {
            get { return _position - _start; }
        }

        public bool AtEnd
        {
            get { return _position >= _bytes.Length; }
        }

        public string NextSlice(int byteCount)
        {
            int count = Math.Min(byteCount, _bytes.Length - _position);
            if (count <= 0) return string.Empty;

            bool last = _position + count >= _bytes.Length;

            // GetMaxCharCount is the encoding's own upper bound for the slice.
            // Sizing from it rather than from an exact count means GetChars never
            // has to refuse for want of room — which it would do by throwing,
            // being unable to split a surrogate pair across two calls.
            int maxChars = _encoding.GetMaxCharCount(count);
            if (_decoded.Length < maxChars) _decoded = new char[maxChars];

            // flush on the last slice only: before that, trailing bytes that do
            // not yet form a character belong to the slice after this one.
            int decodedCount = _decoder.GetChars(_bytes, _position, count, _decoded, 0, last);
            _position += count;

            return Normalise(decodedCount);
        }

        private string Normalise(int count)
        {
            // Worst case every character is a bare newline and every one of them
            // becomes two.
            if (_normalised.Length < count * 2) _normalised = new char[count * 2];

            int i = 0;

            // Left standing when a slice decodes to nothing at all — every byte in
            // it having been the start of a sequence the next slice completes —
            // because the newline it is watching for has still not been seen.
            if (_skipLeadingNewline && count > 0)
            {
                _skipLeadingNewline = false;
                if (_decoded[0] == '\n') i = 1;
            }

            int written = 0;

            for (; i < count; i++)
            {
                char c = _decoded[i];

                if (c == '\r')
                {
                    _normalised[written++] = '\r';
                    _normalised[written++] = '\n';

                    if (i + 1 < count)
                    {
                        if (_decoded[i + 1] == '\n') i++;
                    }
                    else
                    {
                        _skipLeadingNewline = true;
                    }
                }
                else if (c == '\n')
                {
                    _normalised[written++] = '\r';
                    _normalised[written++] = '\n';
                }
                else
                {
                    _normalised[written++] = c;
                }
            }

            CharsProduced += written;
            return new string(_normalised, 0, written);
        }

        // What the document will come to, without building it.
        //
        // The editor needs its line and character counts before the first slice
        // goes in — the footer shows the file's real size from the first frame,
        // and the gutter has to be as wide as the last line number so it does not
        // jump wider as the file arrives. Both are settled here by decoding the
        // file once into a single reused buffer and counting what comes out,
        // which costs a decode pass and no allocation. Against a load that runs
        // for twenty seconds, that pass is under two hundred milliseconds.
        public static bool TryMeasure(byte[] bytes, Encoding encoding, out DocumentShape shape)
        {
            shape = new DocumentShape();

            Decoder decoder = encoding.GetDecoder();
            int start = EncodingDetector.PreambleLength(bytes, encoding);

            const int Step = 1 << 20;
            char[] buffer = new char[encoding.GetMaxCharCount(Step)];

            long decodedChars = 0;
            int crlf = 0;
            int cr = 0;
            int lf = 0;
            bool pendingCarriageReturn = false;

            for (int p = start; p < bytes.Length; p += Step)
            {
                int count = Math.Min(Step, bytes.Length - p);
                bool last = p + count >= bytes.Length;

                int n = decoder.GetChars(bytes, p, count, buffer, 0, last);
                decodedChars += n;

                for (int i = 0; i < n; i++)
                {
                    char c = buffer[i];

                    // Resolves a '\r' left hanging at the end of the last buffer.
                    // Falls through afterwards: the character that settled it is
                    // still a character, and may itself be another '\r'.
                    if (pendingCarriageReturn)
                    {
                        pendingCarriageReturn = false;

                        if (c == '\n') { crlf++; continue; }
                        cr++;
                    }

                    if (c == '\r')
                    {
                        if (i + 1 < n)
                        {
                            if (buffer[i + 1] == '\n') { crlf++; i++; } else { cr++; }
                        }
                        else
                        {
                            pendingCarriageReturn = true;
                        }
                    }
                    else if (c == '\n')
                    {
                        lf++;
                    }
                }
            }

            if (pendingCarriageReturn) cr++;

            // Every break leaves as CRLF, so the ones that arrived as a single
            // character each cost one more than they did.
            long length = decodedChars + cr + lf;

            // A document is addressed by int throughout — the editor's own offsets,
            // the tracked length, the find results. Refusing here is the difference
            // between an honest message and an overflow that corrupts every offset
            // downstream of it.
            if (length > int.MaxValue) return false;

            shape.Length = (int)length;
            shape.LineCount = crlf + cr + lf + 1;
            shape.LineEnding = LineEndings.FromCounts(crlf, cr, lf);
            return true;
        }
    }

    internal struct DocumentShape
    {
        public int Length;
        public int LineCount;
        public string LineEnding;
    }
}
