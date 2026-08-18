using System.Text;

namespace PureNote
{
    public static class EncodingDetector
    {
        public static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        private static readonly Encoding Utf8Bom = new UTF8Encoding(true);

        public static Encoding Detect(byte[] bytes)
        {
            if (StartsWith(bytes, 0xFF, 0xFE, 0x00, 0x00)) return new UTF32Encoding(false, true);
            if (StartsWith(bytes, 0x00, 0x00, 0xFE, 0xFF)) return new UTF32Encoding(true, true);
            if (StartsWith(bytes, 0xEF, 0xBB, 0xBF)) return Utf8Bom;
            if (StartsWith(bytes, 0xFF, 0xFE)) return Encoding.Unicode;
            if (StartsWith(bytes, 0xFE, 0xFF)) return Encoding.BigEndianUnicode;

            if (IsValidUtf8(bytes)) return Utf8NoBom;

            return null;
        }

        public static string Decode(byte[] bytes, Encoding encoding)
        {
            byte[] preamble = encoding.GetPreamble();

            if (preamble.Length > 0 && StartsWith(bytes, preamble))
            {
                return encoding.GetString(bytes, preamble.Length, bytes.Length - preamble.Length);
            }

            return encoding.GetString(bytes);
        }

        public static bool CanRepresent(string text, Encoding encoding)
        {
            if (string.IsNullOrEmpty(text)) return true;

            Encoding strict = (Encoding)encoding.Clone();
            strict.EncoderFallback = EncoderFallback.ExceptionFallback;

            try
            {
                strict.GetBytes(text);
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

        private static bool StartsWith(byte[] bytes, params byte[] prefix)
        {
            if (bytes.Length < prefix.Length) return false;

            for (int i = 0; i < prefix.Length; i++)
            {
                if (bytes[i] != prefix[i]) return false;
            }

            return true;
        }

        private static bool IsValidUtf8(byte[] bytes)
        {
            int i = 0;
            while (i < bytes.Length)
            {
                byte b = bytes[i];
                int extra;

                if (b <= 0x7F) { extra = 0; }
                else if ((b & 0xE0) == 0xC0) { extra = 1; }
                else if ((b & 0xF0) == 0xE0) { extra = 2; }
                else if ((b & 0xF8) == 0xF0) { extra = 3; }
                else { return false; }

                if (i + extra >= bytes.Length) return false;

                for (int j = 1; j <= extra; j++)
                {
                    if ((bytes[i + j] & 0xC0) != 0x80) return false;
                }

                i += extra + 1;
            }

            return true;
        }
    }
}
