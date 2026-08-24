using System.IO;
using System.Text;

namespace PureNote
{
    internal static class DocumentLoader
    {
        private const int Step = 1 << 20;

        private const int GapSlack = 64 * 1024;

        public static bool TryMeasure(Stream stream, Encoding encoding, int preamble, out DocumentShape shape)
        {
            shape = new DocumentShape();

            Decoder decoder = encoding.GetDecoder();
            byte[] buffer = new byte[Step];
            char[] slice = new char[encoding.GetMaxCharCount(Step)];

            long length = 0;
            int crlf = 0;
            int cr = 0;
            int lf = 0;
            bool pendingCarriageReturn = false;

            stream.Position = preamble;
            long remaining = stream.Length - preamble;

            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                remaining -= read;

                int n = decoder.GetChars(buffer, 0, read, slice, 0, remaining <= 0);

                for (int i = 0; i < n; i++)
                {
                    char c = slice[i];

                    if (pendingCarriageReturn)
                    {
                        pendingCarriageReturn = false;

                        if (c == '\n')
                        {
                            cr--;
                            crlf++;
                            continue;
                        }
                    }

                    if (c == '\r')
                    {
                        pendingCarriageReturn = true;
                        cr++;
                        length++;
                        continue;
                    }

                    length++;
                    if (c == '\n') lf++;
                }
            }

            if (length > int.MaxValue - GapSlack) return false;

            shape.Length = (int)length;
            shape.LineCount = crlf + cr + lf + 1;
            shape.LineEnding = LineEndings.FromCounts(crlf, cr, lf);
            return true;
        }

        public static TextDocument Load(Stream stream, Encoding encoding, int preamble, DocumentShape shape)
        {
            char[] target = new char[shape.Length + GapSlack];
            int[] lineStarts = new int[shape.LineCount];

            Decoder decoder = encoding.GetDecoder();
            byte[] buffer = new byte[Step];
            char[] slice = new char[encoding.GetMaxCharCount(Step)];

            int written = 0;
            int lines = 1;
            bool pendingCarriageReturn = false;

            lineStarts[0] = 0;

            stream.Position = preamble;
            long remaining = stream.Length - preamble;

            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                remaining -= read;

                int n = decoder.GetChars(buffer, 0, read, slice, 0, remaining <= 0);

                for (int i = 0; i < n; i++)
                {
                    char c = slice[i];

                    if (pendingCarriageReturn)
                    {
                        pendingCarriageReturn = false;

                        if (c == '\n') continue;
                    }

                    if (c == '\r')
                    {
                        pendingCarriageReturn = true;
                        c = '\n';
                    }

                    target[written++] = c;

                    if (c == '\n') lineStarts[lines++] = written;
                }
            }

            return new TextDocument(target, written, lineStarts, lines);
        }
    }

    internal struct DocumentShape
    {
        public int Length;
        public int LineCount;
        public string LineEnding;
    }
}
