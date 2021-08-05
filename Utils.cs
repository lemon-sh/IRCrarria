using System.Text;

namespace IRCrarria
{
    public static class StringExtensions
    {
        public static string StripNonAscii(this string str)
        {
            return Encoding.ASCII.GetString(Encoding.Convert(Encoding.UTF8,
                Encoding.GetEncoding(Encoding.ASCII.EncodingName, new EncoderReplacementFallback(string.Empty),
                    new DecoderExceptionFallback()), Encoding.UTF8.GetBytes(str)));
        }
    }
}