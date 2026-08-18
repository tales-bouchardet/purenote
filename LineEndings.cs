using System.Text;

namespace PureNote
{
    public static class LineEndings
    {
        public const string Crlf = "CRLF";
        public const string Lf = "LF";
        public const string Cr = "CR";

        public static string Detect(string text)
        {
            int crlf = 0;
            int lf = 0;
            int cr = 0;

            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\r')
                {
                    if (i + 1 < text.Length && text[i + 1] == '\n')
                    {
                        crlf++;
                        i++;
                    }
                    else
                    {
                        cr++;
                    }
                }
                else if (text[i] == '\n')
                {
                    lf++;
                }
            }

            if (crlf == 0 && lf == 0 && cr == 0) return Crlf;
            if (lf > crlf && lf >= cr) return Lf;
            if (cr > crlf && cr > lf) return Cr;
            return Crlf;
        }

        public static string Convert(string text, string ending)
        {
            string target = ending == Lf ? "\n" : ending == Cr ? "\r" : "\r\n";
            StringBuilder sb = new StringBuilder(text.Length);

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (c == '\r')
                {
                    sb.Append(target);
                    if (i + 1 < text.Length && text[i + 1] == '\n') i++;
                }
                else if (c == '\n')
                {
                    sb.Append(target);
                }
                else
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }

        public static string ToEditor(string text)
        {
            return Convert(text, Crlf);
        }
    }
}
