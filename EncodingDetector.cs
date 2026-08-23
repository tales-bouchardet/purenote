using System;
using System.IO;
using System.Text;

namespace PureNote
{
    public static class EncodingDetector
    {
        public static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        private static readonly Encoding Utf8Bom = new UTF8Encoding(true);

        // What a file is read as when it carries no BOM and is not valid UTF-8.
        //
        // Deliberately not UTF-8. Decoding non-UTF-8 bytes as UTF-8 turns every
        // byte the decoder cannot make sense of into U+FFFD inside the document,
        // and U+FFFD is a character UTF-8 holds perfectly well - so CanRepresent
        // says yes, nothing warns on the way out, and the save writes the
        // replacement character back to disk. A Windows-1252 file the user only
        // scrolled through comes back with every accented byte destroyed.
        //
        // Windows-1252 maps all 256 byte values to distinct characters and back
        // again, so a wrong guess here costs a wrong-looking accent rather than
        // the byte itself: whatever was read is what gets written. It is also
        // the likeliest thing for a non-UTF-8 file on a Windows machine to be,
        // and mojibake is visible in a way a lone U+FFFD in good text is not.
        public static readonly Encoding Ansi = Encoding.GetEncoding(1252);

        private const int Step = 1 << 20;

        // Never returns null: bytes that match no BOM and are not valid UTF-8
        // are read as Ansi rather than refused, so opening always goes ahead.
        // The encoding menu is there for the cases the guess gets wrong.
        //
        // Reads the file rather than an array of it. Nothing here is ever the
        // size of the document: the mark is four bytes, and the validity check
        // is a state machine over a buffer it reuses.
        //
        // Every encoding this can return carries its own preamble, and it is
        // only returned when that preamble was actually seen - so the caller can
        // ask the encoding how many bytes to skip rather than being told twice.
        public static Encoding Detect(Stream stream)
        {
            byte[] head = new byte[4];

            stream.Position = 0;
            int got = ReadFully(stream, head, head.Length);

            Encoding marked = FromByteOrderMark(head, got);
            if (marked != null) return marked;

            return IsValidUtf8(stream) ? Utf8NoBom : Ansi;
        }

        private static Encoding FromByteOrderMark(byte[] head, int length)
        {
            if (Starts(head, length, 0xFF, 0xFE, 0x00, 0x00)) return new UTF32Encoding(false, true);
            if (Starts(head, length, 0x00, 0x00, 0xFE, 0xFF)) return new UTF32Encoding(true, true);
            if (Starts(head, length, 0xEF, 0xBB, 0xBF)) return Utf8Bom;
            if (Starts(head, length, 0xFF, 0xFE)) return Encoding.Unicode;
            if (Starts(head, length, 0xFE, 0xFF)) return Encoding.BigEndianUnicode;

            return null;
        }

        // A stream is allowed to hand back less than it was asked for without
        // being at its end, so a single Read is not a way to fill a buffer.
        private static int ReadFully(Stream stream, byte[] buffer, int count)
        {
            int total = 0;

            while (total < count)
            {
                int n = stream.Read(buffer, total, count - total);
                if (n <= 0) break;

                total += n;
            }

            return total;
        }

        // Whether every character in the document survives this encoding.
        //
        // Walked in chunks through a stateful Encoder, and counted rather than
        // encoded: GetByteCount runs the same encoder over the same characters
        // and trips the same fallback, but never builds the output. The whole
        // check costs one 64 KB buffer regardless of how large the document is,
        // where asking the same question with GetBytes over a string of the
        // document meant two copies of it to get back a yes or a no.
        //
        // The Encoder carries its state between chunks, so a surrogate pair split
        // across a chunk boundary is completed by the next one rather than
        // reported as a character the encoding cannot hold.
        internal static bool CanRepresent(TextDocument document, Encoding encoding)
        {
            if (document == null || document.Length == 0) return true;

            Encoding strict = (Encoding)encoding.Clone();
            strict.EncoderFallback = EncoderFallback.ExceptionFallback;

            Encoder encoder = strict.GetEncoder();
            char[] chunk = new char[64 * 1024];

            try
            {
                int offset = 0;
                int length = document.Length;

                while (offset < length)
                {
                    int take = Math.Min(chunk.Length, length - offset);
                    document.CopyTo(offset, chunk, 0, take);

                    offset += take;
                    encoder.GetByteCount(chunk, 0, take, offset >= length);
                }

                return true;
            }
            catch (EncoderFallbackException)
            {
                return false;
            }
        }

        public static string GetDisplayName(Encoding encoding)
        {
            switch (encoding.CodePage)
            {
                case 65001: return encoding.GetPreamble().Length > 0 ? "UTF-8 BOM" : "UTF-8";
                case 1200: return "UTF-16 LE";
                case 1201: return "UTF-16 BE";
                case 12000: return "UTF-32 LE";
                case 12001: return "UTF-32 BE";
                case 20127: return "ASCII";
                case 1252: return "Windows-1252";
                case 28591: return "ISO-8859-1";
                default: return encoding.EncodingName;
            }
        }

        public static Encoding FromDisplayName(string name)
        {
            switch (name)
            {
                case "UTF-8 BOM": return Utf8Bom;
                case "UTF-16 LE": return Encoding.Unicode;
                case "UTF-16 BE": return Encoding.BigEndianUnicode;
                case "UTF-32 LE": return new UTF32Encoding(false, true);
                case "UTF-32 BE": return new UTF32Encoding(true, true);
                case "ASCII": return Encoding.ASCII;
                case "Windows-1252": return Encoding.GetEncoding(1252);
                case "ISO-8859-1": return Encoding.GetEncoding(28591);
                default: return Utf8NoBom;
            }
        }

        private static bool Starts(byte[] head, int length, params byte[] prefix)
        {
            if (length < prefix.Length) return false;

            for (int i = 0; i < prefix.Length; i++)
            {
                if (head[i] != prefix[i]) return false;
            }

            return true;
        }

        // Carried across buffer boundaries as a count of continuation bytes still
        // owed, which is what lets this walk the file a megabyte at a time
        // instead of needing all of it at once. A file that ends owing one is
        // truncated mid-character, and so is not valid UTF-8.
        private static bool IsValidUtf8(Stream stream)
        {
            byte[] buffer = new byte[Step];
            int owed = 0;

            stream.Position = 0;

            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                for (int i = 0; i < read; i++)
                {
                    byte b = buffer[i];

                    if (owed > 0)
                    {
                        if ((b & 0xC0) != 0x80) return false;

                        owed--;
                        continue;
                    }

                    if (b <= 0x7F) continue;
                    if ((b & 0xE0) == 0xC0) { owed = 1; continue; }
                    if ((b & 0xF0) == 0xE0) { owed = 2; continue; }
                    if ((b & 0xF8) == 0xF0) { owed = 3; continue; }

                    return false;
                }
            }

            return owed == 0;
        }
    }
}
