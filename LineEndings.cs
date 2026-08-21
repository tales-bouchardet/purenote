using System.Text;

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

        public static string Convert(string text, string ending)
        {
            string target = ending == Lf ? "\n" : ending == Cr ? "\r" : "\r\n";

            if (AlreadyUses(text, target)) return text;

            StringBuilder sb = new StringBuilder(text.Length + 16);
            int runStart = 0;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c != '\r' && c != '\n') continue;

                sb.Append(text, runStart, i - runStart);
                sb.Append(target);

                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                runStart = i + 1;
            }

            sb.Append(text, runStart, text.Length - runStart);
            return sb.ToString();
        }

        // Loading and saving both usually convert to the style the text already
        // uses, so check first and hand back the original instead of rebuilding an
        // identical copy of the whole document.
        private static bool AlreadyUses(string text, string target)
        {
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c != '\r' && c != '\n') continue;

                bool isCrlf = c == '\r' && i + 1 < text.Length && text[i + 1] == '\n';

                if (isCrlf)
                {
                    if (target != "\r\n") return false;
                    i++;
                }
                else if (target.Length != 1 || target[0] != c)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
