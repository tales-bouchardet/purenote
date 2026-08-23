namespace PureNote
{
    public static class LineEndings
    {
        public const string Crlf = "CRLF";
        public const string Lf = "LF";
        public const string Cr = "CR";

        // Which convention a document arrived in. The breaks are counted by
        // whoever is already walking it — the decoder does it while measuring the
        // file — so nothing has to scan a second time to ask.
        public static string FromCounts(int crlf, int cr, int lf)
        {
            if (lf > crlf && lf >= cr) return Lf;
            if (cr > crlf && cr > lf) return Cr;
            return Crlf;
        }

    }
}
