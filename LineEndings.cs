namespace PureNote
{
    public static class LineEndings
    {
        public const string Crlf = "CRLF";
        public const string Lf = "LF";
        public const string Cr = "CR";

        public static string FromCounts(int crlf, int cr, int lf)
        {
            if (lf > crlf && lf >= cr) return Lf;
            if (cr > crlf && cr > lf) return Cr;
            return Crlf;
        }

    }
}
